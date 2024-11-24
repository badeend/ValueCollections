using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace Badeend.ValueCollections.Internals;

// Various parts of this type have been adapted from:
// https://github.com/dotnet/runtime/blob/4389f9c54d070ca5e0cf7c4931aff56fe36d667f/src/libraries/System.Private.CoreLib/src/System/Collections/Generic/RawSet.cs
[StructLayout(LayoutKind.Auto)]
internal struct RawSet<T> : IEquatable<RawSet<T>>
{
	/// <summary>
	/// When constructing a hashset from an existing collection, it may contain duplicates,
	/// so this is used as the max acceptable excess ratio of capacity to count. Note that
	/// this is only used on the ctor and not to automatically shrink if the hashset has, e.g,
	/// a lot of adds followed by removes. Users must explicitly shrink by calling TrimExcess.
	/// This is set to 3 because capacity is acceptable as 2x rounded up to nearest prime.
	/// </summary>
	private const int ShrinkThreshold = 3;
	private const int StartOfFreeList = -3;

	private int[]? buckets;
	private Entry[]? entries;
	private int count;
	private int freeList;
	private int freeCount;
	private int version;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public RawSet()
	{
	}

	internal RawSet(ref readonly RawSet<T> source)
	{
		if (source.Count == 0)
		{
			// As well as short-circuiting on the rest of the work done,
			// this avoids errors from trying to access source._buckets
			// or source._entries when they aren't initialized.
			return;
		}

		var capacity = source.buckets!.Length;
		var threshold = HashHelpers.ExpandPrime(source.Count + 1);

		if (threshold >= capacity)
		{
			this.buckets = (int[])source.buckets.Clone();
			this.entries = (Entry[])source.entries!.Clone();
			this.freeList = source.freeList;
			this.freeCount = source.freeCount;
			this.count = source.count;
		}
		else
		{
			this.Initialize(source.Count);

			var entries = source.entries;
			for (int i = 0; i < source.count; i++)
			{
				ref Entry entry = ref entries![i];
				if (entry.Next >= -1)
				{
					this.AddIfNotPresent(entry.Value, out _);
				}
			}
		}

		Debug.Assert(this.Count == source.Count);
	}

	internal RawSet(ReadOnlySpan<T> source)
	{
		if (source.Length == 0)
		{
			// As well as short-circuiting on the rest of the work done,
			// this avoids errors from trying to access source._buckets
			// or source._entries when they aren't initialized.
			return;
		}

		// To avoid excess resizes, first set size based on collection's count. The collection may
		// contain duplicates, so call TrimExcess if resulting RawSet is larger than the threshold.
		this.Initialize(source.Length);

		foreach (T item in source)
		{
			this.AddIfNotPresent(item, out _);
		}

		if (this.count > 0 && this.entries!.Length / this.count > ShrinkThreshold)
		{
			this.TrimExcess();
		}
	}

	internal RawSet(IEnumerable<T> source)
	{
		if (source is null)
		{
			ThrowHelpers.ThrowArgumentNullException(ThrowHelpers.Argument.collection);
		}

		// To avoid excess resizes, first set size based on collection's count. The collection may
		// contain duplicates, so call TrimExcess if resulting RawSet is larger than the threshold.
		if (source is ICollection<T> collection)
		{
			var count = collection.Count;
			if (count > 0)
			{
				this.Initialize(count);
			}
		}

		foreach (T item in source)
		{
			this.AddIfNotPresent(item, out _);
		}

		if (this.count > 0 && this.entries!.Length / this.count > ShrinkThreshold)
		{
			this.TrimExcess();
		}
	}

	internal RawSet(int minimumCapacity)
	{
		if (minimumCapacity < 0)
		{
			ThrowHelpers.ThrowArgumentOutOfRangeException(ThrowHelpers.Argument.minimumCapacity);
		}

		if (minimumCapacity > 0)
		{
			this.Initialize(minimumCapacity);
		}
	}

	internal void Clear()
	{
		var count = this.count;
		if (count == 0)
		{
			return;
		}

		Debug.Assert(this.buckets != null, "_buckets should be non-null");
		Debug.Assert(this.entries != null, "_entries should be non-null");

#if NET6_0_OR_GREATER
		Array.Clear(this.buckets);
#else
		Array.Clear(this.buckets, 0, this.buckets!.Length);
#endif
		this.count = 0;
		this.freeList = -1;
		this.freeCount = 0;
		Array.Clear(this.entries, 0, count);
	}

	internal readonly bool Contains(T item) => this.FindItemIndex(item) >= 0;

	private readonly int FindItemIndex(T item)
	{
		var buckets = this.buckets;
		if (buckets is null)
		{
			return -1;
		}

		var entries = this.entries;
		Debug.Assert(entries != null, "Expected _entries to be initialized");

		uint collisionCount = 0;
		var comparer = new DefaultEqualityComparer<T>();

		var hashCode = comparer.GetHashCode(item);
		var i = this.GetBucketRef(hashCode) - 1; // Value in _buckets is 1-based
		while (i >= 0)
		{
			ref Entry entry = ref entries![i];
			if (entry.HashCode == hashCode && comparer.Equals(entry.Value, item))
			{
				return i;
			}

			i = entry.Next;

			collisionCount++;
			if (collisionCount > (uint)entries!.Length)
			{
				// The chain of entries forms a loop, which means a concurrent update has happened.
				ThrowHelpers.ThrowInvalidOperationException_ConcurrentOperationsNotSupported();
			}
		}

		return -1;
	}

	/// <summary>Gets a reference to the specified hashcode's bucket, containing an index into <see cref="entries"/>.</summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private readonly ref int GetBucketRef(int hashCode)
	{
		var buckets = this.buckets!;
		return ref buckets[(uint)hashCode % (uint)buckets.Length];
	}

	internal bool Remove(T item)
	{
		if (this.buckets is null)
		{
			return false;
		}

		var entries = this.entries;
		Debug.Assert(entries != null, "entries should be non-null");

		uint collisionCount = 0;
		var last = -1;

		var comparer = new DefaultEqualityComparer<T>();
		var hashCode = comparer.GetHashCode(item);

		ref int bucket = ref this.GetBucketRef(hashCode);
		var i = bucket - 1; // Value in buckets is 1-based

		while (i >= 0)
		{
			ref Entry entry = ref entries![i];

			if (entry.HashCode == hashCode && comparer.Equals(entry.Value, item))
			{
				if (last < 0)
				{
					bucket = entry.Next + 1; // Value in buckets is 1-based
				}
				else
				{
					entries![last].Next = entry.Next;
				}

				Debug.Assert((StartOfFreeList - this.freeList) < 0, "shouldn't underflow because max hashtable length is MaxPrimeArrayLength = 0x7FEFFFFD(2146435069) _freelist underflow threshold 2147483646");
				entry.Next = StartOfFreeList - this.freeList;

				if (Polyfills.IsReferenceOrContainsReferences<T>())
				{
					entry.Value = default!;
				}

				this.freeList = i;
				this.freeCount++;
				return true;
			}

			last = i;
			i = entry.Next;

			collisionCount++;
			if (collisionCount > (uint)entries!.Length)
			{
				// The chain of entries forms a loop; which means a concurrent update has happened.
				// Break out of the loop and throw, rather than looping forever.
				ThrowHelpers.ThrowInvalidOperationException_ConcurrentOperationsNotSupported();
			}
		}

		return false;
	}

	/// <summary>Gets the number of elements that are contained in the set.</summary>
	internal readonly int Count => this.count - this.freeCount;

	/// <summary>
	/// Gets the total numbers of elements the internal data structure can hold without resizing.
	/// </summary>
	internal readonly int Capacity => this.entries?.Length ?? 0;

	public readonly Enumerator GetEnumerator() => new Enumerator(this);

	internal bool Add(T item) => this.AddIfNotPresent(item, out _);

	internal readonly bool TryGetValue(T equalValue, [MaybeNullWhen(false)] out T actualValue)
	{
		if (this.buckets is not null)
		{
			var index = this.FindItemIndex(equalValue);
			if (index >= 0)
			{
				actualValue = this.entries![index].Value;
				return true;
			}
		}

		actualValue = default;
		return false;
	}

	internal void UnionWith(ref readonly RawSet<T> other)
	{
		// Unioning a set with itself is the same set.
		if (this.entries == other.entries)
		{
			return;
		}

		foreach (T item in other)
		{
			this.AddIfNotPresent(item, out _);
		}
	}

	internal void UnionWith(ReadOnlySpan<T> other)
	{
		foreach (T item in other)
		{
			this.AddIfNotPresent(item, out _);
		}
	}

	internal bool TryUnionWithNonEnumerated(IEnumerable<T> other)
	{
		if (other is null)
		{
			ThrowHelpers.ThrowArgumentNullException(ThrowHelpers.Argument.other);
		}

		if (other is ICollection<T> otherCollection)
		{
			if (otherCollection.Count == 0)
			{
				// Nothing to add.
				return true;
			}

			// Special case for HashSet as that's the most commonly used set type
			// in .NET, has the same equality semantics as us and is guaranteed
			// to not mutate `this` during enumeration.
			if (other is HashSet<T> otherHashSet && otherHashSet.Comparer == EqualityComparer<T>.Default)
			{
				foreach (var element in otherHashSet)
				{
					this.AddIfNotPresent(element, out _);
				}

				return true;
			}
		}

		return false;
	}

	// Does not check/guard against concurrent mutation during enumeration!
	internal void UnionWith(IEnumerable<T> other)
	{
		if (!this.TryUnionWithNonEnumerated(other))
		{
			foreach (T item in other)
			{
				this.AddIfNotPresent(item, out _);
			}
		}
	}

	internal void IntersectWith(ref readonly RawSet<T> other)
	{
		// Same if the set intersecting with itself is the same set.
		if (this.entries == other.entries)
		{
			return;
		}

		// Intersection of anything with empty set is empty set.
		if (this.Count == 0 || other.Count == 0)
		{
			this.Clear();
			return;
		}

		Entry[]? entries = this.entries;
		for (int i = 0; i < this.count; i++)
		{
			ref Entry entry = ref entries![i];
			if (entry.Next >= -1)
			{
				T item = entry.Value;
				if (!other.Contains(item))
				{
					this.Remove(item);
				}
			}
		}
	}

	internal bool TryIntersectWithNonEnumerated(IEnumerable<T> other)
	{
		if (other == null)
		{
			ThrowHelpers.ThrowArgumentNullException(ThrowHelpers.Argument.other);
		}

		// Intersection of anything with empty set is empty set.
		if (this.Count == 0)
		{
			return true;
		}

		// If other is known to be empty, intersection is empty set; remove all elements, and we're done.
		if (other is ICollection<T> otherAsCollection)
		{
			// If other is known to be empty, intersection is empty set; remove all elements, and we're done.
			if (otherAsCollection.Count == 0)
			{
				this.Clear();
				return true;
			}

			// Special case for HashSet as that's the most commonly used set type
			// in .NET, has the same equality semantics as us and is guaranteed
			// to not mutate `this` during enumeration.
			if (other is HashSet<T> otherHashSet && otherHashSet.Comparer == EqualityComparer<T>.Default)
			{
				this.IntersectWithHashSetWithSameComparer(otherHashSet);
				return true;
			}
		}

		return false;
	}

	/// <summary>
	/// If other is a hashset that uses same equality comparer, intersect is much faster
	/// because we can use other's Contains.
	/// </summary>
	private void IntersectWithHashSetWithSameComparer(HashSet<T> other)
	{
		var entries = this.entries;
		for (int i = 0; i < this.count; i++)
		{
			ref Entry entry = ref entries![i];
			if (entry.Next >= -1)
			{
				T item = entry.Value;
				if (!other.Contains(item))
				{
					this.Remove(item);
				}
			}
		}
	}

	internal void ExceptWith(ref readonly RawSet<T> other)
	{
		// This is already the empty set; return.
		if (this.Count == 0)
		{
			return;
		}

		// Special case if other is this; a set minus itself is the empty set.
		if (this.entries == other.entries)
		{
			this.Clear();
			return;
		}

		// Remove every element in other from this.
		foreach (T element in other)
		{
			this.Remove(element);
		}
	}

	internal void ExceptWith(ReadOnlySpan<T> other)
	{
		// This is already the empty set; return.
		if (this.Count == 0)
		{
			return;
		}

		// Remove every element in other from this.
		foreach (T element in other)
		{
			this.Remove(element);
		}
	}

	internal bool TryExceptWithNonEnumerated(IEnumerable<T> other)
	{
		if (other is null)
		{
			ThrowHelpers.ThrowArgumentNullException(ThrowHelpers.Argument.other);
		}

		// This is already the empty set; return.
		if (this.Count == 0)
		{
			return true;
		}

		if (other is ICollection<T> otherCollection)
		{
			if (otherCollection.Count == 0)
			{
				// Nothing to remove.
				return true;
			}

			// Special case for HashSet as that's the most commonly used set type
			// in .NET, has the same equality semantics as us and is guaranteed
			// to not mutate `this` during enumeration.
			if (other is HashSet<T> otherHashSet && otherHashSet.Comparer == EqualityComparer<T>.Default)
			{
				foreach (var element in otherHashSet)
				{
					this.Remove(element);
				}

				return true;
			}
		}

		return false;
	}

	// Does not check/guard against concurrent mutation during enumeration!
	internal void ExceptWith(IEnumerable<T> other)
	{
		if (!this.TryExceptWithNonEnumerated(other))
		{
			foreach (T element in other)
			{
				this.Remove(element);
			}
		}
	}

	internal void SymmetricExceptWith(ref readonly RawSet<T> other)
	{
		// If set is empty, then symmetric difference is other.
		if (this.Count == 0)
		{
			this.UnionWith(in other);
			return;
		}

		// Special-case this; the symmetric difference of a set with itself is the empty set.
		if (this.entries == other.entries)
		{
			this.Clear();
			return;
		}

		foreach (T item in other)
		{
			if (!this.Remove(item))
			{
				this.AddIfNotPresent(item, out _);
			}
		}
	}

	internal void SymmetricExceptWith(ReadOnlySpan<T> other)
	{
		// If set is empty, then symmetric difference is other.
		if (this.Count == 0)
		{
			this.UnionWith(other);
			return;
		}

		foreach (T item in other)
		{
			if (!this.Remove(item))
			{
				this.AddIfNotPresent(item, out _);
			}
		}
	}

	internal bool TrySymmetricExceptWithNonEnumerated(IEnumerable<T> other)
	{
		if (other is null)
		{
			ThrowHelpers.ThrowArgumentNullException(ThrowHelpers.Argument.other);
		}

		// If set is empty, then symmetric difference is other.
		if (this.Count == 0)
		{
			return this.TryUnionWithNonEnumerated(other);
		}

		if (other is ICollection<T> otherCollection)
		{
			if (otherCollection.Count == 0)
			{
				// Other set is empty. The symmetric difference is the current set.
				return true;
			}

			// Special case for HashSet as that's the most commonly used set type
			// in .NET, has the same equality semantics as us and is guaranteed
			// to not mutate `this` during enumeration.
			if (other is HashSet<T> otherHashSet && otherHashSet.Comparer == EqualityComparer<T>.Default)
			{
				foreach (T item in otherHashSet)
				{
					if (!this.Remove(item))
					{
						this.AddIfNotPresent(item, out _);
					}
				}

				return true;
			}
		}

		return false;
	}

	internal readonly bool IsSubsetOf(ref readonly RawSet<T> other)
	{
		// The empty set is a subset of any set, and a set is a subset of itself.
		// Set is always a subset of itself.
		if (this.Count == 0 || this.entries == other.entries)
		{
			return true;
		}

		// If this has more elements then it can't be a subset.
		if (this.Count > other.Count)
		{
			return false;
		}

		return this.IsSubsetOf_Common(in other);
	}

	private readonly bool IsSubsetOf_Common(ref readonly RawSet<T> other)
	{
		foreach (T item in this)
		{
			if (!other.Contains(item))
			{
				return false;
			}
		}

		return true;
	}

	private readonly bool IsSubsetOf_Common(HashSet<T> other)
	{
		foreach (T item in this)
		{
			if (!other.Contains(item))
			{
				return false;
			}
		}

		return true;
	}

	private readonly bool IsSupersetOf_Common(HashSet<T> other)
	{
		foreach (T item in other)
		{
			if (!this.Contains(item))
			{
				return false;
			}
		}

		return true;
	}

	internal readonly bool IsSubsetOf(IEnumerable<T> other)
	{
		if (other is null)
		{
			ThrowHelpers.ThrowArgumentNullException(ThrowHelpers.Argument.other);
		}

		// The empty set is a subset of any set, and a set is a subset of itself.
		if (this.Count == 0)
		{
			return true;
		}

		if (other is ICollection<T> otherAsCollection)
		{
			// If this has more elements then it can't be a subset.
			if (this.Count > otherAsCollection.Count)
			{
				return false;
			}

			// Special case for HashSet as that's the most commonly used set type
			// in .NET, has the same equality semantics as us and is guaranteed
			// to not mutate `this` during enumeration.
			if (other is HashSet<T> otherHashSet && otherHashSet.Comparer == EqualityComparer<T>.Default)
			{
				return this.IsSubsetOf_Common(otherHashSet);
			}
		}

		// Fall back to creating an intermediate heap copy (sigh...)
		// Note that enumerating `other` might trigger mutations on `this`.
		var copy = new RawSet<T>(other);

		return this.IsSubsetOf(ref copy);
	}

	internal readonly bool IsProperSubsetOf(ref readonly RawSet<T> other)
	{
		// No set is a proper subset of itself.
		if (this.entries == other.entries)
		{
			return false;
		}

		// No set is a proper subset of a set with less or equal number of elements.
		if (other.Count <= this.Count)
		{
			return false;
		}

		// The empty set is a proper subset of anything but the empty set.
		if (this.Count == 0)
		{
			// Based on check above, other is not empty when Count == 0.
			return true;
		}

		// This has strictly less than number of items in other, so the following
		// check suffices for proper subset.
		return this.IsSubsetOf_Common(in other);
	}

	internal readonly bool IsProperSubsetOf(IEnumerable<T> other)
	{
		if (other is null)
		{
			ThrowHelpers.ThrowArgumentNullException(ThrowHelpers.Argument.other);
		}

		if (other is ICollection<T> otherAsCollection)
		{
			// No set is a proper subset of a set with less or equal number of elements.
			if (otherAsCollection.Count <= this.Count)
			{
				return false;
			}

			// The empty set is a proper subset of anything but the empty set.
			if (this.Count == 0)
			{
				// Based on check above, other is not empty when Count == 0.
				return true;
			}

			// Special case for HashSet as that's the most commonly used set type
			// in .NET, has the same equality semantics as us and is guaranteed
			// to not mutate `this` during enumeration.
			if (other is HashSet<T> otherHashSet && otherHashSet.Comparer == EqualityComparer<T>.Default)
			{
				// This has strictly less than number of items in other, so the following
				// check suffices for proper subset.
				return this.IsSubsetOf_Common(otherHashSet);
			}
		}

		// Fall back to creating an intermediate heap copy (sigh...)
		// Note that enumerating `other` might trigger mutations on `this`.
		var copy = new RawSet<T>(other);

		return this.IsProperSubsetOf(ref copy);
	}

	internal readonly bool IsSupersetOf(ref readonly RawSet<T> other)
	{
		// A set is always a superset of itself.
		if (this.entries == other.entries)
		{
			return true;
		}

		// If other is the empty set then this is a superset.
		if (other.Count == 0)
		{
			return true;
		}

		// Try to compare based on counts alone if other is a hashset with same equality comparer.
		if (other.Count > this.Count)
		{
			return false;
		}

		foreach (var element in other)
		{
			if (!this.Contains(element))
			{
				return false;
			}
		}

		return true;
	}

	internal readonly bool IsSupersetOf(IEnumerable<T> other)
	{
		if (other is null)
		{
			ThrowHelpers.ThrowArgumentNullException(ThrowHelpers.Argument.other);
		}

		// Try to fall out early based on counts.
		if (other is ICollection<T> otherAsCollection)
		{
			// If other is the empty set then this is a superset.
			if (otherAsCollection.Count == 0)
			{
				return true;
			}

			// Special case for HashSet as that's the most commonly used set type
			// in .NET, has the same equality semantics as us and is guaranteed
			// to not mutate `this` during enumeration.
			if (other is HashSet<T> otherHashSet && otherHashSet.Comparer == EqualityComparer<T>.Default)
			{
				if (otherHashSet.Count > this.Count)
				{
					return false;
				}

				return this.IsSupersetOf_Common(otherHashSet);
			}
		}

		// Note that enumerating `other` might trigger mutations on `this`.
		foreach (T element in other)
		{
			if (!this.Contains(element))
			{
				return false;
			}
		}

		return true;
	}

	internal readonly bool IsProperSupersetOf(ref readonly RawSet<T> other)
	{
		// The empty set isn't a proper superset of any set, and a set is never a strict superset of itself.
		if (this.Count == 0 || this.entries == other.entries)
		{
			return false;
		}

		// If other is the empty set then this is a superset.
		if (other.Count == 0)
		{
			// Note that this has at least one element, based on above check.
			return true;
		}

		if (other.Count >= this.Count)
		{
			return false;
		}

		// Now perform element check.
		return other.IsSubsetOf_Common(in this);
	}

	internal readonly bool IsProperSupersetOf(IEnumerable<T> other)
	{
		if (other is null)
		{
			ThrowHelpers.ThrowArgumentNullException(ThrowHelpers.Argument.other);
		}

		// The empty set isn't a proper superset of any set.
		if (this.Count == 0)
		{
			return false;
		}

		if (other is ICollection<T> otherAsCollection)
		{
			// If other is the empty set then this is a superset.
			if (otherAsCollection.Count == 0)
			{
				// Note that this has at least one element, based on above check.
				return true;
			}

			// Special case for HashSet as that's the most commonly used set type
			// in .NET, has the same equality semantics as us and is guaranteed
			// to not mutate `this` during enumeration.
			if (other is HashSet<T> otherHashSet && otherHashSet.Comparer == EqualityComparer<T>.Default)
			{
				if (otherHashSet.Count >= this.Count)
				{
					return false;
				}

				return this.IsSupersetOf_Common(otherHashSet);
			}
		}

		// Fall back to creating an intermediate heap copy (sigh...)
		// Note that enumerating `other` might trigger mutations on `this`.
		var copy = new RawSet<T>(other);

		return this.IsProperSupersetOf(ref copy);
	}

	internal readonly bool Overlaps(ref readonly RawSet<T> other)
	{
		if (this.Count == 0)
		{
			return false;
		}

		// Set overlaps itself
		if (this.entries == other.entries)
		{
			return true;
		}

		foreach (T element in other)
		{
			if (this.Contains(element))
			{
				return true;
			}
		}

		return false;
	}

	internal readonly bool Overlaps(ReadOnlySpan<T> other)
	{
		if (this.Count == 0)
		{
			return false;
		}

		foreach (T element in other)
		{
			if (this.Contains(element))
			{
				return true;
			}
		}

		return false;
	}

	internal readonly bool Overlaps(IEnumerable<T> other)
	{
		if (other is null)
		{
			ThrowHelpers.ThrowArgumentNullException(ThrowHelpers.Argument.other);
		}

		if (this.Count == 0)
		{
			return false;
		}

		foreach (T element in other)
		{
			if (this.Contains(element))
			{
				return true;
			}
		}

		return false;
	}

	internal readonly bool SetEquals(ref readonly RawSet<T> other)
	{
		// A set is equal to itself.
		if (this.entries == other.entries)
		{
			return true;
		}

		// Attempt to return early: since both contain unique elements, if they have
		// different counts, then they can't be equal.
		if (this.Count != other.Count)
		{
			return false;
		}

		// Already confirmed that the sets have the same number of distinct elements, so if
		// one is a subset of the other then they must be equal.
		return this.IsSubsetOf_Common(in other);
	}

	internal readonly bool SetEquals(IEnumerable<T> other)
	{
		if (other is null)
		{
			ThrowHelpers.ThrowArgumentNullException(ThrowHelpers.Argument.other);
		}

		if (other is ICollection<T> otherAsCollection)
		{
			// If this is empty, they are equal iff other is empty.
			if (this.Count == 0)
			{
				return otherAsCollection.Count == 0;
			}

			// Special case for HashSet as that's the most commonly used set type
			// in .NET, has the same equality semantics as us and is guaranteed
			// to not mutate `this` during enumeration.
			if (other is HashSet<T> otherHashSet && otherHashSet.Comparer == EqualityComparer<T>.Default)
			{
				// Attempt to return early: since both contain unique elements, if they have
				// different counts, then they can't be equal.
				if (this.Count != otherHashSet.Count)
				{
					return false;
				}

				// Already confirmed that the sets have the same number of distinct elements, so if
				// one is a subset of the other then they must be equal.
				return this.IsSubsetOf_Common(otherHashSet);
			}

			// Can't be equal if other set contains fewer elements than this.
			if (this.Count > otherAsCollection.Count)
			{
				return false;
			}
		}

		// Fall back to creating an intermediate heap copy (sigh...)
		// Note that enumerating `other` might trigger mutations on `this`.
		var copy = new RawSet<T>(other);

		return this.SetEquals(ref copy);
	}

	internal readonly void CopyTo(Span<T> destination)
	{
		if (this.Count > destination.Length)
		{
			ThrowHelpers.ThrowArgumentException_DestinationTooShort(ThrowHelpers.Argument.destination);
		}

		var index = 0;
		foreach (var item in this)
		{
			destination[index] = item;
			index++;
		}
	}

	internal int RemoveWhere(Predicate<T> match)
	{
		if (match is null)
		{
			ThrowHelpers.ThrowArgumentNullException(ThrowHelpers.Argument.match);
		}

		// Beware that `match` could mutate this collection as we iterate over it.
		Entry[]? entries = this.entries;
		int numRemoved = 0;
		for (int i = 0; i < this.count; i++)
		{
			ref Entry entry = ref entries![i];
			if (entry.Next >= -1)
			{
				// Cache value in case delegate removes it
				T value = entry.Value;
				if (match(value))
				{
					// Check again that remove actually removed it.
					if (!this.Remove(value))
					{
						ThrowHelpers.ThrowInvalidOperationException_ConcurrentOperationsNotSupported();
					}

					numRemoved++;
				}
			}
		}

		return numRemoved;
	}

	internal int EnsureCapacity(int minimumCapacity)
	{
		if (minimumCapacity < 0)
		{
			ThrowHelpers.ThrowArgumentOutOfRangeException(ThrowHelpers.Argument.minimumCapacity);
		}

		int currentCapacity = this.entries is null ? 0 : this.entries.Length;
		if (currentCapacity >= minimumCapacity)
		{
			return currentCapacity;
		}

		if (this.buckets is null)
		{
			return this.Initialize(minimumCapacity);
		}

		int newSize = HashHelpers.GetPrime(minimumCapacity);
		this.Resize(newSize);
		return newSize;
	}

	private void Resize() => this.Resize(HashHelpers.ExpandPrime(this.count));

	private void Resize(int newSize)
	{
		Debug.Assert(this.entries != null, "_entries should be non-null");
		Debug.Assert(newSize >= this.entries!.Length);

		var entries = new Entry[newSize];

		int count = this.count;
		Array.Copy(this.entries!, entries, count);

		// Assign member variables after both arrays allocated to guard against corruption from OOM if second fails
		this.buckets = new int[newSize];
		for (int i = 0; i < count; i++)
		{
			ref Entry entry = ref entries[i];
			if (entry.Next >= -1)
			{
				ref int bucket = ref this.GetBucketRef(entry.HashCode);
				entry.Next = bucket - 1; // Value in _buckets is 1-based
				bucket = i + 1;
			}
		}

		this.entries = entries;
	}

	internal void TrimExcess() => this.TrimExcess(this.Count);

	internal void TrimExcess(int targetCapacity)
	{
		if (targetCapacity < this.Count)
		{
			ThrowHelpers.ThrowArgumentOutOfRangeException(ThrowHelpers.Argument.targetCapacity);
		}

		var newSize = HashHelpers.GetPrime(targetCapacity);
		var oldEntries = this.entries;
		var currentCapacity = oldEntries is null ? 0 : oldEntries.Length;
		if (newSize >= currentCapacity)
		{
			return;
		}

		var oldCount = this.count;
		this.version++;
		this.Initialize(newSize);
		var entries = this.entries;
		var count = 0;
		for (int i = 0; i < oldCount; i++)
		{
			var hashCode = oldEntries![i].HashCode; // At this point, we know we have entries.
			if (oldEntries[i].Next >= -1)
			{
				ref Entry entry = ref entries![count];
				entry = oldEntries[i];
				ref int bucket = ref this.GetBucketRef(hashCode);
				entry.Next = bucket - 1; // Value in _buckets is 1-based
				bucket = count + 1;
				count++;
			}
		}

		this.count = count;
		this.freeCount = 0;
	}

	/// <summary>
	/// Initializes buckets and slots arrays. Uses suggested capacity by finding next prime
	/// greater than or equal to capacity.
	/// </summary>
	private int Initialize(int capacity)
	{
		int size = HashHelpers.GetPrime(capacity);
		var buckets = new int[size];
		var entries = new Entry[size];

		// Assign member variables after both arrays are allocated to guard against corruption from OOM if second fails.
		this.freeList = -1;
		this.buckets = buckets;
		this.entries = entries;

		return size;
	}

	/// <summary>Adds the specified element to the set if it's not already contained.</summary>
	/// <param name="value">The element to add to the set.</param>
	/// <param name="location">The index into <see cref="entries"/> of the element.</param>
	/// <returns>true if the element is added to the <see cref="RawSet{T}"/> object; false if the element is already present.</returns>
	private bool AddIfNotPresent(T value, out int location)
	{
		if (this.buckets is null)
		{
			this.Initialize(0);
		}

		Debug.Assert(this.buckets != null);

		Entry[]? entries = this.entries;
		Debug.Assert(entries != null, "expected entries to be non-null");

		var comparer = new DefaultEqualityComparer<T>();

		uint collisionCount = 0;

		var hashCode = comparer.GetHashCode(value);
		ref int bucket = ref this.GetBucketRef(hashCode);
		int i = bucket - 1; // Value in _buckets is 1-based
		while (i >= 0)
		{
			ref Entry entry = ref entries![i];
			if (entry.HashCode == hashCode && comparer.Equals(entry.Value, value))
			{
				location = i;
				return false;
			}

			i = entry.Next;

			collisionCount++;
			if (collisionCount > (uint)entries!.Length)
			{
				// The chain of entries forms a loop, which means a concurrent update has happened.
				ThrowHelpers.ThrowInvalidOperationException_ConcurrentOperationsNotSupported();
			}
		}

		int index;
		if (this.freeCount > 0)
		{
			index = this.freeList;
			this.freeCount--;
			Debug.Assert((StartOfFreeList - entries![this.freeList].Next) >= -1, "shouldn't overflow because `next` cannot underflow");
			this.freeList = StartOfFreeList - entries![this.freeList].Next;
		}
		else
		{
			int count = this.count;
			if (count == entries!.Length)
			{
				this.Resize();
				bucket = ref this.GetBucketRef(hashCode);
			}

			index = count;
			this.count = count + 1;
			entries = this.entries;
		}

		{
			ref Entry entry = ref entries![index];
			entry.HashCode = hashCode;
			entry.Next = bucket - 1; // Value in _buckets is 1-based
			entry.Value = value;
			bucket = index + 1;
			this.version++;
			location = index;
		}

		return true;
	}

	private struct Entry
	{
		internal int HashCode;

		/// <summary>
		/// 0-based index of next entry in chain: -1 means end of chain
		/// also encodes whether this entry _itself_ is part of the free list by changing sign and subtracting 3,
		/// so -2 means end of free list, -3 means index 0 but on free list, -4 means index 1 but on free list, etc.
		/// </summary>
		internal int Next;
		internal T Value;
	}

	[StructLayout(LayoutKind.Auto)]
	public struct Enumerator : IRefEnumeratorLike<T>
	{
		private readonly Entry[]? entries;
		private readonly int count;
		private int index;

		internal Enumerator(RawSet<T> set)
		{
			this.entries = set.entries;
			this.count = set.count;
			this.index = -1;
		}

		/// <inheritdoc/>
		public readonly ref readonly T Current
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => ref this.entries![this.index].Value;
		}

		/// <inheritdoc/>
		readonly T IEnumeratorLike<T>.Current => this.Current;

		public bool MoveNext()
		{
			while ((uint)++this.index < (uint)this.count)
			{
				if (this.entries![this.index].Next >= -1)
				{
					return true;
				}
			}

			this.index = this.count;
			return false;
		}
	}

	/// <inheritdoc/>
	[Pure]
	[EditorBrowsable(EditorBrowsableState.Never)]
	public readonly override int GetHashCode() => this.entries?.GetHashCode() ?? 0;

	[Pure]
	[EditorBrowsable(EditorBrowsableState.Never)]
	public readonly bool Equals(ref readonly RawSet<T> other) => object.ReferenceEquals(this.entries, other.entries);

	/// <inheritdoc/>
	readonly bool IEquatable<RawSet<T>>.Equals(RawSet<T> other) => object.ReferenceEquals(this.entries, other.entries);

#pragma warning disable CS0809 // Obsolete member overrides non-obsolete member
	/// <inheritdoc/>
	[Pure]
	[Obsolete("Use == instead.")]
	[EditorBrowsable(EditorBrowsableState.Never)]
	public readonly override bool Equals(object? obj) => obj is RawSet<T> other && this.Equals(in other);
#pragma warning restore CS0809 // Obsolete member overrides non-obsolete member

	/// <summary>
	/// Get a string representation of the collection for debugging purposes.
	/// The format is not stable and may change without prior notice.
	/// </summary>
	[Pure]
	public readonly override string ToString()
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

internal static class RawSet
{
	[Pure]
	internal static int GetSequenceHashCode<T>(this ref readonly RawSet<T> set)
	{
		var contentHasher = new UnorderedHashCode();

		foreach (var item in set)
		{
			contentHasher.Add(item);
		}

		var hasher = new HashCode();
		hasher.Add(typeof(ValueSet<T>));
		hasher.Add(set.Count);
		hasher.AddUnordered(ref contentHasher);

		return hasher.ToHashCode();
	}
}
