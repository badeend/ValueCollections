using System.Collections;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;

namespace Badeend.ValueCollections;

/// <summary>
/// These are extension methods only to remove call site ambiguity.
/// </summary>
public static class ValueListBuilder
{
	/// <summary>
	/// Add the <paramref name="items"/> to the end of the list.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static ValueListBuilder<T> AddRange<T>(this ValueListBuilder<T> builder, ReadOnlySpan<T> items)
	{
		if (builder is null)
		{
			throw new ArgumentNullException(nameof(builder));
		}

		return builder.AddRangeSpan(items);
	}

	/// <summary>
	/// Insert the <paramref name="items"/> into the list at the specified <paramref name="index"/>.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static ValueListBuilder<T> InsertRange<T>(this ValueListBuilder<T> builder, int index, ReadOnlySpan<T> items)
	{
		if (builder is null)
		{
			throw new ArgumentNullException(nameof(builder));
		}

		return builder.InsertRangeSpan(index, items);
	}
}

/// <summary>
/// A mutable list that can be used to efficiently construct new immutable lists.
///
/// Most mutating methods on this class return `this`, allowing the caller to
/// chain multiple mutations in a row.
///
/// When you're done building, call <see cref="ToValueList()"/> to get out the
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
	/// <summary>
	/// Can be one of:
	/// - ValueList{T}: when copy-on-write hasn't kicked in yet.
	/// - List{T}: we're actively building a list.
	/// </summary>
	private IReadOnlyList<T> items;

	private int version;

	/// <summary>
	/// Create a <see cref="ValueList{T}"/> based on the current contents of the
	/// builder.
	///
	/// This is an <c>O(1)</c> operation and performs only a small fixed-size
	/// memory allocation. This does not perform a bulk copy of the contents.
	/// </summary>
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

	/// <inheritdoc/>
	bool ICollection<T>.IsReadOnly => false;

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
	///
	/// To construct a ValueListBuilder from other types of inputs (Spans etc.),
	/// use one of the <c>.ToValueListBuilder()</c> extension methods.
	/// </summary>
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

	internal static ValueListBuilder<T> FromArrayUnsafe(T[] items) => new(UnsafeHelpers.AsList(items));

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
		UnsafeHelpers.AddRange(this.Mutate(), items);
		return this;
	}

	/// <summary>
	/// Add the <paramref name="items"/> to the end of the list.
	/// </summary>
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
		UnsafeHelpers.InsertRange(this.Mutate(), index, items);
		return this;
	}

	/// <summary>
	/// Insert the <paramref name="items"/> into the list at the specified <paramref name="index"/>.
	/// </summary>
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
		UnsafeHelpers.SetCount(this.Mutate(), count);
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
		UnsafeHelpers.EnsureCapacity(this.Mutate(), capacity);
		return this;
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
	/// Create a read-only live view on this builder.
	///
	/// Mutations on the underlying builder instance are still visible through
	/// the ReadOnlyCollection. If you need an immutable snapshot of the builder,
	/// use <see cref="ToValueList"/> instead.
	///
	/// This is an <c>O(1)</c> operation.
	/// </summary>
	public ReadOnlyCollection<T> AsReadOnly() => new(this);

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
