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
		if (items is ValueList<T> valueList)
		{
			return ValueList<T>.CreateImmutableUnsafe(new(ref valueList.inner));
		}

		return ValueList<T>.CreateImmutableUnsafe(new(items));
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
	/// Copy the <paramref name="items"/> into a new <see cref="ValueList{T}.Builder"/>
	/// with a minimum capacity of <paramref name="minimumCapacity"/>.
	/// </summary>
	/// <exception cref="ArgumentOutOfRangeException">
	///   <paramref name="minimumCapacity"/> is less than 0.
	/// </exception>
	/// <remarks>
	/// This is functionally equivalent to:
	/// <code>
	/// items.ToValueListBuilder().EnsureCapacity(minimumCapacity)
	/// </code>
	/// but without unnecessary intermediate copies.
	/// </remarks>
	public static ValueList<T>.Builder ToValueListBuilder<T>(this IEnumerable<T> items, int minimumCapacity)
	{
		if (minimumCapacity < 0)
		{
			ThrowHelpers.ThrowArgumentOutOfRangeException(ThrowHelpers.Argument.minimumCapacity);
		}

		if (items is ICollection<T> collection)
		{
			if (collection is ValueList<T> valueList)
			{
				return valueList.ToBuilder(minimumCapacity);
			}

			minimumCapacity = Math.Max(minimumCapacity, collection.Count);
		}

		var newInner = new RawList<T>(minimumCapacity);
		newInner.AddRange(items);
		return ValueList<T>.Builder.CreateUnsafe(newInner);
	}

	/// <summary>
	/// Add the <paramref name="items"/> to the end of the list.
	/// </summary>
	/// <remarks>
	/// This overload is an extension method to avoid call site ambiguity.
	/// </remarks>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static ValueList<T>.Builder AddRange<T>(this ValueList<T>.Builder builder, ReadOnlySpan<T> items)
	{
		return builder.AddRangeSpan(items);
	}

	/// <summary>
	/// Insert the <paramref name="items"/> into the list at the specified <paramref name="index"/>.
	/// </summary>
	/// <remarks>
	/// This overload is an extension method to avoid call site ambiguity.
	/// </remarks>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static ValueList<T>.Builder InsertRange<T>(this ValueList<T>.Builder builder, int index, ReadOnlySpan<T> items)
	{
		return builder.InsertRangeSpan(index, items);
	}

	/// <summary>
	/// Copy the <paramref name="items"/> into a new <see cref="ValueSet{T}"/>.
	/// </summary>
	public static ValueSet<T> ToValueSet<T>(this IEnumerable<T> items)
	{
		return ValueSet<T>.CreateImmutableFromEnumerable(items);
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
		return ValueSet<T>.Builder.CreateFromEnumerable(items);
	}

#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
	/// <summary>
	/// Copy the <paramref name="items"/> into a new <see cref="ValueSet{T}.Builder"/>
	/// with a minimum capacity of <paramref name="minimumCapacity"/>.
	/// </summary>
	/// <exception cref="ArgumentOutOfRangeException">
	///   <paramref name="minimumCapacity"/> is less than 0.
	/// </exception>
	/// <remarks>
	/// This is functionally equivalent to:
	/// <code>
	/// items.ToValueSetBuilder().EnsureCapacity(minimumCapacity)
	/// </code>
	/// but without unnecessary intermediate copies.
	///
	/// Available on .NET Standard 2.1 and .NET Core 2.1 and higher.
	/// </remarks>
	public static ValueSet<T>.Builder ToValueSetBuilder<T>(this IEnumerable<T> items, int minimumCapacity)
	{
		if (minimumCapacity < 0)
		{
			throw new ArgumentOutOfRangeException(nameof(minimumCapacity));
		}

		var initialCapacity = items switch
		{
			ICollection<T> collection => Math.Max(minimumCapacity, collection.Count),
			_ => minimumCapacity,
		};

		return ValueSet.CreateBuilder<T>(initialCapacity).UnionWith(items);
	}
#endif

	/// <summary>
	/// Add the <paramref name="items"/> to the set.
	/// </summary>
	/// <remarks>
	/// This overload is an extension method to avoid call site ambiguity.
	/// </remarks>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static ValueSet<T>.Builder UnionWith<T>(this ValueSet<T>.Builder builder, ReadOnlySpan<T> items)
	{
		return builder.UnionWithSpan(items);
	}

	/// <summary>
	/// Remove the <paramref name="items"/> from the set.
	/// </summary>
	/// <remarks>
	/// This overload is an extension method to avoid call site ambiguity.
	/// </remarks>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static ValueSet<T>.Builder ExceptWith<T>(this ValueSet<T>.Builder builder, ReadOnlySpan<T> items)
	{
		return builder.ExceptWithSpan(items);
	}

	/// <summary>
	/// Copy the <paramref name="items"/> into a new <see cref="ValueDictionary{TKey, TValue}"/>.
	/// </summary>
	/// <exception cref="ArgumentException">
	/// <paramref name="items"/> contains duplicate keys.
	/// </exception>
	public static ValueDictionary<TKey, TValue> ToValueDictionary<TKey, TValue>(this IEnumerable<KeyValuePair<TKey, TValue>> items)
		where TKey : notnull
	{
		if (items is ValueDictionary<TKey, TValue> dictionary)
		{
			return dictionary;
		}

		if (items is ValueDictionaryBuilder<TKey, TValue> builder)
		{
			return builder.ToValueDictionary();
		}

		var inner = ValueDictionary<TKey, TValue>.EnumerableToDictionary(items);

		return ValueDictionary<TKey, TValue>.FromDictionaryUnsafe(inner);
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

		return ValueDictionary<TKey, TValue>.FromDictionaryUnsafe(inner);
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
		var inner = source.ToDictionary(keySelector, valueSelector);

		return ValueDictionary<TKey, TValue>.FromDictionaryUnsafe(inner);
	}

	/// <summary>
	/// Copy the <paramref name="items"/> into a new <see cref="ValueDictionaryBuilder{TKey, TValue}"/>.
	/// </summary>
	/// <exception cref="ArgumentException">
	/// <paramref name="items"/> contains duplicate keys.
	/// </exception>
	[Obsolete("Use .ToBuilder() instead.")]
	[Browsable(false)]
	[EditorBrowsable(EditorBrowsableState.Never)]
	public static ValueDictionaryBuilder<TKey, TValue> ToValueDictionaryBuilder<TKey, TValue>(this ValueDictionary<TKey, TValue> items)
		where TKey : notnull => items.ToBuilder();

	/// <summary>
	/// Copy the <paramref name="items"/> into a new <see cref="ValueDictionaryBuilder{TKey, TValue}"/>.
	/// </summary>
	/// <remarks>
	/// The capacity of the returned builder may be larger than the size of the
	/// input collection. How much larger exactly is undefined.
	/// </remarks>
	/// <exception cref="ArgumentException">
	/// <paramref name="items"/> contains duplicate keys.
	/// </exception>
	public static ValueDictionaryBuilder<TKey, TValue> ToValueDictionaryBuilder<TKey, TValue>(this IEnumerable<KeyValuePair<TKey, TValue>> items)
		where TKey : notnull
	{
		if (items is ValueDictionary<TKey, TValue> dictionary)
		{
			return dictionary.ToBuilder();
		}

		var inner = ValueDictionary<TKey, TValue>.EnumerableToDictionary(items);

		return ValueDictionaryBuilder<TKey, TValue>.FromDictionaryUnsafe(inner);
	}

#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
	/// <summary>
	/// Copy the <paramref name="items"/> into a new <see cref="ValueDictionaryBuilder{TKey,TValue}"/>
	/// with a minimum capacity of <paramref name="minimumCapacity"/>.
	/// </summary>
	/// <remarks>
	/// This is functionally equivalent to:
	/// <code>
	/// items.ToValueDictionaryBuilder().EnsureCapacity(minimumCapacity)
	/// </code>
	/// but without unnecessary intermediate copies.
	///
	/// Available on .NET Standard 2.1 and .NET Core 2.1 and higher.
	/// </remarks>
	/// <exception cref="ArgumentOutOfRangeException">
	///   <paramref name="minimumCapacity"/> is less than 0.
	/// </exception>
	/// <exception cref="ArgumentException">
	/// <paramref name="items"/> contains duplicate keys.
	/// </exception>
	public static ValueDictionaryBuilder<TKey, TValue> ToValueDictionaryBuilder<TKey, TValue>(this IEnumerable<KeyValuePair<TKey, TValue>> items, int minimumCapacity)
		where TKey : notnull
	{
		if (minimumCapacity < 0)
		{
			throw new ArgumentOutOfRangeException(nameof(minimumCapacity));
		}

		var initialCapacity = items switch
		{
			ICollection<KeyValuePair<TKey, TValue>> collection => Math.Max(minimumCapacity, collection.Count),
			_ => minimumCapacity,
		};

		return new ValueDictionaryBuilder<TKey, TValue>(initialCapacity).AddRange(items);
	}
#endif

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
	public static ValueDictionaryBuilder<TKey, TValue> AddRange<TKey, TValue>(this ValueDictionaryBuilder<TKey, TValue> builder, ReadOnlySpan<KeyValuePair<TKey, TValue>> items)
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
	public static ValueDictionaryBuilder<TKey, TValue> SetItems<TKey, TValue>(this ValueDictionaryBuilder<TKey, TValue> builder, ReadOnlySpan<KeyValuePair<TKey, TValue>> items)
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
	public static ValueDictionaryBuilder<TKey, TValue> RemoveRange<TKey, TValue>(this ValueDictionaryBuilder<TKey, TValue> builder, ReadOnlySpan<TKey> keys)
		where TKey : notnull
	{
		if (builder is null)
		{
			throw new ArgumentNullException(nameof(builder));
		}

		return builder.RemoveRangeSpan(keys);
	}
}
