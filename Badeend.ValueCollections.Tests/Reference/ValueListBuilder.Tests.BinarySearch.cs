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
        [Theory]
        [MemberData(nameof(ValidCollectionSizes))]
        public void BinarySearch_ForEveryItemWithoutDuplicates(int count)
        {
            ValueList<T>.Builder list = GenericListFactory(count);
            var listAsCollection = list.AsCollection();
            foreach (T item in list)
                while (listAsCollection.Count((value) => value.Equals(item)) > 1)
                    list.Remove(item);
            list.Sort();
            ValueList<T>.Builder beforeList = list.ToValueListBuilder();

            Assert.All(Enumerable.Range(0, list.Count), index =>
            {
                Assert.Equal(index, list.BinarySearch(beforeList[index]));
                Assert.Equal(beforeList[index], list[index]);
            });
        }

        [Theory]
        [MemberData(nameof(ValidCollectionSizes))]
        public void BinarySearch_ForEveryItemWithDuplicates(int count)
        {
            if (count > 0)
            {
                ValueList<T>.Builder list = GenericListFactory(count);
                list.Add(list[0]);
                list.Sort();
                ValueList<T>.Builder beforeList = list.ToValueListBuilder();

                Assert.All(Enumerable.Range(0, list.Count), index =>
                {
                    Assert.True(list.BinarySearch(beforeList[index]) >= 0);
                    Assert.Equal(beforeList[index], list[index]);
                });
            }
        }
    }
}
