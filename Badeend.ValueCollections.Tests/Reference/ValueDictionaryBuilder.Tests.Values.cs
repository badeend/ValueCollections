// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics;
using Xunit;

namespace Badeend.ValueCollections.Tests.Reference
{
    public class ValueDictionaryBuilder_Tests_Values : ICollection_Generic_Tests<string>
    {
        protected override bool DefaultValueAllowed => true;
        protected override bool DuplicateValuesAllowed => true;
        protected override bool IsReadOnly => true;
        protected override bool ResetImplemented => false;
        protected override bool Enumerator_Empty_UsesSingletonInstance => true;
        protected override bool Enumerator_Current_UndefinedOperation_Throws => true;
        protected override bool Enumerator_Empty_Current_UndefinedOperation_Throws => true;
        protected override bool Enumerator_Empty_ModifiedDuringEnumeration_ThrowsInvalidOperationException => false;
        protected override bool Enumerator_ModifiedDuringEnumeration_ThrowsInvalidOperationException => true;

        protected override IEnumerable<ModifyEnumerable> GetModifyEnumerables(ModifyOperation operations) => new List<ModifyEnumerable>();

        protected override ICollection<string> GenericICollectionFactory()
        {
            return new ValueDictionaryBuilder<string, string>().Values.AsCollection();
        }

        protected override ICollection<string> GenericICollectionFactory(int count)
        {
            ValueDictionaryBuilder<string, string> list = new ValueDictionaryBuilder<string, string>();
            int seed = 13453;
            for (int i = 0; i < count; i++)
                list.Add(CreateT(seed++), CreateT(seed++));
            return list.Values.AsCollection();
        }

        protected override string CreateT(int seed)
        {
            int stringLength = seed % 10 + 5;
            Random rand = new Random(seed);
            byte[] bytes = new byte[stringLength];
            rand.NextBytes(bytes);
            return Convert.ToBase64String(bytes);
        }

        [Theory]
        [MemberData(nameof(ValidCollectionSizes))]
        public void ValueDictionaryBuilder_ValueCollection_GetEnumerator(int count)
        {
            ValueDictionaryBuilder<string, string> dictionary = new ValueDictionaryBuilder<string, string>();
            int seed = 13453;
            while (dictionary.Count < count)
                dictionary.Add(CreateT(seed++), CreateT(seed++));
            dictionary.Values.GetEnumerator();
        }
    }
}
