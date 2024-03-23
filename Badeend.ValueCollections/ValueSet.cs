using System.Collections;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;

namespace Badeend.ValueCollections;

/// <summary>
/// Initialization methods for <see cref="ValueSet{T}"/>.
/// </summary>
public static class ValueSet
{
	/// <summary>
	/// Copy the <paramref name="items"/> into a new <see cref="ValueSet{T}"/>.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static ValueSet<T> Create<T>(ReadOnlySpan<T> items) => ValueSet<T>.FromReadOnlySpan(items);

	/// <summary>
	/// Create a new empty <see cref="ValueSetBuilder{T}"/>. This builder can
	/// then be used to efficiently construct an immutable <see cref="ValueSet{T}"/>.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static ValueSetBuilder<T> Builder<T>() => new();

	/// <summary>
	/// Create a new <see cref="ValueSetBuilder{T}"/> with the provided
	/// <paramref name="items"/> as its initial content.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static ValueSetBuilder<T> Builder<T>(ReadOnlySpan<T> items) => ValueSetBuilder<T>.FromReadOnlySpan(items);
}

/// <summary>
/// An immutable, thread-safe set with value semantics.
///
/// A set is a collection that contains no duplicate elements, and whose elements
/// are in no particular order. Enumerating a set returns its contents in
/// undefined order and the may even be different on each iteration.
///
/// Constructing new instances can be done using
/// <see cref="ValueSet.Builder{T}()"/> or <see cref="ValueSet{T}.ToBuilder()"/>.
/// For creating ValueSets, <see cref="ValueSetBuilder{T}"/> is generally more
/// efficient than <see cref="HashSet{T}"/>.
///
/// ValueSets have "structural equality". This means that two sets
/// are considered equal only when their contents are equal. As long as a value
/// is present in a ValueSet, its hash code may not change.
/// </summary>
/// <typeparam name="T">The type of items in the set.</typeparam>
[CollectionBuilder(typeof(ValueSet), nameof(ValueSet.Create))]
#if NET5_0_OR_GREATER
public sealed class ValueSet<T> : IReadOnlyCollection<T>, ISet<T>, IEquatable<ValueSet<T>>, IReadOnlySet<T>
#else
public sealed class ValueSet<T> : IReadOnlyCollection<T>, ISet<T>, IEquatable<ValueSet<T>>
#endif
{
	private const int UninitializedHashCode = 0;

	/// <summary>
	/// Get an empty set.
	///
	/// This does not allocate any memory.
	/// </summary>
	public static ValueSet<T> Empty { get; } = new ValueSet<T>(new HashSet<T>());

	private readonly HashSet<T> items;

	/// <summary>
	/// Warning! This class promises to be thread-safe, yet this is a mutable field.
	/// </summary>
	private int hashCode = UninitializedHashCode;

	internal HashSet<T> Items
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => this.items;
	}

	/// <summary>
	/// Number of items in the set.
	/// </summary>
	public int Count
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => this.items.Count;
	}

	/// <summary>
	/// Shortcut for <c>.Count == 0</c>.
	/// </summary>
	public bool IsEmpty
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => this.items.Count == 0;
	}

	/// <inheritdoc/>
	bool ICollection<T>.IsReadOnly => true;

	private ValueSet(HashSet<T> items)
	{
		this.items = items;
	}

	internal static ValueSet<T> FromReadOnlySpan(ReadOnlySpan<T> items)
	{
		if (items.Length == 0)
		{
			return Empty;
		}

		return new(SpanToHashSet(items));
	}

	internal static HashSet<T> SpanToHashSet(ReadOnlySpan<T> items)
	{
#if NET472_OR_GREATER || NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_0_OR_GREATER
		var set = new HashSet<T>(items.Length);

		foreach (var item in items)
		{
			set.Add(item);
		}

		const int TrimThresholdAbsolute = 16;
		const int TrimThresholdRatio = 2;
		if (items.Length > TrimThresholdAbsolute && items.Length / set.Count >= TrimThresholdRatio)
		{
			set.TrimExcess();
		}

		return set;
#else
		var set = new HashSet<T>();

		foreach (var item in items)
		{
			set.Add(item);
		}

		return set;
#endif
	}

	internal static ValueSet<T> FromHashSetUnsafe(HashSet<T> items)
	{
		if (items.Count == 0)
		{
			return Empty;
		}

		return new(items);
	}

	/// <summary>
	/// Create a new <see cref="ValueSetBuilder{T}"/> with this set as its
	/// initial content. This builder can then be used to efficiently construct
	/// a new immutable <see cref="ValueSet{T}"/>.
	/// </summary>
	public ValueSetBuilder<T> ToBuilder() => ValueSetBuilder<T>.FromValueSet(this);

	/// <summary>
	/// Copy the contents of the set into a new array.
	/// </summary>
	public T[] ToArray()
	{
		var array = new T[this.Count];
		this.CopyToUnchecked(array);
		return array;
	}

	/// <inheritdoc/>
	void ICollection<T>.CopyTo(T[] array, int arrayIndex)
	{
		if (array is null)
		{
			throw new ArgumentNullException(nameof(array));
		}

		this.CopyTo(array.AsSpan().Slice(arrayIndex));
	}

	/// <summary>
	/// Copy the contents of the set into an existing <see cref="Span{T}"/>.
	/// </summary>
	/// <exception cref="ArgumentException">
	///   <paramref name="destination"/> is shorter than the source set.
	/// </exception>
	public void CopyTo(Span<T> destination)
	{
		if (destination.Length < this.Count)
		{
			throw new ArgumentException("Destination too short", nameof(destination));
		}

		this.CopyToUnchecked(destination);
	}

	/// <summary>
	/// Attempt to copy the contents of the set into an existing
	/// <see cref="Span{T}"/>. If the <paramref name="destination"/> is too short,
	/// no items are copied and the method returns <see langword="false"/>.
	/// </summary>
	public bool TryCopyTo(Span<T> destination)
	{
		if (destination.Length < this.Count)
		{
			return false;
		}

		this.CopyToUnchecked(destination);
		return true;
	}

	private void CopyToUnchecked(Span<T> destination)
	{
		var index = 0;
		foreach (var item in this)
		{
			destination[index] = item;
			index++;
		}
	}

	/// <summary>
	/// Returns <see langword="true"/> when the set contains the specified
	/// <paramref name="item"/>.
	/// </summary>
	public bool Contains(T item) => this.items.Contains(item);

	/// <inheritdoc/>
	public bool IsSubsetOf(IEnumerable<T> other)
	{
		if (other is ValueSet<T> otherSet)
		{
			return this.items.IsSubsetOf(otherSet.items);
		}

		return this.items.IsSubsetOf(other);
	}

	/// <inheritdoc/>
	public bool IsSupersetOf(IEnumerable<T> other)
	{
		if (other is ValueSet<T> otherSet)
		{
			return this.items.IsSupersetOf(otherSet.items);
		}

		return this.items.IsSupersetOf(other);
	}

	/// <inheritdoc/>
	public bool IsProperSupersetOf(IEnumerable<T> other)
	{
		if (other is ValueSet<T> otherSet)
		{
			return this.items.IsProperSupersetOf(otherSet.items);
		}

		return this.items.IsProperSupersetOf(other);
	}

	/// <inheritdoc/>
	public bool IsProperSubsetOf(IEnumerable<T> other)
	{
		if (other is ValueSet<T> otherSet)
		{
			return this.items.IsProperSubsetOf(otherSet.items);
		}

		return this.items.IsProperSubsetOf(other);
	}

	/// <inheritdoc/>
	public bool Overlaps(IEnumerable<T> other)
	{
		if (other is ValueSet<T> otherSet)
		{
			return this.items.Overlaps(otherSet.items);
		}

		return this.items.Overlaps(other);
	}

	/// <inheritdoc/>
	public bool SetEquals(IEnumerable<T> other)
	{
		if (other is ValueSet<T> otherSet)
		{
			return this.items.SetEquals(otherSet.items);
		}

		return this.items.SetEquals(other);
	}

	/// <inheritdoc/>
	public sealed override int GetHashCode()
	{
		var hashCode = Volatile.Read(ref this.hashCode);
		if (hashCode != UninitializedHashCode)
		{
			return hashCode;
		}

		hashCode = this.ComputeHashCode();
		Volatile.Write(ref this.hashCode, hashCode);
		return hashCode;
	}

	private int ComputeHashCode()
	{
		var contentHasher = new UnorderedHashCode();

		foreach (var item in this.items)
		{
			contentHasher.Add(item);
		}

		var hasher = new HashCode();
		hasher.Add(typeof(ValueSet<T>));
		hasher.Add(this.Count);
		hasher.AddUnordered(ref contentHasher);

		var hashCode = hasher.ToHashCode();
		if (hashCode == UninitializedHashCode)
		{
			// Never return 0, as that is our placeholder value.
			hashCode = 1;
		}

		return hashCode;
	}

	/// <summary>
	/// Returns <see langword="true"/> when the two sets have identical length
	/// and content.
	/// </summary>
	public bool Equals(ValueSet<T>? other) => EqualsUtil(this, other);

#pragma warning disable CS0809 // Obsolete member overrides non-obsolete member
	/// <inheritdoc/>
	[Obsolete("Use == instead.")]
	[EditorBrowsable(EditorBrowsableState.Never)]
	public sealed override bool Equals(object? obj) => obj is ValueSet<T> other && EqualsUtil(this, other);
#pragma warning restore CS0809 // Obsolete member overrides non-obsolete member

	/// <summary>
	/// Check for equality.
	/// </summary>
	public static bool operator ==(ValueSet<T>? left, ValueSet<T>? right) => EqualsUtil(left, right);

	/// <summary>
	/// Check for inequality.
	/// </summary>
	public static bool operator !=(ValueSet<T>? left, ValueSet<T>? right) => !EqualsUtil(left, right);

	private static bool EqualsUtil(ValueSet<T>? left, ValueSet<T>? right)
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

		foreach (var item in left.items)
		{
			if (!right.items.Contains(item))
			{
				return false;
			}
		}

		return true;
	}

	/// <summary>
	/// Returns an enumerator for this <see cref="ValueSet{T}"/>.
	///
	/// Typically, you don't need to manually call this method, but instead use
	/// the built-in <c>foreach</c> syntax.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public Enumerator GetEnumerator() => new Enumerator(this);

	/// <inheritdoc/>
	IEnumerator<T> IEnumerable<T>.GetEnumerator()
	{
		if (this.Count == 0)
		{
			return EnumeratorLike.Empty<T>();
		}
		else
		{
			return EnumeratorLike.AsIEnumerator<T, Enumerator>(new Enumerator(this));
		}
	}

	/// <inheritdoc/>
	IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable<T>)this).GetEnumerator();

	/// <summary>
	/// Enumerator for <see cref="ValueSet{T}"/>.
	/// </summary>
#pragma warning disable CA1034 // Nested types should not be visible
#pragma warning disable CA1815 // Override equals and operator equals on value types
	public struct Enumerator : IEnumeratorLike<T>
	{
		private HashSet<T>.Enumerator inner;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal Enumerator(ValueSet<T> set)
		{
			this.inner = set.items.GetEnumerator();
		}

		/// <inheritdoc/>
		public T Current
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
	public override string ToString()
	{
		if (this.Count == 0)
		{
			return "ValueSet(Count: 0) { }";
		}

		var builder = new StringBuilder();
		builder.Append("ValueSet(Count: ");
		builder.Append(this.Count);
		builder.Append(") { ");

		var index = 0;
		foreach (var item in this)
		{
			if (index > 0)
			{
				builder.Append(", ");
			}

			var itemString = item?.ToString() ?? "null";
			builder.Append(itemString);

			index++;
		}

		builder.Append(" }");
		return builder.ToString();
	}

	/// <inheritdoc/>
	void ICollection<T>.Add(T item) => throw CreateImmutableException();

	/// <inheritdoc/>
	void ICollection<T>.Clear() => throw CreateImmutableException();

	/// <inheritdoc/>
	bool ICollection<T>.Remove(T item) => throw CreateImmutableException();

	/// <inheritdoc/>
	bool ISet<T>.Add(T item) => throw CreateImmutableException();

	/// <inheritdoc/>
	void ISet<T>.UnionWith(IEnumerable<T> other) => throw CreateImmutableException();

	/// <inheritdoc/>
	void ISet<T>.IntersectWith(IEnumerable<T> other) => throw CreateImmutableException();

	/// <inheritdoc/>
	void ISet<T>.ExceptWith(IEnumerable<T> other) => throw CreateImmutableException();

	/// <inheritdoc/>
	void ISet<T>.SymmetricExceptWith(IEnumerable<T> other) => throw CreateImmutableException();

	private static NotSupportedException CreateImmutableException() => new NotSupportedException("Collection is immutable");
}
