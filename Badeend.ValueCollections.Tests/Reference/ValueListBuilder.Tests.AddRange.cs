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
            ValueList<T>.Builder list = GenericListFactory(listLength);
            ValueList<T>.Builder listBeforeAdd = list.ToValueListBuilder();
            IEnumerable<T> enumerable = CreateEnumerable(enumerableType, list.AsCollection(), enumerableLength, numberOfMatchingElements, numberOfDuplicateElements);
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
            ValueList<T>.Builder list = GenericListFactory(listLength);
            ValueList<T>.Builder listBeforeAdd = list.ToValueListBuilder();
            Span<T> span = CreateEnumerable(enumerableType, list.AsCollection(), enumerableLength, numberOfMatchingElements, numberOfDuplicateElements).ToArray();
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

        [Theory]
        [MemberData(nameof(ValidCollectionSizes))]
        public void AddRange_NullEnumerable_ThrowsArgumentNullException(int count)
        {
            ValueList<T>.Builder list = GenericListFactory(count);
            ValueList<T>.Builder listBeforeAdd = list.ToValueListBuilder();
            Assert.Throws<ArgumentNullException>(() => list.AddRange(null));
            Assert.Equal(listBeforeAdd.AsCollection(), list.AsCollection());
        }

        [Fact]
        public void AddRange_AddSelfAsEnumerable_ThrowsExceptionWhenEmpty()
        {
            ValueList<T>.Builder list = GenericListFactory(0);
            var listAsCollection = list.AsCollection();

            // Fails when list is empty.
            Assert.Throws<InvalidOperationException>(() => list.AddRange(list));
            Assert.Equal(0, list.Count);
            Assert.Throws<InvalidOperationException>(() => list.AddRange(listAsCollection));
            Assert.Equal(0, list.Count);
            Assert.Throws<InvalidOperationException>(() => list.AddRange(listAsCollection.Where(_ => true)));
        }

        [Fact]
        public void AddRange_AddSelfAsEnumerable_ThrowsExceptionWhenNotEmpty()
        {
            ValueList<T>.Builder list = GenericListFactory(0);
            var listAsCollection = list.AsCollection();

            // Succeeds
            list.Add(default);

            // Fails version check when list has elements and is added as non-collection.
            Assert.Throws<InvalidOperationException>(() => list.AddRange(list));
            Assert.Equal(1, list.Count);
            Assert.Throws<InvalidOperationException>(() => list.AddRange(listAsCollection));
            Assert.Equal(1, list.Count);
            Assert.Throws<InvalidOperationException>(() => list.AddRange(listAsCollection.Where(_ => true)));
        }

        [Fact]
        public void AddRange_CollectionWithLargeCount_ThrowsOverflowException()
        {
            ValueList<T>.Builder list = GenericListFactory(count: 1);
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
