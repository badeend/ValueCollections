using System.Collections;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
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
		where TKey : notnull => ValueDictionary<TKey, TValue>.CreateImmutableUnsafe(new(items));

	/// <summary>
	/// Create a new empty <see cref="ValueDictionary{TKey, TValue}.Builder"/>. This builder can
	/// then be used to efficiently construct an immutable <see cref="ValueDictionary{TKey, TValue}"/>.
	/// </summary>
	[Pure]
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static ValueDictionary<TKey, TValue>.Builder CreateBuilder<TKey, TValue>()
		where TKey : notnull => ValueDictionary<TKey, TValue>.Builder.CreateUnsafe(new());

	/// <summary>
	/// Construct a new empty dictionary builder with at least the specified
	/// initial capacity.
	/// </summary>
	/// <exception cref="ArgumentOutOfRangeException">
	///   <paramref name="minimumCapacity"/> is less than 0.
	/// </exception>
	[Pure]
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static ValueDictionary<TKey, TValue>.Builder CreateBuilderWithCapacity<TKey, TValue>(int minimumCapacity)
		where TKey : notnull => ValueDictionary<TKey, TValue>.Builder.CreateUnsafe(new(minimumCapacity));

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
		where TKey : notnull => ValueDictionary<TKey, TValue>.Builder.CreateUnsafe(new(items));
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
[DebuggerDisplay("Count = {Count}")]
[DebuggerTypeProxy(typeof(ValueDictionary<,>.DebugView))]
public sealed partial class ValueDictionary<TKey, TValue> : IDictionary<TKey, TValue>, IReadOnlyDictionary<TKey, TValue>, IEquatable<ValueDictionary<TKey, TValue>>
	where TKey : notnull
{
	/// <summary>
	/// Get a new empty dictionary.
	///
	/// This does not allocate any memory.
	/// </summary>
	[Pure]
	public static ValueDictionary<TKey, TValue> Empty { get; } = new();

	private RawDictionary<TKey, TValue> inner;

	// See the BuilderState utility class for more info.
	private int state;

	// The `Builder.Collection` doubles as an "extended metadata" object,
	// containing infrequently used fields. This keeps the ValueDictionary type
	// itself as small as possible to serve the majority of use cases.
	//
	// Beware: this field may be accessed from multiple threads.
	private Builder.Collection? cachedBuilderCollection;

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
	public ref readonly TValue this[TKey key]
	{
		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => ref this.inner.GetValueRef(key);
	}

	/// <inheritdoc/>
	TValue IReadOnlyDictionary<TKey, TValue>.this[TKey key] => this[key];

	/// <inheritdoc/>
	TValue IDictionary<TKey, TValue>.this[TKey key]
	{
		get => this[key];
		set => throw ImmutableException();
	}

	/// <inheritdoc/>
	bool ICollection<KeyValuePair<TKey, TValue>>.IsReadOnly => true;

	// Creates the one and only Empty instance.
	private ValueDictionary()
	{
		this.inner = new();
		this.state = BuilderState.InitialImmutable;

		// Pre-emptively compute the hashcode to prevent ending up on the
		// slow path in various places.
		BuilderState.AdjustAndStoreHashCode(ref this.state, this.inner.GetStructuralHashCode());
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private ValueDictionary(RawDictionary<TKey, TValue> inner, int state)
	{
		this.inner = inner;
		this.state = state;
	}

	internal static ValueDictionary<TKey, TValue> CreateImmutableUnsafe(RawDictionary<TKey, TValue> inner)
	{
		if (inner.Count == 0)
		{
			return Empty;
		}

		return new(inner, BuilderState.InitialImmutable);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static ValueDictionary<TKey, TValue> CreateMutableUnsafe(RawDictionary<TKey, TValue> inner) => new(inner, BuilderState.InitialMutable);

	// The RawDictionary is expected to be immutable.
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static ValueDictionary<TKey, TValue> CreateCowUnsafe(RawDictionary<TKey, TValue> inner) => new(inner, BuilderState.Cow);

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
		if (Utilities.IsReuseWorthwhile(this.inner.Capacity, this.inner.Count))
		{
			return Builder.CreateCowUnsafe(this.inner);
		}
		else
		{
			return Builder.CreateUnsafe(new(ref this.inner));
		}
	}

	/// <summary>
	/// Determines whether this dictionary contains an element with the specified value.
	/// </summary>
	/// <remarks>
	/// This performs a linear scan through the dictionary.
	/// </remarks>
	[Pure]
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool ContainsValue(TValue value) => this.inner.ContainsValue(value);

	/// <summary>
	/// Determines whether this dictionary contains an element with the specified key.
	/// </summary>
	[Pure]
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool ContainsKey(TKey key) => this.inner.ContainsKey(key);

	/// <inheritdoc/>
	bool ICollection<KeyValuePair<TKey, TValue>>.Contains(KeyValuePair<TKey, TValue> item) => this.inner.Contains(item);

	/// <summary>
	/// Attempt to get the value associated with the specified <paramref name="key"/>.
	/// Returns <see langword="false"/> when the key was not found.
	/// </summary>
#pragma warning disable CS8767 // Nullability of reference types in type of parameter doesn't match implicitly implemented member (possibly because of nullability attributes).
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value) => this.inner.TryGetValue(key, out value);
#pragma warning restore CS8767 // Nullability of reference types in type of parameter doesn't match implicitly implemented member (possibly because of nullability attributes).

	private static readonly TValue? DefaultValue = default!;

	/// <summary>
	/// Attempt to get the value associated with the specified <paramref name="key"/>.
	/// Returns <see langword="default"/> when the key was not found.
	/// </summary>
	[Pure]
	public ref readonly TValue? GetValueOrDefault(TKey key)
	{
		ref readonly var value = ref this.inner.GetValueRefOrNullRef(key);
		if (Polyfills.IsNullRef(in value))
		{
			return ref DefaultValue;
		}

		return ref value;
	}

	/// <summary>
	/// Attempt to get the value associated with the specified <paramref name="key"/>.
	/// Returns <paramref name="defaultValue"/> when the key was not found.
	/// </summary>
	[Pure]
	public TValue GetValueOrDefault(TKey key, TValue defaultValue)
	{
		ref readonly var value = ref this.inner.GetValueRefOrNullRef(key);
		if (Polyfills.IsNullRef(in value))
		{
			return defaultValue;
		}

		return value;
	}

	/// <inheritdoc/>
	void ICollection<KeyValuePair<TKey, TValue>>.CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex) => this.inner.CopyTo(array, arrayIndex);

	// Accessible through extension method.
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal ref readonly TValue GetValueRefOrNullRefUnsafe(TKey key) => ref this.inner.GetValueRefOrNullRef(key);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private Builder.Collection GetBuilderCollection()
	{
		// Beware: this cache field may be assigned to from multiple threads.
		return this.cachedBuilderCollection ??= new Builder.Collection(new(this));
	}

	/// <inheritdoc/>
	[Pure]
	public sealed override int GetHashCode()
	{
		if (BuilderState.ReadHashCode(ref this.state, out var hashCode))
		{
			return hashCode;
		}

		return BuilderState.AdjustAndStoreHashCode(ref this.state, this.inner.GetStructuralHashCode());
	}

	/// <summary>
	/// Returns <see langword="true"/> when the two dictionaries have identical
	/// length and content.
	/// </summary>
	[Pure]
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
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
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool operator ==(ValueDictionary<TKey, TValue>? left, ValueDictionary<TKey, TValue>? right) => EqualsUtil(left, right);

	/// <summary>
	/// Check for inequality.
	/// </summary>
	[Pure]
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool operator !=(ValueDictionary<TKey, TValue>? left, ValueDictionary<TKey, TValue>? right) => !EqualsUtil(left, right);

	private static bool EqualsUtil(ValueDictionary<TKey, TValue>? left, ValueDictionary<TKey, TValue>? right)
	{
		if (left is null)
		{
			return right is null;
		}

		if (right is null)
		{
			return false;
		}

		return left.inner.StructuralEquals(ref right.inner);
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
		private RawDictionary<TKey, TValue>.Enumerator inner;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal Enumerator(ValueDictionary<TKey, TValue> dictionary)
		{
			this.inner = dictionary.inner.GetEnumerator();
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
	public override string ToString() => this.inner.ToString();

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

	internal sealed class DebugView(ValueDictionary<TKey, TValue> dictionary)
	{
		[DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
		internal Entry[] Items => CreateEntries(in dictionary.inner);

		[DebuggerDisplay("{Value}", Name = "[{Key}]")]
		internal readonly struct Entry
		{
			[DebuggerBrowsable(DebuggerBrowsableState.Collapsed)]
			internal required TKey Key { get; init; }

			[DebuggerBrowsable(DebuggerBrowsableState.Collapsed)]
			internal required TValue Value { get; init; }
		}

		internal static Entry[] CreateEntries(ref readonly RawDictionary<TKey, TValue> dictionary)
		{
			var items = new Entry[dictionary.Count];
			var index = 0;
			foreach (var entry in dictionary)
			{
				items[index++] = new Entry
				{
					Key = entry.Key,
					Value = entry.Value,
				};
			}

			return items;
		}

		internal static TKey[] CreateKeys(ref readonly RawDictionary<TKey, TValue> dictionary)
		{
			var items = new TKey[dictionary.Count];
			var index = 0;

			foreach (var entry in dictionary)
			{
				items[index++] = entry.Key;
			}

			return items;
		}

		internal static TValue[] CreateValues(ref readonly RawDictionary<TKey, TValue> dictionary)
		{
			var items = new TValue[dictionary.Count];
			var index = 0;

			foreach (var entry in dictionary)
			{
				items[index++] = entry.Value;
			}

			return items;
		}
	}
}
