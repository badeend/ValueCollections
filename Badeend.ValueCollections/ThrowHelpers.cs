using System.Diagnostics.CodeAnalysis;

namespace Badeend.ValueCollections;

internal static class ThrowHelpers
{
	[DoesNotReturn]
	internal static void ThrowBuiltException()
	{
		throw new InvalidOperationException("Builder has already been built");
	}

	[DoesNotReturn]
	internal static void ThrowUninitializedBuilerException()
	{
		throw new InvalidOperationException("Uninitialized builder");
	}
}
