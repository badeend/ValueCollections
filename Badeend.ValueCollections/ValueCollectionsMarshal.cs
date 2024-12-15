using System.Runtime.CompilerServices;
using Badeend.ValueCollections.Internals;

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
	/// > [!WARNING]
	/// > Ownership of the array is moved into the ValueSlice. It is the caller's
	/// responsibility to never mutate the array ever again.
	/// </summary>
	public static ValueSlice<T> AsValueSlice<T>(T[] items) => new(items);

	/// <summary>
	/// Create a new <see cref="ValueList{T}"/> using the provided mutable array
	/// as its backing store. This operation only allocates a fixed amount of
	/// memory for the new ValueList instance. For the actual content it reuses
	/// the provided array instead of copying it over.
	///
	/// > [!WARNING]
	/// > Ownership of the array is moved into the ValueList. It is the caller's
	/// responsibility to never mutate the array ever again.
	/// </summary>
	public static ValueList<T> AsValueList<T>(T[] items) => ValueList<T>.CreateImmutableUnsafe(RawList.CreateFromArrayUnsafe(items, items.Length));

	/// <summary>
	/// Update the count of the <paramref name="builder"/>.
	///
	/// > [!WARNING]
	/// > When increasing the count, this may expose uninitialized, garbage data.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void SetCount<T>(ValueList<T>.Builder builder, int count) => builder.SetCountUnsafe(count);

	/// <summary>
	/// Get a <see cref="Span{T}"/> view over the current data in the
	/// <paramref name="builder"/>.
	///
	/// > [!WARNING]
	/// > The builder should not be built or mutated while the span is in
	/// use. Especially do not feed the span back into the builder itself using e.g.
	/// <see cref="ValueList{T}.Builder.AddRange(ReadOnlySpan{T})"/> or
	/// <see cref="ValueList{T}.Builder.InsertRange(int, ReadOnlySpan{T})"/>.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static Span<T> AsSpan<T>(ValueList<T>.Builder builder) => builder.AsSpanUnsafe();

	/// <summary>
	/// Get a mutable reference to a value in the dictionary, or a null ref if
	/// it does not exist.
	///
	/// > [!WARNING]
	/// > The builder should not be built or mutated while the returned reference
	/// is still in use.
	/// </summary>
	/// <remarks>
	/// Note that a "null ref" is not the same as a reference to a
	/// <c>null</c> value. A null ref can be detected by calling <c>Unsafe.IsNullRef</c>.
	/// </remarks>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static ref TValue GetValueRefOrNullRef<TKey, TValue>(ValueDictionary<TKey, TValue>.Builder builder, TKey key)
		where TKey : notnull => ref builder.GetValueRefOrNullRefUnsafe(key);

	/// <summary>
	/// Get a reference to a value in the dictionary, or a null ref if it does
	/// not exist.
	/// </summary>
	/// <remarks>
	/// Note that a "null ref" is not the same as a reference to a
	/// <c>null</c> value. A null ref can be detected by calling <c>Unsafe.IsNullRef</c>.
	/// </remarks>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static ref readonly TValue GetValueRefOrNullRef<TKey, TValue>(ValueDictionary<TKey, TValue> dictionary, TKey key)
		where TKey : notnull => ref dictionary.GetValueRefOrNullRefUnsafe(key);
}
