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
//
// This implements an Hash Table with Separate Chaining (https://en.wikipedia.org/wiki/Hash_table#Separate_chaining)
[StructLayout(LayoutKind.Auto)]
internal struct RawSet<T> : IEquatable<RawSet<T>>
{
	private const int StartOfFreeList = -3;

	/// <summary>
	/// When constructing a hashset from an existing collection, it may contain duplicates,
	/// so this is used as the max acceptable excess ratio of capacity to count. Note that
	/// this is only used on the ctor and not to automatically shrink if the hashset has, e.g,
	/// a lot of adds followed by removes. Users must explicitly shrink by calling TrimExcess.
	/// This is set to 3 because capacity is acceptable as 2x rounded up to nearest prime.
	/// </summary>
	private const int ShrinkThreshold = 3;

	/// <summary>
	/// The _indexes_ into this `buckets` array are the modulo of the hash codes.
	/// The _values_ of this `buckets` array are indexes into the `entries` array.
	///
	/// The values are offset by 1. A value of 0 means the bucket is empty.
	///
	/// This is `null` for empty sets. `buckets` and `entries` are always of
	/// the same size, and are always null or not-null together.
	/// </summary>
	private int[]? buckets;

	/// <summary>
	/// The storage array of the hash set.
	///
	/// This is `null` for empty sets. `buckets` and `entries` are always of
	/// the same size, and are always null or not-null together.
	/// </summary>
	private Entry[]? entries;

	/// <summary>
	/// How many `entries` are initialized. Initialized entries may be either
	/// actively in use or serve as a free slot for future insertions.
	/// `entries` in the range [end..] are unused capacity.
	/// </summary>
	private int end;

	/// <summary>
	/// Index to the head of the "free" list, or -1 if there are no free
	/// entries (yet). This is always less than `end`.
	/// </summary>
	private int firstFreeIndex;

	/// <summary>
	/// How many `entries` in the range [..end] are NOT actively in use.
	/// </summary>
	/// <remarks>
	/// Whenever an item is removed from the set, its `Entry` continues to exists
	/// as a "free" slot for future insertions. This `freeCount` field
	/// is updated whenever such a slot becomes available or is reclaimed.
	/// </remarks>
	private int freeCount;

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
			this.firstFreeIndex = source.firstFreeIndex;
			this.freeCount = source.freeCount;
			this.end = source.end;
		}
		else
		{
			this.Initialize(source.Count);

			var entries = source.entries;
			for (int i = 0; i < source.end; i++)
			{
				ref Entry entry = ref entries![i];
				if (entry.Next >= -1)
				{
					this.AddIfNotPresent(entry.Value);
				}
			}
		}

		Debug.Assert(this.Count == source.Count);
	}

	internal RawSet(scoped ReadOnlySpan<T> source)
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
			this.AddIfNotPresent(item);
		}

		if (this.end > 0 && this.entries!.Length / this.end > ShrinkThreshold)
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
			this.AddIfNotPresent(item);
		}

		if (this.end > 0 && this.entries!.Length / this.end > ShrinkThreshold)
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
		var end = this.end;
		if (end == 0)
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
		this.end = 0;
		this.firstFreeIndex = -1;
		this.freeCount = 0;
		Array.Clear(this.entries, 0, end);
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
			Debug.Assert(i < this.end);

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
			Debug.Assert(i < this.end);
			ref Entry entry = ref entries![i];

			if (entry.HashCode == hashCode && comparer.Equals(entry.Value, item))
			{
				if (last < 0)
				{
					Debug.Assert(entry.Next < this.end);
					bucket = entry.Next + 1; // Value in buckets is 1-based
				}
				else
				{
					entries![last].Next = entry.Next;
				}

				Debug.Assert((StartOfFreeList - this.firstFreeIndex) < 0, "shouldn't underflow because max hashtable length is MaxPrimeArrayLength = 0x7FEFFFFD(2146435069) _freelist underflow threshold 2147483646");
				entry.Next = StartOfFreeList - this.firstFreeIndex;

				if (Polyfills.IsReferenceOrContainsReferences<T>())
				{
					entry.Value = default!;
				}

				this.firstFreeIndex = i;
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
	internal readonly int Count => this.end - this.freeCount;

	/// <summary>
	/// Gets the total numbers of elements the internal data structure can hold without resizing.
	/// </summary>
	internal readonly int Capacity => this.entries?.Length ?? 0;

	public readonly Enumerator GetEnumerator() => new Enumerator(this);

	internal bool Add(T item) => this.AddIfNotPresent(item);

	internal readonly bool TryGetValue(T equalValue, [MaybeNullWhen(false)] out T actualValue)
	{
		if (this.buckets is not null)
		{
			var index = this.FindItemIndex(equalValue);
			if (index >= 0)
			{
				Debug.Assert(index < this.end);

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
			this.AddIfNotPresent(item);
		}
	}

	internal void UnionWith(scoped ReadOnlySpan<T> other)
	{
		foreach (T item in other)
		{
			this.AddIfNotPresent(item);
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
					this.AddIfNotPresent(element);
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
				this.AddIfNotPresent(item);
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
		for (int i = 0; i < this.end; i++)
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
		for (int i = 0; i < this.end; i++)
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

	internal void ExceptWith(scoped ReadOnlySpan<T> other)
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
				this.AddIfNotPresent(item);
			}
		}
	}

	internal void SymmetricExceptWith(scoped ReadOnlySpan<T> other)
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
				this.AddIfNotPresent(item);
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
						this.AddIfNotPresent(item);
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
		var count = this.Count;
		if (count == 0)
		{
			return true;
		}

		if (other is ICollection<T> otherAsCollection)
		{
			// If this has more elements then it can't be a subset.
			if (count > otherAsCollection.Count)
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

		using var marker = new Marker(in this);

		// Note that enumerating `other` might trigger mutations on `this`.
		foreach (var item in other)
		{
			marker.Mark(item);
		}

		return marker.UnmarkedCount == 0;
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

		var count = this.Count;

		if (other is ICollection<T> otherAsCollection)
		{
			// No set is a proper subset of a set with less or equal number of elements.
			if (otherAsCollection.Count <= count)
			{
				return false;
			}

			// The empty set is a proper subset of anything but the empty set.
			if (count == 0)
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

		using var marker = new Marker(in this);

		var otherHasAdditionalItems = false;

		// Note that enumerating `other` might trigger mutations on `this`.
		foreach (var item in other)
		{
			if (!marker.Mark(item))
			{
				otherHasAdditionalItems = true;
			}
		}

		return marker.UnmarkedCount == 0 && otherHasAdditionalItems;
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

		// Try to compare based on counts alone.
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

	internal readonly bool IsSupersetOf(scoped ReadOnlySpan<T> other)
	{
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

		var count = this.Count;

		// The empty set isn't a proper superset of any set.
		if (count == 0)
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
				if (otherHashSet.Count >= count)
				{
					return false;
				}

				return this.IsSupersetOf_Common(otherHashSet);
			}
		}

		using var marker = new Marker(in this);

		// Note that enumerating `other` might trigger mutations on `this`.
		foreach (var item in other)
		{
			if (!marker.Mark(item))
			{
				return false;
			}
		}

		return marker.UnmarkedCount > 0;
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

	internal readonly bool Overlaps(scoped ReadOnlySpan<T> other)
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

		var count = this.Count;

		if (other is ICollection<T> otherAsCollection)
		{
			// If this is empty, they are equal iff other is empty.
			if (count == 0)
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
				if (count != otherHashSet.Count)
				{
					return false;
				}

				// Already confirmed that the sets have the same number of distinct elements, so if
				// one is a subset of the other then they must be equal.
				return this.IsSubsetOf_Common(otherHashSet);
			}

			// Can't be equal if other set contains fewer elements than this.
			if (count > otherAsCollection.Count)
			{
				return false;
			}
		}

		using var marker = new Marker(in this);

		// Note that enumerating `other` might trigger mutations on `this`.
		foreach (var item in other)
		{
			if (!marker.Mark(item))
			{
				return false;
			}
		}

		return marker.UnmarkedCount == 0;
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
		for (int i = 0; i < this.end; i++)
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

	private void Resize() => this.Resize(HashHelpers.ExpandPrime(this.end));

	private void Resize(int newCapacity)
	{
		Debug.Assert(this.entries != null, "_entries should be non-null");
		Debug.Assert(newCapacity >= this.entries!.Length);

		var entries = new Entry[newCapacity];

		int end = this.end;
		Array.Copy(this.entries!, entries, end);

		// Assign member variables after both arrays allocated to guard against corruption from OOM if second fails
		this.buckets = new int[newCapacity];
		for (int i = 0; i < end; i++)
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

		var newCapacity = HashHelpers.GetPrime(targetCapacity);
		var oldEntries = this.entries;
		var currentCapacity = oldEntries is null ? 0 : oldEntries.Length;
		if (newCapacity >= currentCapacity)
		{
			return;
		}

		var oldEnd = this.end;
		this.Initialize(newCapacity);
		var entries = this.entries;
		var count = 0;
		for (int i = 0; i < oldEnd; i++)
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

		this.end = count;
		this.freeCount = 0;
	}

	/// <summary>
	/// Initializes buckets and slots arrays. Uses suggested capacity by finding next prime
	/// greater than or equal to capacity.
	/// </summary>
	private int Initialize(int minimumCapacity)
	{
		int capacity = HashHelpers.GetPrime(minimumCapacity);
		var buckets = new int[capacity];
		var entries = new Entry[capacity];

		// Assign member variables after both arrays are allocated to guard against corruption from OOM if second fails.
		this.firstFreeIndex = -1;
		this.buckets = buckets;
		this.entries = entries;

		return capacity;
	}

	/// <summary>Adds the specified element to the set if it's not already contained.</summary>
	/// <param name="value">The element to add to the set.</param>
	/// <returns>true if the element is added to the <see cref="RawSet{T}"/> object; false if the element is already present.</returns>
	private bool AddIfNotPresent(T value)
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
			Debug.Assert(i < this.end);

			ref Entry entry = ref entries![i];
			if (entry.HashCode == hashCode && comparer.Equals(entry.Value, value))
			{
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
			index = this.firstFreeIndex;
			this.freeCount--;
			Debug.Assert((StartOfFreeList - entries![this.firstFreeIndex].Next) >= -1, "shouldn't overflow because `next` cannot underflow");
			this.firstFreeIndex = StartOfFreeList - entries![this.firstFreeIndex].Next;
		}
		else
		{
			int end = this.end;
			if (end == entries!.Length)
			{
				this.Resize();
				bucket = ref this.GetBucketRef(hashCode);
			}

			index = end;
			this.end = end + 1;
			entries = this.entries;
		}

		{
			Debug.Assert(index < this.end);

			ref Entry entry = ref entries![index];
			entry.HashCode = hashCode;
			entry.Next = bucket - 1; // Value in _buckets is 1-based
			entry.Value = value;
			bucket = index + 1;
		}

		return true;
	}

	internal ref struct Marker
	{
		private readonly RawSet<T> set;
		private readonly bool[]? marks;
		private int unmarked;

		/// <summary>
		/// How many elements in the set have not been marked yet.
		/// </summary>
		public readonly int UnmarkedCount
		{
			get
			{
				Debug.Assert(this.unmarked >= 0);

				return this.unmarked;
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Marker(ref readonly RawSet<T> set)
		{
			this.set = set;
			this.unmarked = set.Count;

			var end = set.end;
			if (end > 0)
			{
				this.marks = new bool[end];
			}
		}

		// The RawSet may not be mutated in between calls to Mark.
		public bool Mark(T item)
		{
			var marks = this.marks;
			if (marks is null)
			{
				return false;
			}

			int index = this.set.FindItemIndex(item);
			if (index < 0)
			{
				return false;
			}

			if (!marks[index])
			{
				marks[index] = true;
				this.unmarked--;
			}

			return true;
		}

#pragma warning disable CA1822 // Mark members as static
		public void Dispose()
		{
		}
#pragma warning restore CA1822 // Mark members as static
	}

	private readonly int GetArbitraryIndex()
	{
		// Use the hashcode of the backing `entries` array as a semi-random seed.
		// All we care about is educating developers that the order can't be
		// trusted. It doesn't have to cryptographically secure or anything.
		// The hashcode doesn't change over the lifetime of the array
		// so, while the order is _undefined_, it is _consistent_ across multiple
		// enumerations over the exact same instance.
		return this.end > 1 ? RuntimeHelpers.GetHashCode(this.entries) % this.end : 0;
	}

	private struct Entry
	{
		internal int HashCode;

		// Index of the next entry in the chain. This also doubles as an
		// indicator for whether or not this entry is "free" or actively in use.
		//
		// >=  0 : The entry is active, and the index points to the next item in the chain.
		// == -1 : The entry is active, and this is the last entry in the chain.
		// == -2 : The entry is free, and this is the last entry in the free chain.
		// <= -3 : The entry is free, and (after changing the sign and subtracting 3) this points to the next entry in the free list.
		internal int Next;
		internal T Value;
	}

	[StructLayout(LayoutKind.Auto)]
	public struct Enumerator : IRefEnumeratorLike<T>
	{
		private readonly Entry[]? entries;
		private readonly int end;
		private int counter;
		private int index;

		internal Enumerator(RawSet<T> set)
		{
			this.entries = set.entries;
			this.end = set.end;
			this.counter = 0;
			this.index = set.GetArbitraryIndex();
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
			while ((uint)++this.counter <= (uint)this.end)
			{
				if ((uint)++this.index >= (uint)this.end)
				{
					this.index -= this.end;
				}

				if (this.entries![this.index].Next >= -1)
				{
					return true;
				}
			}

			this.index = this.end;
			this.counter = this.end;
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
