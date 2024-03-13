using System.Collections.Immutable;
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
		PlatformAssert(ReflectList(ref list)._version == 0);
		PlatformAssert(ReflectList(ref list)._size == 0);
		PlatformAssert(ReflectList(ref list)._items is int[]);
		PlatformAssert(ReflectList(ref list)._items.Length == 0);

		list.Add(42);

		PlatformAssert(ReflectList(ref list)._version == 1);
		PlatformAssert(ReflectList(ref list)._size == 1);
		PlatformAssert(ReflectList(ref list)._items.Length > 0);
		PlatformAssert(ReflectList(ref list)._items.Length <= 16);

		list.RemoveAt(0);

		PlatformAssert(ReflectList(ref list)._version == 2);
		PlatformAssert(ReflectList(ref list)._size == 0);
		PlatformAssert(ReflectList(ref list)._items.Length > 0);
		PlatformAssert(ReflectList(ref list)._items.Length <= 16);

#if NETFRAMEWORK
		var set = new HashSet<int>();
		PlatformAssert(ReflectNetFrameworkHashSet(ref set).m_version == 0);
		PlatformAssert(ReflectNetFrameworkHashSet(ref set).m_count == 0);
		PlatformAssert(ReflectNetFrameworkHashSet(ref set).m_buckets is null);
		PlatformAssert(ReflectNetFrameworkHashSet(ref set).m_slots is null);

		set.Add(42);

		PlatformAssert(ReflectNetFrameworkHashSet(ref set).m_version == 1);
		PlatformAssert(ReflectNetFrameworkHashSet(ref set).m_count == 1);
		PlatformAssert(ReflectNetFrameworkHashSet(ref set).m_buckets is int[]);
		PlatformAssert(ReflectNetFrameworkHashSet(ref set).m_buckets!.Length > 0);
		PlatformAssert(ReflectNetFrameworkHashSet(ref set).m_buckets!.Length <= 16);
		PlatformAssert(ReflectNetFrameworkHashSet(ref set).m_slots!.Length > 0);
		PlatformAssert(ReflectNetFrameworkHashSet(ref set).m_slots!.Length <= 16);

		set.Remove(42);

		PlatformAssert(ReflectNetFrameworkHashSet(ref set).m_version == 2);
		PlatformAssert(ReflectNetFrameworkHashSet(ref set).m_count == 0);
		PlatformAssert(ReflectNetFrameworkHashSet(ref set).m_buckets!.Length > 0);
		PlatformAssert(ReflectNetFrameworkHashSet(ref set).m_buckets!.Length <= 16);
		PlatformAssert(ReflectNetFrameworkHashSet(ref set).m_slots!.Length > 0);
		PlatformAssert(ReflectNetFrameworkHashSet(ref set).m_slots!.Length <= 16);
#endif
	}

	private static void PlatformAssert(bool condition)
	{
		if (condition == false)
		{
			throw new PlatformNotSupportedException("The installed binary of ValueCollections does not support the current runtime. Reinstalling the nuget package might resolve this error.");
		}
	}

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
		ref var listRef = ref ReflectList(ref list);
		listRef._items = items;
		listRef._size = items.Length;
		return list;
	}

	internal static void EnsureCapacity<T>(List<T> list, int capacity)
	{
#if NET6_0_OR_GREATER
		list.EnsureCapacity(capacity);
#else
		if (capacity < 0)
		{
			throw new ArgumentOutOfRangeException(nameof(capacity));
		}

		if (list.Capacity < capacity)
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

		ref var listRef = ref ReflectList(ref list);
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
		var newCount = checked(list.Count + source.Length);
		SetCount(list, newCount);
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
		var newCount = checked(previousCount + source.Length);
		SetCount(list, newCount);
		var array = GetBackingArray(list);
		source.CopyTo(array.AsSpan(previousCount));
#endif
	}

	internal static int GetCapacity<T>(HashSet<T> set)
	{
#if NET9_0_OR_GREATER
		return set.Capacity;
#elif NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_0_OR_GREATER
		return set.EnsureCapacity(0);
#elif NETFRAMEWORK
		return ReflectNetFrameworkHashSet(ref set).m_slots?.Length ?? 0;
#endif
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static T[] GetBackingArray<T>(List<T> items)
	{
		return ReflectList(ref items)._items;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static ref ListLayout<T> ReflectList<T>(ref List<T> items)
	{
		// Here be dragons...

		// TODO: use UnsafeAccessor when that becomes available for generic types:
		// https://github.com/dotnet/runtime/issues/89439

		// Get the private fields by reinterpreting the List<T> reference as a
		// reference to our own ListLayout<T> with identical memory layout.
		return ref Unsafe.As<List<T>, ListLayout<T>>(ref items);
	}

#if NETFRAMEWORK
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static ref NetFrameworkHashSetLayout<T> ReflectNetFrameworkHashSet<T>(ref HashSet<T> items)
	{
		// Here be dragons...

		// Get the private fields by reinterpreting the HashSet<T> reference as a
		// reference to our own NetFrameworkHashSetLayout<T> with identical memory layout.
		return ref Unsafe.As<HashSet<T>, NetFrameworkHashSetLayout<T>>(ref items);
	}
#endif

#pragma warning disable SA1309 // Field names should not begin with underscore
#pragma warning disable SA1401 // Fields should be private
#pragma warning disable SA1307 // Accessible fields should begin with upper-case letter
#pragma warning disable SA1308 // Variable names should not be prefixed
#pragma warning disable CS0649 // Field 'field' is never assigned to, and will always have its default value 'value'
#pragma warning disable CA1812 // Avoid uninstantiated internal classes
#pragma warning disable CA1852 // Seal internal types
#pragma warning disable CS0414 // Private field is assigned but its value is never used

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
	private class ListLayout<T>
	{
		internal T[] _items = null!;
		internal int _size;
		internal int _version;
#if !NETCOREAPP3_0_OR_GREATER
		private object _syncRoot = null!;
#endif
	}

	/// <summary>
	/// A clone of <see cref="HashSet{T}"/>'s memory layout.
	/// - .NET Framework 4.6.2: https://github.com/microsoft/referencesource/blob/4.6.2/System.Core/System/Collections/Generic/HashSet.cs
	/// - .NET Framework 4.8: https://github.com/microsoft/referencesource/blob/master/System.Core/System/Collections/Generic/HashSet.cs
	/// .
	/// </summary>
	private class NetFrameworkHashSetLayout<T>
	{
		internal int[]? m_buckets;
		internal Slot[]? m_slots;
		internal int m_count;
		internal int m_lastIndex;
		internal int m_freeList;
		internal IEqualityComparer<T> m_comparer = null!;
		internal int m_version;
		internal System.Runtime.Serialization.SerializationInfo? m_siInfo;

		internal struct Slot
		{
			internal int hashCode;
			internal int next;
			internal T value;
		}
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
