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
        public static IEnumerable<object[]> ValidCollectionSizes_GreaterThanOne()
        {
            yield return new object[] { 2 };
            yield return new object[] { 20 };
        }

        #region Sort

        [Theory]
        [MemberData(nameof(ValidCollectionSizes_GreaterThanOne))]
        public void Sort_WithoutDuplicates(int count)
        {
            ValueListBuilder<T> list = GenericListFactory(count);
            IComparer<T> comparer = Comparer<T>.Default;
            list.Sort();
            Assert.All(Enumerable.Range(0, count - 2), i =>
            {
                Assert.True(comparer.Compare(list[i], list[i + 1]) < 0);
            });
        }

        [Theory]
        [MemberData(nameof(ValidCollectionSizes_GreaterThanOne))]
        public void Sort_WithDuplicates(int count)
        {
            ValueListBuilder<T> list = GenericListFactory(count);
            list.Add(list[0]);
            IComparer<T> comparer = Comparer<T>.Default;
            list.Sort();
            Assert.All(Enumerable.Range(0, count - 2), i =>
            {
                Assert.True(comparer.Compare(list[i], list[i + 1]) <= 0);
            });
        }

        #endregion
    }
}
