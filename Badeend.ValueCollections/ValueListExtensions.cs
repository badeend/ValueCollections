using System.Runtime.CompilerServices;

namespace Badeend.ValueCollections;

/// <summary>
/// Extension methods related to <see cref="ValueList{T}"/>.
/// </summary>
public static class ValueListExtensions
{
	/// <summary>
	/// Copy the <paramref name="items"/> into a new <see cref="ValueList{T}"/>.
	/// </summary>
	public static ValueList<T> ToValueList<T>(this ValueSlice<T> items)
	{
		if (items.Length == 0)
		{
			return ValueList<T>.Empty;
		}

		// Try to reuse the existing buffer
		if (items.Offset == 0)
		{
			return ValueList<T>.FromArray(items.Array!, items.Length);
		}

		return ValueList<T>.FromArray(items.ToArray());
	}

	/// <summary>
	/// Copy the <paramref name="items"/> into a new <see cref="ValueList{T}"/>.
	/// </summary>
	public static ValueList<T> ToValueList<T>(this ReadOnlySpan<T> items) => ValueList<T>.FromArray(items.ToArray());

	/// <summary>
	/// Copy the <paramref name="items"/> into a new <see cref="ValueList{T}"/>.
	/// </summary>
	public static ValueList<T> ToValueList<T>(this Span<T> items) => ValueList<T>.FromArray(items.ToArray());

	/// <summary>
	/// Copy the <paramref name="items"/> into a new <see cref="ValueList{T}"/>.
	/// </summary>
	public static ValueList<T> ToValueList<T>(this ReadOnlyMemory<T> items) => ValueList<T>.FromArray(items.ToArray());

	/// <summary>
	/// Copy the <paramref name="items"/> into a new <see cref="ValueList{T}"/>.
	/// </summary>
	public static ValueList<T> ToValueList<T>(this Memory<T> items) => ValueList<T>.FromArray(items.ToArray());

	/// <summary>
	/// Copy the <paramref name="items"/> into a new <see cref="ValueList{T}"/>.
	/// </summary>
	/// <remarks>
	/// If you know the input List will not be used anymore, you might be able
	/// to take advantage of <c>ValueCollectionsMarshal.AsValueList</c> and
	/// prevent unnecessary copying.
	/// </remarks>
	public static ValueList<T> ToValueList<T>(this List<T> items) => ValueList<T>.FromArray(items.ToArray());

	/// <summary>
	/// Copy the <paramref name="items"/> into a new <see cref="ValueList{T}"/>.
	/// </summary>
	public static ValueList<T> ToValueList<T>(this IEnumerable<T> items) => ValueList<T>.FromArray(items.ToArray());

	/// <summary>
	/// Returns <see langword="true"/> when the two lists have identical length
	/// and content.
	///
	/// This is the default comparison mechanism of ValueLists.
	///
	/// Similar to <c>MemoryExtensions.SequenceEqual</c>.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool SequenceEqual<T>(this ValueList<T> list, ValueList<T> other)
		where T : IEquatable<T>
		=> list.AsValueSlice().SequenceEqual(other.AsValueSlice());

	/// <summary>
	/// Returns <see langword="true"/> when the two lists have identical length
	/// and content.
	///
	/// This is the default comparison mechanism of ValueLists.
	///
	/// Similar to <c>MemoryExtensions.SequenceEqual</c>.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool SequenceEqual<T>(this ValueList<T> list, ValueList<T> other, IEqualityComparer<T>? comparer = default)
		=> list.AsValueSlice().SequenceEqual(other.AsValueSlice(), comparer);

	/// <summary>
	/// Return the index of the first occurrence of <paramref name="item"/> in
	/// <paramref name="list"/>, or <c>-1</c> if not found.
	///
	/// Similar to <c>MemoryExtensions.IndexOf</c>.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static int IndexOf<T>(this ValueList<T> list, T item)
		where T : IEquatable<T>
		=> list.AsValueSlice().IndexOf(item);

	/// <summary>
	/// Return the index of the first occurrence of <paramref name="item"/> in
	/// <paramref name="list"/>, or <c>-1</c> if not found.
	///
	/// Similar to <c>MemoryExtensions.IndexOf</c>.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static int IndexOf<T>(this ValueList<T> list, T item, IEqualityComparer<T>? comparer = default)
		=> list.AsValueSlice().IndexOf(item, comparer);

	/// <summary>
	/// Return the index of the last occurrence of <paramref name="item"/> in
	/// <paramref name="list"/>, or <c>-1</c> if not found.
	///
	/// Similar to <c>MemoryExtensions.LastIndexOf</c>.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static int LastIndexOf<T>(this ValueList<T> list, T item)
		where T : IEquatable<T>
		=> list.AsValueSlice().LastIndexOf(item);

	/// <summary>
	/// Return the index of the last occurrence of <paramref name="item"/> in
	/// <paramref name="list"/>, or <c>-1</c> if not found.
	///
	/// Similar to <c>MemoryExtensions.LastIndexOf</c>.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static int LastIndexOf<T>(this ValueList<T> list, T item, IEqualityComparer<T>? comparer = default)
		=> list.AsValueSlice().LastIndexOf(item, comparer);

	/// <summary>
	/// Returns <see langword="true"/> when the <paramref name="list"/>
	/// contains the specified <paramref name="item"/>.
	///
	/// Similar to <c>MemoryExtensions.Contains</c>.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool Contains<T>(this ValueList<T> list, T item)
		where T : IEquatable<T>
		=> list.AsValueSlice().Contains(item);

	/// <summary>
	/// Returns <see langword="true"/> when the <paramref name="list"/>
	/// contains the specified <paramref name="item"/>.
	///
	/// Similar to <c>MemoryExtensions.Contains</c>.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool Contains<T>(this ValueList<T> list, T item, IEqualityComparer<T>? comparer = default)
		=> list.AsValueSlice().Contains(item, comparer);

	/// <summary>
	/// Finds the length of any common prefix shared between
	/// <paramref name="list"/> and <paramref name="other"/>. If there's no
	/// shared prefix, <c>0</c> is returned.
	///
	/// Similar to <c>MemoryExtensions.CommonPrefixLength</c>.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static int CommonPrefixLength<T>(this ValueList<T> list, ValueList<T> other)
		where T : IEquatable<T>
		=> list.AsValueSlice().CommonPrefixLength(other.AsValueSlice());

	/// <summary>
	/// Finds the length of any common prefix shared between
	/// <paramref name="list"/> and <paramref name="other"/>. If there's no
	/// shared prefix, <c>0</c> is returned.
	///
	/// Similar to <c>MemoryExtensions.CommonPrefixLength</c>.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static int CommonPrefixLength<T>(this ValueList<T> list, ValueList<T> other, IEqualityComparer<T>? comparer = default)
		=> list.AsValueSlice().CommonPrefixLength(other.AsValueSlice(), comparer);
}
