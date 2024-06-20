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
        public void CopyTo_InvalidArgs_Throws()
        {
            ValueList<int>.Builder list = [1, 2, 3];
            Assert.Throws<ArgumentException>(() => list.CopyTo((Span<int>)new int[2]));
        }

        [Fact]
        public void CopyTo_ItemsCopiedCorrectly()
        {
            ValueList<int>.Builder list;
            Span<int> destination;

            list = ValueList.Builder<int>();
            destination = Span<int>.Empty;
            list.CopyTo(destination);

            list = [1, 2, 3];
            destination = new int[3];
            list.CopyTo(destination);
            Assert.Equal(new[] { 1, 2, 3 }, destination.ToArray());

            list = [1, 2, 3];
            destination = new int[4];
            list.CopyTo(destination);
            Assert.Equal(new[] { 1, 2, 3, 0 }, destination.ToArray());
        }
    }
}
