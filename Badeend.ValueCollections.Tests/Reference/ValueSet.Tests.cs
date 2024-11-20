// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using Xunit;

namespace Badeend.ValueCollections.Tests.Reference
{
    /// <summary>
    /// Contains tests that ensure the correctness of the ValueSet class.
    /// </summary>
    public abstract class ValueSet_Generic_Tests<T> : ISet_Generic_Tests<T>
    {
        protected override bool IsReadOnly => true;
        protected override bool AddRemoveClear_ThrowsNotSupported => true;
        protected override bool ResetImplemented => false;
        protected override bool Enumerator_Empty_UsesSingletonInstance => true;
        protected override bool Enumerator_Current_UndefinedOperation_Throws => true;
        protected override bool Enumerator_Empty_Current_UndefinedOperation_Throws => true;
        protected override bool Enumerator_Empty_ModifiedDuringEnumeration_ThrowsInvalidOperationException => false;
        protected override bool Enumerator_ModifiedDuringEnumeration_ThrowsInvalidOperationException => true;



        protected override ISet<T> GenericISetFactory()
        {
            return ValueSet<T>.Empty;
        }

        protected override ISet<T> GenericISetFactory(int count)
        {
            var collection = new ValueSetBuilder<T>();
            AddToCollection(collection, count);
            return collection.Build();
        }



        private static IEnumerable<int> NonSquares(int limit)
        {
            for (int i = 0; i != limit; ++i)
            {
                int root = (int)Math.Sqrt(i);
                if (i != root * root)
                    yield return i;
            }
        }

        [Theory]
        [MemberData(nameof(EnumerableTestData))]
        public void ValueSet_Generic_Constructor_IEnumerable(EnumerableType enumerableType, int setLength, int enumerableLength, int numberOfMatchingElements, int numberOfDuplicateElements)
        {
            _ = setLength;
            _ = numberOfMatchingElements;
            IEnumerable<T> enumerable = CreateEnumerable(enumerableType, null, enumerableLength, 0, numberOfDuplicateElements);
            ValueSet<T> set = enumerable.ToValueSet();
            Assert.True(set.SetEquals(enumerable));
        }

        [Theory]
        [MemberData(nameof(ValidCollectionSizes))]
        public void ValueSet_Generic_Constructor_IEnumerable_WithManyDuplicates(int count)
        {
            IEnumerable<T> items = CreateEnumerable(EnumerableType.List, null, count, 0, 0);
            ValueSet<T> hashSetFromDuplicates = Enumerable.Range(0, 40).SelectMany(i => items).ToArray().ToValueSet();
            ValueSet<T> hashSetFromNoDuplicates = items.ToValueSet();
            Assert.True(hashSetFromNoDuplicates.SetEquals(hashSetFromDuplicates));
        }

        [Theory]
        [MemberData(nameof(ValidCollectionSizes))]
        public void ValueSet_Generic_Constructor_ValueSet_SparselyFilled(int count)
        {
            ValueSetBuilder<T> source = CreateEnumerable(EnumerableType.HashSet, null, count, 0, 0).ToValueSetBuilder();
            List<T> sourceElements = source.ToList();
            foreach (int i in NonSquares(count))
                source.Remove(sourceElements[i]);// Unevenly spaced survivors increases chance of catching any spacing-related bugs.


            ValueSet<T> set = source.ToValueSet();
            Assert.True(set.SetEquals(source));
        }

        [Fact]
        public void CanBeCastedToISet()
        {
            ValueSet<T> set = ValueSet<T>.Empty;
            ISet<T> iset = (set as ISet<T>);
            Assert.NotNull(iset);
        }
    }
}
