using System.Collections;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Badeend.ValueCollections.Internals;

namespace Badeend.ValueCollections;

/// <summary>
/// Initialization methods for <see cref="ValueList{T}"/>.
/// </summary>
public static class ValueList
{
	/// <summary>
	/// Copy the <paramref name="items"/> into a new <see cref="ValueList{T}"/>.
	/// </summary>
	[Pure]
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static ValueList<T> Create<T>(ReadOnlySpan<T> items) => ValueList<T>.CreateImmutable(new(items));

	/// <summary>
	/// Create a new empty <see cref="ValueList{T}.Builder"/>. This builder can
	/// then be used to efficiently construct an immutable <see cref="ValueList{T}"/>.
	/// </summary>
	[Pure]
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static ValueList<T>.Builder CreateBuilder<T>() => ValueList<T>.Builder.Create();

	/// <summary>
	/// Create a new empty <see cref="ValueList{T}.Builder"/> with the specified
	/// initial <paramref name="minimumCapacity"/>. This builder can then be used to
	/// efficiently construct an immutable <see cref="ValueList{T}"/>.
	/// </summary>
	/// <exception cref="ArgumentOutOfRangeException">
	/// <paramref name="minimumCapacity"/> is less than 0.
	/// </exception>
	[Pure]
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static ValueList<T>.Builder CreateBuilder<T>(int minimumCapacity) => ValueList<T>.Builder.CreateWithCapacity(minimumCapacity);

	/// <summary>
	/// Create a new <see cref="ValueList{T}.Builder"/> with the provided
	/// <paramref name="items"/> as its initial content.
	/// </summary>
	[Pure]
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static ValueList<T>.Builder CreateBuilder<T>(ReadOnlySpan<T> items)
	{
		var builder = CreateBuilder<T>(items.Length);
		builder.AddRange(items);
		return builder;
	}
}

/// <summary>
/// An immutable, thread-safe list with value semantics.
/// </summary>
/// <remarks>
/// Constructing new instances can be done using
/// <see cref="ValueList.CreateBuilder{T}()"/> or <see cref="ValueList{T}.ToBuilder()"/>.
/// For creating ValueLists, <see cref="ValueList{T}.Builder"/> is generally more
/// efficient than <see cref="List{T}"/>.
///
/// ValueLists have "structural equality". This means that two lists
/// are considered equal only when their contents are equal. As long as a value
/// is present in a ValueList, its hash code may not change.
///
/// Taking a subslice with <see cref="ValueList{T}.Slice(int)"/> and
/// <see cref="ValueList{T}.Slice(int, int)"/> is very cheap as it reuses the
/// same allocation.
/// </remarks>
/// <typeparam name="T">The type of items in the list.</typeparam>
[CollectionBuilder(typeof(ValueList), nameof(ValueList.Create))]
public sealed partial class ValueList<T> : IReadOnlyList<T>, IList<T>, IEquatable<ValueList<T>>
{
	/// <summary>
	/// Get an empty list.
	///
	/// This does not allocate any memory.
	/// </summary>
	[Pure]
	public static ValueList<T> Empty { get; } = new(new(), BuilderState.InitialImmutable);

	private RawList<T> inner;

	// See the BuilderState utility class for more info.
	private int state;

	/// <summary>
	/// Length of the list.
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

	/// <inheritdoc/>
	bool ICollection<T>.IsReadOnly => true;

	/// <summary>
	/// Get an item from the list at the specified <paramref name="index"/>.
	/// </summary>
	public ref readonly T this[int index]
	{
		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => ref this.inner[index];
	}

	/// <inheritdoc/>
	T IReadOnlyList<T>.this[int index] => this[index];

	/// <inheritdoc/>
	T IList<T>.this[int index]
	{
		get => this[index];
		set => ThrowHelpers.ThrowNotSupportedException_CollectionImmutable();
	}

	private ValueList(RawList<T> inner, int state)
	{
		this.inner = inner;
		this.state = state;
	}

	internal static ValueList<T> CreateImmutable(RawList<T> inner)
	{
		if (inner.Count == 0)
		{
			return Empty;
		}

		return new(inner, BuilderState.InitialImmutable);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static ValueList<T> CreateMutable(RawList<T> inner) => new(inner, BuilderState.InitialMutable);

	/// <summary>
	/// Access the list's contents using a <see cref="ValueSlice{T}"/>.
	/// </summary>
	[Pure]
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public ValueSlice<T> AsValueSlice() => new ValueSlice<T>(this.inner.items, 0, this.inner.size);

	/// <summary>
	/// Access the list's contents using a <see cref="ReadOnlySpan{T}"/>.
	/// </summary>
	[Pure]
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public ReadOnlySpan<T> AsSpan() => this.inner.AsSpan();

	/// <summary>
	/// Access the list's contents using a <see cref="ReadOnlyMemory{T}"/>.
	/// </summary>
	[Pure]
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public ReadOnlyMemory<T> AsMemory() => this.inner.AsMemory();

	/// <summary>
	/// Create a subslice, starting at <paramref name="offset"/>.
	/// </summary>
	/// <param name="offset">The index at which to begin the subslice.</param>
	/// <exception cref="ArgumentOutOfRangeException">
	///   <paramref name="offset"/> is below <c>0</c> or greater than the
	///   current list's length.
	/// </exception>
	/// <remarks>
	/// This is an <c>O(1)</c> operation and does not allocates any memory.
	/// </remarks>
	[Pure]
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public ValueSlice<T> Slice(int offset) => this.AsValueSlice().Slice(offset);

	/// <summary>
	/// Create a subslice with a specified <paramref name="length"/>,
	/// starting at <paramref name="offset"/>.
	/// </summary>
	/// <param name="offset">The index at which to begin the subslice.</param>
	/// <param name="length">The length of the new subslice.</param>
	/// <exception cref="ArgumentOutOfRangeException">
	///   <paramref name="offset"/> is below <c>0</c> or greater than the
	///   current list's length.
	/// </exception>
	/// <exception cref="ArgumentOutOfRangeException">
	///   <paramref name="length"/> is below <c>0</c> or would extend beyond the
	///   current list's length.
	/// </exception>
	/// <remarks>
	/// This is an <c>O(1)</c> operation and does not allocates any memory.
	/// </remarks>
	[Pure]
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public ValueSlice<T> Slice(int offset, int length) => this.AsValueSlice().Slice(offset, length);

	/// <summary>
	/// Copy the contents of the list into a new array.
	/// </summary>
	[Pure]
	public T[] ToArray() => this.inner.ToArray();

	/// <summary>
	/// Copy the contents of the list into an existing <see cref="Span{T}"/>.
	/// </summary>
	/// <exception cref="ArgumentException">
	///   <paramref name="destination"/> is shorter than the source slice.
	/// </exception>
	public void CopyTo(Span<T> destination) => this.inner.CopyTo(destination);

	/// <summary>
	/// Attempt to copy the contents of the list into an existing
	/// <see cref="Span{T}"/>. If the <paramref name="destination"/> is too short,
	/// no items are copied and the method returns <see langword="false"/>.
	/// </summary>
	public bool TryCopyTo(Span<T> destination) => this.inner.TryCopyTo(destination);

	/// <summary>
	/// Create a new <see cref="ValueList{T}.Builder"/> with this list as its
	/// initial content. This builder can then be used to efficiently construct
	/// a new immutable <see cref="ValueList{T}"/>.
	/// </summary>
	/// <remarks>
	/// The capacity of the returned builder may be larger than the size of this
	/// list. How much larger exactly is undefined.
	/// </remarks>
	[Pure]
	public Builder ToBuilder() => ValueList.CreateBuilder(this.AsSpan());

	/// <summary>
	/// Create a new <see cref="ValueList{T}.Builder"/> with a capacity of at
	/// least <paramref name="minimumCapacity"/> and with this list as its
	/// initial content. This builder can then be used to efficiently construct
	/// a new immutable <see cref="ValueList{T}"/>.
	/// </summary>
	/// <exception cref="ArgumentOutOfRangeException">
	///   <paramref name="minimumCapacity"/> is less than 0.
	/// </exception>
	/// <remarks>
	/// This is functionally equivalent to:
	/// <code>
	/// list.ToBuilder().EnsureCapacity(minimumCapacity)
	/// </code>
	/// but without unnecessary intermediate copies.
	/// </remarks>
	[Pure]
	public Builder ToBuilder(int minimumCapacity)
	{
		if (minimumCapacity < 0)
		{
			ThrowHelpers.ThrowArgumentOutOfRangeException(ThrowHelpers.Argument.minimumCapacity);
		}

		var capacity = Math.Max(minimumCapacity, this.Count);

		return ValueList.CreateBuilder<T>(capacity).AddRange(this.AsSpan());
	}

	/// <summary>
	/// Return the index of the first occurrence of <paramref name="item"/> in
	/// the list, or <c>-1</c> if not found.
	/// </summary>
	[Pure]
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public int IndexOf(T item) => this.inner.IndexOf(item);

	/// <summary>
	/// Return the index of the last occurrence of <paramref name="item"/> in
	/// the list, or <c>-1</c> if not found.
	/// </summary>
	[Pure]
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public int LastIndexOf(T item) => this.inner.LastIndexOf(item);

	/// <summary>
	/// Returns <see langword="true"/> when the list contains the specified
	/// <paramref name="item"/>.
	/// </summary>
	[Pure]
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool Contains(T item) => this.inner.Contains(item);

	/// <summary>
	/// Perform a binary search for <paramref name="item"/> within the list.
	/// The list is assumed to already be sorted. This uses the
	/// <see cref="Comparer{T}.Default">Default</see> comparer and throws if
	/// <typeparamref name="T"/> is not comparable. If the item is found, its
	/// index is returned. Otherwise a negative value is returned representing
	/// the bitwise complement of the index where the item should be inserted.
	/// </summary>
	[Pure]
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public int BinarySearch(T item) => this.inner.BinarySearch(item);

	/// <inheritdoc/>
	bool ICollection<T>.Contains(T item) => this.inner.Contains(item);

	/// <inheritdoc/>
	int IList<T>.IndexOf(T item) => this.inner.IndexOf(item);

	/// <inheritdoc/>
	void ICollection<T>.CopyTo(T[] array, int arrayIndex)
	{
		if (array is null)
		{
			ThrowHelpers.ThrowArgumentNullException(ThrowHelpers.Argument.array);
		}

		this.inner.CopyTo(array.AsSpan().Slice(arrayIndex));
	}

	/// <inheritdoc/>
	[Pure]
	public sealed override int GetHashCode()
	{
		if (BuilderState.ReadHashCode(ref this.state, out var hashCode))
		{
			return hashCode;
		}

		return BuilderState.AdjustAndStoreHashCode(ref this.state, this.inner.GetSequenceHashCode());
	}

	/// <summary>
	/// Returns <see langword="true"/> when the two lists have identical length
	/// and content.
	/// </summary>
	[Pure]
	public bool Equals(ValueList<T>? other) => other is not null && RawList.SequenceEqual(ref this.inner, ref other.inner);

#pragma warning disable CS0809 // Obsolete member overrides non-obsolete member
	/// <inheritdoc/>
	[Pure]
	[Obsolete("Use == instead.")]
	[EditorBrowsable(EditorBrowsableState.Never)]
	public sealed override bool Equals(object? obj) => obj is ValueList<T> other && EqualsUtil(this, other);
#pragma warning restore CS0809 // Obsolete member overrides non-obsolete member

	/// <summary>
	/// Check for equality.
	/// </summary>
	[Pure]
	public static bool operator ==(ValueList<T>? left, ValueList<T>? right) => EqualsUtil(left, right);

	/// <summary>
	/// Check for inequality.
	/// </summary>
	[Pure]
	public static bool operator !=(ValueList<T>? left, ValueList<T>? right) => !EqualsUtil(left, right);

	private static bool EqualsUtil(ValueList<T>? left, ValueList<T>? right)
	{
		if (left is null)
		{
			return right is null;
		}

		if (right is null)
		{
			return false;
		}

		return RawList.SequenceEqual(ref left.inner, ref right.inner);
	}

#pragma warning disable CA2225 // Operator overloads have named alternates
	/// <summary>
	/// Access the list as a <see cref="ValueSlice{T}"/>.
	/// </summary>
	[Pure]
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static implicit operator ValueSlice<T>(ValueList<T> list) => list.AsValueSlice();

	/// <summary>
	/// Access the list as a <see cref="ReadOnlySpan{T}"/>.
	/// </summary>
	[Pure]
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static implicit operator ReadOnlySpan<T>(ValueList<T> list) => list.AsSpan();

	/// <summary>
	/// Access the list as a <see cref="ReadOnlyMemory{T}"/>.
	/// </summary>
	[Pure]
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static implicit operator ReadOnlyMemory<T>(ValueList<T> list) => list.AsMemory();
#pragma warning restore CA2225 // Operator overloads have named alternates

	/// <summary>
	/// Returns an enumerator for this <see cref="ValueList{T}"/>.
	///
	/// Typically, you don't need to manually call this method, but instead use
	/// the built-in <c>foreach</c> syntax.
	/// </summary>
	[Pure]
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
	/// Enumerator for <see cref="ValueList{T}"/>.
	/// </summary>
#pragma warning disable CA1034 // Nested types should not be visible
#pragma warning disable CA1815 // Override equals and operator equals on value types
	[StructLayout(LayoutKind.Auto)]
	public struct Enumerator : IRefEnumeratorLike<T>
	{
		private RawList<T>.Enumerator inner;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal Enumerator(ValueList<T> list)
		{
			this.inner = list.inner.GetEnumerator();
		}

		/// <inheritdoc/>
		public readonly ref readonly T Current
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => ref this.inner.Current;
		}

		/// <inheritdoc/>
		readonly T IEnumeratorLike<T>.Current => this.Current;

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
	void ICollection<T>.Add(T item) => ThrowHelpers.ThrowNotSupportedException_CollectionImmutable();

	/// <inheritdoc/>
	void ICollection<T>.Clear() => ThrowHelpers.ThrowNotSupportedException_CollectionImmutable();

	/// <inheritdoc/>
	void IList<T>.Insert(int index, T item) => ThrowHelpers.ThrowNotSupportedException_CollectionImmutable();

	/// <inheritdoc/>
	bool ICollection<T>.Remove(T item)
	{
		ThrowHelpers.ThrowNotSupportedException_CollectionImmutable();
		return false;
	}

	/// <inheritdoc/>
	void IList<T>.RemoveAt(int index) => ThrowHelpers.ThrowNotSupportedException_CollectionImmutable();
}
