// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Xunit;

namespace Badeend.ValueCollections.Tests.Reference
{
    /// <summary>
    /// Contains tests that ensure the correctness of the List class.
    /// </summary>
    public abstract partial class ValueListBuilder_Tests<T> : IList_Generic_Tests<T>
    {
        [Fact]
        public void EnsureCapacity_NotInitialized_RequestedZero_ReturnsZero()
        {
            var list = ValueList.CreateBuilder<T>();
            list.EnsureCapacity(0);
            Assert.Equal(0, list.Capacity);
        }

        [Fact]
        public void EnsureCapacity_NegativeCapacityRequested_Throws()
        {
            var list = ValueList.CreateBuilder<T>();
            AssertExtensions.Throws<ArgumentOutOfRangeException>("capacity", () => list.EnsureCapacity(-1));
        }

        public static IEnumerable<object[]> EnsureCapacity_LargeCapacity_Throws_MemberData()
        {
#if NET6_0_OR_GREATER
            yield return new object[] { 5, Array.MaxLength + 1 };
#endif
            yield return new object[] { 1, int.MaxValue };
        }

        [Theory]
        [MemberData(nameof(EnsureCapacity_LargeCapacity_Throws_MemberData))]
        public void EnsureCapacity_LargeCapacity_Throws(int count, int requestCapacity)
        {
            ValueList<T>.Builder list = GenericListFactory(count);
            Assert.Throws<OutOfMemoryException>(() => list.EnsureCapacity(requestCapacity));
        }

        [Theory]
        [InlineData(5)]
        public void EnsureCapacity_RequestedCapacitySmallerThanOrEqualToCurrent_CapacityUnchanged(int currentCapacity)
        {
            var list = ValueList.CreateBuilder<T>(currentCapacity);

            for (int requestCapacity = 0; requestCapacity <= currentCapacity; requestCapacity++)
            {
                list.EnsureCapacity(requestCapacity);
                Assert.Equal(currentCapacity, list.Capacity);
            }
        }

        [Theory]
        [MemberData(nameof(ValidCollectionSizes))]
        public void EnsureCapacity_RequestedCapacitySmallerThanOrEqualToCount_CapacityUnchanged(int count)
        {
            ValueList<T>.Builder list = GenericListFactory(count);
            var currentCapacity = list.Capacity;

            for (int requestCapacity = 0; requestCapacity <= count; requestCapacity++)
            {
                list.EnsureCapacity(requestCapacity);
                Assert.Equal(currentCapacity, list.Capacity);
            }
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(5)]
        public void EnsureCapacity_CapacityIsAtLeastTheRequested(int count)
        {
            ValueList<T>.Builder list = GenericListFactory(count);

            int currentCapacity = list.Capacity;
            int requestCapacity = currentCapacity + 1;
            list.EnsureCapacity(requestCapacity);
            Assert.InRange(list.Capacity, requestCapacity, int.MaxValue);
        }

        [Theory]
        [MemberData(nameof(ValidCollectionSizes))]
        public void EnsureCapacity_RequestingLargerCapacity_DoesNotImpactListContent(int count)
        {
            ValueList<T>.Builder list = GenericListFactory(count);
            var copiedList = list.ToValueListBuilder();

            list.EnsureCapacity(list.Capacity + 1);
            Assert.Equal(copiedList.AsCollection(), list.AsCollection());
        }
    }
}
