// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Badeend.ValueCollections.Tests.Reference
{
    internal static class DictionaryExtensions
    {
        public static bool TryAdd<TKey, TValue>(this IDictionary<TKey, TValue> dict, TKey key, TValue value)
        {
            if (!dict.ContainsKey(key))
            {
                dict.Add(key, value);
                return true;
            }

            return false;
        }

        public static void Deconstruct<TKey, TValue>(this KeyValuePair<TKey, TValue> pair, out TKey key, out TValue value)
        {
            key = pair.Key;
            value = pair.Value;
        }

        public static bool EqualsUnordered<TKey, TValue>(this IDictionary<TKey, TValue>? left, IDictionary<TKey, TValue>? right)
        {
            if (object.ReferenceEquals(left, right))
            {
                return true;
            }

            if (left is null || right is null)
            {
                return false;
            }

            if (left.Count != right.Count)
            {
                return false;
            }

            foreach (var item in left)
            {
                if (!right.Contains(item))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
