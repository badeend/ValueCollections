using System.Collections;
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
	/// class over e.g. <see cref="HashSet{T}"/>. This type can avoiding unnecessary
	/// copying by taking advantage of the immutability of its results. Whereas
	/// calling <c>.ToValueSet()</c> on a regular <see cref="HashSet{T}"/>
	/// <em>always</em> performs a full copy.
	///
	/// The order in which the elements are enumerated is undefined.
	///
	/// Unlike ValueSet, its Builder is <em>not</em> thread-safe.
	/// </remarks>
	[SuppressMessage("Naming", "CA1710:Identifiers should have correct suffix", Justification = "Not applicable for Builder type.")]
#if NET5_0_OR_GREATER
	public sealed class Builder : ISet<T>, IReadOnlyCollection<T>, IReadOnlySet<T>
#else
	public sealed class Builder : ISet<T>, IReadOnlyCollection<T>
#endif
	{
		private const int VersionBuilt = -1;

		/// <summary>
		/// Can be one of:
		/// - ValueSet{T}: when copy-on-write hasn't kicked in yet.
		/// - HashSet{T}: we're actively building a set.
		/// </summary>
		private ISet<T> items;

		/// <summary>
		/// Mutation counter.
		/// `-1` means: Collection has been built and the builder is now read-only.
		/// </summary>
		private int version;

		/// <summary>
		/// Returns <see langword="true"/> when this instance has been built and is
		/// now read-only.
		/// </summary>
		[Pure]
		public bool IsReadOnly => this.version == VersionBuilt;

		/// <summary>
		/// Finalize the builder and export its contents as a <see cref="ValueSet{T}"/>.
		/// This makes the builder read-only. Any future attempt to mutate the
		/// builder will throw.
		///
		/// This is an <c>O(1)</c> operation and performs only a small fixed-size
		/// memory allocation. This does not perform a bulk copy of the contents.
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
			if (this.version == VersionBuilt)
			{
				throw BuiltException();
			}

			this.version = VersionBuilt;

			return this.ToValueSet();
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
			if (this.items is HashSet<T> set)
			{
				var newValueSet = ValueSet<T>.FromHashSetUnsafe(set);
				this.items = newValueSet;
				return newValueSet;
			}

			if (this.items is ValueSet<T> valueSet)
			{
				return valueSet;
			}

			throw UnreachableException();
		}

		private HashSet<T> Mutate()
		{
			if (this.version == VersionBuilt)
			{
				throw BuiltException();
			}

			this.version++;

			if (this.items is HashSet<T> set)
			{
				return set;
			}

			if (this.items is ValueSet<T> valueSet)
			{
				var newHashSet = new HashSet<T>(valueSet.Items);
				this.items = newHashSet;
				return newHashSet;
			}

			throw UnreachableException();
		}

		private ISet<T> Read() => this.items;

		/// <summary>
		/// Try to get the underlying HashSet as enumerable.
		/// Some of HashSet's methods can take advantage of specialized optimizations
		/// when the `other` parameter is also a HashSet.
		/// </summary>
		private static IEnumerable<T> PreferHashSet(IEnumerable<T> input)
		{
			if (input is ValueSet<T> valueSet)
			{
				return valueSet.Items;
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

		/// <summary>
		/// Construct a new empty set builder.
		/// </summary>
		[Pure]
		public Builder()
		{
			this.items = ValueSet<T>.Empty;
		}

#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
		/// <summary>
		/// Construct a new empty set builder with at least the specified initial
		/// capacity.
		/// </summary>
		/// <remarks>
		/// Available on .NET Standard 2.1 and .NET Core 2.1 and higher.
		/// </remarks>
		/// <exception cref="ArgumentOutOfRangeException">
		///   <paramref name="minimumCapacity"/> is less than 0.
		/// </exception>
		[Pure]
		public Builder(int minimumCapacity)
		{
			if (minimumCapacity == 0)
			{
				this.items = ValueSet<T>.Empty;
			}
			else
			{
				this.items = new HashSet<T>(minimumCapacity);
			}
		}

		/// <summary>
		/// The total number of elements the internal data structure can hold without resizing.
		/// </summary>
		/// <remarks>
		/// Available on .NET Standard 2.1 and .NET Core 2.1 and higher.
		/// </remarks>
		public int Capacity
		{
			[Pure]
			get
			{
				var hashSet = this.items switch
				{
					HashSet<T> items => items,
					ValueSet<T> items => items.Items,
					_ => throw UnreachableException(),
				};

#if NET9_0_OR_GREATER
				return hashSet.Capacity;
#else
				return hashSet.EnsureCapacity(0);
#endif
			}
		}

		/// <summary>
		/// Ensures that the capacity of this set is at least the specified capacity.
		/// If the current capacity is less than capacity, it is increased to at
		/// least the specified capacity.
		/// </summary>
		/// <remarks>
		/// Available on .NET Standard 2.1 and .NET Core 2.1 and higher.
		/// </remarks>
		public Builder EnsureCapacity(int minimumCapacity)
		{
			// FYI, earlier .NET Core versions also had EnsureCapacity, but those
			// implementations were buggy and result in an `IndexOutOfRangeException`
			// inside their internal `SetCapacity` method.
			// From .NET 5 onwards it seems to work fine.
#if NET5_0_OR_GREATER
			this.Mutate().EnsureCapacity(minimumCapacity);
#else
			var hashSet = this.Mutate();

			if (minimumCapacity < 0)
			{
				throw new ArgumentOutOfRangeException("capacity"); // TODO: use parameter name
			}

			var currentCapacity = hashSet.EnsureCapacity(0);
			if (currentCapacity >= minimumCapacity)
			{
				// Nothing to do.
				return this;
			}

			this.items = CopyWithCapacity(hashSet, minimumCapacity);
			this.version++;
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
			var hashSet = this.Mutate();

			if (targetCapacity < hashSet.Count)
			{
				throw new ArgumentOutOfRangeException(nameof(targetCapacity));
			}

			var currentCapacity = hashSet.EnsureCapacity(0);
			if (targetCapacity >= currentCapacity)
			{
				// Nothing to do.
				return this;
			}

			this.items = CopyWithCapacity(hashSet, targetCapacity);
			this.version++;
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
		/// Construct a new <see cref="ValueSet{T}.Builder"/> with the provided
		/// <paramref name="items"/> as its initial content.
		/// </summary>
		/// <remarks>
		/// Use <see cref="ValueSet.CreateBuilder{T}(ReadOnlySpan{T})"/> to construct a
		/// Builder from a span.
		/// </remarks>
		public Builder(IEnumerable<T> items)
		{
			if (items is ValueSet<T> valueSet)
			{
				this.items = valueSet;
			}
			else
			{
				this.items = new HashSet<T>(items);
			}
		}

		private Builder(ValueSet<T> items)
		{
			this.items = items;
		}

		private Builder(HashSet<T> items)
		{
			this.items = items;
		}

		internal static Builder FromValueSet(ValueSet<T> items) => new(items);

		internal static Builder FromHashSetUnsafe(HashSet<T> items) => new(items);

		internal static Builder FromReadOnlySpan(ReadOnlySpan<T> items)
		{
			if (items.Length == 0)
			{
				return new();
			}

			return new(ValueSet<T>.SpanToHashSet(items));
		}

		/// <summary>
		/// Attempt to add the <paramref name="item"/> to the set.
		/// Returns <see langword="false"/> when the element was
		/// already present.
		/// </summary>
		/// <remarks>
		/// This is the equivalent of <see cref="HashSet{T}.Add(T)">HashSet.Add</see>.
		/// </remarks>
		public bool TryAdd(T item) => this.Mutate().Add(item);

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

		/// <inheritdoc/>
		bool ISet<T>.Add(T item) => this.TryAdd(item);

		/// <inheritdoc/>
		void ICollection<T>.Add(T item) => this.TryAdd(item);

		/// <summary>
		/// Remove all elements from the set.
		/// </summary>
		public Builder Clear()
		{
			this.Mutate().Clear();
			return this;
		}

		/// <inheritdoc/>
		void ICollection<T>.Clear() => this.Clear();

		/// <summary>
		/// Attempt to remove a specific element from the set.
		/// Returns <see langword="false"/> when the element was not found.
		/// </summary>
		/// <remarks>
		/// This is the equivalent of <see cref="HashSet{T}.Remove(T)">HashSet.Remove</see>.
		/// </remarks>
		public bool TryRemove(T item) => this.Mutate().Remove(item);

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

		/// <inheritdoc/>
		bool ICollection<T>.Remove(T item) => this.TryRemove(item);

		/// <summary>
		/// Remove all elements that match the predicate.
		/// </summary>
		public Builder RemoveWhere(Predicate<T> match)
		{
			this.Mutate().RemoveWhere(match);
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
			this.Mutate().TrimExcess();
			return this;
		}

		/// <summary>
		/// Returns <see langword="true"/> when the set contains the specified
		/// <paramref name="item"/>.
		/// </summary>
		[Pure]
		public bool Contains(T item) => this.items switch
		{
			HashSet<T> items => items.Contains(item),
			ValueSet<T> items => items.Contains(item),
			_ => throw UnreachableException(),
		};

		/// <inheritdoc/>
		void ICollection<T>.CopyTo(T[] array, int arrayIndex)
		{
			if (array is null)
			{
				throw new ArgumentNullException(nameof(array));
			}

			if (arrayIndex < 0 || arrayIndex > array.Length)
			{
				throw new ArgumentOutOfRangeException(nameof(arrayIndex));
			}

			if (array.Length - arrayIndex < this.Count)
			{
				throw new ArgumentException("Destination too short", nameof(arrayIndex));
			}

			var index = arrayIndex;
			foreach (var item in this)
			{
				array[index] = item;
				index++;
			}
		}

		/// <inheritdoc/>
		public bool IsProperSubsetOf(IEnumerable<T> other) => this.Read().IsProperSubsetOf(PreferHashSet(other));

		/// <inheritdoc/>
		public bool IsProperSupersetOf(IEnumerable<T> other) => this.Read().IsProperSupersetOf(PreferHashSet(other));

		/// <inheritdoc/>
		public bool IsSubsetOf(IEnumerable<T> other) => this.Read().IsSubsetOf(PreferHashSet(other));

		/// <inheritdoc/>
		public bool IsSupersetOf(IEnumerable<T> other) => this.Read().IsSupersetOf(PreferHashSet(other));

		/// <inheritdoc/>
		public bool Overlaps(IEnumerable<T> other) => this.Read().Overlaps(other);

		/// <inheritdoc/>
		public bool SetEquals(IEnumerable<T> other) => this.Read().SetEquals(PreferHashSet(other));

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
			if (this == other)
			{
				set.Clear();
			}
			else
			{
				set.ExceptWith(other);
			}

			return this;
		}

		/// <inheritdoc/>
		void ISet<T>.ExceptWith(IEnumerable<T> other) => this.ExceptWith(other);

		// Accessible through an extension method.
		internal Builder ExceptWithSpan(ReadOnlySpan<T> items)
		{
			var set = this.Mutate();

			foreach (var item in items)
			{
				set.Remove(item);
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
			if (this == other)
			{
				set.Clear();
			}
			else
			{
				set.SymmetricExceptWith(PreferHashSet(other));
			}

			return this;
		}

		/// <inheritdoc/>
		void ISet<T>.SymmetricExceptWith(IEnumerable<T> other) => this.SymmetricExceptWith(other);

		/// <summary>
		/// Modify the current builder to contain only elements that are present in
		/// both <see langword="this"/> <em>and</em> the <paramref name="other"/>
		/// collection.
		/// </summary>
		public Builder IntersectWith(IEnumerable<T> other)
		{
			var set = this.Mutate();

			// Special case; intersection of two identical sets is the same set.
			if (this != other)
			{
				set.IntersectWith(PreferHashSet(other));
			}

			return this;
		}

		/// <inheritdoc/>
		void ISet<T>.IntersectWith(IEnumerable<T> other) => this.IntersectWith(other);

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
			if (this != other)
			{
				set.UnionWith(other);
			}

			return this;
		}

		/// <inheritdoc/>
		void ISet<T>.UnionWith(IEnumerable<T> other) => this.UnionWith(other);

		// Accessible through an extension method.
		internal Builder UnionWithSpan(ReadOnlySpan<T> items)
		{
			var set = this.Mutate();

			foreach (var item in items)
			{
				set.Add(item);
			}

			return this;
		}

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
			private readonly Builder builder;
			private readonly int version;
			private ShufflingHashSetEnumerator<T> enumerator;

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			internal Enumerator(Builder builder)
			{
				var innerHashSet = builder.items switch
				{
					HashSet<T> items => items,
					ValueSet<T> items => items.Items,
					_ => throw UnreachableException(),
				};

				this.builder = builder;
				this.version = builder.version;
				this.enumerator = new(innerHashSet, initialSeed: builder.version);
			}

			/// <inheritdoc/>
			public readonly T Current
			{
				[MethodImpl(MethodImplOptions.AggressiveInlining)]
				get => this.enumerator.Current;
			}

			/// <inheritdoc/>
			public bool MoveNext()
			{
				if (this.version != this.builder.version)
				{
					throw new InvalidOperationException("Collection was modified during enumeration.");
				}

				return this.enumerator.MoveNext();
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

		private static InvalidOperationException UnreachableException() => new("Unreachable");

		private static InvalidOperationException BuiltException() => new("Builder has already been built");
	}
}
