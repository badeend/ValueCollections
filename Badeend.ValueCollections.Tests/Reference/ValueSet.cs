// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Xunit;

namespace Badeend.ValueCollections.Tests.Reference
{
    public class ValueSet_Generic_Tests_string : ValueSet_Generic_Tests<string>
    {
        protected override string CreateT(int seed)
        {
            int stringLength = seed % 10 + 5;
            Random rand = new Random(seed);
            byte[] bytes = new byte[stringLength];
            rand.NextBytes(bytes);
            return Convert.ToBase64String(bytes);
        }
    }

    public class ValueSet_Generic_Tests_int : ValueSet_Generic_Tests<int>
    {
        protected override int CreateT(int seed)
        {
            Random rand = new Random(seed);
            return rand.Next();
        }
    }

    public class ValueSet_Generic_Tests_LowEntropyHashCode : ValueSet_Generic_Tests<LowEntropyHashCode>
    {
        protected override LowEntropyHashCode CreateT(int seed)
        {
            Random rand = new Random(seed);
            return new(rand.Next());
        }
    }

    public class ValueSet_Generic_Tests_ConstantHashCode : ValueSet_Generic_Tests<ConstantHashCode>
    {
        protected override ConstantHashCode CreateT(int seed)
        {
            Random rand = new Random(seed);
            return new(rand.Next());
        }
    }

    public class ValueSet_Generic_Tests_BackwardsOrder : ValueSet_Generic_Tests<BackwardsOrder>
    {
        protected override BackwardsOrder CreateT(int seed)
        {
            Random rand = new Random(seed);
            return new(rand.Next());
        }
    }
}
