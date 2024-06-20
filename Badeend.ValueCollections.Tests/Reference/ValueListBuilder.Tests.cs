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
        #region IList<T> Helper Methods
        protected override bool ResetImplemented => false;
        protected override bool Enumerator_Empty_UsesSingletonInstance => true;
        protected override bool Enumerator_Current_UndefinedOperation_Throws => true;
        protected override bool Enumerator_Empty_Current_UndefinedOperation_Throws => true;
        protected override bool Enumerator_Empty_ModifiedDuringEnumeration_ThrowsInvalidOperationException => false;
        protected override bool Enumerator_ModifiedDuringEnumeration_ThrowsInvalidOperationException => true;

        protected override IList<T> GenericIListFactory()
        {
            return GenericListFactory().AsCollection();
        }

        protected override IList<T> GenericIListFactory(int count)
        {
            return GenericListFactory(count).AsCollection();
        }

        #endregion

        #region ValueList<T>.Builder Helper Methods

        protected virtual ValueList<T>.Builder GenericListFactory()
        {
            return ValueList.CreateBuilder<T>();
        }

        protected virtual ValueList<T>.Builder GenericListFactory(int count)
        {
            IEnumerable<T> toCreateFrom = CreateEnumerable(EnumerableType.List, null, count, 0, 0);
            return toCreateFrom.ToValueListBuilder();
        }

        protected void VerifyList(ValueList<T>.Builder list, ValueList<T>.Builder expectedItems)
        {
            Assert.Equal(expectedItems.Count, list.Count);

            //Only verify the indexer. List should be in a good enough state that we
            //do not have to verify consistency with any other method.
            for (int i = 0; i < list.Count; ++i)
            {
                Assert.True(list[i] == null ? expectedItems[i] == null : list[i].Equals(expectedItems[i]));
            }
        }

        #endregion
    }
}
