using System.Reflection;
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
	/// responsibility to never mutate array ever again.
	/// </summary>
	public static ValueSlice<T> AsValueSlice<T>(T[] items) => new(items);

#if NET8_0_OR_GREATER
	/// <summary>
	/// Create a new <see cref="ValueSlice{T}"/> using the provided mutable list
	/// as its backing store. This operation does not allocate any memory.
	///
	/// <b>Warning!</b> Ownership of the list is moved into the ValueSlice. It is the caller's
	/// responsibility to never mutate list ever again.
	///
	/// Supported on .NET 8 and higher.
	/// </summary>
	public static ValueSlice<T> AsValueSlice<T>(List<T> items) => new(GetListItems(items), 0, items.Count);

	[UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_items")]
	private static extern ref T[] GetListItems<T>(List<T> @this);
#endif
}
