using Microsoft.EntityFrameworkCore;

namespace Badeend.ValueCollections.EntityFrameworkCore;

/// <summary>
/// Entity Framework LINQ related extension methods.
/// </summary>
public static class QueryableExtensions
{
	/// <summary>
	/// Asynchronously load the <paramref name="items"/> into a new <see cref="ValueList{T}"/>.
	/// </summary>
	public static Task<ValueList<T>> ToValueListAsync<T>(this IQueryable<T> items, CancellationToken cancellationToken = default)
	{
		return items.AsAsyncEnumerable().ToValueListAsync(cancellationToken);
	}

	/// <summary>
	/// Asynchronously load the <paramref name="items"/> into a new <see cref="ValueSet{T}"/>.
	/// </summary>
	public static Task<ValueSet<T>> ToValueSetAsync<T>(this IQueryable<T> items, CancellationToken cancellationToken = default)
	{
		return items.AsAsyncEnumerable().ToValueSetAsync(cancellationToken);
	}

	/// <summary>
	/// Asynchronously load the <paramref name="items"/> into a new <see cref="ValueDictionary{TKey, TValue}"/>
	/// according to specified key selector function.
	/// </summary>
	/// <param name="items">Elements that will becomes the values of the dictionary.</param>
	/// <param name="keySelector">A function to extract a key from each element.</param>
	/// <param name="cancellationToken">A CancellationToken to observe while waiting for the task to complete.</param>
	/// <exception cref="ArgumentException">
	/// The <paramref name="keySelector"/> produced a duplicate key.
	/// </exception>
	public static Task<ValueDictionary<TKey, TValue>> ToValueDictionaryAsync<TKey, TValue>(this IQueryable<TValue> items, Func<TValue, TKey> keySelector, CancellationToken cancellationToken = default)
		where TKey : notnull
	{
		return items.AsAsyncEnumerable().ToValueDictionaryAsync(keySelector, cancellationToken);
	}

	/// <summary>
	/// Asynchronously load the <paramref name="source"/> into a new <see cref="ValueDictionary{TKey, TValue}"/>
	/// according to specified key selector and element selector functions.
	/// </summary>
	/// <param name="source">Elements to feed into the selector functions.</param>
	/// <param name="keySelector">A function to extract a key from each element.</param>
	/// <param name="valueSelector">A transform function to produce a result element value from each element.</param>
	/// <param name="cancellationToken">A CancellationToken to observe while waiting for the task to complete.</param>
	/// <exception cref="ArgumentException">
	/// The <paramref name="keySelector"/> produced a duplicate key.
	/// </exception>
	public static Task<ValueDictionary<TKey, TValue>> ToValueDictionaryAsync<TSource, TKey, TValue>(this IQueryable<TSource> source, Func<TSource, TKey> keySelector, Func<TSource, TValue> valueSelector, CancellationToken cancellationToken = default)
		where TKey : notnull
	{
		return source.AsAsyncEnumerable().ToValueDictionaryAsync(keySelector, valueSelector, cancellationToken);
	}
}
