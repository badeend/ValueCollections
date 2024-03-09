using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Badeend.ValueCollections;

[SuppressMessage("Performance", "CA1823:Avoid unused private fields", Justification = "They're accessed using Unsafe")]
[SuppressMessage("Style", "IDE0044:Add readonly modifier", Justification = "They're modified using Unsafe")]
internal struct UnorderedHashCode
{
	private const int BucketCount = 32; // Must be a power of 2 and match the number of fields in this struct.
	private const int BucketMask = BucketCount - 1;

#pragma warning disable CS0169 // Private field is never used
	private int h0;
	private int h1;
	private int h2;
	private int h3;
	private int h4;
	private int h5;
	private int h6;
	private int h7;
	private int h8;
	private int h9;
	private int h10;
	private int h11;
	private int h12;
	private int h13;
	private int h14;
	private int h15;
	private int h16;
	private int h17;
	private int h18;
	private int h19;
	private int h20;
	private int h21;
	private int h22;
	private int h23;
	private int h24;
	private int h25;
	private int h26;
	private int h27;
	private int h28;
	private int h29;
	private int h30;
	private int h31;
#pragma warning restore CS0169 // Private field is never used

	internal void Add<T>(T value)
	{
		var itemHashCode = value is not null ? (int)value.GetHashCode() : 0;
		var bucketIndex = itemHashCode & BucketMask;

		Debug.Assert(bucketIndex >= 0 && bucketIndex < BucketCount);
		Debug.Assert(Unsafe.AreSame(ref this, ref Unsafe.As<int, UnorderedHashCode>(ref this.h0)));
		Debug.Assert(Unsafe.SizeOf<UnorderedHashCode>() == 4 * BucketCount);

		ref int bucket = ref Unsafe.Add(ref this.h0, bucketIndex);

		// Use unsigned overload of `Max` as hash codes can be negative.
		bucket = unchecked((int)Math.Max((uint)bucket, (uint)itemHashCode));
	}

	internal void WriteTo(ref HashCode destination)
	{
		for (int i = 0; i < BucketCount; i++)
		{
			destination.Add(Unsafe.Add(ref this.h0, i));
		}
	}
}

internal static class HashCodeExtensions
{
	internal static void AddUnordered(this ref HashCode hashCode, ref UnorderedHashCode unorderedHashCode)
	{
		unorderedHashCode.WriteTo(ref hashCode);
	}
}
