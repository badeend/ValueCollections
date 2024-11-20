using System.Collections;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
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
	public static ValueList<T> Create<T>(ReadOnlySpan<T> items) => ValueList<T>.CreateImmutableFromSpan(items);

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
	// Various parts of this class have been adapted from:
	// https://github.com/dotnet/runtime/blob/5aa9687e110faa19d1165ba680e52585a822464d/src/libraries/System.Private.CoreLib/src/System/Collections/Generic/List.cs

	/// <summary>
	/// Get an empty list.
	///
	/// This does not allocate any memory.
	/// </summary>
	[Pure]
	public static ValueList<T> Empty { get; } = new(Array.Empty<T>(), 0, BuilderState.InitialImmutable);

	private T[] items;
	private int size;

	// See the BuilderState utility class for more info.
	private int state;

	internal int Capacity
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => this.items.Length;
	}

	/// <summary>
	/// Length of the list.
	/// </summary>
	[Pure]
	public int Count
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => this.size;
	}

	/// <summary>
	/// Shortcut for <c>.Count == 0</c>.
	/// </summary>
	[Pure]
	public bool IsEmpty
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => this.size == 0;
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
			// Following trick can reduce the range check by one
			if ((uint)index >= (uint)this.size)
			{
				ThrowHelpers.ThrowArgumentOutOfRangeException(ThrowHelpers.Argument.index);
			}

			return ref this.items[index];
		}
	}

	/// <inheritdoc/>
	T IReadOnlyList<T>.this[int index] => this[index];

	/// <inheritdoc/>
	T IList<T>.this[int index]
	{
		get => this[index];
		set => ThrowHelpers.ThrowNotSupportedException_CollectionImmutable();
	}

	private ValueList(T[] items, int size, int state)
	{
		Debug.Assert(items is not null);
		Debug.Assert(size >= 0);
		Debug.Assert(size <= items!.Length);

		this.items = items;
		this.size = size;
		this.state = state;
	}

	internal static ValueList<T> CreateImmutableFromEnumerable(IEnumerable<T> items)
	{
		if (items is null)
		{
			ThrowHelpers.ThrowArgumentNullException(ThrowHelpers.Argument.items);
		}

		if (items is ValueList<T> list)
		{
			Debug.Assert(BuilderState.IsImmutable(list.state));

			return list;
		}

		// On newer runtimes, Enumerable.ToArray() is faster than simply
		// looping the enumerable ourselves, because the LINQ method has
		// access to an internal optimization to forgo the double virtual
		// interface call per iteration.
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
		var newItems = items.ToArray();
		if (newItems.Length == 0)
		{
			return Empty;
		}

		return new(newItems, newItems.Length, BuilderState.InitialImmutable);
#else
		if (items is ICollection<T> collection)
		{
			int count = collection.Count;
			if (count == 0)
			{
				return Empty;
			}

			var newItems = new T[count];
			collection.CopyTo(newItems, 0);
			return new(newItems, count, BuilderState.InitialImmutable);
		}
		else
		{
			var newList = new ValueList<T>([], 0, BuilderState.InitialImmutable);

			foreach (var item in items)
			{
				Builder.AddUnsafe(newList, item);
			}

			if (newList.Count == 0)
			{
				// Too bad we've just performed a useless allocation.
				// Better drop it now while it's still in the youngest GC generation.
				return Empty;
			}

			return newList;
		}
#endif
	}

	internal static ValueList<T> CreateImmutableFromSpan(ReadOnlySpan<T> items)
	{
		var length = items.Length;
		if (length == 0)
		{
			return Empty;
		}

		var newItems = new T[length];
		items.CopyTo(newItems);
		return new(newItems, length, BuilderState.InitialImmutable);
	}

	internal static ValueList<T> CreateImmutableFromArrayUnsafe(T[] items) => CreateImmutableFromArrayUnsafe(items, items.Length);

	internal static ValueList<T> CreateImmutableFromArrayUnsafe(T[] items, int count)
	{
		if (count == 0)
		{
			return Empty;
		}

		return new(items, count, BuilderState.InitialImmutable);
	}

	internal static ValueList<T> CreateMutable()
	{
		return new(Array.Empty<T>(), 0, BuilderState.InitialMutable);
	}

	internal static ValueList<T> CreateMutableWithCapacity(int capacity)
	{
		if (capacity < 0)
		{
			ThrowHelpers.ThrowArgumentOutOfRangeException(ThrowHelpers.Argument.capacity);
		}

		var items = capacity switch
		{
			0 => Array.Empty<T>(),
			_ => new T[capacity],
		};

		return new(items, 0, BuilderState.InitialMutable);
	}

	internal static ValueList<T> CreateMutableFromArrayUnsafe(T[] items, int count)
	{
		return new(items, count, BuilderState.InitialMutable);
	}

	/// <summary>
	/// Access the list's contents using a <see cref="ValueSlice{T}"/>.
	/// </summary>
	[Pure]
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public ValueSlice<T> AsValueSlice() => new ValueSlice<T>(this.items, 0, this.size);

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
			ThrowHelpers.ThrowArgumentOutOfRangeException(ThrowHelpers.Argument.capacity);
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
			ThrowHelpers.ThrowArgumentNullException(ThrowHelpers.Argument.array);
		}

		this.AsValueSlice().CopyTo(array.AsSpan().Slice(arrayIndex));
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
		var hasher = new HashCode();
		hasher.Add(typeof(ValueList<T>));
		hasher.Add(this.Count);

		foreach (var item in this)
		{
			hasher.Add(item);
		}

		return hasher.ToHashCode();
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
		private readonly T[] items;
		private readonly int end;
		private int current;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal Enumerator(ValueList<T> list)
		{
			this.items = list.items;
			this.end = list.size;
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
		public bool MoveNext()
		{
			// FYI, we don't need to compare with the current `state` (version)
			// of the list, because this enumerator type can only be accessed
			// after the list has already been built and is therefore immutable.
			return (uint)++this.current < this.end;
		}
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
