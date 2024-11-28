using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace Badeend.ValueCollections.Internals;

// Various parts of this class have been adapted from:
// https://github.com/dotnet/runtime/blob/5aa9687e110faa19d1165ba680e52585a822464d/src/libraries/System.Private.CoreLib/src/System/Collections/Generic/List.cs
[StructLayout(LayoutKind.Auto)]
internal struct RawList<T> : IEquatable<RawList<T>>
{
	/// <summary>
	/// Initial capacity for non-zero size lists.
	/// </summary>
	private const int DefaultCapacity = 4;

#pragma warning disable SA1307 // Accessible fields should begin with upper-case letter
	internal T[] items;
	internal int size;
#pragma warning restore SA1307 // Accessible fields should begin with upper-case letter

	[Pure]
	internal readonly int Capacity
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => this.items.Length;
	}

	[Pure]
	internal readonly int Count
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => this.size;
	}

	public readonly ref T this[int index]
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

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal RawList(T[] items, int size)
	{
		Debug.Assert(items is not null);
		Debug.Assert(size >= 0);
		Debug.Assert(size <= items!.Length);

		this.items = items;
		this.size = size;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public RawList()
	{
		this.items = Array.Empty<T>();
		this.size = 0;
	}

	internal RawList(int minimumCapacity)
	{
		if (minimumCapacity < 0)
		{
			ThrowHelpers.ThrowArgumentOutOfRangeException(ThrowHelpers.Argument.minimumCapacity);
		}

		this.size = 0;
		this.items = minimumCapacity == 0 ? Array.Empty<T>() : new T[minimumCapacity];
	}

	internal RawList(ref readonly RawList<T> source)
	{
		int count = source.Count;
		if (count == 0)
		{
			this.items = Array.Empty<T>();
			this.size = 0;
		}
		else
		{
			var newItems = new T[count];
			Array.Copy(source.items, 0, newItems, 0, count);
			this.items = newItems;
			this.size = count;
		}
	}

	internal RawList(scoped ReadOnlySpan<T> source)
	{
		var length = source.Length;
		if (length == 0)
		{
			this.items = Array.Empty<T>();
			this.size = 0;
		}
		else
		{
			var newItems = new T[length];
			source.CopyTo(newItems);
			this.items = newItems;
			this.size = length;
		}
	}

	internal RawList(IEnumerable<T> source)
	{
		if (source is null)
		{
			ThrowHelpers.ThrowArgumentNullException(ThrowHelpers.Argument.source);
		}

		// On newer runtimes, Enumerable.ToArray() is faster than simply
		// looping the enumerable ourselves, because the LINQ method has
		// access to an internal optimization to forgo the double virtual
		// interface call per iteration.
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
		var newItems = source.ToArray();
		this.items = newItems;
		this.size = newItems.Length;
#else
		if (source is ICollection<T> collection)
		{
			int count = collection.Count;
			if (count == 0)
			{
				this.items = Array.Empty<T>();
				this.size = 0;
			}
			else
			{
				var newItems = new T[count];
				collection.CopyTo(newItems, 0);
				this.items = newItems;
				this.size = count;
			}
		}
		else
		{
			this.items = Array.Empty<T>();
			this.size = 0;

			foreach (var item in source)
			{
				this.Add(item);
			}
		}
#endif
	}

	internal void Add(T item)
	{
		T[] items = this.items;
		int size = this.size;
		if ((uint)size < (uint)items.Length)
		{
			this.size = size + 1;
			items[size] = item;
		}
		else
		{
			this.AddWithResize(item);
		}
	}

	// Non-inline from List.Add to improve its code quality as uncommon path
	[MethodImpl(MethodImplOptions.NoInlining)]
	private void AddWithResize(T item)
	{
		Debug.Assert(this.size == this.items.Length);

		int size = this.size;
		this.Grow(size + 1);
		this.size = size + 1;
		this.items[size] = item;
	}

	internal void AddRange(ref readonly RawList<T> items) => this.AddRange(items.AsSpan());

	internal void AddRange(scoped ReadOnlySpan<T> items)
	{
		Debug.Assert(this.items.AsSpan().Overlaps(items) == false);

		if (items.IsEmpty)
		{
			return;
		}

		if (this.items.Length - this.size < items.Length)
		{
			this.Grow(checked(this.size + items.Length));
		}

		items.CopyTo(this.items.AsSpan(this.size));
		this.size += items.Length; // Update size _after_ copying, to handle the case in which we're inserting the list into itself.
	}

	// Appending the list onto itself through this method is unsupported.
	// This may happen when the IEnumerable is implemented in terms of this RawList.
	internal bool TryAddNonEnumeratedRange(IEnumerable<T> items)
	{
		if (items is null)
		{
			ThrowHelpers.ThrowArgumentNullException(ThrowHelpers.Argument.items);
		}

		if (items is not ICollection<T> collection)
		{
			return false;
		}

		int count = collection.Count;
		if (count == 0)
		{
			return true;
		}

		if (this.items.Length - this.size < count)
		{
			this.Grow(checked(this.size + count));
		}

		// Note that this might end up calling our own `CopyTo`.
		collection.CopyTo(this.items, this.size);
		this.size += count; // Update size _after_ copying, to handle the case in which we're inserting the list into itself.

		return true;
	}

	// Does not check/guard against concurrent mutation during enumeration!
	internal void AddRange(IEnumerable<T> items)
	{
		if (!this.TryAddNonEnumeratedRange(items))
		{
			foreach (var item in items)
			{
				this.Add(item);
			}
		}
	}

	internal void Insert(int index, T item)
	{
		// Note that insertions at the end are legal.
		if ((uint)index > (uint)this.size)
		{
			ThrowHelpers.ThrowArgumentOutOfRangeException(ThrowHelpers.Argument.index);
		}

		if (this.size == this.items.Length)
		{
			this.GrowForInsertion(index, 1);
		}
		else if (index < this.size)
		{
			Array.Copy(this.items, index, this.items, index + 1, this.size - index);
		}

		this.items[index] = item;
		this.size++;
	}

	internal void InsertRange(int index, ref readonly RawList<T> other) => this.InsertRange(index, other.AsSpan());

	internal void InsertRange(int index, scoped ReadOnlySpan<T> items)
	{
		Debug.Assert(this.items.AsSpan().Overlaps(items) == false);

		if ((uint)index > (uint)this.size)
		{
			ThrowHelpers.ThrowArgumentOutOfRangeException(ThrowHelpers.Argument.index);
		}

		if (!items.IsEmpty)
		{
			if (this.items.Length - this.size < items.Length)
			{
				this.Grow(checked(this.size + items.Length));
			}

			// If the index at which to insert is less than the number of items in the list,
			// shift all items past that location in the list down to the end, making room
			// to copy in the new data.
			if (index < this.size)
			{
				Array.Copy(this.items, index, this.items, index + items.Length, this.size - index);
			}

			// Copy the source span into the list.
			// Note that this does not handle the unsafe case of trying to insert a CollectionsMarshal.AsSpan(list)
			// or some slice thereof back into the list itself; such an operation has undefined behavior.
			items.CopyTo(this.items.AsSpan(index));
			this.size += items.Length; // Update size _after_ copying, to handle the case in which we're inserting the list into itself.
		}
	}

	// Inserting the list into itself through this method is unsupported.
	// This may happen when the IEnumerable is implemented in terms of this RawList.
	internal bool TryInsertNonEnumeratedRange(int index, IEnumerable<T> items)
	{
		if (items is null)
		{
			ThrowHelpers.ThrowArgumentNullException(ThrowHelpers.Argument.items);
		}

		if ((uint)index > (uint)this.size)
		{
			ThrowHelpers.ThrowArgumentOutOfRangeException(ThrowHelpers.Argument.index);
		}

		if (items is not ICollection<T> collection)
		{
			return false;
		}

		int count = collection.Count;
		if (count == 0)
		{
			return true;
		}

		if (this.items.Length - this.size < count)
		{
			this.GrowForInsertion(index, count);
		}
		else if (index < this.size)
		{
			Array.Copy(this.items, index, this.items, index + count, this.size - index);
		}

		// Note that this might end up calling our own `CopyTo`.
		collection.CopyTo(this.items, index);
		this.size += count; // Update size _after_ copying, to handle the case in which we're inserting the list into itself.

		return true;
	}

	// Does not check/guard against concurrent mutation during enumeration!
	internal void InsertRange(int index, IEnumerable<T> items)
	{
		if (!this.TryInsertNonEnumeratedRange(index, items))
		{
			foreach (var item in items)
			{
				this.Insert(index++, item);
			}
		}
	}

	internal void Clear()
	{
		if (Polyfills.IsReferenceOrContainsReferences<T>())
		{
			int size = this.size;
			this.size = 0;
			if (size > 0)
			{
				Array.Clear(this.items, 0, size); // Clear the elements so that the gc can reclaim the references.
			}
		}
		else
		{
			this.size = 0;
		}
	}

	internal void RemoveAt(int index)
	{
		if ((uint)index >= (uint)this.size)
		{
			ThrowHelpers.ThrowArgumentOutOfRangeException(ThrowHelpers.Argument.index);
		}

		this.RemoveAtUnsafe(index);
	}

	// This assumes Mutate has already been called and that `index` is valid.
	private void RemoveAtUnsafe(int index)
	{
		Debug.Assert((uint)index < (uint)this.size);

		this.size--;
		if (index < this.size)
		{
			Array.Copy(this.items, index + 1, this.items, index, this.size - index);
		}

		if (Polyfills.IsReferenceOrContainsReferences<T>())
		{
			this.items[this.size] = default!;
		}
	}

	internal void RemoveRange(int index, int count)
	{
		if (index < 0)
		{
			ThrowHelpers.ThrowArgumentOutOfRangeException(ThrowHelpers.Argument.index);
		}

		if (count < 0)
		{
			ThrowHelpers.ThrowArgumentOutOfRangeException(ThrowHelpers.Argument.count);
		}

		if (this.size - index < count)
		{
			ThrowHelpers.ThrowArgumentException_InvalidOffsetOrLength();
		}

		if (count > 0)
		{
			this.size -= count;

			if (index < this.size)
			{
				Array.Copy(this.items, index + count, this.items, index, this.size - index);
			}

			if (Polyfills.IsReferenceOrContainsReferences<T>())
			{
				Array.Clear(this.items, this.size, count);
			}
		}
	}

	internal bool Remove(T item)
	{
		int index = this.IndexOf(item);
		if (index >= 0)
		{
			this.RemoveAtUnsafe(index);
			return true;
		}

		return false;
	}

	// `match` may not access/mutate the list.
	internal bool Remove(Predicate<T> match)
	{
		if (match is null)
		{
			ThrowHelpers.ThrowArgumentNullException(ThrowHelpers.Argument.match);
		}

		for (int i = 0; i < this.size; i++)
		{
			if (match(this.items[i]))
			{
				this.RemoveAtUnsafe(i);
				return true;
			}
		}

		return false;
	}

	internal void RemoveAll(T item)
	{
		this.RemoveAll(x => new DefaultEqualityComparer<T>().Equals(x, item));
	}

	// `match` may not access/mutate the list.
	internal void RemoveAll(Predicate<T> match)
	{
		if (match == null)
		{
			ThrowHelpers.ThrowArgumentNullException(ThrowHelpers.Argument.match);
		}

		int freeIndex = 0;   // the first free slot in items array

		// Find the first item which needs to be removed.
		while (freeIndex < this.size && !match(this.items[freeIndex]))
		{
			freeIndex++;
		}

		if (freeIndex < this.size)
		{
			int current = freeIndex + 1;
			while (current < this.size)
			{
				// Find the first item which needs to be kept.
				while (current < this.size && match(this.items[current]))
				{
					current++;
				}

				if (current < this.size)
				{
					// copy item to the free slot.
					this.items[freeIndex++] = this.items[current++];
				}
			}

			if (Polyfills.IsReferenceOrContainsReferences<T>())
			{
				Array.Clear(this.items, freeIndex, this.size - freeIndex); // Clear the elements so that the gc can reclaim the references.
			}

			this.size = freeIndex;
		}
	}

	internal void Reverse()
	{
		if (this.size <= 1)
		{
			return;
		}

		Array.Reverse(this.items, 0, this.size);
	}

	internal void Sort()
	{
		if (this.size <= 1)
		{
			return;
		}

		Array.Sort(this.items, 0, this.size);
	}

	internal void Shuffle()
	{
		Polyfills.Shuffle(this.AsSpan());
	}

	internal void TrimExcess()
	{
		int threshold = (int)(((double)this.items.Length) * 0.9);
		if (this.size < threshold)
		{
			this.SetCapacity(this.size);
		}
	}

	internal void EnsureCapacity(int minimumCapacity)
	{
		if (minimumCapacity < 0)
		{
			ThrowHelpers.ThrowArgumentOutOfRangeException(ThrowHelpers.Argument.minimumCapacity);
		}

		if (this.items.Length < minimumCapacity)
		{
			this.Grow(minimumCapacity);
		}
	}

	/// <summary>
	/// Increase the capacity of this list to at least the specified <paramref name="capacity"/>.
	/// </summary>
	internal void Grow(int capacity)
	{
		this.SetCapacity(this.GetNewCapacity(capacity));
	}

	/// <summary>
	/// Enlarge this list so it may contain at least <paramref name="insertionCount"/> more elements
	/// And copy data to their after-insertion positions.
	/// This method is specifically for insertion, as it avoids 1 extra array copy.
	/// You should only call this method when Count + insertionCount > Capacity.
	/// </summary>
	private void GrowForInsertion(int indexToInsert, int insertionCount = 1)
	{
		Debug.Assert(insertionCount > 0);

		int requiredCapacity = checked(this.size + insertionCount);
		int newCapacity = this.GetNewCapacity(requiredCapacity);

		// Inline and adapt logic from set_Capacity
		T[] newItems = new T[newCapacity];
		if (indexToInsert != 0)
		{
			Array.Copy(this.items, newItems, length: indexToInsert);
		}

		if (this.size != indexToInsert)
		{
			Array.Copy(this.items, indexToInsert, newItems, indexToInsert + insertionCount, this.size - indexToInsert);
		}

		this.items = newItems;
	}

	private void SetCapacity(int capacity)
	{
		if (capacity < this.size)
		{
			ThrowHelpers.ThrowArgumentOutOfRangeException(ThrowHelpers.Argument.minimumCapacity);
		}

		if (capacity != this.items.Length)
		{
			if (capacity > 0)
			{
				T[] newItems = new T[capacity];
				if (this.size > 0)
				{
					Array.Copy(this.items, newItems, this.size);
				}

				this.items = newItems;
			}
			else
			{
				this.items = [];
			}
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private readonly int GetNewCapacity(int capacity)
	{
		Debug.Assert(this.items.Length < capacity);

		int newCapacity = this.items.Length == 0 ? DefaultCapacity : 2 * this.items.Length;

		// Allow the list to grow to maximum possible capacity (~2G elements) before encountering overflow.
		// Note that this check works even when _items.Length overflowed thanks to the (uint) cast
		if ((uint)newCapacity > Polyfills.ArrayMaxLength)
		{
			newCapacity = Polyfills.ArrayMaxLength;
		}

		// If the computed capacity is still less than specified, set to the original argument.
		// Capacities exceeding Polyfills.ArrayMaxLength will be surfaced as OutOfMemoryException by Array.Resize.
		if (newCapacity < capacity)
		{
			newCapacity = capacity;
		}

		return newCapacity;
	}

	[Pure]
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal readonly Span<T> AsSpan() => this.items.AsSpan(0, this.size);

	[Pure]
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal readonly Memory<T> AsMemory() => this.items.AsMemory(0, this.size);

	internal void SetCount(int count)
	{
		if (count < 0)
		{
			ThrowHelpers.ThrowArgumentOutOfRangeException(ThrowHelpers.Argument.count);
		}

		if (count > this.Capacity)
		{
			this.Grow(count);
		}
		else if (count < this.size && Polyfills.IsReferenceOrContainsReferences<T>())
		{
			Array.Clear(this.items, count, this.size - count);
		}

		this.size = count;
	}

	/// <summary>
	/// Copy the contents of the list into a new array.
	/// </summary>
	[Pure]
	internal readonly T[] ToArray()
	{
		var size = this.size;
		if (size == 0)
		{
			return Array.Empty<T>();
		}

		T[] array = new T[size];
		Array.Copy(this.items, array, size);
		return array;
	}

	/// <summary>
	/// Copy the contents of the list into an existing <see cref="Span{T}"/>.
	/// </summary>
	/// <exception cref="ArgumentException">
	///   <paramref name="destination"/> is shorter than the source slice.
	/// </exception>
	internal readonly void CopyTo(Span<T> destination) => this.AsSpan().CopyTo(destination);

	/// <summary>
	/// Attempt to copy the contents of the list into an existing
	/// <see cref="Span{T}"/>. If the <paramref name="destination"/> is too short,
	/// no items are copied and the method returns <see langword="false"/>.
	/// </summary>
	internal readonly bool TryCopyTo(Span<T> destination) => this.AsSpan().TryCopyTo(destination);

	/// <summary>
	/// Return the index of the first occurrence of <paramref name="item"/> in
	/// the list, or <c>-1</c> if not found.
	/// </summary>
	[Pure]
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal readonly int IndexOf(T item)
	{
		if (this.size == 0)
		{
			return -1;
		}

		return Array.IndexOf(this.items, item, 0, this.size);
	}

	/// <summary>
	/// Return the index of the last occurrence of <paramref name="item"/> in
	/// the list, or <c>-1</c> if not found.
	/// </summary>
	[Pure]
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal readonly int LastIndexOf(T item)
	{
		var size = this.size;
		if (size == 0)
		{
			// Special case for empty list
			return -1;
		}

		return Array.LastIndexOf(this.items, item, size - 1, size);
	}

	/// <summary>
	/// Returns <see langword="true"/> when the list contains the specified
	/// <paramref name="item"/>.
	/// </summary>
	[Pure]
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal readonly bool Contains(T item) => this.IndexOf(item) >= 0;

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
	internal readonly int BinarySearch(T item)
	{
		if (this.size == 0)
		{
			return -1;
		}

		return Array.BinarySearch(this.items, 0, this.size, item);
	}

	/// <inheritdoc/>
	[Pure]
	[EditorBrowsable(EditorBrowsableState.Never)]
	public readonly override int GetHashCode() => this.items.GetHashCode();

	[Pure]
	[EditorBrowsable(EditorBrowsableState.Never)]
	public readonly bool Equals(ref readonly RawList<T> other) => object.ReferenceEquals(this.items, other.items);

	/// <inheritdoc/>
	readonly bool IEquatable<RawList<T>>.Equals(RawList<T> other) => object.ReferenceEquals(this.items, other.items);

#pragma warning disable CS0809 // Obsolete member overrides non-obsolete member
	/// <inheritdoc/>
	[Pure]
	[Obsolete("Use == instead.")]
	[EditorBrowsable(EditorBrowsableState.Never)]
	public readonly override bool Equals(object? obj) => obj is RawList<T> other && this.Equals(in other);
#pragma warning restore CS0809 // Obsolete member overrides non-obsolete member

	/// <summary>
	/// Returns an enumerator for this <see cref="RawList{T}"/>.
	///
	/// Typically, you don't need to manually call this method, but instead use
	/// the built-in <c>foreach</c> syntax.
	/// </summary>
	[Pure]
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public readonly Enumerator GetEnumerator() => new Enumerator(this);

	/// <summary>
	/// Enumerator for <see cref="RawList{T}"/>.
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
		internal Enumerator(RawList<T> list)
		{
			this.items = list.items;
			this.end = list.size;
			this.current = -1;
		}

		/// <inheritdoc/>
		public readonly ref readonly T Current
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => ref this.items[this.current];
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
	public readonly override string ToString()
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
}

internal static class RawList
{
	/// <summary>
	/// Returns <see langword="true"/> when the two lists have identical length
	/// and content.
	/// </summary>
	[Pure]
	internal static bool SequenceEqual<T>(this ref readonly RawList<T> left, ref readonly RawList<T> right)
	{
		// Attempt to defer to .NET 6+ vectorized implementation:
#if NET6_0_OR_GREATER
		return System.MemoryExtensions.SequenceEqual(left.AsSpan(), right.AsSpan());
#else
		if (object.ReferenceEquals(left.items, right.items))
		{
			return true;
		}

		var size = left.size;
		if (size != right.size)
		{
			return false;
		}

		var comparer = new DefaultEqualityComparer<T>();

		for (int i = 0; i < size; i++)
		{
			if (!comparer.Equals(left.items[i], right.items[i]))
			{
				return false;
			}
		}

		return true;
#endif
	}

	[Pure]
	internal static int GetSequenceHashCode<T>(this ref readonly RawList<T> list)
	{
		var hasher = new HashCode();
		hasher.Add(typeof(RawList<T>));
		hasher.Add(list.Count);

		foreach (var item in list)
		{
			hasher.Add(item);
		}

		return hasher.ToHashCode();
	}

	[Pure]
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static RawList<T> CreateFromArrayUnsafe<T>(T[] items, int count)
	{
		// Following trick can reduce the range check by one
		if ((uint)count > (uint)items.Length)
		{
			ThrowHelpers.ThrowArgumentOutOfRangeException(ThrowHelpers.Argument.count);
		}

		return new(items, count);
	}
}
