// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
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
        // Has tests that pass a variably sized TestCollection and MyEnumerable to the AddRange function

        [Theory]
        [MemberData(nameof(EnumerableTestData))]
        public void AddRange(EnumerableType enumerableType, int listLength, int enumerableLength, int numberOfMatchingElements, int numberOfDuplicateElements)
        {
            ValueListBuilder<T> list = GenericListFactory(listLength);
            ValueListBuilder<T> listBeforeAdd = list.ToValueListBuilder();
            IEnumerable<T> enumerable = CreateEnumerable(enumerableType, list, enumerableLength, numberOfMatchingElements, numberOfDuplicateElements);
            list.AddRange(enumerable);

            // Check that the first section of the List is unchanged
            Assert.All(Enumerable.Range(0, listLength), index =>
            {
                Assert.Equal(listBeforeAdd[index], list[index]);
            });

            // Check that the added elements are correct
            Assert.All(Enumerable.Range(0, enumerableLength), index =>
            {
                Assert.Equal(enumerable.ElementAt(index), list[index + listLength]);
            });
        }

        [Theory]
        [MemberData(nameof(ListTestData))]
        public void AddRange_Span(EnumerableType enumerableType, int listLength, int enumerableLength, int numberOfMatchingElements, int numberOfDuplicateElements)
        {
            ValueListBuilder<T> list = GenericListFactory(listLength);
            ValueListBuilder<T> listBeforeAdd = list.ToValueListBuilder();
            Span<T> span = CreateEnumerable(enumerableType, list, enumerableLength, numberOfMatchingElements, numberOfDuplicateElements).ToArray();
            list.AddRange(span);

            // Check that the first section of the List is unchanged
            Assert.All(Enumerable.Range(0, listLength), index =>
            {
                Assert.Equal(listBeforeAdd[index], list[index]);
            });

            // Check that the added elements are correct
            for (int i = 0; i < enumerableLength; i++)
            {
                Assert.Equal(span[i], list[i + listLength]);
            };
        }

        [Fact]
        public void AddRange_NullList_ThrowsArgumentNullException()
        {
            AssertExtensions.Throws<ArgumentNullException>("builder", () => ValueCollectionExtensions.AddRange<int>(null, default));
            AssertExtensions.Throws<ArgumentNullException>("builder", () => ValueCollectionExtensions.AddRange<int>(null, new int[1]));
        }

        [Theory]
        [MemberData(nameof(ValidCollectionSizes))]
        public void AddRange_NullEnumerable_ThrowsArgumentNullException(int count)
        {
            ValueListBuilder<T> list = GenericListFactory(count);
            ValueListBuilder<T> listBeforeAdd = list.ToValueListBuilder();
            Assert.Throws<ArgumentNullException>(() => list.AddRange(null));
            Assert.Equal(listBeforeAdd, list);
        }

        [Fact]
        public void AddRange_AddSelfAsEnumerable_ThrowsExceptionWhenNotEmpty()
        {
            ValueListBuilder<T> list = GenericListFactory(0);

            // Succeeds when list is empty.
            list.AddRange(list);
            list.AddRange(list.Where(_ => true));

            // Succeeds when list has elements and is added as collection.
            list.Add(default);
            Assert.Equal(1, list.Count);
            list.AddRange(list);
            Assert.Equal(2, list.Count);
            list.AddRange(list);
            Assert.Equal(4, list.Count);

            // Fails version check when list has elements and is added as non-collection.
            Assert.Throws<InvalidOperationException>(() => list.AddRange(list.Where(_ => true)));
            Assert.Equal(5, list.Count);
            Assert.Throws<InvalidOperationException>(() => list.AddRange(list.Where(_ => true)));
            Assert.Equal(6, list.Count);
        }

        [Fact]
        public void AddRange_CollectionWithLargeCount_ThrowsOverflowException()
        {
            ValueListBuilder<T> list = GenericListFactory(count: 1);
            ICollection<T> collection = new CollectionWithLargeCount();

            Assert.Throws<OverflowException>(() => list.AddRange(collection));
        }

        private class CollectionWithLargeCount : ICollection<T>
        {
            public int Count => int.MaxValue;

            public bool IsReadOnly => throw new NotImplementedException();
            public void Add(T item) => throw new NotImplementedException();
            public void Clear() => throw new NotImplementedException();
            public bool Contains(T item) => throw new NotImplementedException();
            public void CopyTo(T[] array, int arrayIndex) => throw new NotImplementedException();
            public IEnumerator<T> GetEnumerator() => throw new NotImplementedException();
            public bool Remove(T item) => throw new NotImplementedException();
            IEnumerator IEnumerable.GetEnumerator() => throw new NotImplementedException();
        }
    }
}
