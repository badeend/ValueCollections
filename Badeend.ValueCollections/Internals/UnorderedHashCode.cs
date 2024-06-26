namespace Badeend.ValueCollections.Internals;

internal struct UnorderedHashCode
{
	private const int BucketMask = InlineArray32<int>.Length - 1;

	private InlineArray32<int> buckets; // Array length must be a power of 2 for the bitmasking to work.

	internal void Add<T>(T value)
	{
		var itemHashCode = value is not null ? (int)value.GetHashCode() : 0;
		var bucketIndex = itemHashCode & BucketMask;

		var bucket = this.buckets[bucketIndex];

		// Use unsigned overload of `Max` as hash codes can be negative.
		this.buckets[bucketIndex] = unchecked((int)Math.Max((uint)bucket, (uint)itemHashCode));
	}

	internal void WriteTo(ref HashCode destination)
	{
		for (int i = 0; i < InlineArray32<int>.Length; i++)
		{
			destination.Add(this.buckets[i]);
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
