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
        #region IList<T> Helper Methods
        protected override bool IsReadOnly => true;
        protected override bool AddRemoveClear_ThrowsNotSupported => true;
        protected override bool ResetImplemented => false;
        protected override bool Enumerator_Empty_UsesSingletonInstance => true;
        protected override bool Enumerator_Current_UndefinedOperation_Throws => true;
        protected override bool Enumerator_Empty_Current_UndefinedOperation_Throws => true;
        protected override bool Enumerator_Empty_ModifiedDuringEnumeration_ThrowsInvalidOperationException => false;
        protected override bool Enumerator_ModifiedDuringEnumeration_ThrowsInvalidOperationException => true;

        protected override IList<T> GenericIListFactory()
        {
            return GenericListFactory();
        }

        protected override IList<T> GenericIListFactory(int count)
        {
            return GenericListFactory(count);
        }

        #endregion

        #region ValueList<T> Helper Methods

        protected virtual ValueList<T> GenericListFactory()
        {
            return ValueList<T>.Empty;
        }

        protected virtual ValueList<T> GenericListFactory(int count)
        {
            IEnumerable<T> toCreateFrom = CreateEnumerable(EnumerableType.List, null, count, 0, 0);
            return toCreateFrom.ToValueList();
        }

        protected void VerifyList(ValueList<T> list, ValueList<T> expectedItems)
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
