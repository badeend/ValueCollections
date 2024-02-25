using System.Runtime.CompilerServices;

namespace Badeend.ValueCollections;

/// <summary>
/// <b>Unsafe</b> utility methods.
/// </summary>
public static class ValueCollectionsMarshal
{
	/// <summary>
	/// Create a new <see cref="ValueSlice{T}"/> using the provided mutable array
	/// as its backing store. This operation does not allocate any memory.
	///
	/// <b>Warning!</b> Ownership of the array is moved into the ValueSlice. It is the caller's
	/// responsibility to never mutate the array ever again.
	/// </summary>
	public static ValueSlice<T> AsValueSlice<T>(T[] items) => new(items);

	/// <summary>
	/// Create a new <see cref="ValueSlice{T}"/> using the provided mutable list
	/// as its backing store. This operation does not allocate any memory.
	///
	/// <b>Warning!</b> Ownership of the list is moved into the ValueSlice. It is the caller's
	/// responsibility to never mutate the list ever again.
	/// </summary>
	public static ValueSlice<T> AsValueSlice<T>(List<T> items) => new(GetBackingArrayUnsafe(items), 0, items.Count);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static T[] GetBackingArrayUnsafe<T>(List<T> items)
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
	private class ListLayout<T>
	{
		internal T[] _items = null!;
		internal int _size;
		internal int _version;
	}
#pragma warning restore CA1852 // Seal internal types
#pragma warning restore CA1812 // Avoid uninstantiated internal classes
#pragma warning restore CS0649 // Field 'field' is never assigned to, and will always have its default value 'value'
#pragma warning restore SA1401 // Fields should be private
#pragma warning restore SA1309 // Field names should not begin with underscore
}
