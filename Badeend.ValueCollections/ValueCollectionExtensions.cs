using System.Collections.Immutable;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Badeend.ValueCollections;

/// <summary>
/// Extension methods related to ValueCollections.
/// </summary>
public static class ValueCollectionExtensions
{
	/// <summary>
	/// Reinterpret the <see cref="ImmutableArray{T}"/> as a <see cref="ValueSlice{T}"/>.
	/// This does not allocate any memory.
	/// </summary>
	public static ValueSlice<T> AsValueSlice<T>(this ImmutableArray<T> items) => new(UnsafeHelpers.AsArray(items));

	/// <summary>
	/// Copy the <paramref name="items"/> into a new <see cref="ValueSlice{T}"/>.
	/// </summary>
	[Obsolete("Use .AsValueSlice() instead.")]
	[Browsable(false)]
	[EditorBrowsable(EditorBrowsableState.Never)]
	public static ValueSlice<T> ToValueSlice<T>(this ImmutableArray<T> items) => items.AsValueSlice();

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
	public static ValueSlice<T> ToValueSlice<T>(this IEnumerable<T> items)
	{
		if (items is ValueList<T> list)
		{
			return list.AsValueSlice();
		}

		if (items is ValueListBuilder<T> builder)
		{
			return builder.ToValueList().AsValueSlice();
		}

		return new(items.ToArray());
	}

	/// <summary>
	/// Create a new <see cref="ValueList{T}"/> that reuses the backing allocation
	/// of the <see cref="ImmutableArray{T}"/>.
	///
	/// This method allocates a new fixed-size ValueList instance. The items
	/// are not copied.
	/// </summary>
	public static ValueList<T> AsValueList<T>(this ImmutableArray<T> items) => ValueList<T>.FromArrayUnsafe(UnsafeHelpers.AsArray(items));

	/// <summary>
	/// Copy the <paramref name="items"/> into a new <see cref="ValueList{T}"/>.
	/// </summary>
	[Obsolete("Use .AsValueList() instead.")]
	[Browsable(false)]
	[EditorBrowsable(EditorBrowsableState.Never)]
	public static ValueList<T> ToValueList<T>(this ImmutableArray<T> items) => ValueList<T>.FromArrayUnsafe(items.ToArray());

	/// <summary>
	/// Copy the <paramref name="items"/> into a new <see cref="ValueList{T}"/>.
	/// </summary>
	public static ValueList<T> ToValueList<T>(this ReadOnlySpan<T> items) => ValueList<T>.FromArrayUnsafe(items.ToArray());

	/// <summary>
	/// Copy the <paramref name="items"/> into a new <see cref="ValueList{T}"/>.
	/// </summary>
	public static ValueList<T> ToValueList<T>(this Span<T> items) => ValueList<T>.FromArrayUnsafe(items.ToArray());

	/// <summary>
	/// Copy the <paramref name="items"/> into a new <see cref="ValueList{T}"/>.
	/// </summary>
	public static ValueList<T> ToValueList<T>(this ReadOnlyMemory<T> items) => ValueList<T>.FromArrayUnsafe(items.ToArray());

	/// <summary>
	/// Copy the <paramref name="items"/> into a new <see cref="ValueList{T}"/>.
	/// </summary>
	public static ValueList<T> ToValueList<T>(this Memory<T> items) => ValueList<T>.FromArrayUnsafe(items.ToArray());

	/// <summary>
	/// Copy the <paramref name="items"/> into a new <see cref="ValueList{T}"/>.
	/// </summary>
	public static ValueList<T> ToValueList<T>(this IEnumerable<T> items)
	{
		if (items is ValueList<T> list)
		{
			return list;
		}

		if (items is ValueListBuilder<T> builder)
		{
			return builder.ToValueList();
		}

		return ValueList<T>.FromArrayUnsafe(items.ToArray());
	}

	/// <summary>
	/// Copy the <paramref name="items"/> into a new <see cref="ValueListBuilder{T}"/>.
	/// </summary>
	public static ValueListBuilder<T> ToValueListBuilder<T>(this ImmutableArray<T> items) => ValueListBuilder<T>.FromArrayUnsafe(items.ToArray());

	/// <summary>
	/// Copy the <paramref name="items"/> into a new <see cref="ValueListBuilder{T}"/>.
	/// </summary>
	public static ValueListBuilder<T> ToValueListBuilder<T>(this ReadOnlySpan<T> items) => ValueListBuilder<T>.FromArrayUnsafe(items.ToArray());

	/// <summary>
	/// Copy the <paramref name="items"/> into a new <see cref="ValueListBuilder{T}"/>.
	/// </summary>
	public static ValueListBuilder<T> ToValueListBuilder<T>(this Span<T> items) => ValueListBuilder<T>.FromArrayUnsafe(items.ToArray());

	/// <summary>
	/// Copy the <paramref name="items"/> into a new <see cref="ValueListBuilder{T}"/>.
	/// </summary>
	public static ValueListBuilder<T> ToValueListBuilder<T>(this ReadOnlyMemory<T> items) => ValueListBuilder<T>.FromArrayUnsafe(items.ToArray());

	/// <summary>
	/// Copy the <paramref name="items"/> into a new <see cref="ValueListBuilder{T}"/>.
	/// </summary>
	public static ValueListBuilder<T> ToValueListBuilder<T>(this Memory<T> items) => ValueListBuilder<T>.FromArrayUnsafe(items.ToArray());

	/// <summary>
	/// Copy the <paramref name="items"/> into a new <see cref="ValueListBuilder{T}"/>.
	/// </summary>
	[Obsolete("Use .ToBuilder() instead.")]
	[Browsable(false)]
	[EditorBrowsable(EditorBrowsableState.Never)]
	public static ValueListBuilder<T> ToValueListBuilder<T>(this ValueList<T> items) => items.ToBuilder();

	/// <summary>
	/// Copy the <paramref name="items"/> into a new <see cref="ValueListBuilder{T}"/>.
	/// </summary>
	public static ValueListBuilder<T> ToValueListBuilder<T>(this IEnumerable<T> items)
	{
		if (items is ValueList<T> list)
		{
			return list.ToBuilder();
		}

		return ValueListBuilder<T>.FromListUnsafe(new List<T>(items));
	}

	/// <summary>
	/// Add the <paramref name="items"/> to the end of the list.
	/// </summary>
	/// <remarks>
	/// This overload is an extension method to avoid call site ambiguity.
	/// </remarks>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static ValueListBuilder<T> AddRange<T>(this ValueListBuilder<T> builder, ReadOnlySpan<T> items)
	{
		if (builder is null)
		{
			throw new ArgumentNullException(nameof(builder));
		}

		return builder.AddRangeSpan(items);
	}

	/// <summary>
	/// Insert the <paramref name="items"/> into the list at the specified <paramref name="index"/>.
	/// </summary>
	/// <remarks>
	/// This overload is an extension method to avoid call site ambiguity.
	/// </remarks>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static ValueListBuilder<T> InsertRange<T>(this ValueListBuilder<T> builder, int index, ReadOnlySpan<T> items)
	{
		if (builder is null)
		{
			throw new ArgumentNullException(nameof(builder));
		}

		return builder.InsertRangeSpan(index, items);
	}
}
