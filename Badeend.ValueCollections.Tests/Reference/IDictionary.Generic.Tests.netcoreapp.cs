// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Xunit;

namespace Badeend.ValueCollections.Tests.Reference
{
    /// <summary>
    /// Contains tests that ensure the correctness of any class that implements the generic
    /// IDictionary interface
    /// </summary>
    public abstract partial class IDictionary_Generic_Tests<TKey, TValue> : ICollection_Generic_Tests<KeyValuePair<TKey, TValue>>
    {
        [Theory]
        [MemberData(nameof(ValidCollectionSizes))]
        public void KeyValuePair_Deconstruct(int size)
        {
            IDictionary<TKey, TValue> dictionary = GenericIDictionaryFactory(size);
            Assert.All(dictionary, (entry) => {
                TKey key;
                TValue value;
                entry.Deconstruct(out key, out value);
                Assert.Equal(entry.Key, key);
                Assert.Equal(entry.Value, value);

                key = default(TKey);
                value = default(TValue);
                (key, value) = entry;
                Assert.Equal(entry.Key, key);
                Assert.Equal(entry.Value, value);
            });
        }
    }
}
