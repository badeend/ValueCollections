// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using Xunit;

namespace Badeend.ValueCollections.Tests.Reference
{
    /// <summary>
    /// Contains tests that ensure the correctness of the ValueSetBuilder class.
    /// </summary>
    public abstract class ValueSetBuilder_Generic_Tests<T> : ISet_Generic_Tests<T>
    {
        protected override bool ResetImplemented => false;
        protected override bool Enumerator_Empty_UsesSingletonInstance => true;
        protected override bool Enumerator_Current_UndefinedOperation_Throws => true;
        protected override bool Enumerator_Empty_Current_UndefinedOperation_Throws => true;
        protected override bool Enumerator_Empty_ModifiedDuringEnumeration_ThrowsInvalidOperationException => false;
        protected override bool Enumerator_ModifiedDuringEnumeration_ThrowsInvalidOperationException => true;



        protected override ISet<T> GenericISetFactory()
        {
            return new ValueSetBuilder<T>();
        }

        

        private static IEnumerable<int> NonSquares(int limit)
        {
            for (int i = 0; i != limit; ++i)
            {
                int root = (int)Math.Sqrt(i);
                if (i != root * root)
                    yield return i;
            }
        }

        [Fact]
        public void ValueSetBuilder_Generic_Constructor()
        {
            ValueSetBuilder<T> set = new ValueSetBuilder<T>();
            Assert.Empty(set);
        }

        [Theory]
        [MemberData(nameof(EnumerableTestData))]
        public void ValueSetBuilder_Generic_Constructor_IEnumerable(EnumerableType enumerableType, int setLength, int enumerableLength, int numberOfMatchingElements, int numberOfDuplicateElements)
        {
            _ = setLength;
            _ = numberOfMatchingElements;
            IEnumerable<T> enumerable = CreateEnumerable(enumerableType, null, enumerableLength, 0, numberOfDuplicateElements);
            ValueSetBuilder<T> set = new ValueSetBuilder<T>(enumerable);
            Assert.True(set.SetEquals(enumerable));
        }

        [Theory]
        [MemberData(nameof(ValidCollectionSizes))]
        public void ValueSetBuilder_Generic_Constructor_IEnumerable_WithManyDuplicates(int count)
        {
            IEnumerable<T> items = CreateEnumerable(EnumerableType.List, null, count, 0, 0);
            ValueSetBuilder<T> hashSetFromDuplicates = new ValueSetBuilder<T>(Enumerable.Range(0, 40).SelectMany(i => items).ToArray());
            ValueSetBuilder<T> hashSetFromNoDuplicates = new ValueSetBuilder<T>(items);
            Assert.True(hashSetFromNoDuplicates.SetEquals(hashSetFromDuplicates));
        }

        [Theory]
        [MemberData(nameof(ValidCollectionSizes))]
        public void ValueSetBuilder_Generic_Constructor_ValueSetBuilder_SparselyFilled(int count)
        {
            ValueSetBuilder<T> source = CreateEnumerable(EnumerableType.HashSet, null, count, 0, 0).ToValueSetBuilder();
            List<T> sourceElements = source.ToList();
            foreach (int i in NonSquares(count))
                source.Remove(sourceElements[i]);// Unevenly spaced survivors increases chance of catching any spacing-related bugs.


            ValueSetBuilder<T> set = new ValueSetBuilder<T>(source);
            Assert.True(set.SetEquals(source));
        }

        [Fact]
        public void ValueSetBuilder_Generic_Constructor_IEnumerable_Null()
        {
            Assert.Throws<ArgumentNullException>(() => new ValueSetBuilder<T>((IEnumerable<T>)null));
        }

#if NET472_OR_GREATER || NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_0_OR_GREATER
        [Theory]
        [InlineData(1)]
        [InlineData(100)]
        public void ValueSetBuilder_CreateWithCapacity_CapacityAtLeastPassedValue(int capacity)
        {
            var hashSet = new ValueSetBuilder<T>(capacity);
            Assert.True(capacity <= hashSet.Capacity);
        }

        [Fact]
        public void ValueSetBuilderResized_CapacityChanged()
        {
            var hashSet = (ValueSetBuilder<T>)GenericISetFactory(3);
            int initialCapacity = hashSet.Capacity;

            int seed = 85877;
            hashSet.Add(CreateT(seed++));

            int afterCapacity = hashSet.Capacity;

            Assert.True(afterCapacity > initialCapacity);
        }
#endif

        

        [Theory]
        [MemberData(nameof(ValidCollectionSizes))]
        public void ValueSetBuilder_Generic_RemoveWhere_AllElements(int setLength)
        {
            ValueSetBuilder<T> set = (ValueSetBuilder<T>)GenericISetFactory(setLength);
            int removedCount = set.RemoveAndCountWhere((value) => { return true; });
            Assert.Equal(setLength, removedCount);
        }

        [Theory]
        [MemberData(nameof(ValidCollectionSizes))]
        public void ValueSetBuilder_Generic_RemoveWhere_NoElements(int setLength)
        {
            ValueSetBuilder<T> set = (ValueSetBuilder<T>)GenericISetFactory(setLength);
            int removedCount = set.RemoveAndCountWhere((value) => { return false; });
            Assert.Equal(0, removedCount);
            Assert.Equal(setLength, set.Count);
        }

        [Fact]
        public void ValueSetBuilder_Generic_RemoveWhere_NewObject() // Regression Dev10_624201
        {
            object[] array = new object[2];
            object obj = new object();
            ValueSetBuilder<object> set = new ValueSetBuilder<object>();

            set.Add(obj);
            set.Remove(obj);
            foreach (object o in set) { }
            set.CopyTo(array.AsSpan(0, 2));
            set.RemoveWhere((element) => { return false; });
        }

        [Theory]
        [MemberData(nameof(ValidCollectionSizes))]
        public void ValueSetBuilder_Generic_RemoveWhere_NullMatchPredicate(int setLength)
        {
            ValueSetBuilder<T> set = (ValueSetBuilder<T>)GenericISetFactory(setLength);
            Assert.Throws<ArgumentNullException>(() => set.RemoveWhere(null));
        }

        
#if NET472_OR_GREATER || NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_0_OR_GREATER
        [Theory]
        [InlineData(1, -1)]
        [InlineData(2, 1)]
        public void ValueSetBuilder_TrimAccessWithInvalidArg_ThrowOutOfRange(int size, int newCapacity)
        {
            ValueSetBuilder<T> hashSet = (ValueSetBuilder<T>)GenericISetFactory(size);

            AssertExtensions.Throws<ArgumentOutOfRangeException>(() => hashSet.TrimExcess(newCapacity));
        }

        [Theory]
        [InlineData(0, 20, 7)]
        [InlineData(10, 20, 10)]
        [InlineData(10, 20, 13)]
        public void HashHet_Generic_TrimExcess_LargePopulatedValueSetBuilder_TrimReducesSize(int initialCount, int initialCapacity, int trimCapacity)
        {
            ValueSetBuilder<T> set = CreateValueSetBuilderSetWithCapacity(initialCount, initialCapacity);
            ValueSetBuilder<T> clone = new(set);

            Assert.True(set.Capacity >= initialCapacity);
            Assert.Equal(initialCount, set.Count);

            set.TrimExcess(trimCapacity);

            Assert.True(trimCapacity <= set.Capacity && set.Capacity < initialCapacity);
            Assert.Equal(initialCount, set.Count);
            Assert.Equal(clone, set);
        }

        [Theory]
        [InlineData(10, 20, 0)]
        [InlineData(10, 20, 7)]
        public void HashHet_Generic_TrimExcess_LargePopulatedValueSetBuilder_TrimCapacityIsLessThanCount_ThrowsArgumentOutOfRangeException(int initialCount, int initialCapacity, int trimCapacity)
        {
            ValueSetBuilder<T> set = CreateValueSetBuilderSetWithCapacity(initialCount, initialCapacity);

            Assert.True(set.Capacity >= initialCapacity);
            Assert.Equal(initialCount, set.Count);

            Assert.Throws<ArgumentOutOfRangeException>(() => set.TrimExcess(trimCapacity));

            Assert.True(set.Capacity >= initialCapacity);
            Assert.Equal(initialCount, set.Count);
        }
#endif
        [Theory]
        [MemberData(nameof(ValidCollectionSizes))]
        public void ValueSetBuilder_Generic_TrimExcess_OnValidSetThatHasntBeenRemovedFrom(int setLength)
        {
            ValueSetBuilder<T> set = (ValueSetBuilder<T>)GenericISetFactory(setLength);
            set.TrimExcess();
        }

        [Theory]
        [MemberData(nameof(ValidCollectionSizes))]
        public void ValueSetBuilder_Generic_TrimExcess_Repeatedly(int setLength)
        {
            ValueSetBuilder<T> set = (ValueSetBuilder<T>)GenericISetFactory(setLength);
            List<T> expected = set.ToList();
            set.TrimExcess();
            set.TrimExcess();
            set.TrimExcess();
            Assert.True(set.SetEquals(expected));
        }

        [Theory]
        [MemberData(nameof(ValidCollectionSizes))]
        public void ValueSetBuilder_Generic_TrimExcess_AfterRemovingOneElement(int setLength)
        {
            if (setLength > 0)
            {
                ValueSetBuilder<T> set = (ValueSetBuilder<T>)GenericISetFactory(setLength);
                List<T> expected = set.ToList();
                T elementToRemove = set.ElementAt(0);

                set.TrimExcess();
                Assert.True(set.TryRemove(elementToRemove));
                expected.Remove(elementToRemove);
                set.TrimExcess();

                Assert.True(set.SetEquals(expected));
            }
        }

        [Theory]
        [MemberData(nameof(ValidCollectionSizes))]
        public void ValueSetBuilder_Generic_TrimExcess_AfterClearingAndAddingSomeElementsBack(int setLength)
        {
            if (setLength > 0)
            {
                ValueSetBuilder<T> set = (ValueSetBuilder<T>)GenericISetFactory(setLength);
                set.TrimExcess();
                set.Clear();
                set.TrimExcess();
                Assert.Equal(0, set.Count);

                AddToCollection(set, setLength / 10);
                set.TrimExcess();
                Assert.Equal(setLength / 10, set.Count);
            }
        }

        [Theory]
        [MemberData(nameof(ValidCollectionSizes))]
        public void ValueSetBuilder_Generic_TrimExcess_AfterClearingAndAddingAllElementsBack(int setLength)
        {
            if (setLength > 0)
            {
                ValueSetBuilder<T> set = (ValueSetBuilder<T>)GenericISetFactory(setLength);
                set.TrimExcess();
                set.Clear();
                set.TrimExcess();
                Assert.Equal(0, set.Count);

                AddToCollection(set, setLength);
                set.TrimExcess();
                Assert.Equal(setLength, set.Count);
            }
        }

        [Fact]
        public void CanBeCastedToISet()
        {
            ValueSetBuilder<T> set = new ValueSetBuilder<T>();
            ISet<T> iset = (set as ISet<T>);
            Assert.NotNull(iset);
        }

#if NET472_OR_GREATER || NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_0_OR_GREATER
        [Theory]
        [MemberData(nameof(ValidCollectionSizes))]
        public void ValueSetBuilder_Generic_Constructor_int(int capacity)
        {
            ValueSetBuilder<T> set = new ValueSetBuilder<T>(capacity);
            Assert.Equal(0, set.Count);
        }

        [Theory]
        [MemberData(nameof(ValidCollectionSizes))]
        public void ValueSetBuilder_Generic_Constructor_int_AddUpToAndBeyondCapacity(int capacity)
        {
            ValueSetBuilder<T> set = new ValueSetBuilder<T>(capacity);

            AddToCollection(set, capacity);
            Assert.Equal(capacity, set.Count);

            AddToCollection(set, capacity + 1);
            Assert.Equal(capacity + 1, set.Count);
        }

        [Fact]
        public void ValueSetBuilder_Generic_Constructor_Capacity_ToNextPrimeNumber()
        {
            // Highest pre-computed number + 1.
            const int Capacity = 7199370;
            var set = new ValueSetBuilder<T>(Capacity);

            // Assert that the HashTable's capacity is set to the descendant prime number of the given one.
            const int NextPrime = 7199371;
            Assert.Equal(NextPrime, set.EnsureAndGetCapacity(0));
        }

        [Fact]
        public void ValueSetBuilder_Generic_Constructor_int_Negative_ThrowsArgumentOutOfRangeException()
        {
            AssertExtensions.Throws<ArgumentOutOfRangeException>("capacity", () => new ValueSetBuilder<T>(-1));
            AssertExtensions.Throws<ArgumentOutOfRangeException>("capacity", () => new ValueSetBuilder<T>(int.MinValue));
        }

        [Fact]
        public void EnsureCapacity_Generic_NegativeCapacityRequested_Throws()
        {
            var set = new ValueSetBuilder<T>();
            AssertExtensions.Throws<ArgumentOutOfRangeException>("capacity", () => set.EnsureCapacity(-1));
        }

        [Fact]
        public void EnsureCapacity_Generic_HashsetNotInitialized_RequestedZero_ReturnsZero()
        {
            var set = new ValueSetBuilder<T>();
            Assert.Equal(0, set.Capacity);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        public void EnsureCapacity_Generic_HashsetNotInitialized_RequestedNonZero_CapacityIsSetToAtLeastTheRequested(int requestedCapacity)
        {
            var set = new ValueSetBuilder<T>();
            Assert.InRange(set.EnsureAndGetCapacity(requestedCapacity), requestedCapacity, int.MaxValue);
        }

        [Theory]
        [InlineData(3)]
        [InlineData(7)]
        public void EnsureCapacity_Generic_RequestedCapacitySmallerThanCurrent_CapacityUnchanged(int currentCapacity)
        {
            ValueSetBuilder<T> set;

            // assert capacity remains the same when ensuring a capacity smaller or equal than existing
            for (int i = 0; i <= currentCapacity; i++)
            {
                set = new ValueSetBuilder<T>(currentCapacity);
                Assert.Equal(currentCapacity, set.EnsureAndGetCapacity(i));
            }
        }

        [Theory]
        [InlineData(7)]
        [InlineData(89)]
        public void EnsureCapacity_Generic_ExistingCapacityRequested_SameValueReturned(int capacity)
        {
            var set = new ValueSetBuilder<T>(capacity);
            Assert.Equal(capacity, set.EnsureAndGetCapacity(capacity));

            set = (ValueSetBuilder<T>)GenericISetFactory(capacity);
            Assert.Equal(capacity, set.EnsureAndGetCapacity(capacity));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        public void EnsureCapacity_Generic_EnsureCapacityCalledTwice_ReturnsSameValue(int setLength)
        {
            ValueSetBuilder<T> set = (ValueSetBuilder<T>)GenericISetFactory(setLength);
            int capacity = set.Capacity;
            Assert.Equal(capacity, set.EnsureAndGetCapacity(0));

            set = (ValueSetBuilder<T>)GenericISetFactory(setLength);
            capacity = set.EnsureAndGetCapacity(setLength);
            Assert.Equal(capacity, set.EnsureAndGetCapacity(setLength));

            set = (ValueSetBuilder<T>)GenericISetFactory(setLength);
            capacity = set.EnsureAndGetCapacity(setLength + 1);
            Assert.Equal(capacity, set.EnsureAndGetCapacity(setLength + 1));
        }

        [Theory]
        [InlineData(1)]
        [InlineData(5)]
        [InlineData(7)]
        [InlineData(8)]
        public void EnsureCapacity_Generic_HashsetNotEmpty_RequestedSmallerThanCount_ReturnsAtLeastSizeOfCount(int setLength)
        {
            ValueSetBuilder<T> set = (ValueSetBuilder<T>)GenericISetFactory(setLength);
            Assert.InRange(set.EnsureAndGetCapacity(setLength - 1), setLength, int.MaxValue);
        }

        [Theory]
        [InlineData(7)]
        [InlineData(20)]
        public void EnsureCapacity_Generic_HashsetNotEmpty_SetsToAtLeastTheRequested(int setLength)
        {
            ValueSetBuilder<T> set = (ValueSetBuilder<T>)GenericISetFactory(setLength);

            // get current capacity
            int currentCapacity = set.Capacity;

            // assert we can update to a larger capacity
            int newCapacity = set.EnsureAndGetCapacity(currentCapacity * 2);
            Assert.InRange(newCapacity, currentCapacity * 2, int.MaxValue);
        }

        [Fact]
        public void EnsureCapacity_Generic_CapacityIsSetToPrimeNumberLargerOrEqualToRequested()
        {
            var set = new ValueSetBuilder<T>();
            Assert.Equal(17, set.EnsureAndGetCapacity(17));

            set = new ValueSetBuilder<T>();
            Assert.Equal(17, set.EnsureAndGetCapacity(15));

            set = new ValueSetBuilder<T>();
            Assert.Equal(17, set.EnsureAndGetCapacity(13));
        }

        [Theory]
        [InlineData(2)]
        [InlineData(10)]
        public void EnsureCapacity_Generic_GrowCapacityWithFreeList(int setLength)
        {
            ValueSetBuilder<T> set = (ValueSetBuilder<T>)GenericISetFactory(setLength);

            // Remove the first element to ensure we have a free list.
            Assert.True(set.TryRemove(set.ElementAt(0)));

            int currentCapacity = set.Capacity;
            Assert.True(currentCapacity > 0);

            int newCapacity = set.EnsureAndGetCapacity(currentCapacity + 1);
            Assert.True(newCapacity > currentCapacity);
        }

        /// <summary>
        /// Create a ValueSetBuilder with a specific initial capacity and fill it with a specific number of elements.
        /// </summary>
        protected ValueSetBuilder<T> CreateValueSetBuilderSetWithCapacity(int count, int capacity)
        {
            var set = new ValueSetBuilder<T>(capacity);
            int seed = 528;

            for (int i = 0; i < count; i++)
            {
                while (!set.TryAdd(CreateT(seed++))) ;
            }

            return set;
        }
#endif

    }

    file static class ValueSetBuilderExtensions
    {
#if NET472_OR_GREATER || NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_0_OR_GREATER
        internal static int EnsureAndGetCapacity<T>(this ValueSetBuilder<T> builder, int capacity)
        {
            builder.EnsureCapacity(capacity);
            return builder.Capacity;
        }
#endif

        internal static int RemoveAndCountWhere<T>(this ValueSetBuilder<T> builder, Predicate<T> match)
        {
            var countBefore = builder.Count;
            builder.RemoveWhere(match);
            return countBefore - builder.Count;
        }
    }
}
