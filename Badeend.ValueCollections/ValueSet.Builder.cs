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
public sealed partial class ValueSet<T>
{
	/// <summary>
	/// A mutable set that can be used to efficiently construct new immutable sets.
	/// </summary>
	/// <remarks>
	/// Most mutating methods on this class return `this`, allowing the caller to
	/// chain multiple mutations in a row. The boolean-returning
	/// <see cref="HashSet{T}.Add(T)">HashSet.Add</see> and
	/// <see cref="HashSet{T}.Remove(T)">HashSet.Remove</see> are implemented as
	/// <see cref="TryAdd(T)"/> and <see cref="TryRemove(T)"/>.
	///
	/// When you're done building, call <see cref="Build()"/> to extract the
	/// resulting set.
	///
	/// For constructing <see cref="ValueSet{T}"/>s it is recommended to use this
	/// type over e.g. <see cref="HashSet{T}"/>. This type can avoiding unnecessary
	/// copying by taking advantage of the immutability of its results. Whereas
	/// calling <c>.ToValueSet()</c> on a regular <see cref="HashSet{T}"/>
	/// <em>always</em> performs a full copy.
	///
	/// The order in which the elements are enumerated is undefined.
	///
	/// To prevent accidental boxing, this type does not implement commonly used
	/// interfaces such as <see cref="IEnumerable{T}"/> and
	/// <see cref="ISet{T}"/>. You can still use these interfaces by
	/// manually calling <see cref="AsCollection"/> instead.
	///
	/// Unlike the resulting ValueSet, its Builder is <em>not</em> thread-safe.
	///
	/// The <c>default</c> value is an empty read-only builder.
	/// </remarks>
	[DebuggerDisplay("Count = {Count}, Capacity = {Capacity}, IsReadOnly = {IsReadOnly}")]
	[DebuggerTypeProxy(typeof(ValueSet<>.Builder.DebugView))]
	[CollectionBuilder(typeof(ValueSet), nameof(ValueSet.CreateBuilder))]
	public readonly struct Builder : IEquatable<Builder>
	{
		/// <summary>
		/// Only access this field through .Read() or .Mutate().
		/// </summary>
		private readonly ValueSet<T>? set;

		/// <summary>
		/// Returns <see langword="true"/> when this instance has been built and is
		/// now read-only.
		/// </summary>
		[Pure]
		public bool IsReadOnly => this.set is null || BuilderState.IsImmutable(this.set.state);

		/// <summary>
		/// Finalize the builder and export its contents as a <see cref="ValueSet{T}"/>.
		/// This makes the builder read-only. Any future attempt to mutate the
		/// builder will throw.
		///
		/// This is an <c>O(1)</c> operation and performs no heap allocations.
		/// </summary>
		/// <remarks>
		/// If you need an intermediate snapshot of the contents while keeping the
		/// builder open for mutation, use <see cref="ToValueSet"/> instead.
		/// </remarks>
		/// <exception cref="InvalidOperationException">
		/// This instance has already been built.
		/// </exception>
		public ValueSet<T> Build()
		{
			var set = this.set;
			if (set is null || BuilderState.BuildRequiresAttention(set.state))
			{
				MutateUncommon(set);
			}

			set.state = BuilderState.InitialImmutable;

			return set.IsEmpty ? Empty : set;
		}

		/// <summary>
		/// Copy the current contents of the builder into a new <see cref="ValueSet{T}"/>.
		/// </summary>
		/// <remarks>
		/// If you don't need the builder anymore after this method, consider using
		/// <see cref="Build"/> instead.
		/// </remarks>
		[Pure]
		public ValueSet<T> ToValueSet()
		{
			var set = this.set;
			if (set is null)
			{
				return Empty;
			}

			if (BuilderState.IsImmutable(set.state))
			{
				return set.IsEmpty ? Empty : set;
			}
			else if (set.state == BuilderState.Cow)
			{
				return ValueSet<T>.CreateImmutableUnsafe(set.inner);
			}
			else
			{
				return ValueSet<T>.CreateImmutableUnsafe(new(ref set.inner));
			}
		}

		/// <summary>
		/// Copy the current contents of the builder into a new <see cref="ValueSet{T}.Builder"/>.
		/// </summary>
		[Pure]
		public Builder ToValueSetBuilder()
		{
			return ValueSet<T>.Builder.CreateUnsafe(new(in this.ReadOnce()));
		}

		[StructLayout(LayoutKind.Auto)]
		private readonly struct Snapshot
		{
			private readonly ValueSet<T> set;
			private readonly int expectedState;

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			internal Snapshot(ValueSet<T> set, int expectedState)
			{
				this.set = set;
				this.expectedState = expectedState;
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			internal ref readonly RawSet<T> AssertAlive()
			{
				if (this.expectedState != this.set.state)
				{
					this.AssertAliveUncommon();
				}

				return ref this.set.inner;
			}

			private void AssertAliveUncommon()
			{
				// The only valid reason for ending up here is when the snapshot
				// was obtained in an already-built state and the hash code was
				// materialized afterwards.
				if (BuilderState.IsImmutable(this.expectedState))
				{
					Polyfills.DebugAssert(BuilderState.IsImmutable(this.set.state));

					return;
				}

				if (this.set.state == BuilderState.ExclusiveMode)
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
			private readonly ValueSet<T> set;
			private readonly int restoreState;

			internal readonly ref RawSet<T> Inner
			{
				[MethodImpl(MethodImplOptions.AggressiveInlining)]
				get
				{
					Polyfills.DebugAssert(this.set.state == BuilderState.ExclusiveMode);

					return ref this.set.inner;
				}
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			internal MutationGuard(ValueSet<T> set, int restoreState)
			{
				this.set = set;
				this.restoreState = restoreState;
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public void Dispose()
			{
				Polyfills.DebugAssert(this.set.state == BuilderState.ExclusiveMode);

				this.set.state = this.restoreState;
			}
		}

		private Snapshot Read()
		{
			var set = this.set ?? Empty;

			if (set.state == BuilderState.ExclusiveMode)
			{
				ThrowHelpers.ThrowInvalidOperationException_Locked();
			}

			return new Snapshot(set, set.state);
		}

		private ref readonly RawSet<T> ReadOnce()
		{
			var set = this.set ?? Empty;

			if (set.state == BuilderState.ExclusiveMode)
			{
				ThrowHelpers.ThrowInvalidOperationException_Locked();
			}

			return ref set.inner;
		}

		private MutationGuard Mutate()
		{
			var set = this.set;
			if (set is null || BuilderState.MutateRequiresAttention(set.state))
			{
				MutateUncommon(set);
			}

			var stateToRestore = set.state + 1;
			set.state = BuilderState.ExclusiveMode;

			Polyfills.DebugAssert(BuilderState.IsMutable(stateToRestore));

			return new MutationGuard(set, stateToRestore);
		}

		// Only to be used if the mutation can be done at once (i.e. "atomically"),
		// and the outside world can not observe the builder in a temporary intermediate state.
		private ref RawSet<T> MutateOnce()
		{
			var set = this.set;
			if (set is null || BuilderState.MutateRequiresAttention(set.state))
			{
				MutateUncommon(set);
			}

			set.state++;

			Polyfills.DebugAssert(BuilderState.IsMutable(set.state));

			return ref set.inner;
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private static void MutateUncommon([NotNull] ValueSet<T>? set)
		{
			if (set is null)
			{
				ThrowHelpers.ThrowInvalidOperationException_UninitializedBuilder();
			}
			else if (set.state == BuilderState.Cow)
			{
				// Make copy with at least the same amount of capacity.
				var copy = new RawSet<T>(set.inner.Capacity);
				copy.UnionWith(ref set.inner);

				set.inner = copy;
				set.state = BuilderState.InitialMutable;
			}
			else if (set.state == BuilderState.LastMutableVersion)
			{
				set.state = BuilderState.InitialMutable;
			}
			else if (set.state == BuilderState.ExclusiveMode)
			{
				ThrowHelpers.ThrowInvalidOperationException_Locked();
			}
			else
			{
				Polyfills.DebugAssert(BuilderState.IsImmutable(set.state));

				ThrowHelpers.ThrowInvalidOperationException_AlreadyBuilt();
			}
		}

		/// <summary>
		/// Current size of the set.
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
		/// The total number of elements the internal data structure can hold without resizing.
		/// </summary>
		[Pure]
		public int Capacity => this.ReadOnce().Capacity;

		/// <summary>
		/// Create a new uninitialized builder.
		///
		/// An uninitialized builder behaves the same as an already built set
		/// with 0 items and 0 capacity. Reading from it will succeed, but
		/// mutating it will throw.
		///
		/// This is the same as the <c>default</c> value.
		/// </summary>
		[Pure]
		[Obsolete("This creates an uninitialized builder. Use ValueSet.CreateBuilder<T>() instead.")]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public Builder()
		{
		}

		// This takes ownership of the ValueSet
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private Builder(ValueSet<T> set)
		{
			Polyfills.DebugAssert(BuilderState.IsMutable(set.state));

			this.set = set;
		}

		// This takes ownership of the RawSet
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal static Builder CreateUnsafe(RawSet<T> inner) => new(ValueSet<T>.CreateMutableUnsafe(inner));

		// The RawSet is expected to be immutable.
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal static Builder CreateCowUnsafe(RawSet<T> inner) => new(ValueSet<T>.CreateCowUnsafe(inner));

		/// <summary>
		/// Attempt to add the <paramref name="item"/> to the set.
		/// Returns <see langword="false"/> when the element was
		/// already present.
		/// </summary>
		/// <remarks>
		/// This is the equivalent of <see cref="HashSet{T}.Add(T)">HashSet.Add</see>.
		/// </remarks>
		public bool TryAdd(T item) => this.MutateOnce().Add(item);

		/// <summary>
		/// Add the <paramref name="item"/> to the set if it isn't already present.
		/// </summary>
		/// <remarks>
		/// Use <see cref="UnionWith(ValueSet{T})"/> to add multiple values at once.
		/// Use <see cref="TryAdd"/> if you want to know whether the element was
		/// actually added.
		/// </remarks>
		public Builder Add(T item)
		{
			this.TryAdd(item);
			return this;
		}

		/// <summary>
		/// Remove all elements from the set.
		/// </summary>
		/// <remarks>
		/// The capacity remains unchanged until a call to <see cref="TrimExcess()"/> is made.
		/// </remarks>
		public Builder Clear()
		{
			this.MutateOnce().Clear();
			return this;
		}

		/// <summary>
		/// Attempt to remove a specific element from the set.
		/// Returns <see langword="false"/> when the element was not found.
		/// </summary>
		/// <remarks>
		/// This is the equivalent of <see cref="HashSet{T}.Remove(T)">HashSet.Remove</see>.
		/// </remarks>
		public bool TryRemove(T item) => this.MutateOnce().Remove(item);

		/// <summary>
		/// Remove a specific element from the set if it exists.
		/// </summary>
		/// <remarks>
		/// Use <see cref="ExceptWith(ValueSet{T})"/> to remove multiple values at once.
		/// Use <see cref="TryRemove"/> if you want to know whether any element was
		/// actually removed.
		/// </remarks>
		public Builder Remove(T item)
		{
			this.TryRemove(item);
			return this;
		}

		/// <summary>
		/// Remove all elements that match the predicate.
		/// </summary>
		public Builder RemoveWhere(Predicate<T> match)
		{
			using (var guard = this.Mutate())
			{
				guard.Inner.RemoveWhere(match);
			}

			return this;
		}

		/// <summary>
		/// Reduce the capacity of this set as much as possible. After calling this
		/// method, the <c>Capacity</c> of the set may still be higher than
		/// the <see cref="Count"/>.
		/// </summary>
		/// <remarks>
		/// This method can be used to minimize the memory overhead of long-lived
		/// sets. This method is most useful just before calling
		/// <see cref="Build"/>, e.g.:
		/// <code>
		/// var longLivedSet = builder.TrimExcess().Build()
		/// </code>
		/// Excessive use of this method most likely introduces more performance
		/// problems than it solves.
		/// </remarks>
		public Builder TrimExcess()
		{
			this.MutateOnce().TrimExcess();
			return this;
		}

		/// <summary>
		/// Reduce the capacity of the set to roughly the specified value. If the
		/// current capacity is already smaller than the requested capacity, this
		/// method does nothing. The specified <paramref name="targetCapacity"/> is only
		/// a hint. After this method returns, the <see cref="Capacity"/> may be
		/// rounded up to a nearby, implementation-specific value.
		/// </summary>
		/// <exception cref="ArgumentOutOfRangeException">
		/// <paramref name="targetCapacity"/> is less than <see cref="Count"/>.
		/// </exception>
		public Builder TrimExcess(int targetCapacity)
		{
			this.MutateOnce().TrimExcess(targetCapacity);
			return this;
		}

		/// <summary>
		/// Ensures that the capacity of this set is at least the specified capacity.
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
		/// Returns <see langword="true"/> when the set contains the specified
		/// <paramref name="item"/>.
		/// </summary>
		[Pure]
		public bool Contains(T item) => this.ReadOnce().Contains(item);

		/// <summary>
		/// Copy the contents of the set into an existing <see cref="Span{T}"/>.
		/// </summary>
		/// <exception cref="ArgumentException">
		///   <paramref name="destination"/> is shorter than the source slice.
		/// </exception>
		/// <remarks>
		/// The order in which the elements are copied is undefined.
		/// </remarks>
		public void CopyTo(Span<T> destination) => this.ReadOnce().CopyTo(destination);

		/// <summary>
		/// Check whether <c>this</c> set is a subset of the provided collection.
		/// </summary>
		/// <remarks>
		/// This is an <c>O(n)</c> operation, where <c>n</c> is the number of elements in <c>this</c>.
		///
		/// An overload that takes any <c>IEnumerable&lt;T&gt;</c> exists as an
		/// <see cref="ValueCollectionExtensions.IsSubsetOf{T}(ValueSet{T}.Builder, IEnumerable{T})">extension method</see>.
		/// Beware of the performance implications though.
		/// </remarks>
		public bool IsSubsetOf(ValueSet<T> other)
		{
			if (other is null)
			{
				ThrowHelpers.ThrowArgumentNullException(ThrowHelpers.Argument.other);
			}

			return this.ReadOnce().IsSubsetOf(ref other.inner);
		}

		// Accessible through extension method.
		internal bool IsSubsetOfEnumerable(IEnumerable<T> other)
		{
			if (other is null)
			{
				ThrowHelpers.ThrowArgumentNullException(ThrowHelpers.Argument.other);
			}

			if (other is ValueSet<T> valueSet)
			{
				return this.ReadOnce().IsSubsetOf(ref valueSet.inner);
			}

			var snapshot = this.Read();

			ref readonly var inner = ref snapshot.AssertAlive();

			if (inner.TryIsSubsetOfNonEnumerated(other, out var result))
			{
				return result;
			}

			using var marker = new RawSet<T>.Marker(in inner);

			// Note that enumerating `other` might trigger mutations on `this`.
			foreach (var item in other)
			{
				snapshot.AssertAlive();

				marker.Mark(item);
			}

			return marker.UnmarkedCount == 0;
		}

		/// <summary>
		/// Check whether <c>this</c> set is a proper subset of the provided collection.
		/// </summary>
		/// <remarks>
		/// This is an <c>O(n)</c> operation, where <c>n</c> is the number of elements in <c>this</c>.
		///
		/// An overload that takes any <c>IEnumerable&lt;T&gt;</c> exists as an
		/// <see cref="ValueCollectionExtensions.IsProperSubsetOf{T}(ValueSet{T}.Builder, IEnumerable{T})">extension method</see>.
		/// Beware of the performance implications though.
		/// </remarks>
		public bool IsProperSubsetOf(ValueSet<T> other)
		{
			if (other is null)
			{
				ThrowHelpers.ThrowArgumentNullException(ThrowHelpers.Argument.other);
			}

			return this.ReadOnce().IsProperSubsetOf(ref other.inner);
		}

		// Accessible through extension method.
		internal bool IsProperSubsetOfEnumerable(IEnumerable<T> other)
		{
			if (other is null)
			{
				ThrowHelpers.ThrowArgumentNullException(ThrowHelpers.Argument.other);
			}

			if (other is ValueSet<T> valueSet)
			{
				return this.ReadOnce().IsProperSubsetOf(ref valueSet.inner);
			}

			var snapshot = this.Read();

			ref readonly var inner = ref snapshot.AssertAlive();

			if (inner.TryIsProperSubsetOfNonEnumerated(other, out var result))
			{
				return result;
			}

			using var marker = new RawSet<T>.Marker(in inner);

			var otherHasAdditionalItems = false;

			// Note that enumerating `other` might trigger mutations on `this`.
			foreach (var item in other)
			{
				snapshot.AssertAlive();

				if (!marker.Mark(item))
				{
					otherHasAdditionalItems = true;
				}
			}

			return marker.UnmarkedCount == 0 && otherHasAdditionalItems;
		}

		/// <summary>
		/// Check whether <c>this</c> set is a superset of the provided collection.
		/// </summary>
		/// <remarks>
		/// This is an <c>O(n)</c> operation, where <c>n</c> is the number of elements in <paramref name="other"/>.
		///
		/// An overload that takes any <c>IEnumerable&lt;T&gt;</c> exists as an
		/// <see cref="ValueCollectionExtensions.IsSupersetOf{T}(ValueSet{T}.Builder, IEnumerable{T})">extension method</see>.
		/// </remarks>
		public bool IsSupersetOf(ValueSet<T> other)
		{
			if (other is null)
			{
				ThrowHelpers.ThrowArgumentNullException(ThrowHelpers.Argument.other);
			}

			return this.ReadOnce().IsSupersetOf(ref other.inner);
		}

		/// <summary>
		/// Check whether <c>this</c> set is a superset of the provided sequence.
		/// </summary>
		/// <remarks>
		/// This is an <c>O(n)</c> operation, where <c>n</c> is the number of elements in <paramref name="other"/>.
		/// </remarks>
		public bool IsSupersetOf(scoped ReadOnlySpan<T> other) => this.ReadOnce().IsSupersetOf(other);

		// Accessible through extension method.
		internal bool IsSupersetOfEnumerable(IEnumerable<T> other)
		{
			if (other is null)
			{
				ThrowHelpers.ThrowArgumentNullException(ThrowHelpers.Argument.other);
			}

			if (other is ValueSet<T> valueSet)
			{
				return this.ReadOnce().IsSupersetOf(ref valueSet.inner);
			}

			var snapshot = this.Read();

			ref readonly var inner = ref snapshot.AssertAlive();

			if (inner.TryIsSupersetOfNonEnumerated(other, out var result))
			{
				return result;
			}

			// Note that enumerating `other` might trigger mutations on `this`.
			foreach (T element in other)
			{
				snapshot.AssertAlive();

				if (!inner.Contains(element))
				{
					return false;
				}
			}

			return true;
		}

		/// <summary>
		/// Check whether <c>this</c> set is a proper superset of the provided collection.
		/// </summary>
		/// <remarks>
		/// This is an <c>O(n)</c> operation, where <c>n</c> is the number of elements in <c>this</c>.
		///
		/// An overload that takes any <c>IEnumerable&lt;T&gt;</c> exists as an
		/// <see cref="ValueCollectionExtensions.IsProperSupersetOf{T}(ValueSet{T}.Builder, IEnumerable{T})">extension method</see>.
		/// Beware of the performance implications though.
		/// </remarks>
		public bool IsProperSupersetOf(ValueSet<T> other)
		{
			if (other is null)
			{
				ThrowHelpers.ThrowArgumentNullException(ThrowHelpers.Argument.other);
			}

			return this.ReadOnce().IsProperSupersetOf(ref other.inner);
		}

		// Accessible through extension method.
		internal bool IsProperSupersetOfEnumerable(IEnumerable<T> other)
		{
			if (other is null)
			{
				ThrowHelpers.ThrowArgumentNullException(ThrowHelpers.Argument.other);
			}

			if (other is ValueSet<T> valueSet)
			{
				return this.ReadOnce().IsProperSupersetOf(ref valueSet.inner);
			}

			var snapshot = this.Read();

			ref readonly var inner = ref snapshot.AssertAlive();

			if (inner.TryIsProperSupersetOfNonEnumerated(other, out var result))
			{
				return result;
			}

			using var marker = new RawSet<T>.Marker(in inner);

			// Note that enumerating `other` might trigger mutations on `this`.
			foreach (var item in other)
			{
				snapshot.AssertAlive();

				if (!marker.Mark(item))
				{
					return false;
				}
			}

			return marker.UnmarkedCount > 0;
		}

		/// <summary>
		/// Check whether <c>this</c> set and the provided collection share any common elements.
		/// </summary>
		/// <remarks>
		/// This is an <c>O(n)</c> operation, where <c>n</c> is the number of elements in <paramref name="other"/>.
		///
		/// An overload that takes any <c>IEnumerable&lt;T&gt;</c> exists as an
		/// <see cref="ValueCollectionExtensions.Overlaps{T}(ValueSet{T}.Builder, IEnumerable{T})">extension method</see>.
		/// </remarks>
		public bool Overlaps(ValueSet<T> other)
		{
			if (other is null)
			{
				ThrowHelpers.ThrowArgumentNullException(ThrowHelpers.Argument.other);
			}

			return this.ReadOnce().Overlaps(ref other.inner);
		}

		/// <summary>
		/// Check whether <c>this</c> set and the provided collection share any common elements.
		/// </summary>
		/// <remarks>
		/// This is an <c>O(n)</c> operation, where <c>n</c> is the number of elements in <paramref name="other"/>.
		/// </remarks>
		public bool Overlaps(scoped ReadOnlySpan<T> other) => this.ReadOnce().Overlaps(other);

		// Accessible through extension method.
		internal bool OverlapsEnumerable(IEnumerable<T> other)
		{
			if (other is null)
			{
				ThrowHelpers.ThrowArgumentNullException(ThrowHelpers.Argument.other);
			}

			if (other is ValueSet<T> valueSet)
			{
				return this.ReadOnce().Overlaps(ref valueSet.inner);
			}

			var snapshot = this.Read();

			ref readonly var inner = ref snapshot.AssertAlive();

			if (inner.TryOverlapsNonEnumerated(other, out var result))
			{
				return result;
			}

			// Note that enumerating `other` might trigger mutations on `this`.
			foreach (T element in other)
			{
				snapshot.AssertAlive();

				if (inner.Contains(element))
				{
					return true;
				}
			}

			return false;
		}

		/// <summary>
		/// Check whether <c>this</c> set and the provided collection contain
		/// the same elements, ignoring duplicates and the order of the elements.
		/// </summary>
		/// <remarks>
		/// This is an <c>O(n)</c> operation, where <c>n</c> is the number of elements in <paramref name="other"/>.
		///
		/// An overload that takes any <c>IEnumerable&lt;T&gt;</c> exists as an
		/// <see cref="ValueCollectionExtensions.SetEquals{T}(ValueSet{T}.Builder, IEnumerable{T})">extension method</see>.
		/// Beware of the performance implications though.
		/// </remarks>
		public bool SetEquals(ValueSet<T> other)
		{
			if (other is null)
			{
				ThrowHelpers.ThrowArgumentNullException(ThrowHelpers.Argument.other);
			}

			return this.ReadOnce().SetEquals(ref other.inner);
		}

		// Accessible through extension method.
		internal bool SetEqualsEnumerable(IEnumerable<T> other)
		{
			if (other is null)
			{
				ThrowHelpers.ThrowArgumentNullException(ThrowHelpers.Argument.other);
			}

			if (other is ValueSet<T> valueSet)
			{
				return this.ReadOnce().SetEquals(ref valueSet.inner);
			}

			var snapshot = this.Read();

			ref readonly var inner = ref snapshot.AssertAlive();

			if (inner.TrySetEqualsNonEnumerated(other, out var result))
			{
				return result;
			}

			using var marker = new RawSet<T>.Marker(in inner);

			// Note that enumerating `other` might trigger mutations on `this`.
			foreach (var item in other)
			{
				snapshot.AssertAlive();

				if (!marker.Mark(item))
				{
					return false;
				}
			}

			return marker.UnmarkedCount == 0;
		}

		/// <summary>
		/// Remove all elements that appear in the <paramref name="other"/> collection.
		/// </summary>
		/// <remarks>
		/// This is an <c>O(n)</c> operation, where <c>n</c> is the number of elements in <paramref name="other"/>.
		///
		/// An overload that takes any <c>IEnumerable&lt;T&gt;</c> exists as an
		/// <see cref="ValueCollectionExtensions.ExceptWith{T}(ValueSet{T}.Builder, IEnumerable{T})">extension method</see>.
		/// </remarks>
		public Builder ExceptWith(ValueSet<T> other)
		{
			if (other is null)
			{
				ThrowHelpers.ThrowArgumentNullException(ThrowHelpers.Argument.other);
			}

			this.MutateOnce().ExceptWith(ref other.inner);
			return this;
		}

		/// <summary>
		/// Remove all elements that appear in the <paramref name="items"/> sequence.
		/// </summary>
		/// <remarks>
		/// This is an <c>O(n)</c> operation, where <c>n</c> is the number of elements in <paramref name="items"/>.
		/// </remarks>
		public Builder ExceptWith(scoped ReadOnlySpan<T> items)
		{
			this.MutateOnce().ExceptWith(items);
			return this;
		}

		// Accessible through extension method.
		internal Builder ExceptWithEnumerable(IEnumerable<T> other)
		{
			if (other is null)
			{
				ThrowHelpers.ThrowArgumentNullException(ThrowHelpers.Argument.other);
			}

			if (other is ValueSet<T> valueSet)
			{
				return this.ExceptWith(valueSet);
			}

			using (var guard = this.Mutate())
			{
				guard.Inner.ExceptWith(other);
			}

			return this;
		}

		/// <summary>
		/// Remove all elements that appear in both <see langword="this"/>
		/// <em>and</em> the <paramref name="other"/> collection.
		/// </summary>
		/// <remarks>
		/// This is an <c>O(n)</c> operation, where <c>n</c> is the number of elements in <paramref name="other"/>.
		///
		/// An overload that takes any <c>IEnumerable&lt;T&gt;</c> exists as an
		/// <see cref="ValueCollectionExtensions.SymmetricExceptWith{T}(ValueSet{T}.Builder, IEnumerable{T})">extension method</see>.
		/// Beware of the performance implications though.
		/// </remarks>
		public Builder SymmetricExceptWith(ValueSet<T> other)
		{
			if (other is null)
			{
				ThrowHelpers.ThrowArgumentNullException(ThrowHelpers.Argument.other);
			}

			this.MutateOnce().SymmetricExceptWith(ref other.inner);
			return this;
		}

		// Accessible through extension method.
		internal Builder SymmetricExceptWithEnumerable(IEnumerable<T> other)
		{
			if (other is null)
			{
				ThrowHelpers.ThrowArgumentNullException(ThrowHelpers.Argument.other);
			}

			if (other is ValueSet<T> valueSet)
			{
				return this.SymmetricExceptWith(valueSet);
			}

			using (var guard = this.Mutate())
			{
				guard.Inner.SymmetricExceptWith(other);
			}

			return this;
		}

		/// <summary>
		/// Modify the current builder to contain only elements that are present in
		/// both <see langword="this"/> <em>and</em> the <paramref name="other"/>
		/// collection.
		/// </summary>
		/// <remarks>
		/// This is an <c>O(n)</c> operation, where <c>n</c> is the number of elements in <c>this</c>.
		///
		/// An overload that takes any <c>IEnumerable&lt;T&gt;</c> exists as an
		/// <see cref="ValueCollectionExtensions.IntersectWith{T}(ValueSet{T}.Builder, IEnumerable{T})">extension method</see>.
		/// Beware of the performance implications though.
		/// </remarks>
		public Builder IntersectWith(ValueSet<T> other)
		{
			if (other is null)
			{
				ThrowHelpers.ThrowArgumentNullException(ThrowHelpers.Argument.other);
			}

			this.MutateOnce().IntersectWith(ref other.inner);
			return this;
		}

		// Accessible through extension method.
		internal Builder IntersectWithEnumerable(IEnumerable<T> other)
		{
			if (other is null)
			{
				ThrowHelpers.ThrowArgumentNullException(ThrowHelpers.Argument.other);
			}

			if (other is ValueSet<T> valueSet)
			{
				return this.IntersectWith(valueSet);
			}

			using (var guard = this.Mutate())
			{
				guard.Inner.IntersectWith(other);
			}

			return this;
		}

		/// <summary>
		/// Add all elements from the <paramref name="other"/> collection.
		/// </summary>
		/// <remarks>
		/// This is an <c>O(n)</c> operation, where <c>n</c> is the number of elements in <paramref name="other"/>.
		///
		/// An overload that takes any <c>IEnumerable&lt;T&gt;</c> exists as an
		/// <see cref="ValueCollectionExtensions.UnionWith{T}(ValueSet{T}.Builder, IEnumerable{T})">extension method</see>.
		/// </remarks>
		public Builder UnionWith(ValueSet<T> other)
		{
			if (other is null)
			{
				ThrowHelpers.ThrowArgumentNullException(ThrowHelpers.Argument.other);
			}

			this.MutateOnce().UnionWith(ref other.inner);
			return this;
		}

		/// <summary>
		/// Add all elements from the <paramref name="items"/> sequence.
		/// </summary>
		public Builder UnionWith(scoped ReadOnlySpan<T> items)
		{
			this.MutateOnce().UnionWith(items);
			return this;
		}

		// Accessible through extension method.
		internal Builder UnionWithEnumerable(IEnumerable<T> other)
		{
			if (other is null)
			{
				ThrowHelpers.ThrowArgumentNullException(ThrowHelpers.Argument.other);
			}

			if (other is ValueSet<T> valueSet)
			{
				return this.UnionWith(valueSet);
			}

			using (var guard = this.Mutate())
			{
				guard.Inner.UnionWith(other);
			}

			return this;
		}

		/// <summary>
		/// Create a new heap-allocated live view of the builder.
		/// </summary>
		/// <remarks>
		/// This method is an <c>O(1)</c> operation and allocates a new fixed-size
		/// collection instance. The items are not copied. Changes made to the
		/// builder are visible in the collection and vice versa.
		/// </remarks>
		public Collection AsCollection()
		{
			var set = this.set;
			if (set is null)
			{
				return Collection.Empty;
			}

			// Beware: this cache field may be assigned to from multiple threads.
			return set.cachedBuilderCollection ??= new Collection(this);
		}

#pragma warning disable CA1034 // Nested types should not be visible
		/// <summary>
		/// A heap-allocated live view of a builder. Changes made to the
		/// collection are visible in the builder and vice versa.
		/// </summary>
		[DebuggerDisplay("Count = {Count}, Capacity = {Capacity}, IsReadOnly = {IsReadOnly}")]
		[DebuggerTypeProxy(typeof(ValueSet<>.Builder.Collection.DebugView))]
		public sealed class Collection : ISet<T>, IReadOnlyCollection<T>, IReadOnlySet<T>
		{
			internal static readonly Collection Empty = new(default);

			private readonly Builder builder;

			/// <summary>
			/// The underlying builder.
			/// </summary>
			public Builder Builder => this.builder;

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			internal Collection(Builder builder)
			{
				this.builder = builder;
			}

			// Used by DebuggerDisplay attribute
			private int Count => this.builder.Count;

			// Used by DebuggerDisplay attribute
			private int Capacity => this.builder.Capacity;

			// Used by DebuggerDisplay attribute
			private bool IsReadOnly => this.builder.IsReadOnly;

			/// <inheritdoc/>
			int ICollection<T>.Count => this.Count;

			/// <inheritdoc/>
			int IReadOnlyCollection<T>.Count => this.Count;

			/// <inheritdoc/>
			bool ICollection<T>.IsReadOnly => this.IsReadOnly;

			/// <inheritdoc/>
			void ICollection<T>.Add(T item) => this.builder.TryAdd(item);

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
			bool ICollection<T>.Remove(T item) => this.builder.TryRemove(item);

			/// <inheritdoc/>
			bool ISet<T>.Add(T item) => this.builder.TryAdd(item);

			/// <inheritdoc/>
			void ISet<T>.ExceptWith(IEnumerable<T> other) => this.builder.ExceptWith(other);

			/// <inheritdoc/>
			void ISet<T>.IntersectWith(IEnumerable<T> other) => this.builder.IntersectWith(other);

			/// <inheritdoc/>
			bool ISet<T>.IsProperSubsetOf(IEnumerable<T> other) => this.builder.IsProperSubsetOf(other);

			/// <inheritdoc/>
			bool ISet<T>.IsProperSupersetOf(IEnumerable<T> other) => this.builder.IsProperSupersetOf(other);

			/// <inheritdoc/>
			bool ISet<T>.IsSubsetOf(IEnumerable<T> other) => this.builder.IsSubsetOf(other);

			/// <inheritdoc/>
			bool ISet<T>.IsSupersetOf(IEnumerable<T> other) => this.builder.IsSupersetOf(other);

			/// <inheritdoc/>
			bool ISet<T>.Overlaps(IEnumerable<T> other) => this.builder.Overlaps(other);

			/// <inheritdoc/>
			bool ISet<T>.SetEquals(IEnumerable<T> other) => this.builder.SetEquals(other);

			/// <inheritdoc/>
			void ISet<T>.SymmetricExceptWith(IEnumerable<T> other) => this.builder.SymmetricExceptWith(other);

			/// <inheritdoc/>
			void ISet<T>.UnionWith(IEnumerable<T> other) => this.builder.UnionWith(other);

			/// <inheritdoc/>
			bool IReadOnlySet<T>.Contains(T item) => this.builder.Contains(item);

			/// <inheritdoc/>
			bool IReadOnlySet<T>.IsProperSubsetOf(IEnumerable<T> other) => this.builder.IsProperSubsetOf(other);

			/// <inheritdoc/>
			bool IReadOnlySet<T>.IsProperSupersetOf(IEnumerable<T> other) => this.builder.IsProperSupersetOf(other);

			/// <inheritdoc/>
			bool IReadOnlySet<T>.IsSubsetOf(IEnumerable<T> other) => this.builder.IsSubsetOf(other);

			/// <inheritdoc/>
			bool IReadOnlySet<T>.IsSupersetOf(IEnumerable<T> other) => this.builder.IsSupersetOf(other);

			/// <inheritdoc/>
			bool IReadOnlySet<T>.Overlaps(IEnumerable<T> other) => this.builder.Overlaps(other);

			/// <inheritdoc/>
			bool IReadOnlySet<T>.SetEquals(IEnumerable<T> other) => this.builder.SetEquals(other);

			internal sealed class DebugView(Collection collection)
			{
				[DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
				internal T[] Items => ValueSet<T>.DebugView.CreateItems(in collection.builder.ReadOnce());
			}
		}
#pragma warning restore CA1034 // Nested types should not be visible

		/// <summary>
		/// Returns an enumerator for this <see cref="ValueSet{T}.Builder"/>.
		///
		/// Typically, you don't need to manually call this method, but instead use
		/// the built-in <c>foreach</c> syntax.
		/// </summary>
		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Enumerator GetEnumerator() => new Enumerator(this);

		/// <summary>
		/// Enumerator for <see cref="ValueSet{T}.Builder"/>.
		/// </summary>
#pragma warning disable CA1034 // Nested types should not be visible
#pragma warning disable CA1815 // Override equals and operator equals on value types
		[StructLayout(LayoutKind.Auto)]
		public struct Enumerator : IEnumeratorLike<T>
		{
			private readonly Snapshot snapshot;
			private RawSet<T>.Enumerator inner;

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			internal Enumerator(Builder builder)
			{
				var snapshot = builder.Read();
				this.snapshot = snapshot;
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
				this.snapshot.AssertAlive();

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
		public override int GetHashCode() => RuntimeHelpers.GetHashCode(this.set);

		/// <summary>
		/// Returns <see langword="true"/> when the two builders refer to the same allocation.
		/// </summary>
		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(Builder other) => object.ReferenceEquals(this.set, other.set);

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
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator ==(Builder left, Builder right) => left.Equals(right);

		/// <summary>
		/// Check for inequality.
		/// </summary>
		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator !=(Builder left, Builder right) => !left.Equals(right);

		internal sealed class DebugView(Builder builder)
		{
			[DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
			internal T[] Items => ValueSet<T>.DebugView.CreateItems(in builder.ReadOnce());
		}
	}
}
