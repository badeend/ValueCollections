using System.Collections;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
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
		public bool IsReadOnly => this.list is null || BuilderState.IsImmutable(this.list.state);

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
			this.MutateOnce();

			Debug.Assert(this.list is not null);

			this.list!.state = BuilderState.InitialImmutable;

			return this.list;
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
			var list = this.list;
			if (list is null)
			{
				return Empty;
			}

			if (BuilderState.IsImmutable(list.state))
			{
				return list;
			}
			else
			{
				return ValueList<T>.CreateImmutableUnsafe(new(ref list.inner));
			}
		}

		/// <summary>
		/// Copy the current contents of the builder into a new <see cref="ValueList{T}.Builder"/>.
		/// </summary>
		[Pure]
		public Builder ToValueListBuilder()
		{
			return ValueList<T>.Builder.CreateUnsafe(new(in this.ReadOnce()));
		}

		[StructLayout(LayoutKind.Auto)]
		private readonly struct ReadGuard
		{
			private readonly ValueList<T> list;
			private readonly int expectedState;

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			internal ReadGuard(ValueList<T> list, int expectedState)
			{
				this.list = list;
				this.expectedState = expectedState;
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			internal ref readonly RawList<T> AssertAlive()
			{
				if (this.expectedState != this.list.state)
				{
					this.AssertAliveUncommon();
				}

				return ref this.list.inner;
			}

			private void AssertAliveUncommon()
			{
				// The only valid reason for ending up here is when the snapshot
				// was obtained in an already-built state and the hash code was
				// materialized afterwards.
				if (BuilderState.IsImmutable(this.expectedState))
				{
					Debug.Assert(BuilderState.IsImmutable(this.list.state));

					return;
				}

				if (this.list.state == BuilderState.ExclusiveMode)
				{
					ThrowHelpers.ThrowInvalidOperationException_Locked();
				}
				else
				{
					ThrowHelpers.ThrowInvalidOperationException_CollectionModifiedDuringEnumeration();
				}
			}
		}

		[StructLayout(LayoutKind.Auto)]
		private readonly ref struct MutationGuard
		{
			private readonly ValueList<T> list;
			private readonly int restoreState;

			internal readonly ref RawList<T> Inner
			{
				[MethodImpl(MethodImplOptions.AggressiveInlining)]
				get
				{
					Debug.Assert(this.list.state == BuilderState.ExclusiveMode);

					return ref this.list.inner;
				}
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			internal MutationGuard(ValueList<T> list, int restoreState)
			{
				this.list = list;
				this.restoreState = restoreState;
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public void Dispose()
			{
				Debug.Assert(this.list.state == BuilderState.ExclusiveMode);

				this.list.state = this.restoreState;
			}
		}

		private ReadGuard Read()
		{
			var list = this.list ?? Empty;

			if (list.state == BuilderState.ExclusiveMode)
			{
				ThrowHelpers.ThrowInvalidOperationException_Locked();
			}

			return new ReadGuard(list, list.state);
		}

		private ref readonly RawList<T> ReadOnce()
		{
			var list = this.list ?? Empty;

			if (list.state == BuilderState.ExclusiveMode)
			{
				ThrowHelpers.ThrowInvalidOperationException_Locked();
			}

			return ref list.inner;
		}

		private MutationGuard Mutate()
		{
			var list = this.list;
			if (list is null || (uint)list.state >= BuilderState.LastMutableVersion)
			{
				MutateUncommon(list);
			}

			var stateToRestore = list.state + 1;
			list.state = BuilderState.ExclusiveMode;

			Debug.Assert(BuilderState.IsMutable(stateToRestore));

			return new MutationGuard(list, stateToRestore);
		}

		// Only to be used if the mutation can be done at once (i.e. "atomically"),
		// and the outside world can not observe the builder in a temporary intermediate state.
		private ref RawList<T> MutateOnce()
		{
			var list = this.list;
			if (list is null || (uint)list.state >= BuilderState.LastMutableVersion)
			{
				MutateUncommon(list);
			}

			list.state++;

			Debug.Assert(BuilderState.IsMutable(list.state));

			return ref list.inner;
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private static void MutateUncommon([NotNull] ValueList<T>? list)
		{
			if (list is null)
			{
				ThrowHelpers.ThrowInvalidOperationException_UninitializedBuilder();
			}
			else if (list.state == BuilderState.LastMutableVersion)
			{
				list.state = BuilderState.InitialMutable;
			}
			else if (list.state == BuilderState.ExclusiveMode)
			{
				ThrowHelpers.ThrowInvalidOperationException_Locked();
			}
			else
			{
				Debug.Assert(BuilderState.IsImmutable(list.state));

				ThrowHelpers.ThrowInvalidOperationException_AlreadyBuilt();
			}
		}

		/// <summary>
		/// The total number of elements the internal data structure can hold without resizing.
		/// </summary>
		[Pure]
		public int Capacity => this.ReadOnce().Capacity;

		/// <summary>
		/// Gets or sets the element at the specified <paramref name="index"/>.
		/// </summary>
		public T this[int index]
		{
			[Pure]
			get => this.ReadOnce()[index];
			set => this.MutateOnce()[index] = value;
		}

		/// <summary>
		/// Current length of the list.
		/// </summary>
		[Pure]
		public int Count => this.ReadOnce().Count;

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

		// This takes ownership of the ValueList
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private Builder(ValueList<T> list)
		{
			Debug.Assert(BuilderState.IsMutable(list.state));

			this.list = list;
		}

		// This takes ownership of the RawList
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal static Builder CreateUnsafe(RawList<T> inner) => new(ValueList<T>.CreateMutableUnsafe(inner));

		/// <summary>
		/// Replaces an element at a given position in the list with the specified
		/// element.
		/// </summary>
		public Builder SetItem(int index, T value)
		{
			this.MutateOnce()[index] = value;
			return this;
		}

		/// <summary>
		/// Add an <paramref name="item"/> to the end of the list.
		/// </summary>
		public Builder Add(T item)
		{
			this.MutateOnce().Add(item);
			return this;
		}

		/// <summary>
		/// Add the <paramref name="items"/> to the end of the list.
		/// </summary>
		public Builder AddRange(ValueSlice<T> items) => this.AddRange(items.AsSpan());

		/// <summary>
		/// Add the <paramref name="items"/> to the end of the list.
		/// </summary>
		/// <remarks>
		/// An overload that takes any <c>IEnumerable&lt;T&gt;</c> exists as an
		/// <see cref="ValueCollectionExtensions.AddRange{T}(ValueList{T}.Builder, IEnumerable{T})">extension method</see>.
		/// </remarks>
		public Builder AddRange(ValueList<T> items)
		{
			if (items is null)
			{
				ThrowHelpers.ThrowArgumentNullException(ThrowHelpers.Argument.items);
			}

			this.MutateOnce().AddRange(ref items.inner);
			return this;
		}

		/// <summary>
		/// Add the <paramref name="items"/> to the end of the list.
		/// </summary>
		/// <exception cref="InvalidOperationException">
		/// Can't add builder into itself.
		/// </exception>
		public Builder AddRange(Builder items)
		{
			ref var thisInner = ref this.MutateOnce();
			ref readonly var otherInner = ref items.ReadOnce();

			// This check is also what makes the MutateOnce safe.
			if (thisInner.Equals(in otherInner))
			{
				ThrowHelpers.ThrowInvalidOperationException_CantAddOrInsertIntoSelf();
			}

			thisInner.AddRange(in otherInner);

			return this;
		}

		/// <summary>
		/// Add the <paramref name="items"/> to the end of the list.
		/// </summary>
		public Builder AddRange(params ReadOnlySpan<T> items)
		{
			this.MutateOnce().AddRange(items);
			return this;
		}

		// Accessible through extension method.
		internal Builder AddRangeEnumerable(IEnumerable<T> items)
		{
			if (items is null)
			{
				ThrowHelpers.ThrowArgumentNullException(ThrowHelpers.Argument.items);
			}

			if (items is ValueList<T> valueList)
			{
				return this.AddRange(valueList);
			}

			if (items is Collection collection)
			{
				return this.AddRange(collection.Builder);
			}

			using (var guard = this.Mutate())
			{
				guard.Inner.AddRange(items);
			}

			return this;
		}

		/// <summary>
		/// Insert an <paramref name="item"/> into the list at the specified <paramref name="index"/>.
		/// </summary>
		/// <exception cref="ArgumentOutOfRangeException">
		/// Invalid <paramref name="index"/>.
		/// </exception>
		public Builder Insert(int index, T item)
		{
			this.MutateOnce().Insert(index, item);
			return this;
		}

		/// <summary>
		/// Insert the <paramref name="items"/> into the list at the specified <paramref name="index"/>.
		/// </summary>
		/// <exception cref="ArgumentOutOfRangeException">
		/// Invalid <paramref name="index"/>.
		/// </exception>
		public Builder InsertRange(int index, ValueSlice<T> items) => this.InsertRange(index, items.AsSpan());

		/// <summary>
		/// Insert the <paramref name="items"/> into the list at the specified <paramref name="index"/>.
		/// </summary>
		/// <remarks>
		/// An overload that takes any <c>IEnumerable&lt;T&gt;</c> exists as an
		/// <see cref="ValueCollectionExtensions.InsertRange{T}(ValueList{T}.Builder, int, IEnumerable{T})">extension method</see>.
		/// </remarks>
		/// <exception cref="ArgumentOutOfRangeException">
		/// Invalid <paramref name="index"/>.
		/// </exception>
		public Builder InsertRange(int index, ValueList<T> items)
		{
			if (items is null)
			{
				ThrowHelpers.ThrowArgumentNullException(ThrowHelpers.Argument.items);
			}

			this.MutateOnce().InsertRange(index, ref items.inner);
			return this;
		}

		/// <summary>
		/// Insert the <paramref name="items"/> into the list at the specified <paramref name="index"/>.
		/// </summary>
		/// <exception cref="InvalidOperationException">
		/// Can't insert builder into itself.
		/// </exception>
		/// <exception cref="ArgumentOutOfRangeException">
		/// Invalid <paramref name="index"/>.
		/// </exception>
		public Builder InsertRange(int index, Builder items)
		{
			ref var thisInner = ref this.MutateOnce();
			ref readonly var otherInner = ref items.ReadOnce();

			// This check is also what makes the MutateOnce safe.
			if (thisInner.Equals(in otherInner))
			{
				ThrowHelpers.ThrowInvalidOperationException_CantAddOrInsertIntoSelf();
			}

			thisInner.InsertRange(index, in otherInner);

			return this;
		}

		/// <summary>
		/// Insert the <paramref name="items"/> into the list at the specified <paramref name="index"/>.
		/// </summary>
		/// <exception cref="ArgumentOutOfRangeException">
		/// Invalid <paramref name="index"/>.
		/// </exception>
		public Builder InsertRange(int index, params ReadOnlySpan<T> items)
		{
			this.MutateOnce().InsertRange(index, items);
			return this;
		}

		// Accessible through extension method.
		internal Builder InsertRangeEnumerable(int index, IEnumerable<T> items)
		{
			if (items is null)
			{
				ThrowHelpers.ThrowArgumentNullException(ThrowHelpers.Argument.items);
			}

			if (items is ValueList<T> valueList)
			{
				return this.InsertRange(index, valueList);
			}

			if (items is Collection collection)
			{
				return this.InsertRange(index, collection.Builder);
			}

			using (var guard = this.Mutate())
			{
				guard.Inner.InsertRange(index, items);
			}

			return this;
		}

		/// <summary>
		/// Remove all elements from the list.
		/// </summary>
		public Builder Clear()
		{
			this.MutateOnce().Clear();
			return this;
		}

		/// <summary>
		/// Remove the element at the specified <paramref name="index"/>.
		/// </summary>
		public Builder RemoveAt(int index)
		{
			this.MutateOnce().RemoveAt(index);
			return this;
		}

		/// <summary>
		/// Remove a range of elements from the list.
		/// </summary>
		public Builder RemoveRange(int index, int count)
		{
			this.MutateOnce().RemoveRange(index, count);
			return this;
		}

		/// <summary>
		/// Attempt to remove the first occurrence of a specific object from the list.
		/// Returns <see langword="false"/> when the element wasn't found.
		/// </summary>
		public bool TryRemove(T item)
		{
			return this.MutateOnce().Remove(item);
		}

		/// <summary>
		/// Attempt to remove the first element that matches the predicate.
		/// Returns <see langword="false"/> when the element wasn't found.
		/// </summary>
		public bool TryRemove(Predicate<T> match)
		{
			using (var guard = this.Mutate())
			{
				return guard.Inner.Remove(match);
			}
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
			this.MutateOnce().RemoveAll(item);
			return this;
		}

		/// <summary>
		/// Remove all elements that match the predicate.
		/// </summary>
		public Builder RemoveAll(Predicate<T> match)
		{
			using (var guard = this.Mutate())
			{
				guard.Inner.RemoveAll(match);
			}

			return this;
		}

		/// <summary>
		/// Reverse the order of the elements in the list.
		/// </summary>
		public Builder Reverse()
		{
			this.MutateOnce().Reverse();
			return this;
		}

		/// <summary>
		/// Sort all elements in the list.
		/// </summary>
		public Builder Sort()
		{
			this.MutateOnce().Sort();
			return this;
		}

		/// <summary>
		/// Performs an in-place shuffle of all elements in the list using
		/// a cryptographically secure pseudorandom number generator (CPRNG).
		/// </summary>
		public Builder Shuffle()
		{
			this.MutateOnce().Shuffle();
			return this;
		}

		/// <inheritdoc cref="ValueCollectionsMarshal.AsSpan"/>
		internal Span<T> AsSpanUnsafe()
		{
			return this.MutateOnce().AsSpan();
		}

		/// <inheritdoc cref="ValueCollectionsMarshal.SetCount"/>
		internal void SetCountUnsafe(int count)
		{
			this.MutateOnce().SetCount(count);
		}

		/// <summary>
		/// Set the capacity to the actual number of elements in the list, if that
		/// number is less than a threshold value.
		/// </summary>
		public Builder TrimExcess()
		{
			this.MutateOnce().TrimExcess();
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
			this.MutateOnce().EnsureCapacity(minimumCapacity);
			return this;
		}

		/// <summary>
		/// Returns <see langword="true"/> when the list contains the specified
		/// <paramref name="item"/>.
		/// </summary>
		[Pure]
		public bool Contains(T item) => this.ReadOnce().Contains(item);

		/// <summary>
		/// Return the index of the first occurrence of <paramref name="item"/> in
		/// the list, or <c>-1</c> if not found.
		/// </summary>
		[Pure]
		public int IndexOf(T item) => this.ReadOnce().IndexOf(item);

		/// <summary>
		/// Return the index of the last occurrence of <paramref name="item"/> in
		/// the list, or <c>-1</c> if not found.
		/// </summary>
		[Pure]
		public int LastIndexOf(T item) => this.ReadOnce().LastIndexOf(item);

		/// <summary>
		/// Perform a binary search for <paramref name="item"/> within the list.
		/// The list is assumed to already be sorted. This uses the
		/// <see cref="Comparer{T}.Default">Default</see> comparer and throws if
		/// <typeparamref name="T"/> is not comparable. If the item is found, its
		/// index is returned. Otherwise a negative value is returned representing
		/// the bitwise complement of the index where the item should be inserted.
		/// </summary>
		[Pure]
		public int BinarySearch(T item) => this.ReadOnce().BinarySearch(item);

		/// <summary>
		/// Copy the contents of the list into a new array.
		/// </summary>
		[Pure]
		public T[] ToArray() => this.ReadOnce().ToArray();

		/// <summary>
		/// Attempt to copy the contents of the list into an existing
		/// <see cref="Span{T}"/>. If the <paramref name="destination"/> is too short,
		/// no items are copied and the method returns <see langword="false"/>.
		/// </summary>
		public bool TryCopyTo(Span<T> destination) => this.ReadOnce().TryCopyTo(destination);

		/// <summary>
		/// Copy the contents of the list into an existing <see cref="Span{T}"/>.
		/// </summary>
		/// <exception cref="ArgumentException">
		///   <paramref name="destination"/> is shorter than the source list.
		/// </exception>
		public void CopyTo(Span<T> destination) => this.ReadOnce().CopyTo(destination);

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
		public Enumerator GetEnumerator() => new Enumerator(this);

		/// <summary>
		/// Enumerator for <see cref="Builder"/>.
		/// </summary>
#pragma warning disable CA1034 // Nested types should not be visible
#pragma warning disable CA1815 // Override equals and operator equals on value types
		[StructLayout(LayoutKind.Auto)]
		public struct Enumerator : IEnumeratorLike<T>
		{
			private readonly ReadGuard guard;
			private RawList<T>.Enumerator inner;

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			internal Enumerator(Builder builder)
			{
				var snapshot = builder.Read();
				this.guard = snapshot;
				this.inner = snapshot.AssertAlive().GetEnumerator();
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
				this.guard.AssertAlive();

				return this.inner.MoveNext();
			}
		}
#pragma warning restore CA1815 // Override equals and operator equals on value types
#pragma warning restore CA1034 // Nested types should not be visible

		/// <summary>
		/// Get a string representation of the collection for debugging purposes.
		/// The format is not stable and may change without prior notice.
		/// </summary>
		[Pure]
		public override string ToString() => this.ReadOnce().ToString();

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
