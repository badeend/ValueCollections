// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Xunit;

namespace Badeend.ValueCollections.Tests.Reference
{
    /// <summary>
    /// Contains tests that ensure the correctness of the List class.
    /// </summary>
    public abstract partial class ValueList_Tests<T> : IList_Generic_Tests<T>
    {
        [Theory]
        [MemberData(nameof(ValidCollectionSizes))]
        public void ForEach_Verify(int count)
        {
            ValueList<T> list = GenericListFactory(count);
            ValueList<T> visitedItems = new ValueList<T>();
            Action<T> action = delegate (T item) { visitedItems.Add(item); };

            //[] Verify ForEach looks at every item
            visitedItems.Clear();
            list.ForEach(action);
            VerifyList(list, visitedItems);
        }

        [Fact]
        public void ForEach_NullAction_ThrowsArgumentNullException()
        {
            ValueList<T> list = GenericListFactory();
            Assert.Throws<ArgumentNullException>(() => list.ForEach(null));
        }
    }
}
