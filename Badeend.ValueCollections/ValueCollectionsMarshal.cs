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
	public static ValueSlice<T> AsValueSlice<T>(List<T> items) => new(UnsafeHelpers.GetBackingArray(items), 0, items.Count);

	/// <summary>
	/// Create a new <see cref="ValueList{T}"/> using the provided mutable array
	/// as its backing store. This operation only allocates a fixed amount of
	/// memory for the new ValueList instance. For the actual content it reuses
	/// the provided array instead of copying it over.
	///
	/// <b>Warning!</b> Ownership of the array is moved into the ValueList. It is the caller's
	/// responsibility to never mutate the array ever again.
	/// </summary>
	public static ValueList<T> AsValueList<T>(T[] items) => ValueList<T>.FromArray(items);

	/// <summary>
	/// Create a new <see cref="ValueList{T}"/> using the provided mutable list
	/// as its backing store. This operation only allocates a fixed amount of
	/// memory for the new ValueList instance. For the actual content it reuses
	/// the backing array of the provided list rather than of copying the items
	/// over.
	///
	/// <b>Warning!</b> Ownership of the list is moved into the ValueList. It is the caller's
	/// responsibility to never mutate the list ever again.
	/// </summary>
	public static ValueList<T> AsValueList<T>(List<T> items) => ValueList<T>.FromArray(UnsafeHelpers.GetBackingArray(items), items.Count);
}
