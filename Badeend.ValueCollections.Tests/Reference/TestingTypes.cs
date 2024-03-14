// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Collections.Generic;

namespace Badeend.ValueCollections.Tests.Reference
{
    public readonly struct LowEntropyHashCode : IEquatable<LowEntropyHashCode>, IComparable<LowEntropyHashCode>
    {
        private readonly int value;

        public LowEntropyHashCode(int value)
        {
            this.value = value;
        }

        // Use parity as a hashcode so as to have many collisions.
        public override int GetHashCode() => this.value % 2;

        public override bool Equals(object obj) => obj is LowEntropyHashCode other && this.Equals(other);

        public bool Equals(LowEntropyHashCode other) => this.value == other.value;

        public int CompareTo(LowEntropyHashCode other) => this.value - other.value;
    }

    public readonly struct ConstantHashCode : IEquatable<ConstantHashCode>, IComparable<ConstantHashCode>
    {
        private readonly int value;

        public ConstantHashCode(int value)
        {
            this.value = value;
        }

        public override int GetHashCode() => 0;

        public override bool Equals(object obj) => obj is ConstantHashCode other && this.Equals(other);

        public bool Equals(ConstantHashCode other) => this.value == other.value;

        public int CompareTo(ConstantHashCode other) => this.value - other.value;
    }

    public readonly struct BackwardsOrder : IEquatable<BackwardsOrder>, IComparable<BackwardsOrder>
    {
        private readonly int value;

        public BackwardsOrder(int value)
        {
            this.value = value;
        }

        public override int GetHashCode() => this.value;

        public override bool Equals(object obj) => obj is BackwardsOrder other && this.Equals(other);

        public bool Equals(BackwardsOrder other) => this.value == other.value;

        //backwards from the usual integer ordering
        public int CompareTo(BackwardsOrder other) => other.value - this.value;
    }
}
