using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Badeend.ValueCollections;

internal static class UnsafeHelpers
{
	// Prevent silent data corruption in case a binary is used that was compiled
	// for a different target framework.
	static UnsafeHelpers()
	{
		var list = new List<int>();
		PlatformAssert(GetBackingArray(list) is int[]);
		PlatformAssert(GetBackingArray(list).Length == 0);

		list.Add(42);

		PlatformAssert(GetBackingArray(list).Length > 0);
		PlatformAssert(GetBackingArray(list).Length <= 16);
		PlatformAssert(GetBackingArray(list)[0] == 42);

		list[0] = 43;

		PlatformAssert(GetBackingArray(list)[0] == 43);
	}

	private static void PlatformAssert(bool condition)
	{
		if (condition == false)
		{
			throw new PlatformNotSupportedException("The installed binary of ValueCollections does not support the current runtime. Reinstalling the nuget package might resolve this error.");
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static Span<T> AsSpan<T>(List<T> items)
	{
#if NET5_0_OR_GREATER
		return CollectionsMarshal.AsSpan(items);
#else
		return GetBackingArray(items).AsSpan(0, items.Count);
#endif
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static T[] GetBackingArray<T>(List<T> items)
	{
		// Here be dragons...

		// Get access to the private field by reinterpreting the List<T>
		// reference as a reference to our own ListLayout<T>.
		return Unsafe.As<List<T>, ListLayout<T>>(ref items)._items;
	}

#pragma warning disable SA1309 // Field names should not begin with underscore
#pragma warning disable SA1401 // Fields should be private
#pragma warning disable SA1307 // Accessible fields should begin with upper-case letter
#pragma warning disable SA1308 // Variable names should not be prefixed
#pragma warning disable CS0649 // Field 'field' is never assigned to, and will always have its default value 'value'
#pragma warning disable CA1812 // Avoid uninstantiated internal classes
#pragma warning disable CA1852 // Seal internal types
#pragma warning disable CS0414 // Private field is assigned but its value is never used
	private class ListLayout<T>
	{
		internal T[] _items = null!;
	}
#pragma warning restore CS0414 // Private field is assigned but its value is never used
#pragma warning restore CA1852 // Seal internal types
#pragma warning restore CA1812 // Avoid uninstantiated internal classes
#pragma warning restore CS0649 // Field 'field' is never assigned to, and will always have its default value 'value'
#pragma warning restore SA1308 // Variable names should not be prefixed
#pragma warning restore SA1307 // Accessible fields should begin with upper-case letter
#pragma warning restore SA1401 // Fields should be private
#pragma warning restore SA1309 // Field names should not begin with underscore
}
