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
	/// To prevent accidental boxing, this type does not implement commonly used
	/// interfaces such as <see cref="IEnumerable{T}"/> and
	/// <see cref="IList{T}"/>. You can still use these interfaces by
	/// manually calling <see cref="AsCollection"/> instead.
	///
	/// The <c>default</c> value is an empty read-only builder.
	/// </remarks>
	[CollectionBuilder(typeof(ValueList), nameof(ValueList.CreateBuilder))]
	public readonly struct Builder : IEquatable<Builder>
	{
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
			var list = this.Mutate();

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
				return ValueList<T>.CreateImmutable(new(ref list.inner));
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
		public int Capacity => this.Read().inner.Capacity;

		/// <summary>
		/// Gets or sets the element at the specified <paramref name="index"/>.
		/// </summary>
		public T this[int index]
		{
			[Pure]
			get => this.Read().inner[index];
			set => this.Mutate().inner[index] = value;
		}

		/// <summary>
		/// Current length of the list.
		/// </summary>
		[Pure]
		public int Count => this.Read().inner.Count;

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
			return new(ValueList<T>.CreateMutable(new()));
		}

		internal static Builder CreateWithCapacity(int minimumCapacity)
		{
			return new(ValueList<T>.CreateMutable(new(minimumCapacity)));
		}

		internal static Builder CreateFromEnumerable(IEnumerable<T> items)
		{
			if (items is null)
			{
				ThrowHelpers.ThrowArgumentNullException(ThrowHelpers.Argument.items);
			}

			return new(ValueList<T>.CreateMutable(new(items)));
		}

		private bool IsSelf(IEnumerable<T> items)
		{
			return items is Builder.Collection collection && collection.Builder.list == this.list;
		}

		/// <summary>
		/// Replaces an element at a given position in the list with the specified
		/// element.
		/// </summary>
		public Builder SetItem(int index, T value)
		{
			this.Mutate().inner[index] = value;
			return this;
		}

		/// <summary>
		/// Add an <paramref name="item"/> to the end of the list.
		/// </summary>
		public Builder Add(T item)
		{
			this.Mutate().inner.Add(item);
			return this;
		}

		// Accessible through an extension method.
		internal Builder AddRangeSpan(ReadOnlySpan<T> items)
		{
			this.Mutate().inner.AddRange(items);
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

			if (this.IsSelf(items))
			{
				list.inner.AddSelf();
			}
			else
			{
				if (!list.inner.TryAddRange(items))
				{
					this.AddRangeSlow(items);
				}
			}

			return this;
		}

		private void AddRangeSlow(IEnumerable<T> items)
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
			this.Mutate().inner.Insert(index, item);
			return this;
		}

		// Accessible through an extension method.
		internal Builder InsertRangeSpan(int index, ReadOnlySpan<T> items)
		{
			this.Mutate().inner.InsertRange(index, items);
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

			if (this.IsSelf(items))
			{
				list.inner.InsertSelf(index);
			}
			else
			{
				if (!list.inner.TryInsertRange(index, items))
				{
					this.InsertRangeSlow(index, items);
				}
			}

			return this;
		}

		private void InsertRangeSlow(int index, IEnumerable<T> items)
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
			this.Mutate().inner.Clear();
			return this;
		}

		/// <summary>
		/// Remove the element at the specified <paramref name="index"/>.
		/// </summary>
		public Builder RemoveAt(int index)
		{
			this.Mutate().inner.RemoveAt(index);
			return this;
		}

		/// <summary>
		/// Remove a range of elements from the list.
		/// </summary>
		public Builder RemoveRange(int index, int count)
		{
			this.Mutate().inner.RemoveRange(index, count);
			return this;
		}

		/// <summary>
		/// Attempt to remove the first occurrence of a specific object from the list.
		/// Returns <see langword="false"/> when the element wasn't found.
		/// </summary>
		public bool TryRemove(T item)
		{
			return this.Mutate().inner.Remove(item);
		}

		/// <summary>
		/// Attempt to remove the first element that matches the predicate.
		/// Returns <see langword="false"/> when the element wasn't found.
		/// </summary>
		public bool TryRemove(Predicate<T> match)
		{
			return this.Mutate().inner.Remove(match);
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
			this.Mutate().inner.RemoveAll(item);
			return this;
		}

		/// <summary>
		/// Remove all elements that match the predicate.
		/// </summary>
		public Builder RemoveAll(Predicate<T> match)
		{
			this.Mutate().inner.RemoveAll(match);
			return this;
		}

		/// <summary>
		/// Reverse the order of the elements in the list.
		/// </summary>
		public Builder Reverse()
		{
			this.Mutate().inner.Reverse();
			return this;
		}

		/// <summary>
		/// Sort all elements in the list.
		/// </summary>
		public Builder Sort()
		{
			this.Mutate().inner.Sort();
			return this;
		}

		/// <summary>
		/// Performs an in-place shuffle of all elements in the list using
		/// a cryptographically secure pseudorandom number generator (CPRNG).
		/// </summary>
		public Builder Shuffle()
		{
			this.Mutate().inner.Shuffle();
			return this;
		}

		/// <inheritdoc cref="ValueCollectionsMarshal.AsSpan"/>
		internal Span<T> AsSpanUnsafe()
		{
			return this.Mutate().inner.AsSpan();
		}

		/// <inheritdoc cref="ValueCollectionsMarshal.SetCount"/>
		internal void SetCountUnsafe(int count)
		{
			this.Mutate().inner.SetCount(count);
		}

		/// <summary>
		/// Set the capacity to the actual number of elements in the list, if that
		/// number is less than a threshold value.
		/// </summary>
		public Builder TrimExcess()
		{
			this.Mutate().inner.TrimExcess();
			return this;
		}

		/// <summary>
		/// Ensures that the capacity of this list is at least the specified capacity.
		/// If the current capacity is less than capacity, it is increased to at
		/// least the specified capacity.
		/// </summary>
		/// <exception cref="ArgumentOutOfRangeException">
		/// <paramref name="minimumCapacity"/> is less than 0.
		/// </exception>
		public Builder EnsureCapacity(int minimumCapacity)
		{
			this.Mutate().inner.EnsureCapacity(minimumCapacity);
			return this;
		}

		/// <summary>
		/// Returns <see langword="true"/> when the list contains the specified
		/// <paramref name="item"/>.
		/// </summary>
		[Pure]
		public bool Contains(T item) => this.Read().inner.Contains(item);

		/// <summary>
		/// Return the index of the first occurrence of <paramref name="item"/> in
		/// the list, or <c>-1</c> if not found.
		/// </summary>
		[Pure]
		public int IndexOf(T item) => this.Read().inner.IndexOf(item);

		/// <summary>
		/// Return the index of the last occurrence of <paramref name="item"/> in
		/// the list, or <c>-1</c> if not found.
		/// </summary>
		[Pure]
		public int LastIndexOf(T item) => this.Read().inner.LastIndexOf(item);

		/// <summary>
		/// Perform a binary search for <paramref name="item"/> within the list.
		/// The list is assumed to already be sorted. This uses the
		/// <see cref="Comparer{T}.Default">Default</see> comparer and throws if
		/// <typeparamref name="T"/> is not comparable. If the item is found, its
		/// index is returned. Otherwise a negative value is returned representing
		/// the bitwise complement of the index where the item should be inserted.
		/// </summary>
		[Pure]
		public int BinarySearch(T item) => this.Read().inner.BinarySearch(item);

		/// <summary>
		/// Copy the contents of the list into a new array.
		/// </summary>
		[Pure]
		public T[] ToArray() => this.Read().inner.ToArray();

		/// <summary>
		/// Attempt to copy the contents of the list into an existing
		/// <see cref="Span{T}"/>. If the <paramref name="destination"/> is too short,
		/// no items are copied and the method returns <see langword="false"/>.
		/// </summary>
		public bool TryCopyTo(Span<T> destination) => this.Read().inner.TryCopyTo(destination);

		/// <summary>
		/// Copy the contents of the list into an existing <see cref="Span{T}"/>.
		/// </summary>
		/// <exception cref="ArgumentException">
		///   <paramref name="destination"/> is shorter than the source list.
		/// </exception>
		public void CopyTo(Span<T> destination) => this.Read().inner.CopyTo(destination);

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
			private RawList<T>.Enumerator inner;

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			internal Enumerator(ValueList<T> list)
			{
				this.list = list;
				this.expectedState = list.state;
				this.inner = list.inner.GetEnumerator();
			}

			/// <inheritdoc/>
			public readonly T Current
			{
				[MethodImpl(MethodImplOptions.AggressiveInlining)]
				get => this.inner.Current;
			}

			/// <inheritdoc/>
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public bool MoveNext()
			{
				if (this.expectedState != this.list.state)
				{
					this.MoveNextSlow();
				}

				return this.inner.MoveNext();
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
		public override string ToString() => this.Read().inner.ToString();

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
