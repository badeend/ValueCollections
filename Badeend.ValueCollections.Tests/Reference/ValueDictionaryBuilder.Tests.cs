// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.Serialization;
using Xunit;

namespace Badeend.ValueCollections.Tests.Reference
{
    /// <summary>
    /// Contains tests that ensure the correctness of the Dictionary class.
    /// </summary>
    public abstract class ValueDictionaryBuilder_Tests<TKey, TValue> : IDictionary_Generic_Tests<TKey, TValue>
    {
        protected override bool ResetImplemented => false;
        protected override bool Enumerator_Empty_UsesSingletonInstance => true;
        protected override bool Enumerator_Current_UndefinedOperation_Throws => true;
        protected override bool Enumerator_Empty_Current_UndefinedOperation_Throws => true;
        protected override bool Enumerator_Empty_ModifiedDuringEnumeration_ThrowsInvalidOperationException => false;
        protected override bool Enumerator_ModifiedDuringEnumeration_ThrowsInvalidOperationException => true;
        protected override bool IDictionary_Generic_Keys_Values_ModifyingTheDictionaryUpdatesTheCollection => false;


        #region IDictionary<TKey, TValue Helper Methods

        protected override IDictionary<TKey, TValue> GenericIDictionaryFactory() => ValueDictionary.CreateBuilder<TKey, TValue>().AsCollection();

        protected override IDictionary<TKey, TValue> GenericIDictionaryFactory(IEqualityComparer<TKey> comparer) => null;

        protected  ValueDictionary<TKey, TValue>.Builder BuilderFactory(int count)
        {
            var builder = ValueDictionary.CreateBuilder<TKey, TValue>();
            AddToCollection(builder.AsCollection(), count);
            return builder;
        }

        #endregion

        #region Constructors

        [Theory]
        [MemberData(nameof(ValidCollectionSizes))]
        public void ValueDictionaryBuilder_Constructor_IDictionary(int count)
        {
            IDictionary<TKey, TValue> source = GenericIDictionaryFactory(count);
            IDictionary<TKey, TValue> copied = source.ToValueDictionaryBuilder().AsCollection();
            Assert.True(source.EqualsUnordered(copied));
        }

#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
        [Theory]
        [MemberData(nameof(ValidCollectionSizes))]
        public void ValueDictionaryBuilder_Constructor_int(int count)
        {
            var dictionary = ValueDictionary.CreateBuilderWithCapacity<TKey, TValue>(count);
            Assert.Equal(0, dictionary.Count);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(100)]
        public void Dictionary_CreateWithCapacity_CapacityAtLeastPassedValue(int capacity)
        {
            ValueDictionary<TKey, TValue>.Builder dict = ValueDictionary.CreateBuilderWithCapacity<TKey, TValue>(capacity);
            Assert.True(capacity <= dict.Capacity);
        }
#endif
        #endregion

        #region Properties
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
        public void DictResized_CapacityChanged()
        {
            var dict = BuilderFactory(1);
            int initialCapacity = dict.Capacity;

            int seed = 85877;
            for (int i = 0; i < dict.Capacity; i++)
            {
                dict.Add(CreateTKey(seed++), CreateTValue(seed++));
            }

            int afterCapacity = dict.Capacity;

            Assert.True(afterCapacity > initialCapacity);
        }
#endif
        #endregion
        #region ContainsValue

        [Theory]
        [MemberData(nameof(ValidCollectionSizes))]
        public void ValueDictionaryBuilder_ContainsValue_NotPresent(int count)
        {
            ValueDictionary<TKey, TValue>.Builder dictionary = BuilderFactory(count);
            int seed = 4315;
            TValue notPresent = CreateTValue(seed++);
            while (dictionary.Values.AsCollection().Contains(notPresent))
                notPresent = CreateTValue(seed++);
            Assert.False(dictionary.ContainsValue(notPresent));
        }

        [Theory]
        [MemberData(nameof(ValidCollectionSizes))]
        public void ValueDictionaryBuilder_ContainsValue_Present(int count)
        {
            ValueDictionary<TKey, TValue>.Builder dictionary = BuilderFactory(count);
            int seed = 4315;
            KeyValuePair<TKey, TValue> notPresent = CreateT(seed++);
            while (dictionary.AsCollection().Contains(notPresent))
                notPresent = CreateT(seed++);
            dictionary.Add(notPresent.Key, notPresent.Value);
            Assert.True(dictionary.ContainsValue(notPresent.Value));
        }

        [Theory]
        [MemberData(nameof(ValidCollectionSizes))]
        public void ValueDictionaryBuilder_ContainsValue_DefaultValueNotPresent(int count)
        {
            ValueDictionary<TKey, TValue>.Builder dictionary = BuilderFactory(count);
            Assert.False(dictionary.ContainsValue(default(TValue)));
        }

        [Theory]
        [MemberData(nameof(ValidCollectionSizes))]
        public void ValueDictionaryBuilder_ContainsValue_DefaultValuePresent(int count)
        {
            ValueDictionary<TKey, TValue>.Builder dictionary = BuilderFactory(count);
            int seed = 4315;
            TKey notPresent = CreateTKey(seed++);
            while (dictionary.ContainsKey(notPresent))
                notPresent = CreateTKey(seed++);
            dictionary.Add(notPresent, default(TValue));
            Assert.True(dictionary.ContainsValue(default(TValue)));
        }

        #endregion

        #region IReadOnlyDictionary<TKey, TValue>.Keys

        [Theory]
        [MemberData(nameof(ValidCollectionSizes))]
        public void IReadOnlyDictionary_Keys_ContainsAllCorrectKeys(int count)
        {
            IDictionary<TKey, TValue> dictionary = GenericIDictionaryFactory(count);
            IEnumerable<TKey> expected = dictionary.Select((pair) => pair.Key);
            IEnumerable<TKey> keys = ((IReadOnlyDictionary<TKey, TValue>)dictionary).Keys;
            Assert.True(expected.SequenceEqual(keys));
        }

        [Theory]
        [MemberData(nameof(ValidCollectionSizes))]
        public void IReadOnlyDictionary_Values_ContainsAllCorrectValues(int count)
        {
            IDictionary<TKey, TValue> dictionary = GenericIDictionaryFactory(count);
            IEnumerable<TValue> expected = dictionary.Select((pair) => pair.Value);
            IEnumerable<TValue> values = ((IReadOnlyDictionary<TKey, TValue>)dictionary).Values;
            Assert.True(expected.SequenceEqual(values));
        }

        #endregion

        #region Remove(TKey)

        [Theory]
        [MemberData(nameof(ValidCollectionSizes))]
        public void ValueDictionaryBuilder_RemoveKey_ValidKeyNotContainedInDictionary(int count)
        {
            ValueDictionary<TKey, TValue>.Builder dictionary = BuilderFactory(count);
            TValue value;
            TKey missingKey = GetNewKey(dictionary.AsCollection());

            Assert.False(dictionary.TryRemove(missingKey, out value));
            Assert.Equal(count, dictionary.Count);
            Assert.Equal(default(TValue), value);
        }

        [Theory]
        [MemberData(nameof(ValidCollectionSizes))]
        public void ValueDictionaryBuilder_RemoveKey_ValidKeyContainedInDictionary(int count)
        {
            ValueDictionary<TKey, TValue>.Builder dictionary = BuilderFactory(count);
            TKey missingKey = GetNewKey(dictionary.AsCollection());
            TValue outValue;
            TValue inValue = CreateTValue(count);

            dictionary.Add(missingKey, inValue);
            Assert.True(dictionary.TryRemove(missingKey, out outValue));
            Assert.Equal(count, dictionary.Count);
            Assert.Equal(inValue, outValue);
            Assert.False(dictionary.TryGetValue(missingKey, out outValue));
        }

        [Theory]
        [MemberData(nameof(ValidCollectionSizes))]
        public void ValueDictionaryBuilder_RemoveKey_DefaultKeyNotContainedInDictionary(int count)
        {
            ValueDictionary<TKey, TValue>.Builder dictionary = BuilderFactory(count);
            TValue outValue;

            if (DefaultValueAllowed)
            {
                TKey missingKey = default(TKey);
                while (dictionary.ContainsKey(missingKey))
                    dictionary.Remove(missingKey);
                Assert.False(dictionary.TryRemove(missingKey, out outValue));
                Assert.Equal(default(TValue), outValue);
            }
            else
            {
                TValue initValue = CreateTValue(count);
                outValue = initValue;
                Assert.Throws<ArgumentNullException>(() => dictionary.TryRemove(default(TKey), out outValue));
                Assert.Equal(initValue, outValue);
            }
        }

        [Theory]
        [MemberData(nameof(ValidCollectionSizes))]
        public void ValueDictionaryBuilder_RemoveKey_DefaultKeyContainedInDictionary(int count)
        {
            if (DefaultValueAllowed)
            {
                ValueDictionary<TKey, TValue>.Builder dictionary = BuilderFactory(count);
                TKey missingKey = default(TKey);
                TValue value;

                dictionary.TryAdd(missingKey, default(TValue));
                Assert.True(dictionary.TryRemove(missingKey, out value));
            }
        }

        [Fact]
        public void ValueDictionaryBuilder_Remove_RemoveFirstEnumerationThrows()
        {
            ValueDictionary<TKey,TValue>.Builder dict = BuilderFactory(3);
            var enumerator = dict.GetEnumerator();
            enumerator.MoveNext();
            TKey key = enumerator.Current.Key;
            enumerator.MoveNext();
            dict.Remove(key);
            Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
        }

        [Fact]
        public void ValueDictionaryBuilder_Remove_RemoveCurrentEnumerationThrows()
        {
            ValueDictionary<TKey, TValue>.Builder dict = BuilderFactory(3);
            var enumerator = dict.GetEnumerator();
            enumerator.MoveNext();
            enumerator.MoveNext();
            dict.Remove(enumerator.Current.Key);
            Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
        }

        [Fact]
        public void ValueDictionaryBuilder_Remove_RemoveLastEnumerationThrows()
        {
            ValueDictionary<TKey, TValue>.Builder dict = BuilderFactory(3);
            TKey key = default;
            {
                var enumerator = dict.GetEnumerator();
                while (enumerator.MoveNext())
                {
                    key = enumerator.Current.Key;
                }
            }
            {
                var enumerator = dict.GetEnumerator();
                enumerator.MoveNext();
                enumerator.MoveNext();
                dict.Remove(key);
                Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
            }
        }

        #endregion
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
        #region EnsureCapacity

        [Theory]
        [MemberData(nameof(ValidCollectionSizes))]
        public void EnsureCapacity_Generic_RequestingLargerCapacity_DoesInvalidateEnumeration(int count)
        {
            var dictionary = BuilderFactory(count);
            var capacity = dictionary.EnsureCapacity(0);
            var enumerator = dictionary.GetEnumerator();

            dictionary.EnsureCapacity(dictionary.Capacity + 1); // Verify EnsureCapacity does invalidate enumeration

            Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
        }

        [Fact]
        public void EnsureCapacity_Generic_NegativeCapacityRequested_Throws()
        {
            var dictionary = ValueDictionary.CreateBuilder<TKey, TValue>();
            AssertExtensions.Throws<ArgumentOutOfRangeException>("capacity", () => dictionary.EnsureCapacity(-1));
        }

        [Fact]
        public void EnsureCapacity_Generic_DictionaryNotInitialized_RequestedZero_ReturnsZero()
        {
            var dictionary = ValueDictionary.CreateBuilder<TKey, TValue>();
            dictionary.EnsureCapacity(0);
            Assert.Equal(0, dictionary.Capacity);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        public void EnsureCapacity_Generic_DictionaryNotInitialized_RequestedNonZero_CapacityIsSetToAtLeastTheRequested(int requestedCapacity)
        {
            var dictionary = ValueDictionary.CreateBuilder<TKey, TValue>();
            dictionary.EnsureCapacity(requestedCapacity);
            Assert.InRange(dictionary.Capacity, requestedCapacity, int.MaxValue);
        }

        [Theory]
        [InlineData(3)]
        [InlineData(7)]
        public void EnsureCapacity_Generic_RequestedCapacitySmallerThanCurrent_CapacityUnchanged(int currentCapacity)
        {
            ValueDictionary<TKey, TValue>.Builder dictionary;

            // assert capacity remains the same when ensuring a capacity smaller or equal than existing
            for (int i = 0; i <= currentCapacity; i++)
            {
                dictionary = ValueDictionary.CreateBuilderWithCapacity<TKey, TValue>(currentCapacity);
                dictionary.EnsureCapacity(i);
                Assert.Equal(currentCapacity, dictionary.Capacity);
            }
        }

        [Theory]
        [InlineData(7)]
        public void EnsureCapacity_Generic_ExistingCapacityRequested_SameValueReturned(int capacity)
        {
            var dictionary = ValueDictionary.CreateBuilderWithCapacity<TKey, TValue>(capacity);
            dictionary.EnsureCapacity(capacity);
            Assert.Equal(capacity, dictionary.Capacity);

            dictionary = BuilderFactory(capacity);
            dictionary.EnsureCapacity(capacity);
            Assert.Equal(capacity, dictionary.Capacity);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(5)]
        [InlineData(7)]
        public void EnsureCapacity_Generic_DictionaryNotEmpty_RequestedSmallerThanCount_ReturnsAtLeastSizeOfCount(int count)
        {
            var dictionary = BuilderFactory(count);
            dictionary.EnsureCapacity(count - 1);
            Assert.InRange(dictionary.Capacity, count, int.MaxValue);
        }

        [Theory]
        [InlineData(7)]
        [InlineData(20)]
        public void EnsureCapacity_Generic_DictionaryNotEmpty_SetsToAtLeastTheRequested(int count)
        {
            var dictionary = BuilderFactory(count);

            // get current capacity
            int currentCapacity = dictionary.Capacity;

            // assert we can update to a larger capacity
            dictionary.EnsureCapacity(currentCapacity * 2);
            int newCapacity = dictionary.Capacity;
            Assert.InRange(newCapacity, currentCapacity * 2, int.MaxValue);
        }

        [Fact]
        public void EnsureCapacity_Generic_CapacityIsSetToPrimeNumberLargerOrEqualToRequested()
        {
            var dictionary = ValueDictionary.CreateBuilder<TKey, TValue>();
            dictionary.EnsureCapacity(17);
            Assert.Equal(17, dictionary.Capacity);

            dictionary = ValueDictionary.CreateBuilder<TKey, TValue>();
            dictionary.EnsureCapacity(15);
            Assert.Equal(17, dictionary.Capacity);

            dictionary = ValueDictionary.CreateBuilder<TKey, TValue>();
            dictionary.EnsureCapacity(13);
            Assert.Equal(17, dictionary.Capacity);
        }

        #endregion

        #region TrimExcess

        [Fact]
        public void TrimExcess_Generic_NegativeCapacity_Throw()
        {
            var dictionary = ValueDictionary.CreateBuilder<TKey, TValue>();
            AssertExtensions.Throws<ArgumentOutOfRangeException>("capacity", () => dictionary.TrimExcess(-1));
        }

        [Theory]
        [InlineData(20)]
        [InlineData(23)]
        public void TrimExcess_Generic_CapacitySmallerThanCount_Throws(int suggestedCapacity)
        {
            var dictionary = ValueDictionary.CreateBuilder<TKey, TValue>();
            dictionary.Add(GetNewKey(dictionary.AsCollection()), CreateTValue(0));
            AssertExtensions.Throws<ArgumentOutOfRangeException>("capacity", () => dictionary.TrimExcess(0));

            dictionary = ValueDictionary.CreateBuilderWithCapacity<TKey, TValue>(suggestedCapacity);
            dictionary.Add(GetNewKey(dictionary.AsCollection()), CreateTValue(0));
            AssertExtensions.Throws<ArgumentOutOfRangeException>("capacity", () => dictionary.TrimExcess(0));
        }

        [Fact]
        public void TrimExcess_Generic_LargeInitialCapacity_TrimReducesSize()
        {
            var dictionary = ValueDictionary.CreateBuilderWithCapacity<TKey, TValue>(20);
            dictionary.TrimExcess(7);
            Assert.Equal(7, dictionary.Capacity);
        }

        [Theory]
        [InlineData(20)]
        [InlineData(23)]
        public void TrimExcess_Generic_TrimToLargerThanExistingCapacity_DoesNothing(int suggestedCapacity)
        {
            var dictionary = ValueDictionary.CreateBuilder<TKey, TValue>();
            int capacity = dictionary.Capacity;
            dictionary.TrimExcess(suggestedCapacity);
            Assert.Equal(capacity, dictionary.Capacity);

            dictionary = ValueDictionary.CreateBuilderWithCapacity<TKey, TValue>(suggestedCapacity/2);
            capacity = dictionary.Capacity;
            dictionary.TrimExcess(suggestedCapacity);
            Assert.Equal(capacity, dictionary.Capacity);
        }

        [Fact]
        public void TrimExcess_Generic_DictionaryNotInitialized_CapacityRemainsAsMinPossible()
        {
            var dictionary = ValueDictionary.CreateBuilder<TKey, TValue>();
            Assert.Equal(0, dictionary.Capacity);
            dictionary.TrimExcess();
            Assert.Equal(0, dictionary.Capacity);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(85)]
        [InlineData(89)]
        public void TrimExcess_Generic_ClearThenTrimNonEmptyDictionary_SetsCapacityTo3(int count)
        {
            ValueDictionary<TKey, TValue>.Builder dictionary = BuilderFactory(count);
            Assert.Equal(count, dictionary.Count);
            // The smallest possible capacity size after clearing a dictionary is 3
            dictionary.Clear();
            dictionary.TrimExcess();
            Assert.Equal(3, dictionary.Capacity);
        }

        [Theory]
        [InlineData(85)]
        [InlineData(89)]
        public void TrimExcess_NoArguments_TrimsToAtLeastCount(int count)
        {
            var dictionary = ValueDictionary.CreateBuilderWithCapacity<int, int>(20);
            for (int i = 0; i < count; i++)
            {
                dictionary.Add(i, 0);
            }
            dictionary.TrimExcess();
            Assert.InRange(dictionary.Capacity, count, int.MaxValue);
        }

        [Theory]
        [InlineData(85)]
        [InlineData(89)]
        public void TrimExcess_WithArguments_OnDictionaryWithManyElementsRemoved_TrimsToAtLeastRequested(int finalCount)
        {
            const int InitToFinalRatio = 10;
            int initialCount = InitToFinalRatio * finalCount;
            var dictionary = ValueDictionary.CreateBuilderWithCapacity<int, int>(initialCount);
            Assert.InRange(dictionary.Capacity, initialCount, int.MaxValue);
            for (int i = 0; i < initialCount; i++)
            {
                dictionary.Add(i, 0);
            }
            for (int i = 0; i < initialCount - finalCount; i++)
            {
                dictionary.Remove(i);
            }
            for (int i = InitToFinalRatio; i > 0; i--)
            {
                dictionary.TrimExcess(i * finalCount);
                Assert.InRange(dictionary.Capacity, i * finalCount, int.MaxValue);
            }
        }

        [Theory]
        [InlineData(1000, 900, 5000, 85, 89)]
        [InlineData(1000, 400, 5000, 85, 89)]
        [InlineData(1000, 900, 500, 85, 89)]
        [InlineData(1000, 400, 500, 85, 89)]
        [InlineData(1000, 400, 500, 1, 3)]
        public void TrimExcess_NoArgument_TrimAfterEachBulkAddOrRemove_TrimsToAtLeastCount(int initialCount, int numRemove, int numAdd, int newCount, int newCapacity)
        {
            Random random = new Random(32);
            var dictionary = ValueDictionary.CreateBuilder<int, int>();
            dictionary.TrimExcess();
            Assert.InRange(dictionary.Capacity, dictionary.Count, int.MaxValue);

            var initialKeys = new int[initialCount];
            for (int i = 0; i < initialCount; i++)
            {
                initialKeys[i] = i;
            }
            random.Shuffle(initialKeys);
            foreach (var key in initialKeys)
            {
                dictionary.Add(key, 0);
            }
            dictionary.TrimExcess();
            Assert.InRange(dictionary.Capacity, dictionary.Count, int.MaxValue);

            random.Shuffle(initialKeys);
            for (int i = 0; i < numRemove; i++)
            {
                dictionary.Remove(initialKeys[i]);
            }
            dictionary.TrimExcess();
            Assert.InRange(dictionary.Capacity, dictionary.Count, int.MaxValue);

            var moreKeys = new int[numAdd];
            for (int i = 0; i < numAdd; i++)
            {
                moreKeys[i] = i + initialCount;
            }
            random.Shuffle(moreKeys);
            foreach (var key in moreKeys)
            {
                dictionary.Add(key, 0);
            }
            int currentCount = dictionary.Count;
            dictionary.TrimExcess();
            Assert.InRange(dictionary.Capacity, currentCount, int.MaxValue);

            int[] existingKeys = new int[currentCount];
            Array.Copy(initialKeys, numRemove, existingKeys, 0, initialCount - numRemove);
            Array.Copy(moreKeys, 0, existingKeys, initialCount - numRemove, numAdd);
            random.Shuffle(existingKeys);
            for (int i = 0; i < currentCount - newCount; i++)
            {
                dictionary.Remove(existingKeys[i]);
            }
            dictionary.TrimExcess();
            int finalCapacity = dictionary.Capacity;
            Assert.InRange(finalCapacity, newCount, initialCount);
            Assert.Equal(newCapacity, finalCapacity);
        }

        [Fact]
        public void TrimExcess_DictionaryHasElementsChainedWithSameHashcode_Success()
        {
            var dictionary = ValueDictionary.CreateBuilderWithCapacity<string, int>(7);
            for (int i = 0; i < 4; i++)
            {
                dictionary.Add(i.ToString(), 0);
            }
            var s_64bit = new string[] { "95e85f8e-67a3-4367-974f-dd24d8bb2ca2", "eb3d6fe9-de64-43a9-8f58-bddea727b1ca" };
            var s_32bit = new string[] { "25b1f130-7517-48e3-96b0-9da44e8bfe0e", "ba5a3625-bc38-4bf1-a707-a3cfe2158bae" };
            string[] chained = (Environment.Is64BitProcess ? s_64bit : s_32bit).ToArray();
            dictionary.Add(chained[0], 0);
            dictionary.Add(chained[1], 0);
            for (int i = 0; i < 4; i++)
            {
                dictionary.Remove(i.ToString());
            }
            dictionary.TrimExcess(3);
            Assert.Equal(2, dictionary.Count);
            int val;
            Assert.True(dictionary.TryGetValue(chained[0], out val));
            Assert.True(dictionary.TryGetValue(chained[1], out val));
        }

        [Fact]
        public void TrimExcess_Generic_DoesInvalidateEnumeration()
        {
            var dictionary = ValueDictionary.CreateBuilderWithCapacity<TKey, TValue>(20);
            var enumerator = dictionary.GetEnumerator();

            dictionary.TrimExcess(7); // Verify TrimExcess does invalidate enumeration

            Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
        }

        #endregion
#endif
    }
}
