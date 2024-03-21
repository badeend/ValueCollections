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
    public abstract partial class ValueList_Tests<T> : IList_Generic_Tests<T>
    {
        [Fact]
        public void Constructor_Default()
        {
            ValueList<T> list = ValueList<T>.Empty;
            Assert.Equal(0, list.Count); //"Do not expect anything to be in the list."
            Assert.True(((IList<T>)list).IsReadOnly); //"List should be readonly"
        }

        [Theory]
        [MemberData(nameof(EnumerableTestData))]
        public void Constructor_IEnumerable(EnumerableType enumerableType, int listLength, int enumerableLength, int numberOfMatchingElements, int numberOfDuplicateElements)
        {
            _ = listLength;
            _ = numberOfMatchingElements;
            IEnumerable<T> enumerable = CreateEnumerable(enumerableType, null, enumerableLength, 0, numberOfDuplicateElements);
            ValueList<T> list = enumerable.ToValueList();
            List<T> expected = enumerable.ToList();

            Assert.Equal(enumerableLength, list.Count); //"Number of items in list do not match the number of items given."

            for (int i = 0; i < enumerableLength; i++)
                Assert.Equal(expected[i], list[i]); //"Expected object in item array to be the same as in the list"

            Assert.True(((IList<T>)list).IsReadOnly); //"List should be readonly"
        }
    }
}
