using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace Badeend.ValueCollections.Internals;

// Various parts of this type have been adapted from:
// https://github.com/dotnet/runtime/blob/1622f514684d94a521bfb41c88a27079ad943ee7/src/libraries/System.Private.CoreLib/src/System/Collections/Generic/Dictionary.cs
//
// This implements an Hash Table with Separate Chaining (https://en.wikipedia.org/wiki/Hash_table#Separate_chaining)
[StructLayout(LayoutKind.Auto)]
internal struct RawDictionary<TKey, TValue>
	where TKey : notnull
{
	private const int StartOfFreeList = -3;

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

	public readonly int Count => this.end - this.freeCount;

	public readonly int Capacity => this.entries?.Length ?? 0;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public RawDictionary()
	{
	}

	public RawDictionary(int minimumCapacity)
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

	public RawDictionary(ref readonly RawDictionary<TKey, TValue> source)
	{
		if (source.Count == 0)
		{
			// Nothing to copy, all done
			return;
		}

		this.Initialize(source.Count);

		Polyfills.DebugAssert(source.entries is not null);
		Polyfills.DebugAssert(this.entries is not null);
		Polyfills.DebugAssert(this.entries.Length >= source.Count);
		Polyfills.DebugAssert(this.end == 0);

		Entry[] oldEntries = source.entries;

		this.CopyEntries(oldEntries, source.end);
	}

	public RawDictionary(scoped ReadOnlySpan<KeyValuePair<TKey, TValue>> source)
	{
		var length = source.Length;
		if (length == 0)
		{
			// Nothing to copy, all done
			return;
		}

		this.Initialize(length);

		foreach (var entry in source)
		{
			this.Add(entry.Key, entry.Value);
		}
	}

	public RawDictionary(IEnumerable<KeyValuePair<TKey, TValue>> source)
	{
		if (source == null)
		{
			ThrowHelpers.ThrowArgumentNullException(ThrowHelpers.Argument.source);
		}

		var minimumCapacity = (source as ICollection<KeyValuePair<TKey, TValue>>)?.Count ?? 0;
		if (minimumCapacity < 0)
		{
			ThrowHelpers.ThrowArgumentOutOfRangeException(ThrowHelpers.Argument.minimumCapacity);
		}

		if (minimumCapacity > 0)
		{
			this.Initialize(minimumCapacity);
		}

		foreach (var entry in source)
		{
			this.Add(entry.Key, entry.Value);
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void Add(TKey key, TValue value)
	{
		bool modified = this.TryInsert(key, value, InsertionBehavior.ThrowOnExisting);
		Polyfills.DebugAssert(modified); // If there was an existing key and the Add failed, an exception will already have been thrown.
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void Add(KeyValuePair<TKey, TValue> keyValuePair) => this.Add(keyValuePair.Key, keyValuePair.Value);

	// Does not check/guard against concurrent mutation during enumeration!
	public TValue GetOrAdd(TKey key, Func<TKey, TValue> valueFactory)
	{
		ref TValue valRef = ref this.FindValue(key);
		if (!Polyfills.IsNullRef(ref valRef))
		{
			return valRef;
		}

		var newValue = valueFactory(key);
		this.Add(key, newValue);
		return newValue;
	}

	public void AddRange(ref readonly RawDictionary<TKey, TValue> items)
	{
		var count = items.Count;
		if (count == 0)
		{
			// Nothing to add.
			return;
		}

		this.EnsureCapacity(checked(this.Count + count));

		foreach (var entry in items)
		{
			this.Add(entry.Key, entry.Value);
		}
	}

	public void AddRange(scoped ReadOnlySpan<KeyValuePair<TKey, TValue>> items)
	{
		var length = items.Length;
		if (length == 0)
		{
			// Nothing to add.
			return;
		}

		this.EnsureCapacity(checked(this.Count + length));

		foreach (var entry in items)
		{
			this.Add(entry.Key, entry.Value);
		}
	}

	// Does not check/guard against concurrent mutation during enumeration!
	public void AddRange(IEnumerable<KeyValuePair<TKey, TValue>> items)
	{
		if (items is null)
		{
			ThrowHelpers.ThrowArgumentNullException(ThrowHelpers.Argument.items);
		}

		if (items is ICollection<KeyValuePair<TKey, TValue>> collection)
		{
			var count = collection.Count;
			if (count == 0)
			{
				// Nothing to add.
				return;
			}

			this.EnsureCapacity(checked(this.Count + count));
		}

		foreach (var item in items)
		{
			this.Add(item.Key, item.Value);
		}
	}

	public readonly ref TValue GetItem(TKey key)
	{
		ref TValue value = ref this.FindValue(key);
		if (Polyfills.IsNullRef(ref value))
		{
			ThrowHelpers.ThrowKeyNotFoundException(key);
		}

		return ref value;
	}

	public void SetItem(TKey key, TValue value)
	{
		bool modified = this.TryInsert(key, value, InsertionBehavior.OverwriteExisting);
		Polyfills.DebugAssert(modified);
	}

	internal void SetItems(scoped ReadOnlySpan<KeyValuePair<TKey, TValue>> items)
	{
		foreach (var item in items)
		{
			this.SetItem(item.Key, item.Value);
		}
	}

	// Does not check/guard against concurrent mutation during enumeration!
	public void SetItems(IEnumerable<KeyValuePair<TKey, TValue>> items)
	{
		if (items is null)
		{
			ThrowHelpers.ThrowArgumentNullException(ThrowHelpers.Argument.items);
		}

		foreach (var item in items)
		{
			this.SetItem(item.Key, item.Value);
		}
	}

	public readonly bool Contains(KeyValuePair<TKey, TValue> keyValuePair)
	{
		ref TValue value = ref this.FindValue(keyValuePair.Key);
		if (!Polyfills.IsNullRef(ref value) && EqualityComparer<TValue>.Default.Equals(value, keyValuePair.Value))
		{
			return true;
		}

		return false;
	}

	public bool Remove(KeyValuePair<TKey, TValue> keyValuePair)
	{
		ref TValue value = ref this.FindValue(keyValuePair.Key);
		if (!Polyfills.IsNullRef(ref value) && EqualityComparer<TValue>.Default.Equals(value, keyValuePair.Value))
		{
			this.Remove(keyValuePair.Key);
			return true;
		}

		return false;
	}

	public void Clear()
	{
		int end = this.end;
		if (end > 0)
		{
			Polyfills.DebugAssert(this.buckets != null, "_buckets should be non-null");
			Polyfills.DebugAssert(this.entries != null, "_entries should be non-null");

#if NET6_0_OR_GREATER
			Array.Clear(this.buckets);
#else
			Array.Clear(this.buckets, 0, this.buckets.Length);
#endif

			this.end = 0;
			this.firstFreeIndex = -1;
			this.freeCount = 0;
			Array.Clear(this.entries, 0, end);
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public readonly bool ContainsKey(TKey key) => !Polyfills.IsNullRef(ref this.FindValue(key));

	public readonly bool ContainsValue(TValue value)
	{
		var entries = this.entries;
		if (entries is null)
		{
			return false;
		}

		var end = this.end;
		var comparer = new DefaultEqualityComparer<TValue>();

		for (int i = 0; i < end; i++)
		{
			if (entries[i].Next >= -1 && comparer.Equals(entries[i].Value, value))
			{
				return true;
			}
		}

		return false;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public readonly Enumerator GetEnumerator() => new Enumerator(this);

	internal readonly ref TValue FindValue(TKey key)
	{
		if (key == null)
		{
			ThrowHelpers.ThrowArgumentNullException(ThrowHelpers.Argument.key);
		}

		ref Entry entry = ref Polyfills.NullRef<Entry>();
		if (this.buckets != null)
		{
			Polyfills.DebugAssert(this.entries != null, "expected entries to be != null");

			var comparer = new DefaultEqualityComparer<TKey>();
			uint hashCode = (uint)comparer.GetHashCode(key);
			int i = this.GetBucketRef(hashCode);
			var entries = this.entries;
			uint collisionCount = 0;
			i--; // Value in _buckets is 1-based; subtract 1 from i. We do it here so it fuses with the following conditional.
			do
			{
				// Test in if to drop range check for following array access
				if ((uint)i >= (uint)entries.Length)
				{
					goto ReturnNotFound;
				}

				entry = ref entries[i];
				if (entry.HashCode == hashCode && comparer.Equals(entry.Key, key))
				{
					goto ReturnFound;
				}

				i = entry.Next;

				collisionCount++;
			}
			while (collisionCount <= (uint)entries.Length);

			// The chain of entries forms a loop; which means a concurrent update has happened.
			// Break out of the loop and throw, rather than looping forever.
			goto ConcurrentOperation;
		}

		goto ReturnNotFound;

	ConcurrentOperation:
		ThrowHelpers.ThrowInvalidOperationException_ConcurrentOperationsNotSupported();
	ReturnFound:
		ref TValue value = ref entry.Value;
	Return:
		return ref value;
	ReturnNotFound:
		value = ref Polyfills.NullRef<TValue>();
		goto Return;
	}

	private int Initialize(int minimumCapacity)
	{
		var capacity = HashHelpers.GetPrime(minimumCapacity);
		var buckets = new int[capacity];
		var entries = new Entry[capacity];

		// Assign member variables after both arrays allocated to guard against corruption from OOM if second fails
		this.firstFreeIndex = -1;
		this.buckets = buckets;
		this.entries = entries;

		return capacity;
	}

	private bool TryInsert(TKey key, TValue value, InsertionBehavior behavior)
	{
		if (key == null)
		{
			ThrowHelpers.ThrowArgumentNullException(ThrowHelpers.Argument.key);
		}

		if (this.buckets == null)
		{
			this.Initialize(1);
		}

		Polyfills.DebugAssert(this.buckets != null);

		var entries = this.entries;
		Polyfills.DebugAssert(entries != null, "expected entries to be non-null");

		var comparer = new DefaultEqualityComparer<TKey>();
		uint hashCode = (uint)comparer.GetHashCode(key);

		uint collisionCount = 0;
		ref int bucket = ref this.GetBucketRef(hashCode);
		int i = bucket - 1; // Value in _buckets is 1-based

		while ((uint)i < (uint)entries.Length)
		{
			if (entries[i].HashCode == hashCode && comparer.Equals(entries[i].Key, key))
			{
				if (behavior == InsertionBehavior.OverwriteExisting)
				{
					entries[i].Value = value;
					return true;
				}

				if (behavior == InsertionBehavior.ThrowOnExisting)
				{
					ThrowHelpers.ThrowArgumentException_DuplicateKey(key);
				}

				return false;
			}

			i = entries[i].Next;

			collisionCount++;
			if (collisionCount > (uint)entries.Length)
			{
				// The chain of entries forms a loop; which means a concurrent update has happened.
				// Break out of the loop and throw, rather than looping forever.
				ThrowHelpers.ThrowInvalidOperationException_ConcurrentOperationsNotSupported();
			}
		}

		int index;
		if (this.freeCount > 0)
		{
			index = this.firstFreeIndex;
			Polyfills.DebugAssert((StartOfFreeList - entries[this.firstFreeIndex].Next) >= -1, "shouldn't overflow because `next` cannot underflow");
			this.firstFreeIndex = StartOfFreeList - entries[this.firstFreeIndex].Next;
			this.freeCount--;
		}
		else
		{
			var end = this.end;
			if (end == entries.Length)
			{
				this.Resize(HashHelpers.ExpandPrime(end));
				bucket = ref this.GetBucketRef(hashCode);
			}

			index = end;
			this.end = end + 1;
			entries = this.entries;
		}

		ref Entry entry = ref entries![index];
		entry.HashCode = hashCode;
		entry.Next = bucket - 1; // Value in _buckets is 1-based
		entry.Key = key;
		entry.Value = value;
		bucket = index + 1; // Value in _buckets is 1-based

		return true;
	}

	private void Resize(int newCapacity)
	{
		Polyfills.DebugAssert(this.entries != null, "_entries should be non-null");
		Polyfills.DebugAssert(newCapacity >= this.entries.Length);

		var entries = new Entry[newCapacity];

		int end = this.end;
		Array.Copy(this.entries, entries, end);

		// Assign member variables after both arrays allocated to guard against corruption from OOM if second fails
		this.buckets = new int[newCapacity];
		for (int i = 0; i < end; i++)
		{
			if (entries[i].Next >= -1)
			{
				ref int bucket = ref this.GetBucketRef(entries[i].HashCode);
				entries[i].Next = bucket - 1; // Value in _buckets is 1-based
				bucket = i + 1;
			}
		}

		this.entries = entries;
	}

	// The overload Remove(TKey key, out TValue value) is a copy of this method with one additional
	// statement to copy the value for entry being removed into the output parameter.
	// Code has been intentionally duplicated for performance reasons.
	public bool Remove(TKey key)
	{
		if (key == null)
		{
			ThrowHelpers.ThrowArgumentNullException(ThrowHelpers.Argument.key);
		}

		if (this.buckets != null)
		{
			Polyfills.DebugAssert(this.entries != null, "entries should be non-null");
			uint collisionCount = 0;

			var comparer = new DefaultEqualityComparer<TKey>();
			uint hashCode = (uint)comparer!.GetHashCode(key);

			ref int bucket = ref this.GetBucketRef(hashCode);
			var entries = this.entries;
			int last = -1;
			int i = bucket - 1; // Value in buckets is 1-based
			while (i >= 0)
			{
				ref Entry entry = ref entries[i];

				if (entry.HashCode == hashCode && comparer.Equals(entry.Key, key))
				{
					if (last < 0)
					{
						bucket = entry.Next + 1; // Value in buckets is 1-based
					}
					else
					{
						entries[last].Next = entry.Next;
					}

					Polyfills.DebugAssert((StartOfFreeList - this.firstFreeIndex) < 0, "shouldn't underflow because max hashtable length is MaxPrimeArrayLength = 0x7FEFFFFD(2146435069) _freelist underflow threshold 2147483646");
					entry.Next = StartOfFreeList - this.firstFreeIndex;

					if (Polyfills.IsReferenceOrContainsReferences<TKey>())
					{
						entry.Key = default!;
					}

					if (Polyfills.IsReferenceOrContainsReferences<TValue>())
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
				if (collisionCount > (uint)entries.Length)
				{
					// The chain of entries forms a loop; which means a concurrent update has happened.
					// Break out of the loop and throw, rather than looping forever.
					ThrowHelpers.ThrowInvalidOperationException_ConcurrentOperationsNotSupported();
				}
			}
		}

		return false;
	}

	// This overload is a copy of the overload Remove(TKey key) with one additional
	// statement to copy the value for entry being removed into the output parameter.
	// Code has been intentionally duplicated for performance reasons.
	public bool Remove(TKey key, [MaybeNullWhen(false)] out TValue value)
	{
		if (key == null)
		{
			ThrowHelpers.ThrowArgumentNullException(ThrowHelpers.Argument.key);
		}

		if (this.buckets != null)
		{
			Polyfills.DebugAssert(this.entries != null, "entries should be non-null");
			uint collisionCount = 0;

			var comparer = new DefaultEqualityComparer<TKey>();
			uint hashCode = (uint)comparer.GetHashCode(key);

			ref int bucket = ref this.GetBucketRef(hashCode);
			var entries = this.entries;
			int last = -1;
			int i = bucket - 1; // Value in buckets is 1-based
			while (i >= 0)
			{
				ref Entry entry = ref entries[i];

				if (entry.HashCode == hashCode && comparer.Equals(entry.Key, key))
				{
					if (last < 0)
					{
						bucket = entry.Next + 1; // Value in buckets is 1-based
					}
					else
					{
						entries[last].Next = entry.Next;
					}

					value = entry.Value;

					Polyfills.DebugAssert((StartOfFreeList - this.firstFreeIndex) < 0, "shouldn't underflow because max hashtable length is MaxPrimeArrayLength = 0x7FEFFFFD(2146435069) _freelist underflow threshold 2147483646");
					entry.Next = StartOfFreeList - this.firstFreeIndex;

					if (Polyfills.IsReferenceOrContainsReferences<TKey>())
					{
						entry.Key = default!;
					}

					if (Polyfills.IsReferenceOrContainsReferences<TValue>())
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
				if (collisionCount > (uint)entries.Length)
				{
					// The chain of entries forms a loop; which means a concurrent update has happened.
					// Break out of the loop and throw, rather than looping forever.
					ThrowHelpers.ThrowInvalidOperationException_ConcurrentOperationsNotSupported();
				}
			}
		}

		value = default;
		return false;
	}

	internal void RemoveRange(scoped ReadOnlySpan<TKey> keys)
	{
		foreach (var key in keys)
		{
			this.Remove(key);
		}
	}

	// Does not check/guard against concurrent mutation during enumeration!
	public void RemoveRange(IEnumerable<TKey> keys)
	{
		if (keys is null)
		{
			ThrowHelpers.ThrowArgumentNullException(ThrowHelpers.Argument.keys);
		}

		foreach (var key in keys)
		{
			this.Remove(key);
		}
	}

	public readonly bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
	{
		ref TValue valRef = ref this.FindValue(key);
		if (!Polyfills.IsNullRef(ref valRef))
		{
			value = valRef;
			return true;
		}

		value = default;
		return false;
	}

	public bool TryAdd(TKey key, TValue value) => this.TryInsert(key, value, InsertionBehavior.None);

	public int EnsureCapacity(int minimumCapacity)
	{
		if (minimumCapacity < 0)
		{
			ThrowHelpers.ThrowArgumentOutOfRangeException(ThrowHelpers.Argument.minimumCapacity);
		}

		int currentCapacity = this.Capacity;
		if (currentCapacity >= minimumCapacity)
		{
			return currentCapacity;
		}

		if (this.buckets == null)
		{
			return this.Initialize(minimumCapacity);
		}

		int newCapacity = HashHelpers.GetPrime(minimumCapacity);
		this.Resize(newCapacity);
		return newCapacity;
	}

	public void TrimExcess() => this.TrimExcess(this.Count);

	public void TrimExcess(int targetCapacity)
	{
		if (targetCapacity < this.Count)
		{
			ThrowHelpers.ThrowArgumentOutOfRangeException(ThrowHelpers.Argument.targetCapacity);
		}

		int newCapacity = HashHelpers.GetPrime(targetCapacity);
		var oldEntries = this.entries;
		int currentCapacity = oldEntries == null ? 0 : oldEntries.Length;
		if (newCapacity >= currentCapacity)
		{
			return;
		}

		int oldEnd = this.end;
		this.Initialize(newCapacity);

		Polyfills.DebugAssert(oldEntries is not null);

		this.CopyEntries(oldEntries, oldEnd);
	}

	private void CopyEntries(Entry[] entries, int count)
	{
		Polyfills.DebugAssert(this.entries is not null);

		var newEntries = this.entries;
		int newEnd = 0;
		for (int i = 0; i < count; i++)
		{
			uint hashCode = entries[i].HashCode;
			if (entries[i].Next >= -1)
			{
				ref Entry entry = ref newEntries[newEnd];
				entry = entries[i];
				ref int bucket = ref this.GetBucketRef(hashCode);
				entry.Next = bucket - 1; // Value in _buckets is 1-based
				bucket = newEnd + 1;
				newEnd++;
			}
		}

		this.end = newEnd;
		this.freeCount = 0;
	}

	internal readonly void CopyTo(KeyValuePair<TKey, TValue>[] array, int index)
	{
		if (array is null)
		{
			ThrowHelpers.ThrowArgumentNullException(ThrowHelpers.Argument.array);
		}

		if ((uint)index > (uint)array.Length)
		{
			ThrowHelpers.ThrowArgumentOutOfRangeException(ThrowHelpers.Argument.index);
		}

		if (array.Length - index < this.Count)
		{
			ThrowHelpers.ThrowArgumentException_InvalidOffsetOrLength();
		}

		foreach (var entry in this)
		{
			array[index++] = entry;
		}
	}

	internal readonly void Values_CopyTo(TValue[] array, int index)
	{
		if (array is null)
		{
			ThrowHelpers.ThrowArgumentNullException(ThrowHelpers.Argument.array);
		}

		if ((uint)index > (uint)array.Length)
		{
			ThrowHelpers.ThrowArgumentOutOfRangeException(ThrowHelpers.Argument.index);
		}

		if (array.Length - index < this.Count)
		{
			ThrowHelpers.ThrowArgumentException_InvalidOffsetOrLength();
		}

		foreach (var entry in this)
		{
			array[index++] = entry.Value;
		}
	}

	internal readonly void Keys_CopyTo(TKey[] array, int index)
	{
		if (array is null)
		{
			ThrowHelpers.ThrowArgumentNullException(ThrowHelpers.Argument.array);
		}

		if ((uint)index > (uint)array.Length)
		{
			ThrowHelpers.ThrowArgumentOutOfRangeException(ThrowHelpers.Argument.index);
		}

		if (array.Length - index < this.Count)
		{
			ThrowHelpers.ThrowArgumentException_InvalidOffsetOrLength();
		}

		foreach (var entry in this)
		{
			array[index++] = entry.Key;
		}
	}

	// Does not check/guard against concurrent mutation during enumeration!
	internal readonly bool Keys_IsProperSubsetOf(IEnumerable<TKey> other)
	{
		if (other is null)
		{
			ThrowHelpers.ThrowArgumentNullException(ThrowHelpers.Argument.other);
		}

		var otherCopy = new RawSet<TKey>(other); // TODO: don't copy

		if (this.Count >= otherCopy.Count)
		{
			return false;
		}

		foreach (var entry in this)
		{
			if (!otherCopy.Contains(entry.Key))
			{
				return false;
			}
		}

		return true;
	}

	// Does not check/guard against concurrent mutation during enumeration!
	internal readonly bool Keys_IsProperSupersetOf(IEnumerable<TKey> other)
	{
		if (other is null)
		{
			ThrowHelpers.ThrowArgumentNullException(ThrowHelpers.Argument.other);
		}

		if (this.Count == 0)
		{
			return false;
		}

		var otherCopy = new RawSet<TKey>(other); // TODO: don't copy

		int matchCount = 0;
		foreach (var item in otherCopy)
		{
			matchCount++;
			if (!this.ContainsKey(item))
			{
				return false;
			}
		}

		return this.Count > matchCount;
	}

	// Does not check/guard against concurrent mutation during enumeration!
	internal readonly bool Keys_IsSubsetOf(IEnumerable<TKey> other)
	{
		if (other is null)
		{
			ThrowHelpers.ThrowArgumentNullException(ThrowHelpers.Argument.other);
		}

		var otherCopy = new RawSet<TKey>(other); // TODO: don't copy

		if (this.Count > otherCopy.Count)
		{
			return false;
		}

		foreach (var entry in this)
		{
			if (!otherCopy.Contains(entry.Key))
			{
				return false;
			}
		}

		return true;
	}

	// Does not check/guard against concurrent mutation during enumeration!
	internal readonly bool Keys_IsSupersetOf(IEnumerable<TKey> other)
	{
		if (other is null)
		{
			ThrowHelpers.ThrowArgumentNullException(ThrowHelpers.Argument.other);
		}

		foreach (var item in other)
		{
			if (!this.ContainsKey(item))
			{
				return false;
			}
		}

		return true;
	}

	// Does not check/guard against concurrent mutation during enumeration!
	internal readonly bool Keys_Overlaps(IEnumerable<TKey> other)
	{
		if (other is null)
		{
			ThrowHelpers.ThrowArgumentNullException(ThrowHelpers.Argument.other);
		}

		if (this.Count == 0)
		{
			return false;
		}

		foreach (var item in other)
		{
			if (this.ContainsKey(item))
			{
				return true;
			}
		}

		return false;
	}

	// Does not check/guard against concurrent mutation during enumeration!
	internal readonly bool Keys_SetEquals(IEnumerable<TKey> other)
	{
		if (other is null)
		{
			ThrowHelpers.ThrowArgumentNullException(ThrowHelpers.Argument.other);
		}

		var otherCopy = new RawSet<TKey>(other); // TODO: don't copy

		if (this.Count != otherCopy.Count)
		{
			return false;
		}

		foreach (var item in otherCopy)
		{
			if (!this.ContainsKey(item))
			{
				return false;
			}
		}

		return true;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private readonly ref int GetBucketRef(uint hashCode)
	{
		var buckets = this.buckets!;
		return ref buckets[(uint)hashCode % buckets.Length];
	}

	internal readonly bool StructuralEquals(ref readonly RawDictionary<TKey, TValue> other)
	{
		if (object.ReferenceEquals(this.entries, other.entries))
		{
			return true;
		}

		if (this.Count != other.Count)
		{
			return false;
		}

		var entries = this.entries;
		var end = this.end;
		for (int i = 0; i < end; i++)
		{
			ref readonly Entry entry = ref entries![i];
			if (entry.Next >= -1 && !other.Contains(new(entry.Key, entry.Value)))
			{
				return false;
			}
		}

		return true;
	}

	internal readonly int GetStructuralHashCode()
	{
		var contentHasher = new UnorderedHashCode();

		var entries = this.entries;
		var end = this.end;
		for (int i = 0; i < end; i++)
		{
			ref readonly Entry entry = ref entries![i];
			if (entry.Next >= -1)
			{
				contentHasher.Add(HashCode.Combine(entry.Key, entry.Value));
			}
		}

		var hasher = new HashCode();
		hasher.Add(typeof(ValueDictionary<TKey, TValue>));
		hasher.Add(this.Count);
		hasher.AddUnordered(ref contentHasher);

		return hasher.ToHashCode();
	}

	public readonly override string ToString()
	{
		if (this.Count == 0)
		{
			return "[]";
		}

		var builder = new StringBuilder();
		builder.Append('[');

		var index = 0;
		foreach (var entry in this)
		{
			if (index > 0)
			{
				builder.Append(", ");
			}

			var keyString = entry.Key?.ToString() ?? "null";
			var valueString = entry.Value?.ToString() ?? "null";
			builder.Append(keyString);
			builder.Append(": ");
			builder.Append(valueString);

			index++;
		}

		builder.Append(']');
		return builder.ToString();
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
		internal uint HashCode;

		// Index of the next entry in the chain. This also doubles as an
		// indicator for whether or not this entry is "free" or actively in use.
		//
		// >=  0 : The entry is active, and the index points to the next item in the chain.
		// == -1 : The entry is active, and this is the last entry in the chain.
		// == -2 : The entry is free, and this is the last entry in the free chain.
		// <= -3 : The entry is free, and (after changing the sign and subtracting 3) this points to the next entry in the free list.
		internal int Next;

		internal TKey Key;
		internal TValue Value;
	}

	[StructLayout(LayoutKind.Auto)]
	public struct Enumerator : IEnumeratorLike<KeyValuePair<TKey, TValue>>
	{
		private readonly Entry[]? entries;
		private readonly int end;
		private int counter;
		private int index;

		internal Enumerator(RawDictionary<TKey, TValue> dictionary)
		{
			this.entries = dictionary.entries;
			this.end = dictionary.end;
			this.counter = 0;
			this.index = dictionary.GetArbitraryIndex();
		}

		/// <inheritdoc/>
		public KeyValuePair<TKey, TValue> Current
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				ref readonly Entry entry = ref this.entries![this.index];
				return new KeyValuePair<TKey, TValue>(entry.Key, entry.Value);
			}
		}

		public readonly ref readonly TKey CurrentKey
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => ref this.entries![this.index].Key;
		}

		public readonly ref readonly TValue CurrentValue
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => ref this.entries![this.index].Value;
		}

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

	/// <summary>
	/// Used internally to control behavior of insertion into a <see cref="Dictionary{TKey, TValue}"/> or <see cref="HashSet{T}"/>.
	/// </summary>
	private enum InsertionBehavior : byte
	{
		/// <summary>
		/// The default insertion behavior.
		/// </summary>
		None = 0,

		/// <summary>
		/// Specifies that an existing entry with the same key should be overwritten if encountered.
		/// </summary>
		OverwriteExisting = 1,

		/// <summary>
		/// Specifies that if an existing entry with the same key is encountered, an exception should be thrown.
		/// </summary>
		ThrowOnExisting = 2,
	}
}
