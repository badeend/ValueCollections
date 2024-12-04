namespace Badeend.ValueCollections.Internals;

internal static class Utilities
{
	/// <summary>
	/// Check if it is worthwhile to reuse an existing allocation,
	/// without unnecessarily keeping a large allocation alive.
	/// </summary>
	internal static bool IsReuseWorthwhile(int capacity, int size)
	{
		const int SmallCapacity = 4;
		const double SizeRatio = 0.75;

		if (capacity <= SmallCapacity)
		{
			return true;
		}

		var threshold = (int)(((double)capacity) * SizeRatio);

		return size >= threshold;
	}
}
