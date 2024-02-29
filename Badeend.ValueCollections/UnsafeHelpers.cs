using System.Collections.Immutable;
using System.Runtime.CompilerServices;

namespace Badeend.ValueCollections;

internal static class UnsafeHelpers
{
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static T[] AsArray<T>(ImmutableArray<T> items)
	{
		return Unsafe.As<ImmutableArray<T>, T[]>(ref items);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static ImmutableArray<T> AsImmutableArray<T>(T[] items)
	{
		return Unsafe.As<T[], ImmutableArray<T>>(ref items);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static T[] GetBackingArray<T>(List<T> items)
	{
		// Here be dragons...

		// TODO: use UnsafeAccessor when that becomes available for generic types:
		// https://github.com/dotnet/runtime/issues/89439

		// Get the backing array by reinterpreting the List<T> reference as a
		// reference to our own ListLayout<T> with identical memory layout.
		return Unsafe.As<List<T>, ListLayout<T>>(ref items)._items;
	}

	/// <summary>
	/// A clone of <see cref="List{T}"/>'s memory layout.
	///
	/// Historically the memory layout has been surprisingly stable:
	/// - .NET Framework 4.6.2: https://github.com/microsoft/referencesource/blob/4.6.2/mscorlib/system/collections/generic/list.cs
	/// - .NET Framework 4.8: https://github.com/microsoft/referencesource/blob/master/mscorlib/system/collections/generic/list.cs
	/// - .NET 5: https://github.com/dotnet/runtime/blob/v5.0.0/src/libraries/System.Private.CoreLib/src/System/Collections/Generic/List.cs
	/// - .NET 8: https://github.com/dotnet/runtime/blob/v8.0.0/src/libraries/System.Private.CoreLib/src/System/Collections/Generic/List.cs
	/// .
	/// </summary>
#pragma warning disable SA1309 // Field names should not begin with underscore
#pragma warning disable SA1401 // Fields should be private
#pragma warning disable CS0649 // Field 'field' is never assigned to, and will always have its default value 'value'
#pragma warning disable CA1812 // Avoid uninstantiated internal classes
#pragma warning disable CA1852 // Seal internal types
#pragma warning disable CS0414 // Private field is assigned but its value is never used
	private class ListLayout<T>
	{
		internal T[] _items = null!;
		internal int _size;
		internal int _version;
#if !NETCOREAPP3_0_OR_GREATER
		private object _syncRoot = null!;
#endif
	}
#pragma warning restore CS0414 // Private field is assigned but its value is never used
#pragma warning restore CA1852 // Seal internal types
#pragma warning restore CA1812 // Avoid uninstantiated internal classes
#pragma warning restore CS0649 // Field 'field' is never assigned to, and will always have its default value 'value'
#pragma warning restore SA1401 // Fields should be private
#pragma warning restore SA1309 // Field names should not begin with underscore
}
