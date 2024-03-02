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
	public static ValueList<T> AsValueList<T>(T[] items) => ValueList<T>.FromArrayUnsafe(items);

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
	public static ValueList<T> AsValueList<T>(List<T> items) => ValueList<T>.FromArrayUnsafe(UnsafeHelpers.GetBackingArray(items), items.Count);

	/// <summary>
	/// Create a new <see cref="ValueListBuilder{T}"/> using the provided mutable
	/// array as its initial backing store. This is a <c>O(1)</c> operation that
	/// only allocates a fixed amount of memory for the new ValueListBuilder
	/// instance. For the actual content it reuses the provided array instead of
	/// copying it over.
	///
	/// <b>Warning!</b> Ownership of the array is moved into the ValueListBuilder.
	/// It is the caller's responsibility to never touch the array ever again.
	/// </summary>
	public static ValueListBuilder<T> AsValueListBuilder<T>(T[] items) => ValueListBuilder<T>.FromArrayUnsafe(items);

	/// <summary>
	/// Create a new <see cref="ValueListBuilder{T}"/> using the provided mutable
	/// list as its initial backing store. This is a <c>O(1)</c> operation that
	/// only allocates a fixed amount of memory for the new ValueListBuilder
	/// instance. For the actual content it reuses the backing array of the
	/// provided list instead of copying it over.
	///
	/// <b>Warning!</b> Ownership of the list is moved into the ValueListBuilder.
	/// It is the caller's responsibility to never touch the list ever again.
	/// </summary>
	public static ValueListBuilder<T> AsValueListBuilder<T>(List<T> items) => ValueListBuilder<T>.FromListUnsafe(items);

	/// <summary>
	/// Update the count of the <paramref name="builder"/>. When increasing the
	/// count, this exposes uninitialized data.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void SetCount<T>(ValueListBuilder<T> builder, int count) => builder.SetCountUnsafe(count);

	/// <summary>
	/// Get a <see cref="Span{T}"/> view over the current data in the
	/// <paramref name="builder"/>.
	///
	/// <b>Warning!</b> The builder should not be accessed while the span is in
	/// use. Unlike the <see cref="List{T}"/> equivalent of this method
	/// (<c>CollectionsMarshal.AsSpan</c>), even just <em>reading</em> from the
	/// builder might trigger undefined behavior.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static Span<T> AsSpan<T>(ValueListBuilder<T> builder) => builder.AsSpanUnsafe();
}
