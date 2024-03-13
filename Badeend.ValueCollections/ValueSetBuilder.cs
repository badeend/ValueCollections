using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Badeend.ValueCollections;

/// <summary>
/// A mutable set that can be used to efficiently construct new immutable sets.
///
/// Most mutating methods on this class return `this`, allowing the caller to
/// chain multiple mutations in a row. The boolean-returning
/// <see cref="HashSet{T}.Add(T)">HashSet.Add</see> and
/// <see cref="HashSet{T}.Remove(T)">HashSet.Remove</see> are implemented as
/// <see cref="TryAdd(T)"/> and <see cref="TryRemove(T)"/>.
///
/// When you're done building, call <see cref="ToValueSet()"/> to get out the
/// resulting set.
///
/// For constructing <see cref="ValueSet{T}"/>s it is recommended to use this
/// class over e.g. <see cref="HashSet{T}"/>. This type can avoiding unnecessary
/// copying by taking advantage of the immutability of its results. Whereas
/// calling <c>.ToValueSet()</c> on a regular <see cref="HashSet{T}"/>
/// <em>always</em> performs a full copy.
///
/// Unlike ValueSet, ValueSetBuilder is <em>not</em> thread-safe.
/// </summary>
/// <typeparam name="T">The type of items in the set.</typeparam>
[CollectionBuilder(typeof(ValueSet), nameof(ValueSet.Builder))]
[SuppressMessage("Naming", "CA1710:Identifiers should have correct suffix", Justification = "Not applicable for Builder type.")]
#if NET5_0_OR_GREATER
public sealed class ValueSetBuilder<T> : ISet<T>, IReadOnlyCollection<T>, IReadOnlySet<T>
#else
public sealed class ValueSetBuilder<T> : ISet<T>, IReadOnlyCollection<T>
#endif
{
	/// <summary>
	/// Can be one of:
	/// - ValueSet{T}: when copy-on-write hasn't kicked in yet.
	/// - HashSet{T}: we're actively building a set.
	/// </summary>
	private ISet<T> items;

	private int version;

	/// <summary>
	/// Create a <see cref="ValueSet{T}"/> based on the current contents of the
	/// builder.
	///
	/// This is an <c>O(1)</c> operation and performs only a small fixed-size
	/// memory allocation. This does not perform a bulk copy of the contents.
	/// </summary>
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
		this.version++;
		return this.GetOrCreateHashSet();
	}

	private HashSet<T> MutateForCapacityOnly()
	{
		// Don't update version.
		return this.GetOrCreateHashSet();
	}

	private HashSet<T> GetOrCreateHashSet()
	{
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
	public int Count => this.Read().Count;

	/// <inheritdoc/>
	bool ICollection<T>.IsReadOnly => false;

	/// <summary>
	/// Construct a new empty set builder.
	/// </summary>
	public ValueSetBuilder()
	{
		this.items = ValueSet<T>.Empty;
	}

#if NET472_OR_GREATER || NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_0_OR_GREATER
	/// <summary>
	/// Construct a new empty set builder with the specified initial capacity.
	/// </summary>
	/// <remarks>
	/// Available on .NET Framework 4.7.2 and higher.
	/// </remarks>
	/// <exception cref="ArgumentOutOfRangeException">
	///   <paramref name="capacity"/> is less than 0.
	/// </exception>
	public ValueSetBuilder(int capacity)
	{
		if (capacity == 0)
		{
			this.items = ValueSet<T>.Empty;
		}
		else
		{
			this.items = new HashSet<T>(capacity);
		}
	}

	/// <summary>
	/// The total number of elements the internal data structure can hold without resizing.
	/// </summary>
	/// <remarks>
	/// Available on .NET Framework 4.7.2 and higher.
	/// </remarks>
	public int Capacity
	{
		get
		{
			var hashSet = this.items switch
			{
				HashSet<T> items => items,
				ValueSet<T> items => items.Items,
				_ => throw UnreachableException(),
			};

			return UnsafeHelpers.GetCapacity(hashSet);
		}
	}

	/// <summary>
	/// Ensures that the capacity of this set is at least the specified capacity.
	/// If the current capacity is less than capacity, it is increased to at
	/// least the specified capacity.
	/// </summary>
	/// <remarks>
	/// Available on .NET Framework 4.7.2 and higher.
	/// </remarks>
	public ValueSetBuilder<T> EnsureCapacity(int capacity)
	{
		// FYI, earlier .NET Core versions also had EnsureCapacity, but those
		// implementations were buggy and result in an `IndexOutOfRangeException`
		// inside their internal `SetCapacity` method.
		// From .NET 5 onwards it seems to work fine.
#if NET5_0_OR_GREATER
		this.MutateForCapacityOnly().EnsureCapacity(capacity);
#else
		if (capacity < 0)
		{
			throw new ArgumentOutOfRangeException(nameof(capacity));
		}

		if (this.Capacity >= capacity)
		{
			// Nothing to do.
			return this;
		}

		this.items = CopyWithCapacity(this.Read(), capacity);
#endif
		return this;
	}

	/// <summary>
	/// Reduce the capacity of the set to roughly the specified value. If the
	/// current capacity is already smaller than the requested capacity, this
	/// method does nothing. The specified <paramref name="capacity"/> is only
	/// a hint. After this method returns, the <see cref="Capacity"/> may be
	/// rounded up to a nearby, implementation-specific value.
	/// </summary>
	/// <exception cref="ArgumentOutOfRangeException">
	/// <paramref name="capacity"/> is less than <see cref="Count"/>.
	/// </exception>
	/// <remarks>
	/// Available on .NET Framework 4.7.2 and higher.
	/// </remarks>
	public ValueSetBuilder<T> TrimExcess(int capacity)
	{
#if NET9_0_OR_GREATER
		this.MutateForCapacityOnly().TrimExcess(capacity);
#else
		var count = this.Count;
		var currentCapacity = this.Capacity;
		if (capacity < count)
		{
			throw new ArgumentOutOfRangeException(nameof(capacity));
		}

		if (capacity >= currentCapacity)
		{
			// Nothing to do.
			return this;
		}

		this.items = CopyWithCapacity(this.Read(), capacity);
#endif
		return this;
	}

	private static HashSet<T> CopyWithCapacity(ISet<T> input, int minimumCapacity)
	{
		var copy = new HashSet<T>(minimumCapacity);
		copy.UnionWith(input);
		return copy;
	}
#endif

	/// <summary>
	/// Construct a new <see cref="ValueSetBuilder{T}"/> with the provided
	/// <paramref name="items"/> as its initial content.
	///
	/// To construct a ValueSetBuilder from other types of inputs (Spans etc.),
	/// use one of the <c>.ToValueSetBuilder()</c> extension methods.
	/// </summary>
	public ValueSetBuilder(IEnumerable<T> items)
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

	private ValueSetBuilder(ValueSet<T> items)
	{
		this.items = items;
	}

	private ValueSetBuilder(HashSet<T> items)
	{
		this.items = items;
	}

	internal static ValueSetBuilder<T> FromValueSet(ValueSet<T> items) => new(items);

	internal static ValueSetBuilder<T> FromHashSetUnsafe(HashSet<T> items) => new(items);

	internal static ValueSetBuilder<T> FromReadOnlySpan(ReadOnlySpan<T> items)
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
	public ValueSetBuilder<T> Add(T item)
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
	public ValueSetBuilder<T> Clear()
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
	public ValueSetBuilder<T> Remove(T item)
	{
		this.TryRemove(item);
		return this;
	}

	/// <inheritdoc/>
	bool ICollection<T>.Remove(T item) => this.TryRemove(item);

	/// <summary>
	/// Remove all elements that match the predicate.
	/// </summary>
	public ValueSetBuilder<T> RemoveWhere(Predicate<T> match)
	{
		this.Mutate().RemoveWhere(match);
		return this;
	}

	/// <summary>
	/// Reduce the capacity of this set as much as possible. After calling this
	/// method, the <see cref="Capacity"/> of the set may still be higher than
	/// the <see cref="Count"/>.
	/// </summary>
	/// <remarks>
	/// This method can be used to minimize the memory overhead of long-lived
	/// sets. This method is most useful just before calling
	/// <see cref="ToValueSet"/>, e.g.:
	/// <code>
	/// var longLivedSet = builder.TrimExcess().ToValueSet()
	/// </code>
	/// Excessive use of this method most likely introduces more performance
	/// problems than it solves.
	/// </remarks>
	public ValueSetBuilder<T> TrimExcess()
	{
		this.MutateForCapacityOnly().TrimExcess();
		return this;
	}

	/// <summary>
	/// Returns <see langword="true"/> when the set contains the specified
	/// <paramref name="item"/>.
	/// </summary>
	public bool Contains(T item) => this.items switch
	{
		HashSet<T> items => items.Contains(item),
		ValueSet<T> items => items.Contains(item),
		_ => throw UnreachableException(),
	};

	/// <summary>
	/// Copy the contents of the set into a new array.
	/// </summary>
	public T[] ToArray() => this.items switch
	{
		HashSet<T> items => items.ToArray(),
		ValueSet<T> items => items.ToArray(),
		_ => throw UnreachableException(),
	};

	/// <summary>
	/// Copy the contents of the set into an existing <see cref="Span{T}"/>.
	/// </summary>
	/// <exception cref="ArgumentException">
	///   <paramref name="destination"/> is shorter than the source set.
	/// </exception>
	public void CopyTo(Span<T> destination)
	{
		if (destination.Length < this.Count)
		{
			throw new ArgumentException("Destination too short", nameof(destination));
		}

		this.CopyToUnchecked(destination);
	}

	/// <summary>
	/// Attempt to copy the contents of the set into an existing
	/// <see cref="Span{T}"/>. If the <paramref name="destination"/> is too short,
	/// no items are copied and the method returns <see langword="false"/>.
	/// </summary>
	public bool TryCopyTo(Span<T> destination)
	{
		if (destination.Length < this.Count)
		{
			return false;
		}

		this.CopyToUnchecked(destination);
		return true;
	}

	private void CopyToUnchecked(Span<T> destination)
	{
		var index = 0;
		foreach (var item in this)
		{
			destination[index] = item;
			index++;
		}
	}

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
	public ValueSetBuilder<T> ExceptWith(IEnumerable<T> other)
	{
		this.Mutate().ExceptWith(other);
		return this;
	}

	/// <inheritdoc/>
	void ISet<T>.ExceptWith(IEnumerable<T> other) => this.ExceptWith(other);

	// Accessible through an extension method.
	internal ValueSetBuilder<T> ExceptWithSpan(ReadOnlySpan<T> items)
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
	public ValueSetBuilder<T> SymmetricExceptWith(IEnumerable<T> other)
	{
		this.Mutate().SymmetricExceptWith(PreferHashSet(other));
		return this;
	}

	/// <inheritdoc/>
	void ISet<T>.SymmetricExceptWith(IEnumerable<T> other) => this.SymmetricExceptWith(other);

	/// <summary>
	/// Modify the current builder to contain only elements that are present in
	/// both <see langword="this"/> <em>and</em> the <paramref name="other"/>
	/// collection.
	/// </summary>
	public ValueSetBuilder<T> IntersectWith(IEnumerable<T> other)
	{
		this.Mutate().IntersectWith(PreferHashSet(other));
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
	public ValueSetBuilder<T> UnionWith(IEnumerable<T> other)
	{
		this.Mutate().UnionWith(other);
		return this;
	}

	/// <inheritdoc/>
	void ISet<T>.UnionWith(IEnumerable<T> other) => this.UnionWith(other);

	// Accessible through an extension method.
	internal ValueSetBuilder<T> UnionWithSpan(ReadOnlySpan<T> items)
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
	/// Returns an enumerator for this <see cref="ValueSetBuilder{T}"/>.
	///
	/// Typically, you don't need to manually call this method, but instead use
	/// the built-in <c>foreach</c> syntax.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public Enumerator GetEnumerator() => new Enumerator(this);

	/// <summary>
	/// Enumerator for <see cref="ValueSetBuilder{T}"/>.
	/// </summary>
#pragma warning disable CA1034 // Nested types should not be visible
#pragma warning disable CA1815 // Override equals and operator equals on value types
	public struct Enumerator : IEnumeratorLike<T>
	{
		private readonly ValueSetBuilder<T> builder;
		private readonly int version;
		private HashSet<T>.Enumerator enumerator;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal Enumerator(ValueSetBuilder<T> builder)
		{
			this.builder = builder;
			this.version = builder.version;
			this.enumerator = builder.items switch
			{
				HashSet<T> items => items.GetEnumerator(),
				ValueSet<T> items => items.Items.GetEnumerator(),
				_ => throw UnreachableException(),
			};
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

	private static InvalidOperationException UnreachableException() => new("Unreachable");
}
