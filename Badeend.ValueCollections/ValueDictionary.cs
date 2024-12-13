using System.Collections;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Badeend.ValueCollections.Internals;

namespace Badeend.ValueCollections;

/// <summary>
/// Initialization methods for <see cref="ValueDictionary{TKey, TValue}"/>.
/// </summary>
[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "The accompanying generic class _does_ implement the correct interfaces.")]
public static class ValueDictionary
{
	/// <summary>
	/// Copy the <paramref name="items"/> into a new <see cref="ValueDictionary{TKey,TValue}"/>.
	/// </summary>
	/// <exception cref="ArgumentException">
	/// <paramref name="items"/> contains duplicate keys.
	/// </exception>
	[Pure]
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static ValueDictionary<TKey, TValue> Create<TKey, TValue>(scoped ReadOnlySpan<KeyValuePair<TKey, TValue>> items)
		where TKey : notnull => ValueDictionary<TKey, TValue>.CreateImmutableUnsafe(ValueDictionary<TKey, TValue>.SpanToDictionary(items));

	/// <summary>
	/// Create a new empty <see cref="ValueDictionary{TKey, TValue}.Builder"/>. This builder can
	/// then be used to efficiently construct an immutable <see cref="ValueDictionary{TKey, TValue}"/>.
	/// </summary>
	[Pure]
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static ValueDictionary<TKey, TValue>.Builder CreateBuilder<TKey, TValue>()
		where TKey : notnull => ValueDictionary<TKey, TValue>.Builder.CreateUnsafe(new());

#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
	/// <summary>
	/// Construct a new empty dictionary builder with at least the specified
	/// initial capacity.
	/// </summary>
	/// <remarks>
	/// Available on .NET Standard 2.1 and .NET Core 2.1 and higher.
	/// </remarks>
	/// <exception cref="ArgumentOutOfRangeException">
	///   <paramref name="minimumCapacity"/> is less than 0.
	/// </exception>
	[Pure]
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static ValueDictionary<TKey, TValue>.Builder CreateBuilderWithCapacity<TKey, TValue>(int minimumCapacity)
		where TKey : notnull => ValueDictionary<TKey, TValue>.Builder.CreateUnsafe(new(minimumCapacity));
#endif

	/// <summary>
	/// Create a new <see cref="ValueDictionary{TKey, TValue}.Builder"/> with the provided
	/// <paramref name="items"/> as its initial content.
	/// </summary>
	/// <exception cref="ArgumentException">
	/// <paramref name="items"/> contains duplicate keys.
	/// </exception>
	[Pure]
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static ValueDictionary<TKey, TValue>.Builder CreateBuilder<TKey, TValue>(scoped ReadOnlySpan<KeyValuePair<TKey, TValue>> items)
		where TKey : notnull => ValueDictionary<TKey, TValue>.Builder.CreateUnsafe(ValueDictionary<TKey, TValue>.SpanToDictionary(items));
}

/// <summary>
/// An immutable, thread-safe dictionary with value semantics.
/// </summary>
/// <remarks>
/// A dictionary provides a mapping from a set of keys to a set of values,
/// contains no duplicate keys, and stores its elements in no particular order.
///
/// Constructing new instances can be done using
/// <see cref="ValueDictionary.CreateBuilder{TKey, TValue}()"/> or
/// <see cref="ValueDictionary{TKey, TValue}.ToBuilder()"/>. For creating
/// ValueDictionaries, <see cref="ValueDictionary{TKey, TValue}.Builder"/> is
/// generally more efficient than <see cref="Dictionary{TKey, TValue}"/>.
///
/// ValueDictionaries have "structural equality". This means that two dictionaries
/// are considered equal only when their contents are equal. As long as a key or
/// a value is present in a ValueDictionary, its hash code may not change.
///
/// The order in which the entries are enumerated is undefined.
/// </remarks>
/// <typeparam name="TKey">The type of keys in the dictionary.</typeparam>
/// <typeparam name="TValue">The type of values in the dictionary.</typeparam>
public sealed partial class ValueDictionary<TKey, TValue> : IDictionary<TKey, TValue>, IReadOnlyDictionary<TKey, TValue>, IEquatable<ValueDictionary<TKey, TValue>>
	where TKey : notnull
{
	/// <summary>
	/// Get a new empty dictionary.
	///
	/// This does not allocate any memory.
	/// </summary>
	[Pure]
	public static ValueDictionary<TKey, TValue> Empty { get; } = new(new(), BuilderState.InitialImmutable);

	private Dictionary<TKey, TValue> inner;

	// See the BuilderState utility class for more info.
	private int state;

	/// <summary>
	/// Number of items in the dictionary.
	/// </summary>
	[Pure]
	public int Count
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => this.inner.Count;
	}

	/// <summary>
	/// Shortcut for <c>.Count == 0</c>.
	/// </summary>
	[Pure]
	public bool IsEmpty
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => this.inner.Count == 0;
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
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => this.inner[key];
	}

	/// <inheritdoc/>
	TValue IDictionary<TKey, TValue>.this[TKey key]
	{
		get => this[key];
		set => throw ImmutableException();
	}

	/// <inheritdoc/>
	bool ICollection<KeyValuePair<TKey, TValue>>.IsReadOnly => true;

	private ValueDictionary(Dictionary<TKey, TValue> inner, int state)
	{
		this.inner = inner;
		this.state = state;
	}

	internal static ValueDictionary<TKey, TValue> CreateImmutableUnsafe(Dictionary<TKey, TValue> inner)
	{
		if (inner.Count == 0)
		{
			return Empty;
		}

		return new(inner, BuilderState.InitialImmutable);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static ValueDictionary<TKey, TValue> CreateMutableUnsafe(Dictionary<TKey, TValue> inner) => new(inner, BuilderState.InitialMutable);

	// The RawDictionary is expected to be immutable.
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static ValueDictionary<TKey, TValue> CreateCowUnsafe(Dictionary<TKey, TValue> inner) => new(inner, BuilderState.Cow);

	internal static Dictionary<TKey, TValue> SpanToDictionary(scoped ReadOnlySpan<KeyValuePair<TKey, TValue>> items)
	{
		var dictionary = new Dictionary<TKey, TValue>(items.Length);

		foreach (var entry in items)
		{
			dictionary.Add(entry.Key, entry.Value);
		}

		return dictionary;
	}

	internal static Dictionary<TKey, TValue> EnumerableToDictionary(IEnumerable<KeyValuePair<TKey, TValue>> items)
	{
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_0_OR_GREATER
		return new Dictionary<TKey, TValue>(items);
#else
		Dictionary<TKey, TValue> dictionary;

		if (items is ICollection<KeyValuePair<TKey, TValue>> collection)
		{
			dictionary = new Dictionary<TKey, TValue>(collection.Count);
		}
		else
		{
			dictionary = new Dictionary<TKey, TValue>();
		}

		foreach (var entry in items)
		{
			dictionary.Add(entry.Key, entry.Value);
		}

		return dictionary;
#endif
	}

	/// <summary>
	/// Create a new <see cref="ValueDictionary{TKey, TValue}.Builder"/> with this
	/// dictionary as its initial content. This builder can then be used to
	/// efficiently construct a new immutable <see cref="ValueDictionary{TKey, TValue}"/>.
	/// </summary>
	/// <remarks>
	/// The capacity of the returned builder may be larger than the size of this
	/// dictionary. How much larger exactly is undefined.
	/// </remarks>
	[Pure]
	public Builder ToBuilder()
	{
		// if (Utilities.IsReuseWorthwhile(this.inner.Capacity, this.inner.Count))
		// {
		// return Builder.CreateCowUnsafe(this.inner);
		// }
		// else
		// {
		// return Builder.CreateUnsafe(new(ref this.inner));
		// }
		return Builder.CreateUnsafe(new(this.inner));
	}

	/// <summary>
	/// Determines whether this dictionary contains an element with the specified value.
	/// </summary>
	/// <remarks>
	/// This performs a linear scan through the dictionary.
	/// </remarks>
	[Pure]
	public bool ContainsValue(TValue value) => this.inner.ContainsValue(value);

	/// <summary>
	/// Determines whether this dictionary contains an element with the specified key.
	/// </summary>
	[Pure]
	public bool ContainsKey(TKey key) => this.inner.ContainsKey(key);

	/// <inheritdoc/>
	bool ICollection<KeyValuePair<TKey, TValue>>.Contains(KeyValuePair<TKey, TValue> item) => ((ICollection<KeyValuePair<TKey, TValue>>)this.inner).Contains(item);

	/// <summary>
	/// Attempt to get the value associated with the specified <paramref name="key"/>.
	/// Returns <see langword="false"/> when the key was not found.
	/// </summary>
#pragma warning disable CS8767 // Nullability of reference types in type of parameter doesn't match implicitly implemented member (possibly because of nullability attributes).
	public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value) => this.inner.TryGetValue(key, out value);
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

	/// <inheritdoc/>
	[Pure]
	public sealed override int GetHashCode()
	{
		if (BuilderState.ReadHashCode(ref this.state, out var hashCode))
		{
			return hashCode;
		}

		return BuilderState.AdjustAndStoreHashCode(ref this.state, this.ComputeHashCode());
	}

	private int ComputeHashCode()
	{
		var contentHasher = new UnorderedHashCode();

		foreach (var entry in this.inner)
		{
			contentHasher.Add(HashCode.Combine(entry.Key, entry.Value));
		}

		var hasher = new HashCode();
		hasher.Add(typeof(ValueDictionary<TKey, TValue>));
		hasher.Add(this.Count);
		hasher.AddUnordered(ref contentHasher);

		return hasher.ToHashCode();
	}

	/// <summary>
	/// Returns <see langword="true"/> when the two dictionaries have identical
	/// length and content.
	/// </summary>
	[Pure]
	public bool Equals(ValueDictionary<TKey, TValue>? other) => EqualsUtil(this, other);

#pragma warning disable CS0809 // Obsolete member overrides non-obsolete member
	/// <inheritdoc/>
	[Pure]
	[Obsolete("Use == instead.")]
	[EditorBrowsable(EditorBrowsableState.Never)]
	public sealed override bool Equals(object? obj) => obj is ValueDictionary<TKey, TValue> other && EqualsUtil(this, other);
#pragma warning restore CS0809 // Obsolete member overrides non-obsolete member

	/// <summary>
	/// Check for equality.
	/// </summary>
	[Pure]
	public static bool operator ==(ValueDictionary<TKey, TValue>? left, ValueDictionary<TKey, TValue>? right) => EqualsUtil(left, right);

	/// <summary>
	/// Check for inequality.
	/// </summary>
	[Pure]
	public static bool operator !=(ValueDictionary<TKey, TValue>? left, ValueDictionary<TKey, TValue>? right) => !EqualsUtil(left, right);

	private static bool EqualsUtil(ValueDictionary<TKey, TValue>? left, ValueDictionary<TKey, TValue>? right)
	{
		if (object.ReferenceEquals(left, right))
		{
			return true;
		}

		if (left is null || right is null)
		{
			return false;
		}

		if (left.Count != right.Count)
		{
			return false;
		}

		foreach (var item in left)
		{
			if (!right.Contains(item))
			{
				return false;
			}
		}

		return true;
	}

	/// <summary>
	/// Returns an enumerator for this <see cref="ValueDictionary{TKey, TValue}"/>.
	///
	/// Typically, you don't need to manually call this method, but instead use
	/// the built-in <c>foreach</c> syntax.
	/// </summary>
	[Pure]
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public Enumerator GetEnumerator() => new Enumerator(this);

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
	/// Enumerator for <see cref="ValueDictionary{TKey, TValue}"/>.
	/// </summary>
#pragma warning disable CA1034 // Nested types should not be visible
#pragma warning disable CA1815 // Override equals and operator equals on value types
	[StructLayout(LayoutKind.Auto)]
	public struct Enumerator : IEnumeratorLike<KeyValuePair<TKey, TValue>>
	{
		private ShufflingDictionaryEnumerator<TKey, TValue> inner;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal Enumerator(ValueDictionary<TKey, TValue> dictionary)
		{
			this.inner = new(dictionary.inner);
		}

		/// <inheritdoc/>
		public KeyValuePair<TKey, TValue> Current
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => this.inner.Current;
		}

		/// <inheritdoc/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool MoveNext() => this.inner.MoveNext();
	}
#pragma warning restore CA1815 // Override equals and operator equals on value types
#pragma warning restore CA1034 // Nested types should not be visible

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

	/// <inheritdoc/>
	void ICollection<KeyValuePair<TKey, TValue>>.Add(KeyValuePair<TKey, TValue> item) => throw ImmutableException();

	/// <inheritdoc/>
	void ICollection<KeyValuePair<TKey, TValue>>.Clear() => throw ImmutableException();

	/// <inheritdoc/>
	bool ICollection<KeyValuePair<TKey, TValue>>.Remove(KeyValuePair<TKey, TValue> item) => throw ImmutableException();

	/// <inheritdoc/>
	void IDictionary<TKey, TValue>.Add(TKey key, TValue value) => throw ImmutableException();

	/// <inheritdoc/>
	bool IDictionary<TKey, TValue>.Remove(TKey key) => throw ImmutableException();

	private static NotSupportedException ImmutableException() => new NotSupportedException("Collection is immutable");
}
