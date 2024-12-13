using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using Badeend.ValueCollections.Internals;

namespace Badeend.ValueCollections;

/// <summary>
/// Extension methods related to ValueCollections.
/// </summary>
public static class ValueCollectionExtensions
{
	/// <summary>
	/// Copy the <paramref name="items"/> into a new <see cref="ValueList{T}"/>.
	/// </summary>
	public static ValueList<T> ToValueList<T>(this IEnumerable<T> items)
	{
		if (items is null)
		{
			ThrowHelpers.ThrowArgumentNullException(ThrowHelpers.Argument.items);
		}

		if (items is ValueList<T> valueList)
		{
			return valueList;
		}

		return ValueList<T>.CreateImmutableUnsafe(new(items));
	}

	/// <summary>
	/// Asynchronously copy the <paramref name="items"/> into a new <see cref="ValueList{T}"/>.
	/// </summary>
	public static async Task<ValueList<T>> ToValueListAsync<T>(this IAsyncEnumerable<T> items, CancellationToken cancellationToken = default)
	{
		if (items is null)
		{
			ThrowHelpers.ThrowArgumentNullException(ThrowHelpers.Argument.items);
		}

		var builder = ValueList.CreateBuilder<T>();

		await foreach (var element in items.WithCancellation(cancellationToken).ConfigureAwait(false))
		{
			builder.Add(element);
		}

		return builder.Build();
	}

	/// <summary>
	/// Copy the <paramref name="items"/> into a new <see cref="ValueList{T}.Builder"/>.
	/// </summary>
	[Pure]
	[Obsolete("Use .ToBuilder() instead.")]
	[Browsable(false)]
	[EditorBrowsable(EditorBrowsableState.Never)]
	public static ValueList<T>.Builder ToValueListBuilder<T>(this ValueList<T> items) => items.ToBuilder();

	/// <summary>
	/// Copy the <paramref name="items"/> into a new <see cref="ValueList{T}.Builder"/>.
	/// </summary>
	/// <remarks>
	/// The capacity of the returned builder may be larger than the size of the
	/// input collection. How much larger exactly is undefined.
	/// </remarks>
	public static ValueList<T>.Builder ToValueListBuilder<T>(this IEnumerable<T> items)
	{
		if (items is ValueList<T> valueList)
		{
			return valueList.ToBuilder();
		}

		return ValueList<T>.Builder.CreateUnsafe(new(items));
	}

	/// <summary>
	/// Add the <paramref name="items"/> to the end of the list.
	/// </summary>
	/// <remarks>
	/// This overload is an extension method to avoid call site ambiguity.
	/// </remarks>
	/// <exception cref="InvalidOperationException">
	/// Can't add builder into itself.
	/// </exception>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static ValueList<T>.Builder AddRange<T>(this ValueList<T>.Builder builder, IEnumerable<T> items)
	{
		return builder.AddRangeEnumerable(items);
	}

	/// <summary>
	/// Insert the <paramref name="items"/> into the list at the specified <paramref name="index"/>.
	/// </summary>
	/// <remarks>
	/// This overload is an extension method to avoid call site ambiguity.
	/// </remarks>
	/// <exception cref="InvalidOperationException">
	/// Can't insert builder into itself.
	/// </exception>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Invalid <paramref name="index"/>.
	/// </exception>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static ValueList<T>.Builder InsertRange<T>(this ValueList<T>.Builder builder, int index, IEnumerable<T> items)
	{
		return builder.InsertRangeEnumerable(index, items);
	}

	/// <summary>
	/// Copy the <paramref name="items"/> into a new <see cref="ValueSet{T}"/>.
	/// </summary>
	public static ValueSet<T> ToValueSet<T>(this IEnumerable<T> items)
	{
		if (items is null)
		{
			ThrowHelpers.ThrowArgumentNullException(ThrowHelpers.Argument.items);
		}

		if (items is ValueSet<T> valueSet)
		{
			return valueSet;
		}

		return ValueSet<T>.CreateImmutableUnsafe(new(items));
	}

	/// <summary>
	/// Asynchronously copy the <paramref name="items"/> into a new <see cref="ValueSet{T}"/>.
	/// </summary>
	public static async Task<ValueSet<T>> ToValueSetAsync<T>(this IAsyncEnumerable<T> items, CancellationToken cancellationToken = default)
	{
		if (items is null)
		{
			ThrowHelpers.ThrowArgumentNullException(ThrowHelpers.Argument.items);
		}

		var builder = ValueSet.CreateBuilder<T>();

		await foreach (var element in items.WithCancellation(cancellationToken).ConfigureAwait(false))
		{
			builder.Add(element);
		}

		return builder.Build();
	}

	/// <summary>
	/// Copy the <paramref name="items"/> into a new <see cref="ValueSet{T}.Builder"/>.
	/// </summary>
	[Pure]
	[Obsolete("Use .ToBuilder() instead.")]
	[Browsable(false)]
	[EditorBrowsable(EditorBrowsableState.Never)]
	public static ValueSet<T>.Builder ToValueSetBuilder<T>(this ValueSet<T> items) => items.ToBuilder();

	/// <summary>
	/// Copy the <paramref name="items"/> into a new <see cref="ValueSet{T}.Builder"/>.
	/// </summary>
	/// <remarks>
	/// The capacity of the returned builder may be larger than the size of the
	/// input collection. How much larger exactly is undefined.
	/// </remarks>
	public static ValueSet<T>.Builder ToValueSetBuilder<T>(this IEnumerable<T> items)
	{
		if (items is ValueSet<T> valueSet)
		{
			return valueSet.ToBuilder();
		}

		return ValueSet<T>.Builder.CreateUnsafe(new(items));
	}

	/// <summary>
	/// Check whether <c>this</c> set is a subset of the provided collection.
	/// </summary>
	/// <remarks>
	/// > [!WARNING]
	/// > In the worst case scenario this ends up allocating a temporary copy of
	/// the <paramref name="other"/> collection.
	/// </remarks>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool IsSubsetOf<T>(this ValueSet<T> set, IEnumerable<T> other) => set.IsSubsetOfEnumerable(other);

	/// <summary>
	/// Check whether <c>this</c> set is a proper subset of the provided collection.
	/// </summary>
	/// <remarks>
	/// > [!WARNING]
	/// > In the worst case scenario this ends up allocating a temporary copy of
	/// the <paramref name="other"/> collection.
	/// </remarks>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool IsProperSubsetOf<T>(this ValueSet<T> set, IEnumerable<T> other) => set.IsProperSubsetOfEnumerable(other);

	/// <summary>
	/// Check whether <c>this</c> set is a superset of the provided collection.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool IsSupersetOf<T>(this ValueSet<T> set, IEnumerable<T> other) => set.IsSupersetOfEnumerable(other);

	/// <summary>
	/// Check whether <c>this</c> set is a proper superset of the provided collection.
	/// </summary>
	/// <remarks>
	/// > [!WARNING]
	/// > In the worst case scenario this ends up allocating a temporary copy of
	/// the <paramref name="other"/> collection.
	/// </remarks>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool IsProperSupersetOf<T>(this ValueSet<T> set, IEnumerable<T> other) => set.IsProperSupersetOfEnumerable(other);

	/// <summary>
	/// Check whether <c>this</c> set and the provided collection share any common elements.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool Overlaps<T>(this ValueSet<T> set, IEnumerable<T> other) => set.OverlapsEnumerable(other);

	/// <summary>
	/// Check whether <c>this</c> set and the provided collection contain
	/// the same elements, ignoring duplicates and the order of the elements.
	/// </summary>
	/// <remarks>
	/// > [!WARNING]
	/// > In the worst case scenario this ends up allocating a temporary copy of
	/// the <paramref name="other"/> collection.
	/// </remarks>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool SetEquals<T>(this ValueSet<T> set, IEnumerable<T> other) => set.SetEqualsEnumerable(other);

	/// <summary>
	/// Check whether <c>this</c> set is a subset of the provided collection.
	/// </summary>
	/// <remarks>
	/// > [!WARNING]
	/// > In the worst case scenario this ends up allocating a temporary copy of
	/// the <paramref name="other"/> collection.
	/// </remarks>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool IsSubsetOf<T>(this ValueSet<T>.Builder builder, IEnumerable<T> other) => builder.IsSubsetOfEnumerable(other);

	/// <summary>
	/// Check whether <c>this</c> set is a proper subset of the provided collection.
	/// </summary>
	/// <remarks>
	/// > [!WARNING]
	/// > In the worst case scenario this ends up allocating a temporary copy of
	/// the <paramref name="other"/> collection.
	/// </remarks>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool IsProperSubsetOf<T>(this ValueSet<T>.Builder builder, IEnumerable<T> other) => builder.IsProperSubsetOfEnumerable(other);

	/// <summary>
	/// Check whether <c>this</c> set is a superset of the provided collection.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool IsSupersetOf<T>(this ValueSet<T>.Builder builder, IEnumerable<T> other) => builder.IsSupersetOfEnumerable(other);

	/// <summary>
	/// Check whether <c>this</c> set is a proper superset of the provided collection.
	/// </summary>
	/// <remarks>
	/// > [!WARNING]
	/// > In the worst case scenario this ends up allocating a temporary copy of
	/// the <paramref name="other"/> collection.
	/// </remarks>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool IsProperSupersetOf<T>(this ValueSet<T>.Builder builder, IEnumerable<T> other) => builder.IsProperSupersetOfEnumerable(other);

	/// <summary>
	/// Check whether <c>this</c> set and the provided collection share any common elements.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool Overlaps<T>(this ValueSet<T>.Builder builder, IEnumerable<T> other) => builder.OverlapsEnumerable(other);

	/// <summary>
	/// Check whether <c>this</c> set and the provided collection contain
	/// the same elements, ignoring duplicates and the order of the elements.
	/// </summary>
	/// <remarks>
	/// > [!WARNING]
	/// > In the worst case scenario this ends up allocating a temporary copy of
	/// the <paramref name="other"/> collection.
	/// </remarks>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool SetEquals<T>(this ValueSet<T>.Builder builder, IEnumerable<T> other) => builder.SetEqualsEnumerable(other);

	/// <summary>
	/// Remove all elements that appear in the <paramref name="other"/> collection.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static ValueSet<T>.Builder ExceptWith<T>(this ValueSet<T>.Builder builder, IEnumerable<T> other) => builder.ExceptWithEnumerable(other);

	/// <summary>
	/// Remove all elements that appear in both <see langword="this"/>
	/// <em>and</em> the <paramref name="other"/> collection.
	/// </summary>
	/// <remarks>
	/// > [!WARNING]
	/// > In the worst case scenario this ends up allocating a temporary copy of
	/// the <paramref name="other"/> collection.
	/// </remarks>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static ValueSet<T>.Builder SymmetricExceptWith<T>(this ValueSet<T>.Builder builder, IEnumerable<T> other) => builder.SymmetricExceptWithEnumerable(other);

	/// <summary>
	/// Modify the current builder to contain only elements that are present in
	/// both <see langword="this"/> <em>and</em> the <paramref name="other"/>
	/// collection.
	/// </summary>
	/// <remarks>
	/// > [!WARNING]
	/// > In the worst case scenario this ends up allocating a temporary copy of
	/// the <paramref name="other"/> collection.
	/// </remarks>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static ValueSet<T>.Builder IntersectWith<T>(this ValueSet<T>.Builder builder, IEnumerable<T> other) => builder.IntersectWithEnumerable(other);

	/// <summary>
	/// Add all elements from the <paramref name="other"/> collection.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static ValueSet<T>.Builder UnionWith<T>(this ValueSet<T>.Builder builder, IEnumerable<T> other) => builder.UnionWithEnumerable(other);

	/// <summary>
	/// Copy the <paramref name="items"/> into a new <see cref="ValueDictionary{TKey, TValue}"/>.
	/// </summary>
	/// <exception cref="ArgumentException">
	/// <paramref name="items"/> contains duplicate keys.
	/// </exception>
	public static ValueDictionary<TKey, TValue> ToValueDictionary<TKey, TValue>(this IEnumerable<KeyValuePair<TKey, TValue>> items)
		where TKey : notnull
	{
		if (items is null)
		{
			ThrowHelpers.ThrowArgumentNullException(ThrowHelpers.Argument.items);
		}

		if (items is ValueDictionary<TKey, TValue> dictionary)
		{
			return dictionary;
		}

		return ValueDictionary<TKey, TValue>.CreateImmutableUnsafe(ValueDictionary<TKey, TValue>.EnumerableToDictionary(items));
	}

	/// <summary>
	/// Asynchronously copy the <paramref name="items"/> into a new <see cref="ValueDictionary{TKey, TValue}"/>.
	/// </summary>
	/// <exception cref="ArgumentException">
	/// <paramref name="items"/> contains duplicate keys.
	/// </exception>
	public static Task<ValueDictionary<TKey, TValue>> ToValueDictionary<TKey, TValue>(this IAsyncEnumerable<KeyValuePair<TKey, TValue>> items, CancellationToken cancellationToken = default)
		where TKey : notnull
	{
		return items.ToValueDictionaryAsync(static e => e.Key, static e => e.Value, cancellationToken);
	}

	/// <summary>
	/// Creates a <see cref="ValueDictionary{TKey, TValue}"/> from an
	/// <see cref="IEnumerable{T}"/> according to specified key selector function.
	/// </summary>
	/// <param name="items">Elements that will becomes the values of the dictionary.</param>
	/// <param name="keySelector">A function to extract a key from each element.</param>
	/// <exception cref="ArgumentException">
	/// The <paramref name="keySelector"/> produced a duplicate key.
	/// </exception>
	public static ValueDictionary<TKey, TValue> ToValueDictionary<TKey, TValue>(this IEnumerable<TValue> items, Func<TValue, TKey> keySelector)
		where TKey : notnull
	{
		var inner = items.ToDictionary(keySelector);

		return ValueDictionary<TKey, TValue>.CreateImmutableUnsafe(inner);
	}

	/// <summary>
	/// Asynchronously creates a <see cref="ValueDictionary{TKey, TValue}"/> from an
	/// <see cref="IEnumerable{T}"/> according to specified key selector function.
	/// </summary>
	/// <param name="items">Elements that will becomes the values of the dictionary.</param>
	/// <param name="keySelector">A function to extract a key from each element.</param>
	/// <param name="cancellationToken">A CancellationToken to observe while waiting for the task to complete.</param>
	/// <exception cref="ArgumentException">
	/// The <paramref name="keySelector"/> produced a duplicate key.
	/// </exception>
	public static Task<ValueDictionary<TKey, TValue>> ToValueDictionaryAsync<TKey, TValue>(this IAsyncEnumerable<TValue> items, Func<TValue, TKey> keySelector, CancellationToken cancellationToken = default)
		where TKey : notnull
	{
		return items.ToValueDictionaryAsync(keySelector, static e => e, cancellationToken);
	}

	/// <summary>
	/// Creates a <see cref="ValueDictionary{TKey, TValue}"/> from an
	/// <see cref="IEnumerable{T}"/> according to specified key selector and
	/// element selector functions.
	/// </summary>
	/// <param name="source">Elements to feed into the selector functions.</param>
	/// <param name="keySelector">A function to extract a key from each element.</param>
	/// <param name="valueSelector">A transform function to produce a result element value from each element.</param>
	/// <exception cref="ArgumentException">
	/// The <paramref name="keySelector"/> produced a duplicate key.
	/// </exception>
	public static ValueDictionary<TKey, TValue> ToValueDictionary<TSource, TKey, TValue>(this IEnumerable<TSource> source, Func<TSource, TKey> keySelector, Func<TSource, TValue> valueSelector)
		where TKey : notnull
	{
		if (source is null)
		{
			ThrowHelpers.ThrowArgumentNullException(ThrowHelpers.Argument.source);
		}

		if (keySelector is null)
		{
			ThrowHelpers.ThrowArgumentNullException(ThrowHelpers.Argument.keySelector);
		}

		if (valueSelector is null)
		{
			ThrowHelpers.ThrowArgumentNullException(ThrowHelpers.Argument.valueSelector);
		}

		return ValueDictionary<TKey, TValue>.CreateImmutableUnsafe(source.ToDictionary(keySelector, valueSelector));
	}

	/// <summary>
	/// Asynchronously creates a <see cref="ValueDictionary{TKey, TValue}"/> from an
	/// <see cref="IEnumerable{T}"/> according to specified key selector and
	/// element selector functions.
	/// </summary>
	/// <param name="source">Elements to feed into the selector functions.</param>
	/// <param name="keySelector">A function to extract a key from each element.</param>
	/// <param name="valueSelector">A transform function to produce a result element value from each element.</param>
	/// <param name="cancellationToken">A CancellationToken to observe while waiting for the task to complete.</param>
	/// <exception cref="ArgumentException">
	/// The <paramref name="keySelector"/> produced a duplicate key.
	/// </exception>
	public static async Task<ValueDictionary<TKey, TValue>> ToValueDictionaryAsync<TSource, TKey, TValue>(this IAsyncEnumerable<TSource> source, Func<TSource, TKey> keySelector, Func<TSource, TValue> valueSelector, CancellationToken cancellationToken = default)
		where TKey : notnull
	{
		if (source is null)
		{
			ThrowHelpers.ThrowArgumentNullException(ThrowHelpers.Argument.source);
		}

		if (keySelector is null)
		{
			ThrowHelpers.ThrowArgumentNullException(ThrowHelpers.Argument.keySelector);
		}

		if (valueSelector is null)
		{
			ThrowHelpers.ThrowArgumentNullException(ThrowHelpers.Argument.valueSelector);
		}

		var builder = ValueDictionary.CreateBuilder<TKey, TValue>();

		await foreach (var element in source.WithCancellation(cancellationToken).ConfigureAwait(false))
		{
			builder.Add(keySelector(element), valueSelector(element));
		}

		return builder.Build();
	}

	/// <summary>
	/// Copy the <paramref name="items"/> into a new <see cref="ValueDictionary{TKey, TValue}.Builder"/>.
	/// </summary>
	/// <exception cref="ArgumentException">
	/// <paramref name="items"/> contains duplicate keys.
	/// </exception>
	[Obsolete("Use .ToBuilder() instead.")]
	[Browsable(false)]
	[EditorBrowsable(EditorBrowsableState.Never)]
	public static ValueDictionary<TKey, TValue>.Builder ToValueDictionaryBuilder<TKey, TValue>(this ValueDictionary<TKey, TValue> items)
		where TKey : notnull => items.ToBuilder();

	/// <summary>
	/// Copy the <paramref name="items"/> into a new <see cref="ValueDictionary{TKey, TValue}.Builder"/>.
	/// </summary>
	/// <remarks>
	/// The capacity of the returned builder may be larger than the size of the
	/// input collection. How much larger exactly is undefined.
	/// </remarks>
	/// <exception cref="ArgumentException">
	/// <paramref name="items"/> contains duplicate keys.
	/// </exception>
	public static ValueDictionary<TKey, TValue>.Builder ToValueDictionaryBuilder<TKey, TValue>(this IEnumerable<KeyValuePair<TKey, TValue>> items)
		where TKey : notnull
	{
		if (items is ValueDictionary<TKey, TValue> dictionary)
		{
			return dictionary.ToBuilder();
		}

		var inner = ValueDictionary<TKey, TValue>.EnumerableToDictionary(items);

		return ValueDictionary<TKey, TValue>.Builder.CreateUnsafe(inner);
	}

	/// <summary>
	/// Add multiple entries to the dictionary.
	/// </summary>
	/// <remarks>
	/// This overload is an extension method to avoid call site ambiguity.
	/// </remarks>
	/// <exception cref="ArgumentException">
	/// <paramref name="items"/> contains a duplicate key or a key that already
	/// exists in the dictionary.
	/// </exception>
	public static ValueDictionary<TKey, TValue>.Builder AddRange<TKey, TValue>(this ValueDictionary<TKey, TValue>.Builder builder, scoped ReadOnlySpan<KeyValuePair<TKey, TValue>> items)
		where TKey : notnull
	{
		if (builder is null)
		{
			throw new ArgumentNullException(nameof(builder));
		}

		return builder.AddRangeSpan(items);
	}

	/// <summary>
	/// Sets the specified key/value pairs in the dictionary, possibly
	/// overwriting existing values for the keys.
	/// </summary>
	/// <remarks>
	/// When the same key appears multiple times in the <paramref name="items"/>,
	/// the last value overwrites any earlier values.
	///
	/// This overload is an extension method to avoid call site ambiguity.
	/// </remarks>
	public static ValueDictionary<TKey, TValue>.Builder SetItems<TKey, TValue>(this ValueDictionary<TKey, TValue>.Builder builder, scoped ReadOnlySpan<KeyValuePair<TKey, TValue>> items)
		where TKey : notnull
	{
		if (builder is null)
		{
			throw new ArgumentNullException(nameof(builder));
		}

		return builder.SetItemsSpan(items);
	}

	/// <summary>
	/// Remove the provided <paramref name="keys"/> from the dictionary.
	/// </summary>
	/// <remarks>
	/// This overload is an extension method to avoid call site ambiguity.
	/// </remarks>
	public static ValueDictionary<TKey, TValue>.Builder RemoveRange<TKey, TValue>(this ValueDictionary<TKey, TValue>.Builder builder, scoped ReadOnlySpan<TKey> keys)
		where TKey : notnull
	{
		if (builder is null)
		{
			throw new ArgumentNullException(nameof(builder));
		}

		return builder.RemoveRangeSpan(keys);
	}
}
