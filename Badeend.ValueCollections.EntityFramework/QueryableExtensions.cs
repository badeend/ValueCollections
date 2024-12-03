using System.Data.Entity.Infrastructure;

namespace Badeend.ValueCollections.EntityFramework;

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

	private static DbAsyncEnumerableWrapper<T> AsAsyncEnumerable<T>(this IQueryable<T> source)
	{
		if (source is null)
		{
			throw new ArgumentNullException(nameof(source));
		}

		if (source is IDbAsyncEnumerable<T> dbAsyncEnumerable)
		{
			return new DbAsyncEnumerableWrapper<T>(dbAsyncEnumerable);
		}

		throw new InvalidOperationException($"The source 'IQueryable' doesn't implement 'IDbAsyncEnumerable<{typeof(T)}>'. Only sources that implement 'IDbAsyncEnumerable' can be used for Entity Framework asynchronous operations.");
	}

	private sealed class DbAsyncEnumerableWrapper<T>(IDbAsyncEnumerable<T> inner) : IAsyncEnumerable<T>, IAsyncEnumerator<T>
	{
		private IDbAsyncEnumerator<T>? innerEnumerator;
		private CancellationToken cancellationToken;

		IAsyncEnumerator<T> IAsyncEnumerable<T>.GetAsyncEnumerator(CancellationToken cancellationToken)
		{
			if (this.innerEnumerator is not null)
			{
				throw new InvalidOperationException("This wrapper does not support multiple enumerations concurrently.");
			}

			this.innerEnumerator = inner.GetAsyncEnumerator();
			return this;
		}

		T IAsyncEnumerator<T>.Current => this.innerEnumerator!.Current;

		async ValueTask<bool> IAsyncEnumerator<T>.MoveNextAsync()
		{
			if (this.innerEnumerator is null)
			{
				throw new InvalidOperationException("The enumerator has not been initialized.");
			}

			return await this.innerEnumerator.MoveNextAsync(this.cancellationToken).ConfigureAwait(false);
		}

		ValueTask IAsyncDisposable.DisposeAsync()
		{
			this.innerEnumerator?.Dispose();
			this.innerEnumerator = null;
			this.cancellationToken = default;
			return default;
		}
	}
}
