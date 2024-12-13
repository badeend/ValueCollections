using System.Collections;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Badeend.ValueCollections.Internals;

namespace Badeend.ValueCollections;

#pragma warning disable CA1815 // Override equals and operator equals on value types
#pragma warning disable CA1034 // Nested types should not be visible

/// <content>
/// Values collections.
/// </content>
public partial class ValueDictionary<TKey, TValue>
{
	/// <summary>
	/// All values in the dictionary in no particular order.
	/// </summary>
	[Pure]
	public ValuesEnumerator Values
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => new(this);
	}

	/// <inheritdoc/>
	ICollection<TValue> IDictionary<TKey, TValue>.Values => this.Values.AsCollection();

	/// <inheritdoc/>
	IEnumerable<TValue> IReadOnlyDictionary<TKey, TValue>.Values => this.Values.AsCollection();

	/// <summary>
	/// This type facilitates reading only the values of a dictionary.
	/// </summary>
	/// <remarks>
	/// This is a stack-allocated enumerator over the values in the dictionary.
	///
	/// To prevent accidental boxing, ValuesEnumerator does not implement any interface.
	/// If you want to use the values as a collection (e.g. <see cref="IEnumerable{TValue}"/>,
	/// <see cref="IReadOnlyCollection{TValue}"/>, etc.) you can still manually box
	/// it by calling <see cref="AsCollection"/>.
	/// </remarks>
	[StructLayout(LayoutKind.Auto)]
	public struct ValuesEnumerator : IEnumeratorLike<TValue>
	{
		private readonly ValueDictionary<TKey, TValue> dictionary;
		private ValueDictionary<TKey, TValue>.Enumerator inner;

		internal ValuesEnumerator(ValueDictionary<TKey, TValue> dictionary)
		{
			this.dictionary = dictionary;
			this.inner = dictionary.GetEnumerator();
		}

		/// <summary>
		/// Create a new heap-allocated view over the values in the dictionary.
		/// </summary>
		/// <remarks>
		/// This method is an <c>O(1)</c> operation and allocates a new fixed-size
		/// collection instance. The items are not copied.
		/// </remarks>
		public ValuesCollection AsCollection() => this.dictionary.Count == 0 ? ValuesCollection.Empty : new ValuesCollection(this.dictionary);

		/// <summary>
		/// Returns a new ValuesEnumerator.
		///
		/// Typically, you don't need to manually call this method, but instead use
		/// the built-in <c>foreach</c> syntax.
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public ValuesEnumerator GetEnumerator() => new(this.dictionary);

		/// <inheritdoc/>
		public readonly TValue Current
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => this.inner.Current.Value;
		}

		/// <inheritdoc/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool MoveNext() => this.inner.MoveNext();
	}

	/// <summary>
	/// A heap-allocated read-only view over all the values in the dictionary.
	/// </summary>
	public sealed class ValuesCollection : ICollection<TValue>, IReadOnlyCollection<TValue>
	{
		internal static readonly ValuesCollection Empty = new ValuesCollection(ValueDictionary<TKey, TValue>.Empty);

		private readonly ValueDictionary<TKey, TValue> dictionary;

		internal ValuesCollection(ValueDictionary<TKey, TValue> dictionary)
		{
			this.dictionary = dictionary;
		}

		/// <inheritdoc/>
		IEnumerator<TValue> IEnumerable<TValue>.GetEnumerator()
		{
			if (this.dictionary.Count == 0)
			{
				return EnumeratorLike.Empty<TValue>();
			}
			else
			{
				return EnumeratorLike.AsIEnumerator<TValue, ValuesEnumerator>(this.dictionary.Values);
			}
		}

		/// <inheritdoc/>
		IEnumerator IEnumerable.GetEnumerator() => (this as IEnumerable<TValue>).GetEnumerator();

		/// <inheritdoc/>
		int ICollection<TValue>.Count => this.dictionary.Count;

		/// <inheritdoc/>
		int IReadOnlyCollection<TValue>.Count => this.dictionary.Count;

		/// <inheritdoc/>
		bool ICollection<TValue>.IsReadOnly => true;

		/// <inheritdoc/>
		bool ICollection<TValue>.Contains(TValue item) => this.dictionary.ContainsValue(item);

		/// <inheritdoc/>
		void ICollection<TValue>.CopyTo(TValue[] array, int index) => this.dictionary.inner.Values_CopyTo(array, index);

		/// <inheritdoc/>
		void ICollection<TValue>.Add(TValue item) => throw ImmutableException();

		/// <inheritdoc/>
		bool ICollection<TValue>.Remove(TValue item) => throw ImmutableException();

		/// <inheritdoc/>
		void ICollection<TValue>.Clear() => throw ImmutableException();
	}
}
