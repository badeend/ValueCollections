// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Badeend.ValueCollections.Tests.Reference
{
    /// <summary>
    /// Contains tests that ensure the correctness of the List class.
    /// </summary>
    public abstract partial class ValueListBuilder_Tests<T> : IList_Generic_Tests<T>
    {
        [Fact]
        public void Constructor_Default()
        {
            ValueListBuilder<T> list = new ValueListBuilder<T>();
            Assert.Equal(0, list.Capacity); //"Expected capacity of list to be the same as given."
            Assert.Equal(0, list.Count); //"Do not expect anything to be in the list."
            Assert.False(((IList<T>)list).IsReadOnly); //"List should not be readonly"
        }

        [Theory]
        [InlineData(0)]
        [InlineData(10)]
        [InlineData(15)]
        [InlineData(16)]
        [InlineData(17)]
        [InlineData(100)]
        public void Constructor_Capacity(int capacity)
        {
            ValueListBuilder<T> list = new ValueListBuilder<T>(capacity);
            Assert.Equal(capacity, list.Capacity); //"Expected capacity of list to be the same as given."
            Assert.Equal(0, list.Count); //"Do not expect anything to be in the list."
            Assert.False(((IList<T>)list).IsReadOnly); //"List should not be readonly"
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(int.MinValue)]
        public void Constructor_NegativeCapacity_ThrowsArgumentOutOfRangeException(int capacity)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ValueListBuilder<T>(capacity));
        }

        [Theory]
        [MemberData(nameof(EnumerableTestData))]
        public void Constructor_IEnumerable(EnumerableType enumerableType, int listLength, int enumerableLength, int numberOfMatchingElements, int numberOfDuplicateElements)
        {
            _ = listLength;
            _ = numberOfMatchingElements;
            IEnumerable<T> enumerable = CreateEnumerable(enumerableType, null, enumerableLength, 0, numberOfDuplicateElements);
            ValueListBuilder<T> list = new ValueListBuilder<T>(enumerable);
            ValueListBuilder<T> expected = enumerable.ToValueListBuilder();

            Assert.Equal(enumerableLength, list.Count); //"Number of items in list do not match the number of items given."

            for (int i = 0; i < enumerableLength; i++)
                Assert.Equal(expected[i], list[i]); //"Expected object in item array to be the same as in the list"

            Assert.False(((IList<T>)list).IsReadOnly); //"List should not be readonly"
        }

        [Fact]
        public void Constructo_NullIEnumerable_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => { ValueListBuilder<T> _list = new ValueListBuilder<T>(null); }); //"Expected ArgumentnUllException for null items"
        }
    }
}
