using System.Collections;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Badeend.ValueCollections.Internals;

namespace Badeend.ValueCollections;

/// <content>
/// Builder code.
/// </content>
public sealed partial class ValueList<T>
{
	/// <summary>
	/// A mutable list builder that can be used to efficiently construct new immutable lists.
	/// </summary>
	/// <remarks>
	/// Most mutating methods on this type return `this`, allowing the caller to
	/// chain multiple mutations in a row.
	///
	/// When you're done building, call <see cref="Build()"/> to extract the
	/// resulting list.
	///
	/// For constructing <see cref="ValueList{T}"/>s it is recommended to use this
	/// type over e.g. <see cref="List{T}"/>. This type can avoiding unnecessary
	/// copying by taking advantage of the immutability of its results. Whereas
	/// calling <c>.ToValueList()</c> on a regular <see cref="List{T}"/>
	/// <em>always</em> performs a full copy.
	///
	/// Unlike the resulting ValueList, its Builder is <em>not</em> thread-safe.
	///
	/// The <c>default</c> value is an empty read-only builder.
	/// </remarks>
	[CollectionBuilder(typeof(ValueList), nameof(ValueList.CreateBuilder))]
	public readonly struct Builder : IEquatable<Builder>
	{
		// Various parts of this class have been adapted from:
		// https://github.com/dotnet/runtime/blob/5aa9687e110faa19d1165ba680e52585a822464d/src/libraries/System.Private.CoreLib/src/System/Collections/Generic/List.cs

		/// <summary>
		/// Initial capacity for non-zero size lists.
		/// </summary>
		private const int DefaultCapacity = 4;

		/// <summary>
		/// Only access this field through .Read() or .Mutate().
		/// </summary>
		private readonly ValueList<T>? list;

		/// <summary>
		/// Returns <see langword="true"/> when this instance has been built and is
		/// now read-only.
		/// </summary>
		[Pure]
		public bool IsReadOnly => BuilderState.IsImmutable(this.Read().state);

		/// <summary>
		/// Finalize the builder and export its contents as a <see cref="ValueList{T}"/>.
		/// This makes the builder read-only. Any future attempt to mutate the
		/// builder will throw.
		///
		/// This is an <c>O(1)</c> operation and performs no heap allocations.
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
			_ = this.Mutate();
			var list = this.Read();

			if (BuilderState.IsImmutable(list.state))
			{
				ThrowHelpers.ThrowInvalidOperationException_AlreadyBuilt();
			}

			list.state = BuilderState.InitialImmutable;

			return list;
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
			var list = this.Read();

			if (BuilderState.IsImmutable(list.state))
			{
				return list;
			}
			else
			{
				return ValueList<T>.CreateImmutableFromSpan(list.AsSpan());
			}
		}

		/// <summary>
		/// Copy the current contents of the builder into a new <see cref="ValueList{T}.Builder"/>.
		/// </summary>
		[Pure]
		public Builder ToValueListBuilder()
		{
			var list = this.Read();

			return ValueList.CreateBuilder<T>(list.Count).AddRange(list.AsSpan());
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private ValueList<T> Mutate()
		{
			var list = this.list;
			if (list is null || (uint)list.state >= BuilderState.LastMutableVersion)
			{
				SlowPath(list);
			}

			list.state++;

			Debug.Assert(BuilderState.IsMutable(list.state));

			return list;

			[MethodImpl(MethodImplOptions.NoInlining)]
			static void SlowPath([NotNull] ValueList<T>? list)
			{
				if (list is null)
				{
					ThrowHelpers.ThrowInvalidOperationException_UninitializedBuiler();
				}
				else if (list.state == BuilderState.LastMutableVersion)
				{
					list.state = BuilderState.InitialMutable;
				}
				else
				{
					Debug.Assert(BuilderState.IsImmutable(list.state));

					ThrowHelpers.ThrowInvalidOperationException_AlreadyBuilt();
				}
			}
		}

		private ValueList<T> Read() => this.list ?? Empty;

		/// <summary>
		/// The total number of elements the internal data structure can hold without resizing.
		/// </summary>
		[Pure]
		public int Capacity => this.Read().Capacity;

		/// <summary>
		/// Gets or sets the element at the specified <paramref name="index"/>.
		/// </summary>
		public T this[int index]
		{
			[Pure]
			get => this.Read()[index];
			set
			{
				var list = this.Mutate();

				if ((uint)index >= (uint)list.size)
				{
					ThrowHelpers.ThrowArgumentOutOfRangeException(ThrowHelpers.Argument.index);
				}

				list.items[index] = value;
			}
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

		/// <summary>
		/// Create a new uninitialized builder.
		///
		/// An uninitialized builder behaves the same as an already built list
		/// with 0 items and 0 capacity. Reading from it will succeed, but
		/// mutating it will throw.
		///
		/// This is the same as the <c>default</c> value.
		/// </summary>
		[Pure]
		[Obsolete("This creates an uninitialized builder. Use ValueList.CreateBuilder<T>() instead.")]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public Builder()
		{
		}

		private Builder(ValueList<T> list)
		{
			Debug.Assert(BuilderState.IsMutable(list.state));

			this.list = list;
		}

		internal static Builder Create()
		{
			return new(ValueList<T>.CreateMutable());
		}

		internal static Builder CreateWithCapacity(int capacity)
		{
			return new(ValueList<T>.CreateMutableWithCapacity(capacity));
		}

		internal static Builder CreateFromEnumerable(IEnumerable<T> items)
		{
			if (items is null)
			{
				ThrowHelpers.ThrowArgumentNullException(ThrowHelpers.Argument.items);
			}

			// On newer runtimes, Enumerable.ToArray() is faster than simply
			// looping the enumerable ourselves, because the LINQ method has
			// access to an internal optimization to forgo the double virtual
			// interface call per iteration.
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
			var newItems = items.ToArray();
			return new(ValueList<T>.CreateMutableFromArrayUnsafe(newItems, newItems.Length));
#else
			if (items is ICollection<T> collection)
			{
				int count = collection.Count;
				if (count == 0)
				{
					return Create();
				}

				var newItems = new T[count];
				collection.CopyTo(newItems, 0);
				return new(ValueList<T>.CreateMutableFromArrayUnsafe(newItems, count));
			}
			else
			{
				var builder = Create();
				var list = builder.list;
				Debug.Assert(list is not null);

				foreach (var item in items)
				{
					AddUnsafe(list!, item);
				}

				return builder;
			}
#endif
		}

		/// <summary>
		/// Replaces an element at a given position in the list with the specified
		/// element.
		/// </summary>
		public Builder SetItem(int index, T value)
		{
			this[index] = value;
			return this;
		}

		/// <summary>
		/// Add an <paramref name="item"/> to the end of the list.
		/// </summary>
		public Builder Add(T item)
		{
			var list = this.Mutate();

			AddUnsafe(list, item);

			return this;
		}

		// Add item to the list without checking for mutability.
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal static void AddUnsafe(ValueList<T> list, T item)
		{
			T[] items = list.items;
			int size = list.size;
			if ((uint)size < (uint)items.Length)
			{
				list.size = size + 1;
				items[size] = item;
			}
			else
			{
				AddWithResize(list, item);
			}
		}

		// Non-inline from List.Add to improve its code quality as uncommon path
		[MethodImpl(MethodImplOptions.NoInlining)]
		private static void AddWithResize(ValueList<T> list, T item)
		{
			Debug.Assert(list.size == list.items.Length);

			int size = list.size;
			Grow(list, size + 1);
			list.size = size + 1;
			list.items[size] = item;
		}

		// Accessible through an extension method.
		internal Builder AddRangeSpan(ReadOnlySpan<T> items)
		{
			var list = this.Mutate();

			if (!items.IsEmpty)
			{
				if (list.items.Length - list.size < items.Length)
				{
					Grow(list, checked(list.size + items.Length));
				}

				items.CopyTo(list.items.AsSpan(list.size));
				list.size += items.Length;
			}

			return this;
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
			var list = this.Mutate();

			if (items is null)
			{
				ThrowHelpers.ThrowArgumentNullException(ThrowHelpers.Argument.items);
			}

			if (items is ICollection<T> collection)
			{
				int count = collection.Count;
				if (count > 0)
				{
					if (list.items.Length - list.size < count)
					{
						Grow(list, checked(list.size + count));
					}

					collection.CopyTo(list.items, list.size);
					list.size += count; // Update size _after_ copying, to handle the case in which we're inserting the list into itself.
				}
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
				this.Add(item);
			}
		}

		/// <summary>
		/// Insert an <paramref name="item"/> into the list at the specified <paramref name="index"/>.
		/// </summary>
		public Builder Insert(int index, T item)
		{
			var list = this.Mutate();

			// Note that insertions at the end are legal.
			if ((uint)index > (uint)list.size)
			{
				ThrowHelpers.ThrowArgumentOutOfRangeException(ThrowHelpers.Argument.index);
			}

			if (list.size == list.items.Length)
			{
				GrowForInsertion(list, index, 1);
			}
			else if (index < list.size)
			{
				Array.Copy(list.items, index, list.items, index + 1, list.size - index);
			}

			list.items[index] = item;
			list.size++;

			return this;
		}

		// Accessible through an extension method.
		internal Builder InsertRangeSpan(int index, ReadOnlySpan<T> items)
		{
			var list = this.Mutate();

			if ((uint)index > (uint)list.size)
			{
				ThrowHelpers.ThrowArgumentOutOfRangeException(ThrowHelpers.Argument.index);
			}

			if (!items.IsEmpty)
			{
				if (list.items.Length - list.size < items.Length)
				{
					Grow(list, checked(list.size + items.Length));
				}

				// If the index at which to insert is less than the number of items in the list,
				// shift all items past that location in the list down to the end, making room
				// to copy in the new data.
				if (index < list.size)
				{
					Array.Copy(list.items, index, list.items, index + items.Length, list.size - index);
				}

				// Copy the source span into the list.
				// Note that this does not handle the unsafe case of trying to insert a CollectionsMarshal.AsSpan(list)
				// or some slice thereof back into the list itself; such an operation has undefined behavior.
				items.CopyTo(list.items.AsSpan(index));
				list.size += items.Length;
			}

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
			var list = this.Mutate();

			if (items is null)
			{
				ThrowHelpers.ThrowArgumentNullException(ThrowHelpers.Argument.items);
			}

			if ((uint)index > (uint)list.size)
			{
				ThrowHelpers.ThrowArgumentOutOfRangeException(ThrowHelpers.Argument.index);
			}

			if (items is ICollection<T> collection)
			{
				int count = collection.Count;
				if (count > 0)
				{
					if (list.items.Length - list.size < count)
					{
						GrowForInsertion(list, index, count);
					}
					else if (index < list.size)
					{
						Array.Copy(list.items, index, list.items, index + count, list.size - index);
					}

					// If we're inserting the list into itself, we want to be able to deal with that.
					if (collection is ValueList<T>.Builder.Collection vlbc && object.ReferenceEquals(vlbc.Builder.list, list))
					{
						// Copy first part of _items to insert location
						Array.Copy(list.items, 0, list.items, index, index);

						// Copy last part of _items back to inserted location
						Array.Copy(list.items, index + count, list.items, index * 2, list.size - index);
					}
					else
					{
						collection.CopyTo(list.items, index);
					}

					list.size += count;
				}
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
				this.Insert(index++, item);
			}
		}

		/// <summary>
		/// Remove all elements from the list.
		/// </summary>
		public Builder Clear()
		{
			var list = this.Mutate();

			if (Polyfills.IsReferenceOrContainsReferences<T>())
			{
				int size = list.size;
				list.size = 0;
				if (size > 0)
				{
					Array.Clear(list.items, 0, size); // Clear the elements so that the gc can reclaim the references.
				}
			}
			else
			{
				list.size = 0;
			}

			return this;
		}

		/// <summary>
		/// Remove the element at the specified <paramref name="index"/>.
		/// </summary>
		public Builder RemoveAt(int index)
		{
			var list = this.Mutate();

			if ((uint)index >= (uint)list.size)
			{
				ThrowHelpers.ThrowArgumentOutOfRangeException(ThrowHelpers.Argument.index);
			}

			RemoveAtUnsafe(list, index);

			return this;
		}

		// This assumes Mutate has already been called and that `index` is valid.
		private static void RemoveAtUnsafe(ValueList<T> list, int index)
		{
			Debug.Assert((uint)index < (uint)list.size);

			list.size--;
			if (index < list.size)
			{
				Array.Copy(list.items, index + 1, list.items, index, list.size - index);
			}

			if (Polyfills.IsReferenceOrContainsReferences<T>())
			{
				list.items[list.size] = default!;
			}
		}

		/// <summary>
		/// Remove a range of elements from the list.
		/// </summary>
		public Builder RemoveRange(int index, int count)
		{
			var list = this.Mutate();

			if (index < 0)
			{
				ThrowHelpers.ThrowArgumentOutOfRangeException(ThrowHelpers.Argument.index);
			}

			if (count < 0)
			{
				ThrowHelpers.ThrowArgumentOutOfRangeException(ThrowHelpers.Argument.count);
			}

			if (list.size - index < count)
			{
				ThrowHelpers.ThrowArgumentException_InvalidOffsetOrLength();
			}

			if (count > 0)
			{
				list.size -= count;

				if (index < list.size)
				{
					Array.Copy(list.items, index + count, list.items, index, list.size - index);
				}

				if (Polyfills.IsReferenceOrContainsReferences<T>())
				{
					Array.Clear(list.items, list.size, count);
				}
			}

			return this;
		}

		/// <summary>
		/// Attempt to remove the first occurrence of a specific object from the list.
		/// Returns <see langword="false"/> when the element wasn't found.
		/// </summary>
		public bool TryRemove(T item)
		{
			var list = this.Mutate();

			int index = list.IndexOf(item);
			if (index >= 0)
			{
				RemoveAtUnsafe(list, index);
				return true;
			}

			return false;
		}

		/// <summary>
		/// Attempt to remove the first element that matches the predicate.
		/// Returns <see langword="false"/> when the element wasn't found.
		/// </summary>
		public bool TryRemove(Predicate<T> match)
		{
			var list = this.Mutate();

			if (match is null)
			{
				ThrowHelpers.ThrowArgumentNullException(ThrowHelpers.Argument.match);
			}

			// Note that the `match` function can read and even modify the list we're about to remove from.
			for (int i = 0; i < list.size; i++)
			{
				if (match(list.items[i]))
				{
					RemoveAtUnsafe(list, i);
					return true;
				}
			}

			return false;
		}

		/// <summary>
		/// Remove the first occurrence of a specific object from the list.
		/// </summary>
		public Builder Remove(T item)
		{
			_ = this.TryRemove(item);
			return this;
		}

		/// <summary>
		/// Remove the first element that matches the predicate.
		/// </summary>
		public Builder Remove(Predicate<T> match)
		{
			_ = this.TryRemove(match);
			return this;
		}

		/// <summary>
		/// Remove all occurrences of a specific object from the list.
		/// </summary>
		public Builder RemoveAll(T item)
		{
			return this.RemoveAll(x => EqualityComparer<T>.Default.Equals(x, item));
		}

		/// <summary>
		/// Remove all elements that match the predicate.
		/// </summary>
		public Builder RemoveAll(Predicate<T> match)
		{
			var list = this.Mutate();

			if (match == null)
			{
				ThrowHelpers.ThrowArgumentNullException(ThrowHelpers.Argument.match);
			}

			int freeIndex = 0;   // the first free slot in items array

			// Find the first item which needs to be removed.
			while (freeIndex < list.size && !match(list.items[freeIndex]))
			{
				freeIndex++;
			}

			if (freeIndex < list.size)
			{
				int current = freeIndex + 1;
				while (current < list.size)
				{
					// Find the first item which needs to be kept.
					while (current < list.size && match(list.items[current]))
					{
						current++;
					}

					if (current < list.size)
					{
						// copy item to the free slot.
						list.items[freeIndex++] = list.items[current++];
					}
				}

				if (Polyfills.IsReferenceOrContainsReferences<T>())
				{
					Array.Clear(list.items, freeIndex, list.size - freeIndex); // Clear the elements so that the gc can reclaim the references.
				}

				list.size = freeIndex;
			}

			return this;
		}

		/// <summary>
		/// Reverse the order of the elements in the list.
		/// </summary>
		public Builder Reverse()
		{
			var list = this.Mutate();

			if (list.size > 1)
			{
				Array.Reverse(list.items, 0, list.size);
			}

			return this;
		}

		/// <summary>
		/// Sort all  elements in the list.
		/// </summary>
		public Builder Sort()
		{
			var list = this.Mutate();

			if (list.size > 1)
			{
				Array.Sort(list.items, 0, list.size);
			}

			return this;
		}

		/// <inheritdoc cref="ValueCollectionsMarshal.AsSpan"/>
		internal Span<T> AsSpanUnsafe()
		{
			var list = this.Mutate();
			return list.items.AsSpan(0, list.size);
		}

		/// <inheritdoc cref="ValueCollectionsMarshal.SetCount"/>
		internal void SetCountUnsafe(int count)
		{
			var list = this.Mutate();

			if (count < 0)
			{
				ThrowHelpers.ThrowArgumentOutOfRangeException(ThrowHelpers.Argument.count);
			}

			if (count > list.Capacity)
			{
				Grow(list, count);
			}
			else if (count < list.size && Polyfills.IsReferenceOrContainsReferences<T>())
			{
				Array.Clear(list.items, count, list.size - count);
			}

			list.size = count;
		}

		/// <summary>
		/// Set the capacity to the actual number of elements in the list, if that
		/// number is less than a threshold value.
		/// </summary>
		public Builder TrimExcess()
		{
			var list = this.Mutate();

			int threshold = (int)(((double)list.items.Length) * 0.9);
			if (list.size < threshold)
			{
				SetCapacity(list, list.size);
			}

			return this;
		}

		/// <summary>
		/// Ensures that the capacity of this list is at least the specified capacity.
		/// If the current capacity is less than capacity, it is increased to at
		/// least the specified capacity.
		/// </summary>
		public Builder EnsureCapacity(int capacity)
		{
			var list = this.Mutate();

			if (capacity < 0)
			{
				ThrowHelpers.ThrowArgumentOutOfRangeException(ThrowHelpers.Argument.capacity);
			}

			if (list.items.Length < capacity)
			{
				Grow(list, capacity);
			}

			return this;
		}

		/// <summary>
		/// Increase the capacity of this list to at least the specified <paramref name="capacity"/>.
		/// </summary>
		private static void Grow(ValueList<T> list, int capacity)
		{
			SetCapacity(list, GetNewCapacity(list, capacity));
		}

		/// <summary>
		/// Enlarge this list so it may contain at least <paramref name="insertionCount"/> more elements
		/// And copy data to their after-insertion positions.
		/// This method is specifically for insertion, as it avoids 1 extra array copy.
		/// You should only call this method when Count + insertionCount > Capacity.
		/// </summary>
		private static void GrowForInsertion(ValueList<T> list, int indexToInsert, int insertionCount = 1)
		{
			Debug.Assert(insertionCount > 0);

			int requiredCapacity = checked(list.size + insertionCount);
			int newCapacity = GetNewCapacity(list, requiredCapacity);

			// Inline and adapt logic from set_Capacity
			T[] newItems = new T[newCapacity];
			if (indexToInsert != 0)
			{
				Array.Copy(list.items, newItems, length: indexToInsert);
			}

			if (list.size != indexToInsert)
			{
				Array.Copy(list.items, indexToInsert, newItems, indexToInsert + insertionCount, list.size - indexToInsert);
			}

			list.items = newItems;
		}

		private static void SetCapacity(ValueList<T> list, int capacity)
		{
			if (capacity < list.size)
			{
				ThrowHelpers.ThrowArgumentOutOfRangeException(ThrowHelpers.Argument.capacity);
			}

			if (capacity != list.items.Length)
			{
				if (capacity > 0)
				{
					T[] newItems = new T[capacity];
					if (list.size > 0)
					{
						Array.Copy(list.items, newItems, list.size);
					}

					list.items = newItems;
				}
				else
				{
					list.items = [];
				}
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static int GetNewCapacity(ValueList<T> list, int capacity)
		{
			Debug.Assert(list.items.Length < capacity);

			int newCapacity = list.items.Length == 0 ? DefaultCapacity : 2 * list.items.Length;

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

		/// <summary>
		/// Returns <see langword="true"/> when the list contains the specified
		/// <paramref name="item"/>.
		/// </summary>
		[Pure]
		public bool Contains(T item) => this.Read().Contains(item);

		/// <summary>
		/// Return the index of the first occurrence of <paramref name="item"/> in
		/// the list, or <c>-1</c> if not found.
		/// </summary>
		[Pure]
		public int IndexOf(T item) => this.Read().IndexOf(item);

		/// <summary>
		/// Return the index of the last occurrence of <paramref name="item"/> in
		/// the list, or <c>-1</c> if not found.
		/// </summary>
		[Pure]
		public int LastIndexOf(T item) => this.Read().LastIndexOf(item);

		/// <summary>
		/// Perform a binary search for <paramref name="item"/> within the list.
		/// The list is assumed to already be sorted. This uses the
		/// <see cref="Comparer{T}.Default">Default</see> comparer and throws if
		/// <typeparamref name="T"/> is not comparable. If the item is found, its
		/// index is returned. Otherwise a negative value is returned representing
		/// the bitwise complement of the index where the item should be inserted.
		/// </summary>
		[Pure]
		public int BinarySearch(T item) => this.Read().BinarySearch(item);

		/// <summary>
		/// Copy the contents of the list into a new array.
		/// </summary>
		[Pure]
		public T[] ToArray() => this.Read().ToArray();

		/// <summary>
		/// Attempt to copy the contents of the list into an existing
		/// <see cref="Span{T}"/>. If the <paramref name="destination"/> is too short,
		/// no items are copied and the method returns <see langword="false"/>.
		/// </summary>
		public bool TryCopyTo(Span<T> destination) => this.Read().TryCopyTo(destination);

		/// <summary>
		/// Copy the contents of the list into an existing <see cref="Span{T}"/>.
		/// </summary>
		/// <exception cref="ArgumentException">
		///   <paramref name="destination"/> is shorter than the source list.
		/// </exception>
		public void CopyTo(Span<T> destination) => this.Read().CopyTo(destination);

		/// <summary>
		/// Create a new heap-allocated live view of the builder.
		/// </summary>
		/// <remarks>
		/// This method is an <c>O(1)</c> operation and allocates a new fixed-size
		/// collection instance. The items are not copied. Changes made to the
		/// builder are visible in the collection and vice versa.
		/// </remarks>
		public Collection AsCollection() => new Collection(this);

#pragma warning disable CA1034 // Nested types should not be visible
		/// <summary>
		/// A heap-allocated live view of a builder. Changes made to the
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
					ThrowHelpers.ThrowArgumentNullException(ThrowHelpers.Argument.array);
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
					return EnumeratorLike.AsIEnumerator<T, Enumerator>(this.builder.GetEnumerator());
				}
			}

			/// <inheritdoc/>
			IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable<T>)this).GetEnumerator();

			/// <inheritdoc/>
			int IList<T>.IndexOf(T item) => this.builder.IndexOf(item);

			/// <inheritdoc/>
			void IList<T>.Insert(int index, T item) => this.builder.Insert(index, item);

			/// <inheritdoc/>
			bool ICollection<T>.Remove(T item) => this.builder.TryRemove(item);

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
		public Enumerator GetEnumerator() => new Enumerator(this.Read());

		/// <summary>
		/// Enumerator for <see cref="Builder"/>.
		/// </summary>
#pragma warning disable CA1034 // Nested types should not be visible
#pragma warning disable CA1815 // Override equals and operator equals on value types
		[StructLayout(LayoutKind.Auto)]
		public struct Enumerator : IEnumeratorLike<T>
		{
			private readonly ValueList<T> list;
			private readonly int expectedState;
			private int current;

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			internal Enumerator(ValueList<T> list)
			{
				this.list = list;
				this.expectedState = list.state;
				this.current = -1;
			}

			/// <inheritdoc/>
			public readonly T Current
			{
				[MethodImpl(MethodImplOptions.AggressiveInlining)]
				get => this.list.items![this.current];
			}

			/// <inheritdoc/>
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public bool MoveNext()
			{
				if (this.expectedState != this.list.state)
				{
					this.MoveNextSlow();
				}

				return (uint)++this.current < this.list.size;
			}

			private void MoveNextSlow()
			{
				// The only valid reason for ending up here is when the enumerator
				// was obtained in an already-built state and the hash code was
				// materialized during enumeration.
				if (BuilderState.IsImmutable(this.expectedState))
				{
					Debug.Assert(BuilderState.IsImmutable(this.list.state));

					return;
				}

				ThrowHelpers.ThrowInvalidOperationException_CollectionModifiedDuringEnumeration();
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
		[Pure]
		public override int GetHashCode() => RuntimeHelpers.GetHashCode(this.list);

		/// <summary>
		/// Returns <see langword="true"/> when the two builders refer to the same allocation.
		/// </summary>
		[Pure]
		public bool Equals(Builder other) => object.ReferenceEquals(this.list, other.list);

#pragma warning disable CS0809 // Obsolete member overrides non-obsolete member
		/// <inheritdoc/>
		[Pure]
		[Obsolete("Avoid boxing. Use == instead.")]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public override bool Equals(object? obj) => obj is Builder builder && obj.Equals(builder);
#pragma warning restore CS0809 // Obsolete member overrides non-obsolete member

		/// <summary>
		/// Check for equality.
		/// </summary>
		[Pure]
		public static bool operator ==(Builder left, Builder right) => left.Equals(right);

		/// <summary>
		/// Check for inequality.
		/// </summary>
		[Pure]
		public static bool operator !=(Builder left, Builder right) => !left.Equals(right);
	}
}
