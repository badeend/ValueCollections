using System.Diagnostics;

namespace Badeend.ValueCollections.Internals;

// Adapted from: https://github.com/dotnet/runtime/blob/df1eaf934a8437f400a1965488bdbbb0a57bf571/src/libraries/Common/src/System/Collections/Generic/BitHelper.cs
internal readonly struct RawBitArray
{
	private const int IntSize = 32;

	private readonly int[]? data;

#if DEBUG
	private readonly int length;
#endif

	public RawBitArray(int length)
	{
		Polyfills.DebugAssert(length >= 0);

		if (length > 0)
		{
			var capacity = ((length - 1) / IntSize) + 1; // Computes (n+31)/32, but avoids overflow.
			this.data = new int[capacity];
		}

#if DEBUG
		this.length = length;
#endif
	}

	public readonly bool this[int index]
	{
		get
		{
#if DEBUG
			Polyfills.DebugAssert((uint)index < (uint)this.length);
#endif

			var indexIntoArray = (int)((uint)index / IntSize);
			var indexIntoInt = (int)((uint)index % IntSize);

			return (this.data![indexIntoArray] & (1 << indexIntoInt)) != 0;
		}

		set
		{
#if DEBUG
			Polyfills.DebugAssert(value == true, "At the time of writing we only ever set an index to true");
			Polyfills.DebugAssert((uint)index < (uint)this.length);
#endif

			var indexIntoArray = (int)((uint)index / IntSize);
			var indexIntoInt = (int)((uint)index % IntSize);

			this.data![indexIntoArray] |= 1 << indexIntoInt;
		}
	}
}
