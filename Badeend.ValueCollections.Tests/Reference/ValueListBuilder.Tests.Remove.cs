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
        #region RemoveAll(Pred<T>)

        [Theory]
        [MemberData(nameof(ValidCollectionSizes))]
        public void RemoveAll_AllElements(int count)
        {
            ValueList<T>.Builder list = GenericListFactory(count);
            ValueList<T>.Builder beforeList = list.ToValueListBuilder();
            list.RemoveAll((value) => { return true; });
            Assert.Equal(0, list.Count);
        }

        [Theory]
        [MemberData(nameof(ValidCollectionSizes))]
        public void RemoveAll_NoElements(int count)
        {
            ValueList<T>.Builder list = GenericListFactory(count);
            ValueList<T>.Builder beforeList = list.ToValueListBuilder();
            list.RemoveAll((value) => { return false; });
            Assert.Equal(count, list.Count);
            VerifyList(list, beforeList);
        }

        [Theory]
        [MemberData(nameof(ValidCollectionSizes))]
        public void RemoveAll_DefaultElements(int count)
        {
            ValueList<T>.Builder list = GenericListFactory(count);
            ValueList<T>.Builder beforeList = list.ToValueListBuilder();
            Predicate<T> EqualsDefaultElement = (value) => { return default(T) == null ? value == null : default(T).Equals(value); };
            int expectedCount = beforeList.Count - beforeList.AsCollection().Count((value) => EqualsDefaultElement(value));
            list.RemoveAll(EqualsDefaultElement);
            Assert.Equal(expectedCount, list.Count);
        }

        [Fact]
        public void RemoveAll_NullMatchPredicate()
        {
            AssertExtensions.Throws<ArgumentNullException>("match", () => ValueList.CreateBuilder<T>().RemoveAll(null));
        }

        #endregion

        #region RemoveRange

        [Theory]
        [InlineData(10, 3, 3)]
        [InlineData(10, 0, 10)]
        [InlineData(10, 10, 0)]
        [InlineData(10, 5, 5)]
        [InlineData(10, 0, 5)]
        [InlineData(10, 1, 9)]
        [InlineData(10, 9, 1)]
        [InlineData(10, 2, 8)]
        [InlineData(10, 8, 2)]
        public void Remove_Range(int listLength, int index, int count)
        {
            ValueList<T>.Builder list = GenericListFactory(listLength);
            ValueList<T>.Builder beforeList = list.ToValueListBuilder();

            list.RemoveRange(index, count);
            Assert.Equal(list.Count, listLength - count); //"Expected them to be the same."
            for (int i = 0; i < index; i++)
            {
                Assert.Equal(list[i], beforeList[i]); //"Expected them to be the same."
            }

            for (int i = index; i < count - (index + count); i++)
            {
                Assert.Equal(list[i], beforeList[i + count]); //"Expected them to be the same."
            }
        }

        [Theory]
        [MemberData(nameof(ValidCollectionSizes))]
        public void RemoveRange_InvalidParameters(int listLength)
        {
            if (listLength % 2 != 0)
                listLength++;
            ValueList<T>.Builder list = GenericListFactory(listLength);
            Tuple<int, int>[] InvalidParameters = new Tuple<int, int>[]
            {
                Tuple.Create(listLength     ,1             ),
                Tuple.Create(listLength+1   ,0             ),
                Tuple.Create(listLength+1   ,1             ),
                Tuple.Create(listLength     ,2             ),
                Tuple.Create(listLength/2   ,listLength/2+1),
                Tuple.Create(listLength-1   ,2             ),
                Tuple.Create(listLength-2   ,3             ),
                Tuple.Create(1              ,listLength    ),
                Tuple.Create(0              ,listLength+1  ),
                Tuple.Create(1              ,listLength+1  ),
                Tuple.Create(2              ,listLength    ),
                Tuple.Create(listLength/2+1 ,listLength/2  ),
                Tuple.Create(2              ,listLength-1  ),
                Tuple.Create(3              ,listLength-2  ),
            };

            Assert.All(InvalidParameters, invalidSet =>
            {
                if (invalidSet.Item1 >= 0 && invalidSet.Item2 >= 0)
                    AssertExtensions.Throws<ArgumentException>(null, () => list.RemoveRange(invalidSet.Item1, invalidSet.Item2));
            });
        }

        [Theory]
        [MemberData(nameof(ValidCollectionSizes))]
        public void RemoveRange_NegativeParameters(int listLength)
        {
            if (listLength % 2 != 0)
                listLength++;
            ValueList<T>.Builder list = GenericListFactory(listLength);
            Tuple<int, int>[] InvalidParameters = new Tuple<int, int>[]
            {
                Tuple.Create(-1,-1),
                Tuple.Create(-1, 0),
                Tuple.Create(-1, 1),
                Tuple.Create(-1, 2),
                Tuple.Create(0 ,-1),
                Tuple.Create(1 ,-1),
                Tuple.Create(2 ,-1),
            };

            Assert.All(InvalidParameters, invalidSet =>
            {
                Assert.Throws<ArgumentOutOfRangeException>(() => list.RemoveRange(invalidSet.Item1, invalidSet.Item2));
            });
        }

        #endregion
    }
}
