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
	public static ValueSlice<T> ToValueSlice<T>(this IEnumerable<T> items)
	{
		if (items is ValueList<T> list)
		{
			return list.AsValueSlice();
		}

		if (items is ValueListBuilder<T> builder)
		{
			return builder.Build().AsValueSlice();
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
	[Obsolete("Use .Build() instead.")]
	[Browsable(false)]
	[EditorBrowsable(EditorBrowsableState.Never)]
	public static ValueList<T> ToValueList<T>(this ValueListBuilder<T> items) => items.Build();

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
			return builder.Build();
		}

		return ValueList<T>.FromArrayUnsafe(items.ToArray());
	}

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

	/// <summary>
	/// Copy the <paramref name="items"/> into a new <see cref="ValueSet{T}"/>.
	/// </summary>
	[Obsolete("Use .Build() instead.")]
	[Browsable(false)]
	[EditorBrowsable(EditorBrowsableState.Never)]
	public static ValueSet<T> ToValueSet<T>(this ValueSetBuilder<T> items) => items.Build();

	/// <summary>
	/// Copy the <paramref name="items"/> into a new <see cref="ValueSet{T}"/>.
	/// </summary>
	public static ValueSet<T> ToValueSet<T>(this IEnumerable<T> items)
	{
		if (items is ValueSet<T> set)
		{
			return set;
		}

		if (items is ValueSetBuilder<T> builder)
		{
			return builder.Build();
		}

		return ValueSet<T>.FromHashSetUnsafe(new HashSet<T>(items));
	}

	/// <summary>
	/// Copy the <paramref name="items"/> into a new <see cref="ValueSetBuilder{T}"/>.
	/// </summary>
	[Obsolete("Use .ToBuilder() instead.")]
	[Browsable(false)]
	[EditorBrowsable(EditorBrowsableState.Never)]
	public static ValueSetBuilder<T> ToValueSetBuilder<T>(this ValueSet<T> items) => items.ToBuilder();

	/// <summary>
	/// Copy the <paramref name="items"/> into a new <see cref="ValueSetBuilder{T}"/>.
	/// </summary>
	public static ValueSetBuilder<T> ToValueSetBuilder<T>(this IEnumerable<T> items)
	{
		if (items is ValueSet<T> set)
		{
			return set.ToBuilder();
		}

		return ValueSetBuilder<T>.FromHashSetUnsafe(new HashSet<T>(items));
	}

	/// <summary>
	/// Add the <paramref name="items"/> to the set.
	/// </summary>
	/// <remarks>
	/// This overload is an extension method to avoid call site ambiguity.
	/// </remarks>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static ValueSetBuilder<T> UnionWith<T>(this ValueSetBuilder<T> builder, ReadOnlySpan<T> items)
	{
		if (builder is null)
		{
			throw new ArgumentNullException(nameof(builder));
		}

		return builder.UnionWithSpan(items);
	}

	/// <summary>
	/// Remove the <paramref name="items"/> from the set.
	/// </summary>
	/// <remarks>
	/// This overload is an extension method to avoid call site ambiguity.
	/// </remarks>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static ValueSetBuilder<T> ExceptWith<T>(this ValueSetBuilder<T> builder, ReadOnlySpan<T> items)
	{
		if (builder is null)
		{
			throw new ArgumentNullException(nameof(builder));
		}

		return builder.ExceptWithSpan(items);
	}
}
