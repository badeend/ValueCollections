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
		public bool IsReadOnly => BuilderState.IsImmutable(this.Read().state);

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
			var set = this.Mutate();

			set.state = BuilderState.InitialImmutable;

			return set;
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
			var set = this.Read();

			if (BuilderState.IsImmutable(set.state))
			{
				return set;
			}
			else
			{
				return ValueSet<T>.CreateImmutableFromEnumerable(set);
			}
		}

		/// <summary>
		/// Copy the current contents of the builder into a new <see cref="ValueSet{T}.Builder"/>.
		/// </summary>
		[Pure]
		public Builder ToValueSetBuilder()
		{
			return Builder.CreateFromEnumerable(this.Read());
		}

		private ValueSet<T> Mutate()
		{
			var set = this.set;
			if (set is null || (uint)set.state >= BuilderState.LastMutableVersion)
			{
				SlowPath(set);
			}

			set.state++;

			Debug.Assert(BuilderState.IsMutable(set.state));

			return set;

			[MethodImpl(MethodImplOptions.NoInlining)]
			static void SlowPath([NotNull] ValueSet<T>? set)
			{
				if (set is null)
				{
					ThrowHelpers.ThrowInvalidOperationException_UninitializedBuiler();
				}
				else if (set.state == BuilderState.LastMutableVersion)
				{
					set.state = BuilderState.InitialMutable;
				}
				else
				{
					Debug.Assert(BuilderState.IsImmutable(set.state));

					ThrowHelpers.ThrowInvalidOperationException_AlreadyBuilt();
				}
			}
		}

		private ValueSet<T> Read() => this.set ?? Empty;

		/// <summary>
		/// Try to get the underlying HashSet as enumerable.
		/// Some of HashSet's methods can take advantage of specialized optimizations
		/// when the `other` parameter is also a HashSet.
		/// </summary>
		private static IEnumerable<T> PreferHashSet(IEnumerable<T> input)
		{
			if (input is ValueSet<T> valueSet)
			{
				return valueSet.items;
			}

			return input;
		}

		/// <summary>
		/// Current size of the set.
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

#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
		/// <summary>
		/// The total number of elements the internal data structure can hold without resizing.
		/// </summary>
		/// <remarks>
		/// Available on .NET Standard 2.1 and .NET Core 2.1 and higher.
		/// </remarks>
		[Pure]
		public int Capacity => this.Read().Capacity;

		/// <summary>
		/// Ensures that the capacity of this set is at least the specified capacity.
		/// If the current capacity is less than capacity, it is increased to at
		/// least the specified capacity.
		/// </summary>
		/// <remarks>
		/// Available on .NET Standard 2.1 and .NET Core 2.1 and higher.
		/// </remarks>
		/// <exception cref="ArgumentOutOfRangeException">
		/// <paramref name="minimumCapacity"/> is less than 0.
		/// </exception>
		public Builder EnsureCapacity(int minimumCapacity)
		{
			// FYI, earlier .NET Core versions also had EnsureCapacity, but those
			// implementations were buggy and result in an `IndexOutOfRangeException`
			// inside their internal `SetCapacity` method.
			// From .NET 5 onwards it seems to work fine.
#if NET5_0_OR_GREATER
			this.Mutate().items.EnsureCapacity(minimumCapacity);
#else
			var set = this.Mutate();

			if (minimumCapacity < 0)
			{
				throw new ArgumentOutOfRangeException("capacity"); // TODO: use parameter name
			}

			var currentCapacity = set.items.EnsureCapacity(0);
			if (currentCapacity >= minimumCapacity)
			{
				// Nothing to do.
				return this;
			}

			set.items = CopyWithCapacity(set.items, minimumCapacity);
#endif
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
		/// <remarks>
		/// Available on .NET Standard 2.1 and .NET Core 2.1 and higher.
		/// </remarks>
		public Builder TrimExcess(int targetCapacity)
		{
#if NET9_0_OR_GREATER
			this.Mutate().TrimExcess(targetCapacity);
#else
			var set = this.Mutate();

			if (targetCapacity < set.Count)
			{
				throw new ArgumentOutOfRangeException(nameof(targetCapacity));
			}

			var currentCapacity = set.items.EnsureCapacity(0);
			if (targetCapacity >= currentCapacity)
			{
				// Nothing to do.
				return this;
			}

			set.items = CopyWithCapacity(set.items, targetCapacity);
#endif
			return this;
		}

		private static HashSet<T> CopyWithCapacity(HashSet<T> input, int minimumCapacity)
		{
			var copy = new HashSet<T>(minimumCapacity);
			copy.UnionWith(input);
			return copy;
		}
#endif

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

		private Builder(ValueSet<T> set)
		{
			Debug.Assert(BuilderState.IsMutable(set.state));

			this.set = set;
		}

		internal static Builder Create()
		{
			return new(ValueSet<T>.CreateMutable());
		}

#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
		internal static Builder CreateWithCapacity(int minimumCapacity)
		{
			return new(ValueSet<T>.CreateMutableWithCapacity(minimumCapacity));
		}
#endif

		internal static Builder CreateFromEnumerable(IEnumerable<T> items)
		{
			return new(ValueSet<T>.CreateMutableFromEnumerable(items));
		}

		internal static Builder CreateFromSpan(ReadOnlySpan<T> items)
		{
			return new(ValueSet<T>.CreateMutableFromSpan(items));
		}

		/// <summary>
		/// Attempt to add the <paramref name="item"/> to the set.
		/// Returns <see langword="false"/> when the element was
		/// already present.
		/// </summary>
		/// <remarks>
		/// This is the equivalent of <see cref="HashSet{T}.Add(T)">HashSet.Add</see>.
		/// </remarks>
		public bool TryAdd(T item) => this.Mutate().items.Add(item);

		/// <summary>
		/// Add the <paramref name="item"/> to the set if it isn't already present.
		/// </summary>
		/// <remarks>
		/// Use <see cref="UnionWith"/> to add multiple values at once.
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
		public Builder Clear()
		{
			this.Mutate().items.Clear();
			return this;
		}

		/// <summary>
		/// Attempt to remove a specific element from the set.
		/// Returns <see langword="false"/> when the element was not found.
		/// </summary>
		/// <remarks>
		/// This is the equivalent of <see cref="HashSet{T}.Remove(T)">HashSet.Remove</see>.
		/// </remarks>
		public bool TryRemove(T item) => this.Mutate().items.Remove(item);

		/// <summary>
		/// Remove a specific element from the set if it exists.
		/// </summary>
		/// <remarks>
		/// Use <see cref="ExceptWith"/> to remove multiple values at once.
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
			this.Mutate().items.RemoveWhere(match);
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
			this.Mutate().items.TrimExcess();
			return this;
		}

		/// <summary>
		/// Returns <see langword="true"/> when the set contains the specified
		/// <paramref name="item"/>.
		/// </summary>
		[Pure]
		public bool Contains(T item) => this.Read().Contains(item);

		/// <summary>
		/// Copy the contents of the set into an existing <see cref="Span{T}"/>.
		/// </summary>
		/// <exception cref="ArgumentException">
		///   <paramref name="destination"/> is shorter than the source slice.
		/// </exception>
		/// <remarks>
		/// The order in which the elements are copied is undefined.
		/// </remarks>
		public void CopyTo(Span<T> destination)
		{
			if (destination.Length < this.Count)
			{
				throw new ArgumentException("Destination too short", nameof(destination));
			}

			var index = 0;
			foreach (var item in this)
			{
				destination[index] = item;
				index++;
			}
		}

		/// <summary>
		/// Check whether <c>this</c> set is a proper subset of the provided collection.
		/// </summary>
		public bool IsProperSubsetOf(IEnumerable<T> other) => this.Read().IsProperSubsetOf(other);

		/// <summary>
		/// Check whether <c>this</c> set is a proper superset of the provided collection.
		/// </summary>
		public bool IsProperSupersetOf(IEnumerable<T> other) => this.Read().IsProperSupersetOf(other);

		/// <summary>
		/// Check whether <c>this</c> set is a subset of the provided collection.
		/// </summary>
		public bool IsSubsetOf(IEnumerable<T> other) => this.Read().IsSubsetOf(other);

		/// <summary>
		/// Check whether <c>this</c> set is a superset of the provided collection.
		/// </summary>
		public bool IsSupersetOf(IEnumerable<T> other) => this.Read().IsSupersetOf(other);

		/// <summary>
		/// Check whether <c>this</c> set and the provided collection share any common elements.
		/// </summary>
		public bool Overlaps(IEnumerable<T> other) => this.Read().Overlaps(other);

		/// <summary>
		/// Check whether <c>this</c> set and the provided collection contain
		/// the same elements, ignoring duplicates and the order of the elements.
		/// </summary>
		public bool SetEquals(IEnumerable<T> other) => this.Read().SetEquals(other);

		private bool ReferenceEqualsEnumerable(IEnumerable<T> other)
		{
			return other is ValueSet<T>.Builder.Collection vsbc && vsbc.Builder == this;
		}

		/// <summary>
		/// Remove all elements that appear in the <paramref name="other"/> collection.
		/// </summary>
		/// <remarks>
		/// <see cref="ValueCollectionExtensions.ExceptWith">More overloads</see> are
		/// available as extension methods.
		/// </remarks>
		public Builder ExceptWith(IEnumerable<T> other)
		{
			var set = this.Mutate();

			// Special case; a set minus itself is always an empty set.
			if (this.ReferenceEqualsEnumerable(other))
			{
				set.items.Clear();
			}
			else
			{
				set.items.ExceptWith(other);
			}

			return this;
		}

		// Accessible through an extension method.
		internal Builder ExceptWithSpan(ReadOnlySpan<T> items)
		{
			var set = this.Mutate();

			foreach (var item in items)
			{
				set.items.Remove(item);
			}

			return this;
		}

		/// <summary>
		/// Remove all elements that appear in both <see langword="this"/>
		/// <em>and</em> the <paramref name="other"/> collection.
		/// </summary>
		public Builder SymmetricExceptWith(IEnumerable<T> other)
		{
			var set = this.Mutate();

			// Special case; a set minus itself is always an empty set.
			if (this.ReferenceEqualsEnumerable(other))
			{
				set.items.Clear();
			}
			else
			{
				set.items.SymmetricExceptWith(PreferHashSet(other));
			}

			return this;
		}

		/// <summary>
		/// Modify the current builder to contain only elements that are present in
		/// both <see langword="this"/> <em>and</em> the <paramref name="other"/>
		/// collection.
		/// </summary>
		public Builder IntersectWith(IEnumerable<T> other)
		{
			var set = this.Mutate();

			// Special case; intersection of two identical sets is the same set.
			if (!this.ReferenceEqualsEnumerable(other))
			{
				set.items.IntersectWith(PreferHashSet(other));
			}

			return this;
		}

		/// <summary>
		/// Add all elements from the <paramref name="other"/> collection.
		/// </summary>
		/// <remarks>
		/// <see cref="ValueCollectionExtensions.UnionWith">More overloads</see> are
		/// available as extension methods.
		/// </remarks>
		public Builder UnionWith(IEnumerable<T> other)
		{
			var set = this.Mutate();

			// Special case; union of two identical sets is the same set.
			if (!this.ReferenceEqualsEnumerable(other))
			{
				set.items.UnionWith(other);
			}

			return this;
		}

		// Accessible through an extension method.
		internal Builder UnionWithSpan(ReadOnlySpan<T> items)
		{
			var set = this.Mutate();

			foreach (var item in items)
			{
				set.items.Add(item);
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
		public Collection AsCollection() => new Collection(this);

#pragma warning disable CA1034 // Nested types should not be visible
		/// <summary>
		/// A heap-allocated live view of a builder. Changes made to the
		/// collection are visible in the builder and vice versa.
		/// </summary>
#if NET5_0_OR_GREATER
		public sealed class Collection : ISet<T>, IReadOnlyCollection<T>, IReadOnlySet<T>
#else
		public sealed class Collection : ISet<T>, IReadOnlyCollection<T>
#endif
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
			int ICollection<T>.Count => this.builder.Count;

			/// <inheritdoc/>
			int IReadOnlyCollection<T>.Count => this.builder.Count;

			/// <inheritdoc/>
			bool ICollection<T>.IsReadOnly => this.builder.IsReadOnly;

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

#if NET5_0_OR_GREATER
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
#endif
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
		public Enumerator GetEnumerator() => new Enumerator(this.Read());

		/// <summary>
		/// Enumerator for <see cref="ValueSet{T}.Builder"/>.
		/// </summary>
#pragma warning disable CA1034 // Nested types should not be visible
#pragma warning disable CA1815 // Override equals and operator equals on value types
		[StructLayout(LayoutKind.Auto)]
		public struct Enumerator : IEnumeratorLike<T>
		{
			private readonly ValueSet<T> set;
			private readonly int expectedState;
			private ValueSet<T>.Enumerator inner;

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			internal Enumerator(ValueSet<T> set)
			{
				this.set = set;
				this.expectedState = set.state;
				this.inner = set.GetEnumerator();
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
				if (this.expectedState != this.set.state)
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
					Debug.Assert(BuilderState.IsImmutable(this.set.state));

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
		public override int GetHashCode() => RuntimeHelpers.GetHashCode(this.set);

		/// <summary>
		/// Returns <see langword="true"/> when the two builders refer to the same allocation.
		/// </summary>
		[Pure]
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
		public static bool operator ==(Builder left, Builder right) => left.Equals(right);

		/// <summary>
		/// Check for inequality.
		/// </summary>
		[Pure]
		public static bool operator !=(Builder left, Builder right) => !left.Equals(right);
	}
}