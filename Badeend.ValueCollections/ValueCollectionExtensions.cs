namespace Badeend.ValueCollections;

/// <summary>
/// Extension methods related to ValueCollections.
/// </summary>
public static class ValueCollectionExtensions
{
	/// <summary>
	/// Copy the <paramref name="items"/> into a new <see cref="ValueSlice{T}"/>.
	/// </summary>
	public static ValueSlice<T> ToValueSlice<T>(this ReadOnlySpan<T> items) => new(items.ToArray());

	/// <summary>
	/// Copy the <paramref name="items"/> into a new <see cref="ValueSlice{T}"/>.
	/// </summary>
	public static ValueSlice<T> ToValueSlice<T>(this Span<T> items) => new(items.ToArray());

	/// <summary>
	/// Copy the <paramref name="items"/> into a new <see cref="ValueSlice{T}"/>.
	/// </summary>
	public static ValueSlice<T> ToValueSlice<T>(this ReadOnlyMemory<T> items) => new(items.ToArray());

	/// <summary>
	/// Copy the <paramref name="items"/> into a new <see cref="ValueSlice{T}"/>.
	/// </summary>
	public static ValueSlice<T> ToValueSlice<T>(this Memory<T> items) => new(items.ToArray());

	/// <summary>
	/// Copy the <paramref name="items"/> into a new <see cref="ValueSlice{T}"/>.
	/// </summary>
	/// <remarks>
	/// If you know the input List will not be used anymore, you might be able
	/// to take advantage of <c>ValueCollectionsMarshal.AsValueSlice</c> and
	/// prevent unnecessary copying.
	/// </remarks>
	public static ValueSlice<T> ToValueSlice<T>(this List<T> items) => new(items.ToArray());

	/// <summary>
	/// Copy the <paramref name="items"/> into a new <see cref="ValueSlice{T}"/>.
	/// </summary>
	public static ValueSlice<T> ToValueSlice<T>(this IEnumerable<T> items) => new(items.ToArray());

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
}
