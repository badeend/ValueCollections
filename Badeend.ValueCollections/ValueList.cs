using System.Collections;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

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
	public static ValueList<T> Create<T>(ReadOnlySpan<T> items) => ValueList<T>.FromArrayUnsafe(items.ToArray());

	/// <summary>
	/// Create a new empty <see cref="ValueList{T}.Builder"/>. This builder can
	/// then be used to efficiently construct an immutable <see cref="ValueList{T}"/>.
	/// </summary>
	[Pure]
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static ValueList<T>.Builder CreateBuilder<T>() => ValueList<T>.Builder.Create();

	/// <summary>
	/// Create a new empty <see cref="ValueList{T}.Builder"/> with the specified
	/// initial <paramref name="capacity"/>. This builder can then be used to
	/// efficiently construct an immutable <see cref="ValueList{T}"/>.
	/// </summary>
	[Pure]
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static ValueList<T>.Builder CreateBuilder<T>(int capacity) => ValueList<T>.Builder.CreateWithCapacity(capacity);

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
	private const int UninitializedHashCode = 0;

	/// <summary>
	/// Get an empty list.
	///
	/// This does not allocate any memory.
	/// </summary>
	[Pure]
	public static ValueList<T> Empty { get; } = new ValueList<T>(Array.Empty<T>(), 0);

	private readonly List<T> items;

	/// <summary>
	/// Warning! This class promises to be thread-safe, yet this is a mutable field.
	/// </summary>
	private int hashCode = UninitializedHashCode;

	internal T[] Items
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => UnsafeHelpers.GetBackingArray(this.items);
	}

	internal int Capacity
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => this.items.Capacity;
	}

	/// <summary>
	/// Length of the list.
	/// </summary>
	[Pure]
	public int Count
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => this.items.Count;
	}

	/// <summary>
	/// Shortcut for <c>.Count == 0</c>.
	/// </summary>
	[Pure]
	public bool IsEmpty
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => this.items.Count == 0;
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
		get
		{
			if (index < 0 || index >= this.items.Count)
			{
				throw new ArgumentOutOfRangeException(nameof(index));
			}

			return ref this.Items![index];
		}
	}

	/// <inheritdoc/>
	T IReadOnlyList<T>.this[int index] => this[index];

	/// <inheritdoc/>
	T IList<T>.this[int index]
	{
		get => this[index];
		set => throw CreateImmutableException();
	}

	private ValueList(T[] items, int count)
	{
		// TODO: don't copy! 

		this.items = new List<T>(count);

		for (int i = 0; i < count; i++)
		{
			this.items.Add(items[i]);
		}
	}

	internal static ValueList<T> FromArrayUnsafe(T[] items) => FromArrayUnsafe(items, items.Length);

	internal static ValueList<T> FromArrayUnsafe(T[] items, int count)
	{
		if (count == 0)
		{
			return Empty;
		}

		return new(items, count);
	}

	/// <summary>
	/// Access the list's contents using a <see cref="ValueSlice{T}"/>.
	/// </summary>
	[Pure]
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public ValueSlice<T> AsValueSlice() => new ValueSlice<T>(this.Items, 0, this.Count);

	/// <summary>
	/// Access the list's contents using a <see cref="ReadOnlySpan{T}"/>.
	/// </summary>
	[Pure]
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public ReadOnlySpan<T> AsSpan() => this.AsValueSlice().Span;

	/// <summary>
	/// Access the list's contents using a <see cref="ReadOnlyMemory{T}"/>.
	/// </summary>
	[Pure]
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public ReadOnlyMemory<T> AsMemory() => this.AsValueSlice().Memory;

	/// <summary>
	/// Create a subslice, starting at <paramref name="offset"/>.
	///
	/// This does not allocate any memory.
	/// </summary>
	/// <param name="offset">The index at which to begin the subslice.</param>
	/// <exception cref="ArgumentOutOfRangeException">
	///   <paramref name="offset"/> is below <c>0</c> or greater than the
	///   current list's length.
	/// </exception>
	[Pure]
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public ValueSlice<T> Slice(int offset) => this.AsValueSlice().Slice(offset);

	/// <summary>
	/// Create a subslice with a specified <paramref name="length"/>,
	/// starting at <paramref name="offset"/>.
	///
	/// This does not allocate any memory.
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
	[Pure]
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public ValueSlice<T> Slice(int offset, int length) => this.AsValueSlice().Slice(offset, length);

	/// <summary>
	/// Copy the contents of the list into a new array.
	/// </summary>
	[Pure]
	public T[] ToArray() => this.AsValueSlice().ToArray();

	/// <summary>
	/// Copy the contents of the list into an existing <see cref="Span{T}"/>.
	/// </summary>
	/// <exception cref="ArgumentException">
	///   <paramref name="destination"/> is shorter than the source slice.
	/// </exception>
	public void CopyTo(Span<T> destination) => this.AsValueSlice().CopyTo(destination);

	/// <summary>
	/// Attempt to copy the contents of the list into an existing
	/// <see cref="Span{T}"/>. If the <paramref name="destination"/> is too short,
	/// no items are copied and the method returns <see langword="false"/>.
	/// </summary>
	public bool TryCopyTo(Span<T> destination) => this.AsValueSlice().TryCopyTo(destination);

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
	public Builder ToBuilder() => Builder.FromValueList(this);

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
			throw new ArgumentOutOfRangeException(nameof(minimumCapacity));
		}

		if (minimumCapacity <= this.Count)
		{
			return Builder.FromValueList(this);
		}
		else
		{
			return ValueList.CreateBuilder<T>(minimumCapacity).AddRange(this.AsSpan());
		}
	}

	/// <summary>
	/// Return the index of the first occurrence of <paramref name="item"/> in
	/// the list, or <c>-1</c> if not found.
	/// </summary>
	[Pure]
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public int IndexOf(T item) => this.AsValueSlice().IndexOf(item);

	/// <summary>
	/// Return the index of the last occurrence of <paramref name="item"/> in
	/// the list, or <c>-1</c> if not found.
	/// </summary>
	[Pure]
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public int LastIndexOf(T item) => this.AsValueSlice().LastIndexOf(item);

	/// <summary>
	/// Returns <see langword="true"/> when the list contains the specified
	/// <paramref name="item"/>.
	/// </summary>
	[Pure]
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool Contains(T item) => this.AsValueSlice().Contains(item);

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
	public int BinarySearch(T item) => this.AsValueSlice().BinarySearch(item);

	/// <inheritdoc/>
	bool ICollection<T>.Contains(T item) => this.AsValueSlice().Contains(item);

	/// <inheritdoc/>
	int IList<T>.IndexOf(T item) => this.AsValueSlice().IndexOf(item);

	/// <inheritdoc/>
	void ICollection<T>.CopyTo(T[] array, int arrayIndex)
	{
		if (array is null)
		{
			throw new ArgumentNullException(nameof(array));
		}

		this.AsValueSlice().CopyTo(array.AsSpan().Slice(arrayIndex));
	}

	/// <inheritdoc/>
	[Pure]
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
		var hasher = new HashCode();
		hasher.Add(typeof(ValueList<T>));
		hasher.Add(this.Count);

		foreach (var item in this)
		{
			hasher.Add(item);
		}

		var hashCode = hasher.ToHashCode();
		if (hashCode == UninitializedHashCode)
		{
			// Never return 0, as that is our placeholder value.
			hashCode = 1;
		}

		return hashCode;
	}

	/// <summary>
	/// Returns <see langword="true"/> when the two lists have identical length
	/// and content.
	/// </summary>
	[Pure]
	public bool Equals(ValueList<T>? other) => other is null ? false : this.AsValueSlice() == other.AsValueSlice();

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
		if (object.ReferenceEquals(left, right))
		{
			return true;
		}

		return left?.AsValueSlice() == right?.AsValueSlice();
	}

#pragma warning disable CA2225 // Operator overloads have named alternates
	/// <summary>
	/// Convert list to slice.
	/// </summary>
	[Pure]
	public static implicit operator ValueSlice<T>(ValueList<T> list) => list.AsValueSlice();
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
		private readonly T[] items;
		private readonly int end;
		private int current;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal Enumerator(ValueList<T> list)
		{
			this.items = list.Items;
			this.end = list.Count;
			this.current = -1;
		}

		/// <inheritdoc/>
		public readonly ref readonly T Current
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => ref this.items![this.current];
		}

		/// <inheritdoc/>
		readonly T IEnumeratorLike<T>.Current => this.Current;

		/// <inheritdoc/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool MoveNext() => ++this.current < this.end;
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

		builder.Append(']');
		return builder.ToString();
	}

	/// <inheritdoc/>
	void ICollection<T>.Add(T item) => throw CreateImmutableException();

	/// <inheritdoc/>
	void ICollection<T>.Clear() => throw CreateImmutableException();

	/// <inheritdoc/>
	void IList<T>.Insert(int index, T item) => throw CreateImmutableException();

	/// <inheritdoc/>
	bool ICollection<T>.Remove(T item) => throw CreateImmutableException();

	/// <inheritdoc/>
	void IList<T>.RemoveAt(int index) => throw CreateImmutableException();

	private static NotSupportedException CreateImmutableException() => new NotSupportedException("Collection is immutable");
}
