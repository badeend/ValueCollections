using System.Collections;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace Badeend.ValueCollections;

/// <content>
/// Builder code.
/// </content>
public sealed partial class ValueList<T>
{
	/// <summary>
	/// A mutable list that can be used to efficiently construct new immutable lists.
	/// </summary>
	/// <remarks>
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
	/// Unlike ValueList, Builder is <em>not</em> thread-safe.
	/// </remarks>
	[CollectionBuilder(typeof(ValueList), nameof(ValueList.CreateBuilder))]
	public sealed class Builder
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

		private Collection? collectionCache;

		/// <summary>
		/// Returns <see langword="true"/> when this instance has been built and is
		/// now read-only.
		/// </summary>
		[Pure]
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
		[Pure]
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

		/// <summary>
		/// Copy the current contents of the builder into a new <see cref="ValueList{T}.Builder"/>.
		/// </summary>
		[Pure]
		public Builder ToValueListBuilder()
		{
			return ValueList.CreateBuilder<T>(this.Count).AddRange(this.AsSpanUnsafe());
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
		public int Capacity
		{
			[Pure]
			get => this.items switch
			{
				List<T> items => items.Capacity,
				ValueList<T> items => items.Capacity,
				_ => throw UnreachableException(),
			};
		}

		/// <summary>
		/// Gets or sets the element at the specified <paramref name="index"/>.
		/// </summary>
		public T this[int index]
		{
			[Pure]
			get => this.Read()[index];
			set => this.Mutate()[index] = value;
		}

		/// <summary>
		/// Current length of the list.
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

		private Builder(ValueList<T> items)
		{
			this.items = items;
		}

		private Builder(List<T> items)
		{
			this.items = items;
		}

		internal static Builder Create()
		{
			return new(ValueList<T>.Empty);
		}

		internal static Builder CreateWithCapacity(int capacity)
		{
			if (capacity < 0)
			{
				throw new ArgumentOutOfRangeException(nameof(capacity));
			}

			if (capacity == 0)
			{
				return new(ValueList<T>.Empty);
			}
			else
			{
				return new(new List<T>(capacity));
			}
		}

		internal static Builder FromEnumerable(IEnumerable<T> items)
		{
			if (items is ValueList<T> valueList)
			{
				return new(valueList);
			}
			else
			{
				return new(new List<T>(items));
			}
		}

		internal static Builder FromValueList(ValueList<T> items) => new(items);

		internal static Builder FromListUnsafe(List<T> items) => new(items);

		/// <summary>
		/// Replaces an element at a given position in the list with the specified
		/// element.
		/// </summary>
		public Builder SetItem(int index, T value)
		{
			this.Mutate()[index] = value;
			return this;
		}

		/// <summary>
		/// Add an <paramref name="item"/> to the end of the list.
		/// </summary>
		public Builder Add(T item)
		{
			this.Mutate().Add(item);
			return this;
		}

		// Accessible through an extension method.
		internal Builder AddRangeSpan(ReadOnlySpan<T> items)
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
		/// <see cref="ValueCollectionExtensions.AddRange{T}(ValueList{T}.Builder, ReadOnlySpan{T})">More overloads</see> are
		/// available as extension methods.
		/// </remarks>
		public Builder AddRange(IEnumerable<T> items)
		{
			if (items is null)
			{
				throw new ArgumentNullException(nameof(items));
			}

			if (items is ICollection<T> collection)
			{
				var list = this.Mutate();

				if (checked(list.Count + collection.Count) < list.Count)
				{
					throw new OverflowException();
				}

				list.AddRange(items);
			}
			else
			{
				this.AddRangeEnumerable(items);
			}

			return this;
		}

		private void AddRangeEnumerable(IEnumerable<T> items)
		{
			foreach (var item in items)
			{
				// Something not immediately obvious from just the code itself is that
				// nothing prevents consumers from calling this method with an `items`
				// argument that is (indirectly) derived from `this`. e.g.
				// ```builder.AddRange(builder.Where(_ => true))```
				// Without precaution that could result in an infinite loop with
				// infinite memory growth.
				// We "protect" our consumers from this by invalidating the enumerator
				// on each iteration such that an exception will be thrown.
				this.Mutate().Add(item);
			}
		}

		/// <summary>
		/// Insert an <paramref name="item"/> into the list at the specified <paramref name="index"/>.
		/// </summary>
		public Builder Insert(int index, T item)
		{
			this.Mutate().Insert(index, item);
			return this;
		}

		// Accessible through an extension method.
		internal Builder InsertRangeSpan(int index, ReadOnlySpan<T> items)
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
		public Builder InsertRange(int index, IEnumerable<T> items)
		{
			if (items is null)
			{
				throw new ArgumentNullException(nameof(items));
			}

			if (items is ICollection<T> collection)
			{
				var list = this.Mutate();

				if (checked(list.Count + collection.Count) < list.Count)
				{
					throw new OverflowException();
				}

				list.InsertRange(index, items);
			}
			else
			{
				this.InsertRangeEnumerable(index, items);
			}

			return this;
		}

		private void InsertRangeEnumerable(int index, IEnumerable<T> items)
		{
			foreach (var item in items)
			{
				// Something not immediately obvious from just the code itself is that
				// nothing prevents consumers from calling this method with an `items`
				// argument that is (indirectly) derived from `this`. e.g.
				// ```builder.InsertRange(0, builder.Where(_ => true))```
				// Without precaution that could result in an infinite loop with
				// infinite memory growth.
				// We "protect" our consumers from this by invalidating the enumerator
				// on each iteration such that an exception will be thrown.
				this.Mutate().Insert(index++, item);
			}
		}

		/// <summary>
		/// Remove all elements from the list.
		/// </summary>
		public Builder Clear()
		{
			this.Mutate().Clear();
			return this;
		}

		/// <summary>
		/// Remove the element at the specified <paramref name="index"/>.
		/// </summary>
		public Builder RemoveAt(int index)
		{
			this.Mutate().RemoveAt(index);
			return this;
		}

		/// <summary>
		/// Remove a range of elements from the list.
		/// </summary>
		public Builder RemoveRange(int index, int count)
		{
			this.Mutate().RemoveRange(index, count);
			return this;
		}

		/// <summary>
		/// Attempt to remove the first occurrence of a specific object from the list.
		/// Returns <see langword="false"/> when the element wasn't found.
		/// </summary>
		public bool TryRemoveFirst(T item)
		{
			return this.Mutate().Remove(item);
		}

		/// <summary>
		/// Attempt to remove the first element that matches the predicate.
		/// Returns <see langword="false"/> when the element wasn't found.
		/// </summary>
		public bool TryRemoveFirst(Predicate<T> match)
		{
			var list = this.Mutate();
			var index = list.FindIndex(match);
			if (index >= 0)
			{
				list.RemoveAt(index);
				return true;
			}

			return false;
		}

		/// <summary>
		/// Remove the first occurrence of a specific object from the list.
		/// </summary>
		public Builder RemoveFirst(T item)
		{
			_ = this.TryRemoveFirst(item);
			return this;
		}

		/// <summary>
		/// Remove the first element that matches the predicate.
		/// </summary>
		public Builder RemoveFirst(Predicate<T> match)
		{
			_ = this.TryRemoveFirst(match);
			return this;
		}

		/// <summary>
		/// Remove all occurrences of a specific object from the list.
		/// </summary>
		public Builder RemoveAll(T item)
		{
			this.Mutate().RemoveAll(x => EqualityComparer<T>.Default.Equals(x, item));
			return this;
		}

		/// <summary>
		/// Remove all elements that match the predicate.
		/// </summary>
		public Builder RemoveAll(Predicate<T> match)
		{
			this.Mutate().RemoveAll(match);
			return this;
		}

		/// <summary>
		/// Reverse the order of the elements in the list.
		/// </summary>
		public Builder Reverse()
		{
			this.Mutate().Reverse();
			return this;
		}

		/// <summary>
		/// Sort all  elements in the list.
		/// </summary>
		public Builder Sort()
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
		public Builder TrimExcess()
		{
			this.Mutate().TrimExcess();
			return this;
		}

		/// <summary>
		/// Ensures that the capacity of this list is at least the specified capacity.
		/// If the current capacity is less than capacity, it is increased to at
		/// least the specified capacity.
		/// </summary>
		public Builder EnsureCapacity(int capacity)
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
		[Pure]
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
		[Pure]
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
		[Pure]
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
		[Pure]
		public int BinarySearch(T item) => this.items switch
		{
			List<T> items => items.BinarySearch(item),
			ValueList<T> items => items.BinarySearch(item),
			_ => throw UnreachableException(),
		};

		/// <summary>
		/// Copy the contents of the list into a new array.
		/// </summary>
		[Pure]
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
		/// Create a new heap-allocated live view of the builder.
		/// </summary>
		/// <remarks>
		/// This method is an <c>O(1)</c> operation and allocates a new fixed-size
		/// collection instance. The items are not copied. Changes made to the
		/// builder are visible in the collection and vice versa.
		/// </remarks>
		public Collection AsCollection() => this.collectionCache ??= new Collection(this);

#pragma warning disable CA1034 // Nested types should not be visible
		/// <summary>
		/// A new heap-allocated live view of a builder. Changes made to the
		/// collection are visible in the builder and vice versa.
		/// </summary>
		public sealed class Collection : IList<T>, IReadOnlyList<T>
		{
			private readonly Builder builder;

			/// <summary>
			/// The underlying builder.
			/// </summary>
			public Builder Builder => this.builder;

			internal Collection(Builder builder)
			{
				this.builder = builder;
			}

			/// <inheritdoc/>
			T IList<T>.this[int index] { get => this.builder[index]; set => this.builder[index] = value; }

			/// <inheritdoc/>
			T IReadOnlyList<T>.this[int index] => this.builder[index];

			/// <inheritdoc/>
			int ICollection<T>.Count => this.builder.Count;

			/// <inheritdoc/>
			int IReadOnlyCollection<T>.Count => this.builder.Count;

			/// <inheritdoc/>
			bool ICollection<T>.IsReadOnly => this.builder.IsReadOnly;

			/// <inheritdoc/>
			void ICollection<T>.Add(T item) => this.builder.Add(item);

			/// <inheritdoc/>
			void ICollection<T>.Clear() => this.builder.Clear();

			/// <inheritdoc/>
			bool ICollection<T>.Contains(T item) => this.builder.Contains(item);

			/// <inheritdoc/>
			void ICollection<T>.CopyTo(T[] array, int arrayIndex)
			{
				if (array is null)
				{
					throw new ArgumentNullException(nameof(array));
				}

				this.builder.CopyTo(array.AsSpan(arrayIndex));
			}

			/// <inheritdoc/>
			IEnumerator<T> IEnumerable<T>.GetEnumerator()
			{
				if (this.builder.Count == 0)
				{
					return EnumeratorLike.Empty<T>();
				}
				else
				{
					return EnumeratorLike.AsIEnumerator<T, Enumerator>(new Enumerator(this.builder));
				}
			}

			/// <inheritdoc/>
			IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable<T>)this).GetEnumerator();

			/// <inheritdoc/>
			int IList<T>.IndexOf(T item) => this.builder.IndexOf(item);

			/// <inheritdoc/>
			void IList<T>.Insert(int index, T item) => this.builder.Insert(index, item);

			/// <inheritdoc/>
			bool ICollection<T>.Remove(T item) => this.builder.TryRemoveFirst(item);

			/// <inheritdoc/>
			void IList<T>.RemoveAt(int index) => this.builder.RemoveAt(index);
		}
#pragma warning restore CA1034 // Nested types should not be visible

		/// <summary>
		/// Returns an enumerator for this <see cref="Builder"/>.
		///
		/// Typically, you don't need to manually call this method, but instead use
		/// the built-in <c>foreach</c> syntax.
		/// </summary>
		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Enumerator GetEnumerator() => new Enumerator(this);

		/// <summary>
		/// Enumerator for <see cref="Builder"/>.
		/// </summary>
#pragma warning disable CA1034 // Nested types should not be visible
#pragma warning disable CA1815 // Override equals and operator equals on value types
		[StructLayout(LayoutKind.Auto)]
		public struct Enumerator : IEnumeratorLike<T>
		{
			private readonly Builder builder;
			private readonly T[] items;
			private readonly int version;
			private int current;

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			internal Enumerator(Builder builder)
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
	}
}
