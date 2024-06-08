using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using System.Text;

namespace Badeend.ValueCollections;

/// <summary>
/// A mutable dictionary that can be used to efficiently construct new immutable
/// dictionaries.
/// </summary>
/// <remarks>
/// Most mutating methods on this class return `this`, allowing the caller to
/// chain multiple mutations in a row.
///
/// When you're done building, call <see cref="Build()"/> to extract the
/// resulting dictionary.
///
/// For constructing <see cref="ValueDictionary{TKey, TValue}"/>s it is
/// recommended to use this class over e.g. <see cref="Dictionary{TKey, TValue}"/>.
/// This type can avoiding unnecessary copying by taking advantage of the
/// immutability of its results. Whereas calling <c>.ToValueDictionary()</c> on
/// a regular <see cref="Dictionary{TKey, TValue}"/> <em>always</em> performs a
/// full copy.
///
/// The order in which the entries are enumerated is undefined.
///
/// Unlike ValueDictionary, ValueDictionaryBuilder is <em>not</em> thread-safe.
/// </remarks>
/// <typeparam name="TKey">The type of keys in the dictionary.</typeparam>
/// <typeparam name="TValue">The type of values in the dictionary.</typeparam>
[CollectionBuilder(typeof(ValueDictionary), nameof(ValueDictionary.Builder))]
[SuppressMessage("Naming", "CA1710:Identifiers should have correct suffix", Justification = "Not applicable for Builder type.")]
public sealed class ValueDictionaryBuilder<TKey, TValue> : IDictionary<TKey, TValue>, IReadOnlyDictionary<TKey, TValue>
	where TKey : notnull
{
	private const int VersionBuilt = -1;

	/// <summary>
	/// Can be one of:
	/// - ValueDictionary{TKey, TValue}: when copy-on-write hasn't kicked in yet.
	/// - Dictionary{T}: we're actively building a dictionary.
	/// </summary>
	private IReadOnlyDictionary<TKey, TValue> items;

	/// <summary>
	/// Mutation counter.
	/// `-1` means: Collection has been built and the builder is now read-only.
	/// </summary>
	private int version;

	/// <summary>
	/// Returns <see langword="true"/> when this instance has been built and is
	/// now read-only.
	/// </summary>
	[Pure]
	public bool IsReadOnly => this.version == VersionBuilt;

	/// <summary>
	/// Finalize the builder and export its contents as a <see cref="ValueDictionary{TKey, TValue}"/>.
	/// This makes the builder read-only. Any future attempt to mutate the
	/// builder will throw.
	///
	/// This is an <c>O(1)</c> operation and performs only a small fixed-size
	/// memory allocation. This does not perform a bulk copy of the contents.
	/// </summary>
	/// <remarks>
	/// If you need an intermediate snapshot of the contents while keeping the
	/// builder open for mutation, use <see cref="ToValueDictionary"/> instead.
	/// </remarks>
	/// <exception cref="InvalidOperationException">
	/// This instance has already been built.
	/// </exception>
	public ValueDictionary<TKey, TValue> Build()
	{
		if (this.version == VersionBuilt)
		{
			throw BuiltException();
		}

		this.version = VersionBuilt;

		return this.ToValueDictionary();
	}

	/// <summary>
	/// Copy the current contents of the builder into a new <see cref="ValueDictionary{TKey, TValue}"/>.
	/// </summary>
	/// <remarks>
	/// If you don't need the builder anymore after this method, consider using
	/// <see cref="Build"/> instead.
	/// </remarks>
	[Pure]
	public ValueDictionary<TKey, TValue> ToValueDictionary()
	{
		if (this.items is Dictionary<TKey, TValue> dictionary)
		{
			var newValueDictionary = ValueDictionary<TKey, TValue>.FromDictionaryUnsafe(dictionary);
			this.items = newValueDictionary;
			return newValueDictionary;
		}

		if (this.items is ValueDictionary<TKey, TValue> valueDictionary)
		{
			return valueDictionary;
		}

		throw UnreachableException();
	}

	private Dictionary<TKey, TValue> Mutate()
	{
		if (this.version == VersionBuilt)
		{
			throw BuiltException();
		}

		this.version++;

		if (this.items is Dictionary<TKey, TValue> dictionary)
		{
			return dictionary;
		}

		if (this.items is ValueDictionary<TKey, TValue> valueDictionary)
		{
			var newDictionary = new Dictionary<TKey, TValue>(valueDictionary.Items);
			this.items = newDictionary;
			return newDictionary;
		}

		throw UnreachableException();
	}

	private IReadOnlyDictionary<TKey, TValue> Read() => this.items;

	private Dictionary<TKey, TValue> ReadUnsafe() => this.items switch
	{
		Dictionary<TKey, TValue> items => items,
		ValueDictionary<TKey, TValue> items => items.Items,
		_ => throw UnreachableException(),
	};

	/// <summary>
	/// Current size of the dictionary.
	/// </summary>
	[Pure]
	public int Count => this.Read().Count;

	/// <summary>
	/// Shortcut for <c>.Count == 0</c>.
	/// </summary>
	[Pure]
	public bool IsEmpty
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => this.Count == 0;
	}

	/// <summary>
	/// Get the value associated with the specified key.
	/// </summary>
	/// <exception cref="ArgumentNullException">
	/// <paramref name="key"/> is <c>null</c>.
	/// </exception>
	/// <exception cref="KeyNotFoundException">
	/// The <paramref name="key"/> does not exist.
	/// </exception>
	public TValue this[TKey key]
	{
		[Pure]
		get => this.Read()[key];
		set => this.Mutate()[key] = value;
	}

	private KeyCollection KeysInternal
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => new KeyCollection(this, this.version);
	}

	/// <summary>
	/// All keys in the dictionary in no particular order.
	/// </summary>
	/// <remarks>
	/// Every modification to the builder invalidates any <c>Keys</c> collection
	/// obtained before that moment.
	/// </remarks>
	[Pure]
	public IReadOnlyCollection<TKey> Keys
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => this.KeysInternal;
	}

	/// <inheritdoc/>
	ICollection<TKey> IDictionary<TKey, TValue>.Keys => this.KeysInternal;

	/// <inheritdoc/>
	IEnumerable<TKey> IReadOnlyDictionary<TKey, TValue>.Keys => this.KeysInternal;

	private ValueCollection ValuesInternal
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => new ValueCollection(this, this.version);
	}

	/// <summary>
	/// All values in the dictionary in no particular order.
	/// </summary>
	/// <remarks>
	/// Every modification to the builder invalidates any <c>Values</c> collection
	/// obtained before that moment.
	/// </remarks>
	[Pure]
	public IReadOnlyCollection<TValue> Values
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => this.ValuesInternal;
	}

	/// <inheritdoc/>
	ICollection<TValue> IDictionary<TKey, TValue>.Values => this.ValuesInternal;

	/// <inheritdoc/>
	IEnumerable<TValue> IReadOnlyDictionary<TKey, TValue>.Values => this.ValuesInternal;

	/// <summary>
	/// Construct a new empty dictionary builder.
	/// </summary>
	[Pure]
	public ValueDictionaryBuilder()
	{
		this.items = ValueDictionary<TKey, TValue>.Empty;
	}

	/// <summary>
	/// Construct a new <see cref="ValueDictionaryBuilder{TKey, TValue}"/> with the provided
	/// <paramref name="items"/> as its initial content.
	/// </summary>
	/// <remarks>
	/// Use <see cref="ValueDictionary.Builder{TKey, TValue}(ReadOnlySpan{KeyValuePair{TKey, TValue}})"/>
	/// to construct a ValueDictionaryBuilder from a span.
	/// </remarks>
	public ValueDictionaryBuilder(IEnumerable<KeyValuePair<TKey, TValue>> items)
	{
		if (items is ValueDictionary<TKey, TValue> valueDictionary)
		{
			this.items = valueDictionary;
		}
		else
		{
			this.items = ValueDictionary<TKey, TValue>.EnumerableToDictionary(items);
		}
	}

	private ValueDictionaryBuilder(ValueDictionary<TKey, TValue> items)
	{
		this.items = items;
	}

	private ValueDictionaryBuilder(Dictionary<TKey, TValue> items)
	{
		this.items = items;
	}

#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
	/// <summary>
	/// Construct a new empty dictionary builder with at least the specified
	/// initial capacity.
	/// </summary>
	/// <remarks>
	/// Available on .NET Standard 2.1 and .NET Core 2.1 and higher.
	/// </remarks>
	/// <exception cref="ArgumentOutOfRangeException">
	///   <paramref name="capacity"/> is less than 0.
	/// </exception>
	[Pure]
	public ValueDictionaryBuilder(int capacity)
	{
		if (capacity == 0)
		{
			this.items = ValueDictionary<TKey, TValue>.Empty;
		}
		else
		{
			this.items = new Dictionary<TKey, TValue>(capacity);
		}
	}

	/// <summary>
	/// The total number of elements the internal data structure can hold without resizing.
	/// </summary>
	/// <remarks>
	/// Available on .NET Standard 2.1 and .NET Core 2.1 and higher.
	/// </remarks>
	public int Capacity
	{
		[Pure]
		get
		{
			var dictionary = this.items switch
			{
				Dictionary<TKey, TValue> items => items,
				ValueDictionary<TKey, TValue> items => items.Items,
				_ => throw UnreachableException(),
			};

#if NET9_0_OR_GREATER
			return dictionary.Capacity;
#else
			return dictionary.EnsureCapacity(0);
#endif
		}
	}

	/// <summary>
	/// Ensures that the capacity of this dictionary is at least the specified capacity.
	/// If the current capacity is less than capacity, it is increased to at
	/// least the specified capacity.
	/// </summary>
	/// <remarks>
	/// Available on .NET Standard 2.1 and .NET Core 2.1 and higher.
	/// </remarks>
	public ValueDictionaryBuilder<TKey, TValue> EnsureCapacity(int capacity)
	{
		this.Mutate().EnsureCapacity(capacity);
		return this;
	}

	/// <summary>
	/// Reduce the capacity of the dictionary to roughly the specified value. If the
	/// current capacity is already smaller than the requested capacity, this
	/// method does nothing. The specified <paramref name="capacity"/> is only
	/// a hint. After this method returns, the <see cref="Capacity"/> may be
	/// rounded up to a nearby, implementation-specific value.
	/// </summary>
	/// <exception cref="ArgumentOutOfRangeException">
	/// <paramref name="capacity"/> is less than <see cref="Count"/>.
	/// </exception>
	/// <remarks>
	/// Available on .NET Standard 2.1 and .NET Core 2.1 and higher.
	/// </remarks>
	public ValueDictionaryBuilder<TKey, TValue> TrimExcess(int capacity)
	{
		this.Mutate().TrimExcess(capacity);
		return this;
	}
#endif

	internal static ValueDictionaryBuilder<TKey, TValue> FromValueDictionary(ValueDictionary<TKey, TValue> items) => new(items);

	internal static ValueDictionaryBuilder<TKey, TValue> FromDictionaryUnsafe(Dictionary<TKey, TValue> items) => new(items);

	internal static ValueDictionaryBuilder<TKey, TValue> FromReadOnlySpan(ReadOnlySpan<KeyValuePair<TKey, TValue>> items)
	{
		if (items.Length == 0)
		{
			return new();
		}

		return new(ValueDictionary<TKey, TValue>.SpanToDictionary(items));
	}

	/// <summary>
	/// Determines whether this dictionary contains an element with the specified value.
	/// </summary>
	/// <remarks>
	/// This performs a linear scan through the dictionary.
	/// </remarks>
	[Pure]
	public bool ContainsValue(TValue value) => this.ReadUnsafe().ContainsValue(value);

	/// <summary>
	/// Determines whether this dictionary contains an element with the specified key.
	/// </summary>
	[Pure]
	public bool ContainsKey(TKey key) => this.Read().ContainsKey(key);

	/// <inheritdoc/>
	bool ICollection<KeyValuePair<TKey, TValue>>.Contains(KeyValuePair<TKey, TValue> item) => this.Read().Contains(item);

	/// <summary>
	/// Attempt to get the value associated with the specified <paramref name="key"/>.
	/// Returns <see langword="false"/> when the key was not found.
	/// </summary>
#pragma warning disable CS8767 // Nullability of reference types in type of parameter doesn't match implicitly implemented member (possibly because of nullability attributes).
	public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value) => this.Read().TryGetValue(key, out value);
#pragma warning restore CS8767 // Nullability of reference types in type of parameter doesn't match implicitly implemented member (possibly because of nullability attributes).

	/// <summary>
	/// Attempt to get the value associated with the specified <paramref name="key"/>.
	/// Returns <see langword="default"/> when the key was not found.
	/// </summary>
	[Pure]
	public TValue? GetValueOrDefault(TKey key) => this.GetValueOrDefault(key, default!);

	/// <summary>
	/// Attempt to get the value associated with the specified <paramref name="key"/>.
	/// Returns <paramref name="defaultValue"/> when the key was not found.
	/// </summary>
	[Pure]
	public TValue GetValueOrDefault(TKey key, TValue defaultValue)
	{
		return this.TryGetValue(key, out var value) ? value : defaultValue;
	}

	/// <inheritdoc/>
	void ICollection<KeyValuePair<TKey, TValue>>.CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
	{
		if (array is null)
		{
			throw new ArgumentNullException(nameof(array));
		}

		if (arrayIndex < 0 || arrayIndex > array.Length)
		{
			throw new ArgumentOutOfRangeException(nameof(arrayIndex));
		}

		if (array.Length - arrayIndex < this.Count)
		{
			throw new ArgumentException("Destination too short", nameof(arrayIndex));
		}

		var index = arrayIndex;
		foreach (var item in this)
		{
			array[index] = item;
			index++;
		}
	}

	/// <summary>
	/// Attempt to add the <paramref name="key"/> and <paramref name="value"/>
	/// to the dictionary. Returns <see langword="false"/> when the key was
	/// already present.
	/// </summary>
	public bool TryAdd(TKey key, TValue value)
	{
		var dictionary = this.Mutate();

#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_0_OR_GREATER
		return dictionary.TryAdd(key, value);
#else
		if (!dictionary.ContainsKey(key))
		{
			dictionary.Add(key, value);
			return true;
		}

		return false;
#endif
	}

	/// <summary>
	/// Add the <paramref name="key"/> and <paramref name="value"/> to the
	/// dictionary.
	/// </summary>
	/// <remarks>
	/// This method throws an exception when the key already exists. If this is
	/// not desired, you can use <see cref="TryAdd"/> or <see cref="SetItem"/>
	/// instead.
	/// </remarks>
	/// <exception cref="ArgumentException">
	/// The <paramref name="key"/> already exists.
	/// </exception>
	public ValueDictionaryBuilder<TKey, TValue> Add(TKey key, TValue value)
	{
		this.Mutate().Add(key, value);
		return this;
	}

	/// <inheritdoc/>
	void IDictionary<TKey, TValue>.Add(TKey key, TValue value) => this.Add(key, value);

	/// <inheritdoc/>
	void ICollection<KeyValuePair<TKey, TValue>>.Add(KeyValuePair<TKey, TValue> item) => this.Add(item.Key, item.Value);

	/// <summary>
	/// Add multiple entries to the dictionary.
	/// </summary>
	/// <remarks>
	/// <see cref="ValueCollectionExtensions.AddRange{TKey, TValue}(ValueDictionaryBuilder{TKey, TValue}, ReadOnlySpan{KeyValuePair{TKey, TValue}})">More overloads</see>
	/// are available as extension methods.
	/// </remarks>
	/// <exception cref="ArgumentException">
	/// <paramref name="items"/> contains a duplicate key or a key that already
	/// exists in the dictionary.
	/// </exception>
	public ValueDictionaryBuilder<TKey, TValue> AddRange(IEnumerable<KeyValuePair<TKey, TValue>> items)
	{
		if (items is null)
		{
			throw new ArgumentNullException(nameof(items));
		}

		var dictionary = this.Mutate();

#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
		if (items is ICollection<KeyValuePair<TKey, TValue>> collection)
		{
			dictionary.EnsureCapacity(dictionary.Count + collection.Count);
		}
#endif

		foreach (var item in items)
		{
			dictionary.Add(item.Key, item.Value);
		}

		return this;
	}

	// Accessible through an extension method.
	internal ValueDictionaryBuilder<TKey, TValue> AddRangeSpan(ReadOnlySpan<KeyValuePair<TKey, TValue>> items)
	{
		var dictionary = this.Mutate();
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
		dictionary.EnsureCapacity(dictionary.Count + items.Length);
#endif
		foreach (var item in items)
		{
			dictionary.Add(item.Key, item.Value);
		}

		return this;
	}

	/// <summary>
	/// Set the specified <paramref name="key"/> in the dictionary, possibly
	/// overwriting an existing value for the key.
	/// </summary>
	/// <remarks>
	/// Functionally equivalent to:
	/// <code>
	/// builder[key] = value;
	/// </code>
	/// </remarks>
	public ValueDictionaryBuilder<TKey, TValue> SetItem(TKey key, TValue value)
	{
		this.Mutate()[key] = value;
		return this;
	}

	/// <summary>
	/// Sets the specified key/value pairs in the dictionary, possibly
	/// overwriting existing values for the keys.
	/// </summary>
	/// <remarks>
	/// When the same key appears multiple times in the <paramref name="items"/>,
	/// the last value overwrites any earlier values.
	///
	/// <see cref="ValueCollectionExtensions.SetItems{TKey, TValue}(ValueDictionaryBuilder{TKey, TValue}, ReadOnlySpan{KeyValuePair{TKey, TValue}})">More overloads</see>
	/// are available as extension methods.
	/// </remarks>
	public ValueDictionaryBuilder<TKey, TValue> SetItems(IEnumerable<KeyValuePair<TKey, TValue>> items)
	{
		if (items is null)
		{
			throw new ArgumentNullException(nameof(items));
		}

		var dictionary = this.Mutate();

		foreach (var item in items)
		{
			dictionary[item.Key] = item.Value;
		}

		return this;
	}

	// Accessible through an extension method.
	internal ValueDictionaryBuilder<TKey, TValue> SetItemsSpan(ReadOnlySpan<KeyValuePair<TKey, TValue>> items)
	{
		var dictionary = this.Mutate();

		foreach (var item in items)
		{
			dictionary[item.Key] = item.Value;
		}

		return this;
	}

	/// <summary>
	/// Attempt to remove a specific <paramref name="key"/> from the dictionary.
	/// Returns <see langword="false"/> when the key was not found.
	/// </summary>
	/// <remarks>
	/// This is the equivalent of <see cref="Dictionary{TKey, TValue}.Remove(TKey)">Dictionary.Remove</see>.
	/// </remarks>
	public bool TryRemove(TKey key) => this.Mutate().Remove(key);

	/// <summary>
	/// Attempt to remove a specific <paramref name="key"/> from the dictionary.
	/// Returns <see langword="false"/> when the key was not found. The removed
	/// value (if any) is stored in <paramref name="value"/>.
	/// </summary>
	public bool TryRemove(TKey key, [MaybeNullWhen(false)] out TValue value)
	{
		var dictionary = this.Mutate();

#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
		return dictionary.Remove(key, out value);
#else
		if (dictionary.TryGetValue(key, out value))
		{
			dictionary.Remove(key);
			return true;
		}

		value = default;
		return false;
#endif
	}

	/// <summary>
	/// Remove a specific key from the dictionary if it exists.
	/// </summary>
	/// <remarks>
	/// Use <c>TryRemove</c> if you want to know whether any element was
	/// actually removed.
	/// </remarks>
	public ValueDictionaryBuilder<TKey, TValue> Remove(TKey key)
	{
		this.TryRemove(key);
		return this;
	}

	/// <inheritdoc/>
	bool IDictionary<TKey, TValue>.Remove(TKey key) => this.TryRemove(key);

	/// <inheritdoc/>
	bool ICollection<KeyValuePair<TKey, TValue>>.Remove(KeyValuePair<TKey, TValue> item) => ((ICollection<KeyValuePair<TKey, TValue>>)this.Mutate()).Remove(item);

	/// <summary>
	/// Remove the provided <paramref name="keys"/> from the dictionary.
	/// </summary>
	/// <remarks>
	/// <see cref="ValueCollectionExtensions.RemoveRange{TKey, TValue}(ValueDictionaryBuilder{TKey, TValue}, ReadOnlySpan{TKey})">More overloads</see>
	/// are available as extension methods.
	/// </remarks>
	public ValueDictionaryBuilder<TKey, TValue> RemoveRange(IEnumerable<TKey> keys)
	{
		if (keys is null)
		{
			throw new ArgumentNullException(nameof(keys));
		}

		var dictionary = this.Mutate();

		foreach (var key in keys)
		{
			dictionary.Remove(key);
		}

		return this;
	}

	// Accessible through an extension method.
	internal ValueDictionaryBuilder<TKey, TValue> RemoveRangeSpan(ReadOnlySpan<TKey> keys)
	{
		var dictionary = this.Mutate();

		foreach (var key in keys)
		{
			dictionary.Remove(key);
		}

		return this;
	}

	/// <summary>
	/// Remove all elements from the dictionary.
	/// </summary>
	public ValueDictionaryBuilder<TKey, TValue> Clear()
	{
		this.Mutate().Clear();
		return this;
	}

	/// <inheritdoc/>
	void ICollection<KeyValuePair<TKey, TValue>>.Clear() => this.Clear();

	/// <summary>
	/// Reduce the capacity of this dictionary as much as possible. After calling this
	/// method, the <c>Capacity</c> of the dictionary may still be higher than
	/// the <see cref="Count"/>.
	/// </summary>
	/// <remarks>
	/// This method can be used to minimize the memory overhead of long-lived
	/// dictionaries. This method is most useful just before calling
	/// <see cref="Build"/>, e.g.:
	/// <code>
	/// var longLivedDictionary = builder.TrimExcess().Build()
	/// </code>
	/// Excessive use of this method most likely introduces more performance
	/// problems than it solves.
	/// </remarks>
	public ValueDictionaryBuilder<TKey, TValue> TrimExcess()
	{
		var dictionary = this.Mutate();

#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
		dictionary.TrimExcess();
#else
		var copy = new Dictionary<TKey, TValue>(dictionary.Count);

		foreach (var entry in dictionary)
		{
			copy.Add(entry.Key, entry.Value);
		}

		this.items = copy;
#endif
		return this;
	}

	/// <inheritdoc/>
	IEnumerator<KeyValuePair<TKey, TValue>> IEnumerable<KeyValuePair<TKey, TValue>>.GetEnumerator()
	{
		if (this.Count == 0)
		{
			return EnumeratorLike.Empty<KeyValuePair<TKey, TValue>>();
		}
		else
		{
			return EnumeratorLike.AsIEnumerator<KeyValuePair<TKey, TValue>, Enumerator>(new Enumerator(this));
		}
	}

	/// <inheritdoc/>
	IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable<KeyValuePair<TKey, TValue>>)this).GetEnumerator();

	/// <summary>
	/// Returns an enumerator for this <see cref="ValueDictionaryBuilder{TKey, TValue}"/>.
	///
	/// Typically, you don't need to manually call this method, but instead use
	/// the built-in <c>foreach</c> syntax.
	/// </summary>
	[Pure]
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public Enumerator GetEnumerator() => new Enumerator(this);

	/// <summary>
	/// Enumerator for <see cref="ValueDictionaryBuilder{TKey, TValue}"/>.
	/// </summary>
#pragma warning disable CA1034 // Nested types should not be visible
#pragma warning disable CA1815 // Override equals and operator equals on value types
	public struct Enumerator : IEnumeratorLike<KeyValuePair<TKey, TValue>>
	{
		private readonly ValueDictionaryBuilder<TKey, TValue> builder;
		private readonly int version;
		private ShufflingDictionaryEnumerator<TKey, TValue> enumerator;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal Enumerator(ValueDictionaryBuilder<TKey, TValue> builder)
		{
			var innerDictionary = builder.items switch
			{
				Dictionary<TKey, TValue> items => items,
				ValueDictionary<TKey, TValue> items => items.Items,
				_ => throw UnreachableException(),
			};

			this.builder = builder;
			this.version = builder.version;
			this.enumerator = new(innerDictionary, initialSeed: builder.version);
		}

		/// <inheritdoc/>
		public readonly KeyValuePair<TKey, TValue> Current
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => this.enumerator.Current;
		}

		/// <inheritdoc/>
		public bool MoveNext()
		{
			if (this.version != this.builder.version)
			{
				throw new InvalidOperationException("Collection was modified during enumeration.");
			}

			return this.enumerator.MoveNext();
		}
	}
#pragma warning restore CA1815 // Override equals and operator equals on value types
#pragma warning restore CA1034 // Nested types should not be visible

	private sealed class KeyCollection : ICollection<TKey>, IReadOnlyCollection<TKey>
	{
		private readonly ValueDictionaryBuilder<TKey, TValue> builder;
		private readonly int version;

		public KeyCollection(ValueDictionaryBuilder<TKey, TValue> builder, int version)
		{
			this.builder = builder;
			this.version = version;
		}

		private ValueDictionaryBuilder<TKey, TValue> Read()
		{
			if (this.version != this.builder.version)
			{
				throw new InvalidOperationException("ValueDictionaryBuilder.Keys collection invalidated because of modifications to the builder.");
			}

			return this.builder;
		}

		public int Count => this.Read().Count;

		public bool IsReadOnly => true;

		public bool Contains(TKey item) => this.Read().ContainsKey(item);

		public void CopyTo(TKey[] array, int index)
		{
			if (array == null)
			{
				throw new ArgumentNullException(nameof(array));
			}

			if (index < 0 || index > array.Length)
			{
				throw new ArgumentOutOfRangeException(nameof(index));
			}

			var builder = this.Read();

			if (array.Length - index < builder.Count)
			{
				throw new ArgumentException("Destination too small", nameof(array));
			}

			foreach (var entry in builder)
			{
				array[index++] = entry.Key;
			}
		}

		public IEnumerator<TKey> GetEnumerator()
		{
			var builder = this.Read();
			if (builder.Count == 0)
			{
				return EnumeratorLike.Empty<TKey>();
			}
			else
			{
				return EnumeratorLike.AsIEnumerator<TKey, Enumerator>(new Enumerator(builder.GetEnumerator()));
			}
		}

		IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

#pragma warning disable CA1034 // Nested types should not be visible
#pragma warning disable CA1815 // Override equals and operator equals on value types
		private struct Enumerator : IEnumeratorLike<TKey>
		{
			private ValueDictionaryBuilder<TKey, TValue>.Enumerator inner;

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			internal Enumerator(ValueDictionaryBuilder<TKey, TValue>.Enumerator inner)
			{
				this.inner = inner;
			}

			/// <inheritdoc/>
			public readonly TKey Current
			{
				[MethodImpl(MethodImplOptions.AggressiveInlining)]
				get => this.inner.Current.Key;
			}

			/// <inheritdoc/>
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public bool MoveNext() => this.inner.MoveNext();
		}
#pragma warning restore CA1815 // Override equals and operator equals on value types
#pragma warning restore CA1034 // Nested types should not be visible

		public void Add(TKey item) => throw ImmutableException();

		public bool Remove(TKey item) => throw ImmutableException();

		public void Clear() => throw ImmutableException();
	}

	private sealed class ValueCollection : ICollection<TValue>, IReadOnlyCollection<TValue>
	{
		private readonly ValueDictionaryBuilder<TKey, TValue> builder;
		private readonly int version;

		public ValueCollection(ValueDictionaryBuilder<TKey, TValue> builder, int version)
		{
			this.builder = builder;
			this.version = version;
		}

		private ValueDictionaryBuilder<TKey, TValue> Read()
		{
			if (this.version != this.builder.version)
			{
				throw new InvalidOperationException("ValueDictionaryBuilder.Values collection invalidated because of modifications to the builder.");
			}

			return this.builder;
		}

		public int Count => this.Read().Count;

		public bool IsReadOnly => true;

		public bool Contains(TValue item) => this.Read().ContainsValue(item);

		public void CopyTo(TValue[] array, int index)
		{
			if (array == null)
			{
				throw new ArgumentNullException(nameof(array));
			}

			if (index < 0 || index > array.Length)
			{
				throw new ArgumentOutOfRangeException(nameof(index));
			}

			var builder = this.Read();

			if (array.Length - index < builder.Count)
			{
				throw new ArgumentException("Destination too small", nameof(array));
			}

			foreach (var entry in builder)
			{
				array[index++] = entry.Value;
			}
		}

		public IEnumerator<TValue> GetEnumerator()
		{
			var builder = this.Read();
			if (builder.Count == 0)
			{
				return EnumeratorLike.Empty<TValue>();
			}
			else
			{
				return EnumeratorLike.AsIEnumerator<TValue, Enumerator>(new Enumerator(builder.GetEnumerator()));
			}
		}

		IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

#pragma warning disable CA1034 // Nested types should not be visible
#pragma warning disable CA1815 // Override equals and operator equals on value types
		private struct Enumerator : IEnumeratorLike<TValue>
		{
			private ValueDictionaryBuilder<TKey, TValue>.Enumerator inner;

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			internal Enumerator(ValueDictionaryBuilder<TKey, TValue>.Enumerator inner)
			{
				this.inner = inner;
			}

			/// <inheritdoc/>
			public readonly TValue Current
			{
				[MethodImpl(MethodImplOptions.AggressiveInlining)]
				get => this.inner.Current.Value;
			}

			/// <inheritdoc/>
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public bool MoveNext() => this.inner.MoveNext();
		}
#pragma warning restore CA1815 // Override equals and operator equals on value types
#pragma warning restore CA1034 // Nested types should not be visible

		public void Add(TValue item) => throw ImmutableException();

		public bool Remove(TValue item) => throw ImmutableException();

		public void Clear() => throw ImmutableException();
	}

	/// <summary>
	/// Get a string representation of the collection for debugging purposes.
	/// The format is not stable and may change without prior notice.
	/// </summary>
	[Pure]
	public override string ToString()
	{
		if (this.Count == 0)
		{
			return "[]";
		}

		var builder = new StringBuilder();
		builder.Append('[');

		var index = 0;
		foreach (var entry in this)
		{
			if (index > 0)
			{
				builder.Append(", ");
			}

			var keyString = entry.Key?.ToString() ?? "null";
			var valueString = entry.Value?.ToString() ?? "null";
			builder.Append(keyString);
			builder.Append(": ");
			builder.Append(valueString);

			index++;
		}

		builder.Append(']');
		return builder.ToString();
	}

	private static InvalidOperationException UnreachableException() => new("Unreachable");

	private static InvalidOperationException BuiltException() => new("Builder has already been built");

	private static NotSupportedException ImmutableException() => new("Collection is immutable");
}
