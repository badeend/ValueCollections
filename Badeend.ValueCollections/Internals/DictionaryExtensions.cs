namespace Badeend.ValueCollections.Internals;

internal static class DictionaryExtensions
{
	internal static void Values_CopyTo<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, TValue[] array, int index)
		where TKey : notnull
	{
		if (array == null)
		{
			throw new ArgumentNullException(nameof(array));
		}

		if (index < 0 || index > array.Length)
		{
			throw new ArgumentOutOfRangeException(nameof(index));
		}

		if (array.Length - index < dictionary.Count)
		{
			throw new ArgumentException("Destination too small", nameof(array));
		}

		foreach (var entry in new ShufflingDictionaryEnumerator<TKey, TValue>(dictionary))
		{
			array[index++] = entry.Value;
		}
	}

	internal static void Keys_CopyTo<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, TKey[] array, int index)
		where TKey : notnull
	{
		if (array == null)
		{
			throw new ArgumentNullException(nameof(array));
		}

		if (index < 0 || index > array.Length)
		{
			throw new ArgumentOutOfRangeException(nameof(index));
		}

		if (array.Length - index < dictionary.Count)
		{
			throw new ArgumentException("Destination too small", nameof(array));
		}

		foreach (var entry in new ShufflingDictionaryEnumerator<TKey, TValue>(dictionary))
		{
			array[index++] = entry.Key;
		}
	}

	internal static bool Keys_IsProperSubsetOf<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, IEnumerable<TKey> other)
		where TKey : notnull
	{
		if (other is null)
		{
			throw new ArgumentNullException(nameof(other));
		}

		var otherAsSet = other as ISet<TKey> ?? new HashSet<TKey>(other);

		if (dictionary.Count >= otherAsSet.Count)
		{
			return false;
		}

		foreach (var entry in dictionary)
		{
			if (!otherAsSet.Contains(entry.Key))
			{
				return false;
			}
		}

		return true;
	}

	internal static bool Keys_IsProperSupersetOf<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, IEnumerable<TKey> other)
		where TKey : notnull
	{
		if (other is null)
		{
			throw new ArgumentNullException(nameof(other));
		}

		if (dictionary.Count == 0)
		{
			return false;
		}

		if (other is ISet<TKey> otherAsSet && otherAsSet.Count >= dictionary.Count)
		{
			return false;
		}

		int matchCount = 0;
		foreach (var item in other)
		{
			matchCount++;
			if (!dictionary.ContainsKey(item))
			{
				return false;
			}
		}

		return dictionary.Count > matchCount;
	}

	internal static bool Keys_IsSubsetOf<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, IEnumerable<TKey> other)
		where TKey : notnull
	{
		if (other is null)
		{
			throw new ArgumentNullException(nameof(other));
		}

		var otherAsSet = other as ISet<TKey> ?? new HashSet<TKey>(other);

		if (dictionary.Count > otherAsSet.Count)
		{
			return false;
		}

		foreach (var entry in dictionary)
		{
			if (!otherAsSet.Contains(entry.Key))
			{
				return false;
			}
		}

		return true;
	}

	internal static bool Keys_IsSupersetOf<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, IEnumerable<TKey> other)
		where TKey : notnull
	{
		if (other is null)
		{
			throw new ArgumentNullException(nameof(other));
		}

		foreach (var item in other)
		{
			if (!dictionary.ContainsKey(item))
			{
				return false;
			}
		}

		return true;
	}

	internal static bool Keys_Overlaps<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, IEnumerable<TKey> other)
		where TKey : notnull
	{
		if (other is null)
		{
			throw new ArgumentNullException(nameof(other));
		}

		if (dictionary.Count == 0)
		{
			return false;
		}

		foreach (var item in other)
		{
			if (dictionary.ContainsKey(item))
			{
				return true;
			}
		}

		return false;
	}

	internal static bool Keys_SetEquals<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, IEnumerable<TKey> other)
		where TKey : notnull
	{
		if (other is null)
		{
			throw new ArgumentNullException(nameof(other));
		}

		var otherAsSet = other as ISet<TKey> ?? new HashSet<TKey>(other);

		if (dictionary.Count != otherAsSet.Count)
		{
			return false;
		}

		foreach (var item in otherAsSet)
		{
			if (!dictionary.ContainsKey(item))
			{
				return false;
			}
		}

		return true;
	}
}
