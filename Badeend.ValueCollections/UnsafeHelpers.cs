using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

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
	internal static Span<T> AsSpan<T>(List<T> items)
	{
#if NET5_0_OR_GREATER
		return CollectionsMarshal.AsSpan(items);
#else
		return GetBackingArray(items).AsSpan(0, items.Count);
#endif
	}

	internal static List<T> AsList<T>(T[] items)
	{
		var list = new List<T>();
		ref var listRef = ref Reflect(ref list);
		listRef._items = items;
		listRef._size = items.Length;
		return list;
	}

	internal static void EnsureCapacity<T>(List<T> list, int capacity)
	{
#if NET6_0_OR_GREATER
		list.EnsureCapacity(capacity);
#else
		if (list.Count < capacity)
		{
			list.Capacity = capacity;
		}
#endif
	}

	internal static void SetCount<T>(List<T> list, int count)
	{
#if NET8_0_OR_GREATER
		CollectionsMarshal.SetCount(list, count);
#else
		if (count < 0)
		{
			throw new ArgumentOutOfRangeException(nameof(count));
		}

		if (count > list.Capacity)
		{
			EnsureCapacity(list, count);
		}

		var previousCount = list.Count;

		ref var listRef = ref Reflect(ref list);
		listRef._size = count;
		listRef._version++;

		if (count < previousCount && ShouldClearOldEntries<T>())
		{
			Array.Clear(listRef._items, count, previousCount - count);
		}
#endif
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static bool ShouldClearOldEntries<T>()
	{
#if NETCOREAPP2_0_OR_GREATER
		return RuntimeHelpers.IsReferenceOrContainsReferences<T>();
#else
		return true;
#endif
	}

	internal static void InsertRange<T>(List<T> list, int index, ReadOnlySpan<T> source)
	{
#if NET8_0_OR_GREATER
		CollectionExtensions.InsertRange(list, index, source);
#else
		if (index < 0 || index > list.Count)
		{
			throw new ArgumentOutOfRangeException(nameof(index));
		}

		if (source.Length == 0)
		{
			return;
		}

		var previousCount = list.Count;
		SetCount(list, list.Count + source.Length);
		var array = GetBackingArray(list);

		// If the insertion point in the middle of the list, shift the existing content over:
		if (index < previousCount)
		{
			Array.Copy(array, index, array, index + source.Length, previousCount - index);
		}

		source.CopyTo(array.AsSpan(index));
#endif
	}

	internal static void AddRange<T>(List<T> list, ReadOnlySpan<T> source)
	{
#if NET8_0_OR_GREATER
		CollectionExtensions.AddRange(list, source);
#else
		if (source.Length == 0)
		{
			return;
		}

		var previousCount = list.Count;
		SetCount(list, previousCount + source.Length);
		var array = GetBackingArray(list);
		source.CopyTo(array.AsSpan(previousCount));
#endif
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static T[] GetBackingArray<T>(List<T> items)
	{
		return Reflect(ref items)._items;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static ref ListLayout<T> Reflect<T>(ref List<T> items)
	{
		// Here be dragons...

		// TODO: use UnsafeAccessor when that becomes available for generic types:
		// https://github.com/dotnet/runtime/issues/89439

		// Get the private fields by reinterpreting the List<T> reference as a
		// reference to our own ListLayout<T> with identical memory layout.
		return ref Unsafe.As<List<T>, ListLayout<T>>(ref items);
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
