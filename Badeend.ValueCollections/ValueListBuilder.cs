using System.Collections;
using System.Runtime.CompilerServices;

namespace Badeend.ValueCollections;

/// <summary>
/// A mutable list that can be used to efficiently construct new immutable lists.
///
/// Most mutating methods on this class return `this`, allowing the caller to
/// chain multiple mutations in a row.
///
/// When you're done building, call <see cref="Build()"/> to extract the
/// resulting list.
///
/// For constructing <see cref="ValueList{T}"/>s it is recommended to use this
/// class over e.g. <see cref="List{T}"/>. This type can avoiding unnecessary
/// copying by taking advantage of the immutability of its results. Whereas
/// calling <c>.ToValueList()</c> on a regular <see cref="List{T}"/>
/// <em>always</em> performs a full copy.
///
/// Unlike ValueList, ValueListBuilder is <em>not</em> thread-safe.
/// </summary>
/// <typeparam name="T">The type of items in the list.</typeparam>
[CollectionBuilder(typeof(ValueList), nameof(ValueList.Builder))]
public sealed class ValueListBuilder<T> : IList<T>, IReadOnlyList<T>
{
	private const int VersionBuilt = -1;

	/// <summary>
	/// Can be one of:
	/// - ValueList{T}: when copy-on-write hasn't kicked in yet.
	/// - List{T}: we're actively building a list.
	/// </summary>
	private IReadOnlyList<T> items;

	/// <summary>
	/// Mutation counter.
	/// `-1` means: Collection has been built and the builder is now read-only.
	/// </summary>
	private int version;

	/// <summary>
	/// Returns <see langword="true"/> when this instance has been built and is
	/// now read-only.
	/// </summary>
	public bool IsReadOnly => this.version == VersionBuilt;

	/// <summary>
	/// Finalize the builder and export its contents as a <see cref="ValueList{T}"/>.
	/// This makes the builder read-only. Any future attempt to mutate the
	/// builder will throw.
	///
	/// This is an <c>O(1)</c> operation and performs only a small fixed-size
	/// memory allocation. This does not perform a bulk copy of the contents.
	/// </summary>
	/// <remarks>
	/// If you need an intermediate snapshot of the contents while keeping the
	/// builder open for mutation, use <see cref="ToValueList"/> instead.
	/// </remarks>
	/// <exception cref="InvalidOperationException">
	/// This instance has already been built.
	/// </exception>
	public ValueList<T> Build()
	{
		if (this.version == VersionBuilt)
		{
			throw BuiltException();
		}

		this.version = VersionBuilt;

		return this.ToValueList();
	}

	/// <summary>
	/// Copy the current contents of the builder into a new <see cref="ValueList{T}"/>.
	/// </summary>
	/// <remarks>
	/// If you don't need the builder anymore after this method, consider using
	/// <see cref="Build"/> instead.
	/// </remarks>
	public ValueList<T> ToValueList()
	{
		if (this.items is List<T> list)
		{
			var newValueList = ValueList<T>.FromArrayUnsafe(UnsafeHelpers.GetBackingArray(list), list.Count);
			this.items = newValueList;
			return newValueList;
		}

		if (this.items is ValueList<T> valueList)
		{
			return valueList;
		}

		throw UnreachableException();
	}

	private List<T> Mutate()
	{
		if (this.version == VersionBuilt)
		{
			throw BuiltException();
		}

		this.version++;

		if (this.items is List<T> list)
		{
			return list;
		}

		if (this.items is ValueList<T> valueList)
		{
			var newList = new List<T>(valueList);
			this.items = newList;
			return newList;
		}

		throw UnreachableException();
	}

	private IReadOnlyList<T> Read() => this.items;

	/// <summary>
	/// The total number of elements the internal data structure can hold without resizing.
	/// </summary>
	public int Capacity => this.items switch
	{
		List<T> items => items.Capacity,
		ValueList<T> items => items.Capacity,
		_ => throw UnreachableException(),
	};

	/// <summary>
	/// Gets or sets the element at the specified <paramref name="index"/>.
	/// </summary>
	public T this[int index]
	{
		get => this.Read()[index];
		set => this.Mutate()[index] = value;
	}

	/// <summary>
	/// Current length of the list.
	/// </summary>
	public int Count => this.Read().Count;

	/// <summary>
	/// Shortcut for <c>.Count == 0</c>.
	/// </summary>
	public bool IsEmpty
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => this.Count == 0;
	}

	/// <summary>
	/// Construct a new empty list builder.
	/// </summary>
	public ValueListBuilder()
	{
		this.items = ValueList<T>.Empty;
	}

	/// <summary>
	/// Construct a new empty list builder with the specified initial capacity.
	/// </summary>
	/// <exception cref="ArgumentOutOfRangeException">
	///   <paramref name="capacity"/> is less than 0.
	/// </exception>
	public ValueListBuilder(int capacity)
	{
		if (capacity == 0)
		{
			this.items = ValueList<T>.Empty;
		}
		else
		{
			this.items = new List<T>(capacity);
		}
	}

	/// <summary>
	/// Construct a new <see cref="ValueListBuilder{T}"/> with the provided
	/// <paramref name="items"/> as its initial content.
	/// </summary>
	/// <remarks>
	/// Use <see cref="ValueList.Builder{T}(ReadOnlySpan{T})"/> to construct a
	/// ValueListBuilder from a span.
	/// </remarks>
	public ValueListBuilder(IEnumerable<T> items)
	{
		if (items is ValueList<T> valueList)
		{
			this.items = valueList;
		}
		else
		{
			this.items = new List<T>(items);
		}
	}

	private ValueListBuilder(ValueList<T> items)
	{
		this.items = items;
	}

	private ValueListBuilder(List<T> items)
	{
		this.items = items;
	}

	internal static ValueListBuilder<T> FromValueList(ValueList<T> items) => new(items);

	internal static ValueListBuilder<T> FromListUnsafe(List<T> items) => new(items);

	/// <summary>
	/// Replaces an element at a given position in the list with the specified
	/// element.
	/// </summary>
	public ValueListBuilder<T> SetItem(int index, T value)
	{
		this.Mutate()[index] = value;
		return this;
	}

	/// <summary>
	/// Add an <paramref name="item"/> to the end of the list.
	/// </summary>
	public ValueListBuilder<T> Add(T item)
	{
		this.Mutate().Add(item);
		return this;
	}

	// Accessible through an extension method.
	internal ValueListBuilder<T> AddRangeSpan(ReadOnlySpan<T> items)
	{
		AddRange(this.Mutate(), items);
		return this;
	}

	private static void AddRange(List<T> list, ReadOnlySpan<T> items)
	{
#if NET8_0_OR_GREATER
		CollectionExtensions.AddRange(list, items);
#else
		EnsureCapacity(list, list.Count + items.Length);

		foreach (var item in items)
		{
			list.Add(item);
		}
#endif
	}

	/// <summary>
	/// Add the <paramref name="items"/> to the end of the list.
	/// </summary>
	/// <remarks>
	/// <see cref="ValueCollectionExtensions.AddRange">More overloads</see> are
	/// available as extension methods.
	/// </remarks>
	public ValueListBuilder<T> AddRange(IEnumerable<T> items)
	{
		if (items is ICollection<T> collection)
		{
			return this.AddRangeCollection(collection);
		}

		if (items is null)
		{
			throw new ArgumentNullException(nameof(items));
		}

		var list = this.Mutate();
		foreach (var item in items)
		{
			list.Add(item);

			// Something not immediately obvious from just the code itself is that
			// nothing prevents consumers from calling this method with an `items`
			// argument that is (indirectly) derived from `this`. e.g.
			// ```builder.AddRange(builder.Where(_ => true))```
			// Without precaution that could result in an infinite loop with
			// infinite memory growth.
			// We "protect" our consumers from this by invalidating the enumerator
			// on each iteration such that an exception will be thrown.
			this.version++;
		}

		return this;
	}

	private ValueListBuilder<T> AddRangeCollection(ICollection<T> items)
	{
		var list = this.Mutate();

		if (checked(list.Count + items.Count) < list.Count)
		{
			throw new OverflowException();
		}

		list.AddRange(items);
		return this;
	}

	/// <summary>
	/// Insert an <paramref name="item"/> into the list at the specified <paramref name="index"/>.
	/// </summary>
	public ValueListBuilder<T> Insert(int index, T item)
	{
		this.Mutate().Insert(index, item);
		return this;
	}

	// Accessible through an extension method.
	internal ValueListBuilder<T> InsertRangeSpan(int index, ReadOnlySpan<T> items)
	{
		var list = this.Mutate();

#if NET8_0_OR_GREATER
		CollectionExtensions.InsertRange(list, index, items);
#else
		if (index == this.Count)
		{
			AddRange(list, items);
			return this;
		}

		if (index < 0 || index > list.Count)
		{
			throw new ArgumentOutOfRangeException(nameof(index));
		}

		if (items.Length == 0)
		{
			// Nothing to do
		}
		else if (items.Length == 1)
		{
			list.Insert(index, items[0]);
		}
		else
		{
			// FYI, the following only works because List<T>.InsertRange has
			// specialized behavior for ICollection<T>

			// Make room inside the backing array:
			list.InsertRange(index, new NoOpCollection(items.Length));

			// Actually write the content:
			items.CopyTo(UnsafeHelpers.GetBackingArray(list).AsSpan(index));
		}
#endif

		return this;
	}

	/// <summary>
	/// Insert the <paramref name="items"/> into the list at the specified <paramref name="index"/>.
	/// </summary>
	/// <remarks>
	/// <see cref="ValueCollectionExtensions.InsertRange">More overloads</see> are
	/// available as extension methods.
	/// </remarks>
	public ValueListBuilder<T> InsertRange(int index, IEnumerable<T> items)
	{
		if (items is ICollection<T> collection)
		{
			return this.InsertRangeCollection(index, collection);
		}

		if (items is null)
		{
			throw new ArgumentNullException(nameof(items));
		}

		var list = this.Mutate();
		foreach (var item in items)
		{
			list.Insert(index++, item);

			// Something not immediately obvious from just the code itself is that
			// nothing prevents consumers from calling this method with an `items`
			// argument that is (indirectly) derived from `this`. e.g.
			// ```builder.InsertRange(0, builder.Where(_ => true))```
			// Without precaution that could result in an infinite loop with
			// infinite memory growth.
			// We "protect" our consumers from this by invalidating the enumerator
			// on each iteration such that an exception will be thrown.
			this.version++;
		}

		return this;
	}

	private ValueListBuilder<T> InsertRangeCollection(int index, ICollection<T> items)
	{
		var list = this.Mutate();

		if (checked(list.Count + items.Count) < list.Count)
		{
			throw new OverflowException();
		}

		list.InsertRange(index, items);
		return this;
	}

	/// <summary>
	/// Remove all elements from the list.
	/// </summary>
	public ValueListBuilder<T> Clear()
	{
		this.Mutate().Clear();
		return this;
	}

	/// <summary>
	/// Remove the element at the specified <paramref name="index"/>.
	/// </summary>
	public ValueListBuilder<T> RemoveAt(int index)
	{
		this.Mutate().RemoveAt(index);
		return this;
	}

	/// <summary>
	/// Remove a range of elements from the list.
	/// </summary>
	public ValueListBuilder<T> RemoveRange(int index, int count)
	{
		this.Mutate().RemoveRange(index, count);
		return this;
	}

	/// <summary>
	/// Remove the first occurrence of a specific object from the list.
	/// </summary>
	public ValueListBuilder<T> RemoveFirst(T item)
	{
		this.Mutate().Remove(item);
		return this;
	}

	/// <summary>
	/// Remove the first element that matches the predicate.
	/// </summary>
	public ValueListBuilder<T> RemoveFirst(Predicate<T> match)
	{
		var list = this.Mutate();
		var index = list.FindIndex(match);
		if (index >= 0)
		{
			list.RemoveAt(index);
		}

		return this;
	}

	/// <summary>
	/// Remove all occurrences of a specific object from the list.
	/// </summary>
	public ValueListBuilder<T> RemoveAll(T item)
	{
		this.Mutate().RemoveAll(x => EqualityComparer<T>.Default.Equals(x, item));
		return this;
	}

	/// <summary>
	/// Remove all elements that match the predicate.
	/// </summary>
	public ValueListBuilder<T> RemoveAll(Predicate<T> match)
	{
		this.Mutate().RemoveAll(match);
		return this;
	}

	/// <summary>
	/// Reverse the order of the elements in the list.
	/// </summary>
	public ValueListBuilder<T> Reverse()
	{
		this.Mutate().Reverse();
		return this;
	}

	/// <summary>
	/// Sort all  elements in the list.
	/// </summary>
	public ValueListBuilder<T> Sort()
	{
		this.Mutate().Sort();
		return this;
	}

	/// <inheritdoc cref="ValueCollectionsMarshal.AsSpan"/>
	internal Span<T> AsSpanUnsafe() => UnsafeHelpers.AsSpan(this.Mutate());

	/// <inheritdoc cref="ValueCollectionsMarshal.SetCount"/>
	internal void SetCountUnsafe(int count)
	{
		var list = this.Mutate();

#if NET8_0_OR_GREATER
		System.Runtime.InteropServices.CollectionsMarshal.SetCount(list, count);
#else
		if (count < 0)
		{
			throw new ArgumentOutOfRangeException(nameof(count));
		}

		var currentCount = list.Count;

		if (count > currentCount)
		{
			list.AddRange(new NoOpCollection(count - currentCount));
		}
		else if (count < currentCount)
		{
			list.RemoveRange(count, currentCount - count);
		}
#endif
	}

	/// <summary>
	/// Set the capacity to the actual number of elements in the list, if that
	/// number is less than a threshold value.
	/// </summary>
	public ValueListBuilder<T> TrimExcess()
	{
		this.Mutate().TrimExcess();
		return this;
	}

	/// <summary>
	/// Ensures that the capacity of this list is at least the specified capacity.
	/// If the current capacity is less than capacity, it is increased to at
	/// least the specified capacity.
	/// </summary>
	public ValueListBuilder<T> EnsureCapacity(int capacity)
	{
		EnsureCapacity(this.Mutate(), capacity);
		return this;
	}

	private static void EnsureCapacity(List<T> list, int capacity)
	{
#if NET6_0_OR_GREATER
		list.EnsureCapacity(capacity);
#else
		if (capacity < 0)
		{
			throw new ArgumentOutOfRangeException(nameof(capacity));
		}

		var currentCapacity = list.Capacity;
		if (currentCapacity < capacity)
		{
			const int DefaultCapacity = 4;
			const int MaxCapacity = 0X7FFFFFC7;

			var newCapacity = currentCapacity == 0 ? DefaultCapacity : 2 * currentCapacity;

			// Allow the list to grow to maximum possible capacity (~2G elements) before encountering overflow.
			// Note that this check works even when _items.Length overflowed thanks to the (uint) cast
			if ((uint)newCapacity > MaxCapacity)
			{
				newCapacity = MaxCapacity;
			}

			// If the computed capacity is still less than specified, set to the original argument.
			// Capacities exceeding Array.MaxLength will be surfaced as OutOfMemoryException by Array.Resize.
			if (newCapacity < capacity)
			{
				newCapacity = capacity;
			}

			list.Capacity = newCapacity;
		}
#endif
	}

	/// <summary>
	/// Returns <see langword="true"/> when the list contains the specified
	/// <paramref name="item"/>.
	/// </summary>
	public bool Contains(T item) => this.items switch
	{
		List<T> items => items.Contains(item),
		ValueList<T> items => items.Contains(item),
		_ => throw UnreachableException(),
	};

	/// <summary>
	/// Return the index of the first occurrence of <paramref name="item"/> in
	/// the list, or <c>-1</c> if not found.
	/// </summary>
	public int IndexOf(T item) => this.items switch
	{
		List<T> items => items.IndexOf(item),
		ValueList<T> items => items.IndexOf(item),
		_ => throw UnreachableException(),
	};

	/// <summary>
	/// Return the index of the last occurrence of <paramref name="item"/> in
	/// the list, or <c>-1</c> if not found.
	/// </summary>
	public int LastIndexOf(T item) => this.items switch
	{
		List<T> items => items.LastIndexOf(item),
		ValueList<T> items => items.LastIndexOf(item),
		_ => throw UnreachableException(),
	};

	/// <summary>
	/// Perform a binary search for <paramref name="item"/> within the list.
	/// The list is assumed to already be sorted. This uses the
	/// <see cref="Comparer{T}.Default">Default</see> comparer and throws if
	/// <typeparamref name="T"/> is not comparable. If the item is found, its
	/// index is returned. Otherwise a negative value is returned representing
	/// the bitwise complement of the index where the item should be inserted.
	/// </summary>
	public int BinarySearch(T item) => this.items switch
	{
		List<T> items => items.BinarySearch(item),
		ValueList<T> items => items.BinarySearch(item),
		_ => throw UnreachableException(),
	};

	/// <summary>
	/// Copy the contents of the list into a new array.
	/// </summary>
	public T[] ToArray() => this.items switch
	{
		List<T> items => items.ToArray(),
		ValueList<T> items => items.ToArray(),
		_ => throw UnreachableException(),
	};

	/// <summary>
	/// Attempt to copy the contents of the list into an existing
	/// <see cref="Span{T}"/>. If the <paramref name="destination"/> is too short,
	/// no items are copied and the method returns <see langword="false"/>.
	/// </summary>
	public bool TryCopyTo(Span<T> destination) => this.items switch
	{
		List<T> items => UnsafeHelpers.AsSpan(items).TryCopyTo(destination),
		ValueList<T> items => items.AsValueSlice().TryCopyTo(destination),
		_ => throw UnreachableException(),
	};

	/// <summary>
	/// Copy the contents of the list into an existing <see cref="Span{T}"/>.
	/// </summary>
	/// <exception cref="ArgumentException">
	///   <paramref name="destination"/> is shorter than the source list.
	/// </exception>
	public void CopyTo(Span<T> destination)
	{
		switch (this.items)
		{
			case List<T> items:
				UnsafeHelpers.AsSpan(items).CopyTo(destination);
				return;
			case ValueList<T> items:
				items.AsValueSlice().CopyTo(destination);
				return;
			default:
				throw UnreachableException();
		}
	}

	/// <inheritdoc/>
	void ICollection<T>.Add(T item) => this.Add(item);

	/// <inheritdoc/>
	void ICollection<T>.Clear() => this.Clear();

	/// <inheritdoc/>
	void ICollection<T>.CopyTo(T[] array, int arrayIndex)
	{
		if (array is null)
		{
			throw new ArgumentNullException(nameof(array));
		}

		this.CopyTo(array.AsSpan(arrayIndex));
	}

	/// <inheritdoc/>
	void IList<T>.Insert(int index, T item) => this.Insert(index, item);

	/// <inheritdoc/>
	bool ICollection<T>.Remove(T item) => this.Mutate().Remove(item);

	/// <inheritdoc/>
	void IList<T>.RemoveAt(int index) => this.RemoveAt(index);

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

	private static InvalidOperationException UnreachableException() => new("Unreachable");

	private static InvalidOperationException BuiltException() => new("Builder has already been built");

#if !NET8_0_OR_GREATER
	/// <summary>
	/// ICollection with a specified size, but no contents.
	/// </summary>
	private sealed class NoOpCollection : ICollection<T>
	{
		private readonly int size;

		public int Count => this.size;

		public bool IsReadOnly => true;

		internal NoOpCollection(int size)
		{
			this.size = size;
		}

		public void CopyTo(T[] array, int arrayIndex)
		{
			// Do nothing.
		}

		public IEnumerator<T> GetEnumerator() => throw new NotImplementedException();

		IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

		public void Add(T item) => throw new NotSupportedException();

		public void Clear() => throw new NotSupportedException();

		public bool Contains(T item) => throw new NotSupportedException();

		public bool Remove(T item) => throw new NotSupportedException();
	}
#endif

	/// <summary>
	/// Returns an enumerator for this <see cref="ValueListBuilder{T}"/>.
	///
	/// Typically, you don't need to manually call this method, but instead use
	/// the built-in <c>foreach</c> syntax.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public Enumerator GetEnumerator() => new Enumerator(this);

	/// <summary>
	/// Enumerator for <see cref="ValueListBuilder{T}"/>.
	/// </summary>
#pragma warning disable CA1034 // Nested types should not be visible
#pragma warning disable CA1815 // Override equals and operator equals on value types
	public struct Enumerator : IEnumeratorLike<T>
	{
		private readonly ValueListBuilder<T> builder;
		private readonly T[] items;
		private readonly int version;
		private int current;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal Enumerator(ValueListBuilder<T> builder)
		{
			this.builder = builder;
			this.items = builder.items switch
			{
				List<T> items => UnsafeHelpers.GetBackingArray(items),
				ValueList<T> items => items.Items,
				_ => throw UnreachableException(),
			};
			this.version = builder.version;
			this.current = -1;
		}

		/// <inheritdoc/>
		public readonly T Current
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => this.items![this.current];
		}

		/// <inheritdoc/>
		public bool MoveNext()
		{
			if (this.version != this.builder.version)
			{
				throw new InvalidOperationException("Collection was modified during enumeration.");
			}

			return ++this.current < this.builder.Count;
		}
	}
#pragma warning restore CA1815 // Override equals and operator equals on value types
#pragma warning restore CA1034 // Nested types should not be visible
}
