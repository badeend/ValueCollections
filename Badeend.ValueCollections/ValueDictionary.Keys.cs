using System.Collections;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Badeend.ValueCollections.Internals;

namespace Badeend.ValueCollections;

#pragma warning disable CA1815 // Override equals and operator equals on value types
#pragma warning disable CA1034 // Nested types should not be visible

/// <content>
/// Keys collections.
/// </content>
public partial class ValueDictionary<TKey, TValue>
{
	/// <summary>
	/// All keys in the dictionary in no particular order.
	/// </summary>
	[Pure]
	public KeysEnumerator Keys
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => new(this);
	}

	/// <inheritdoc/>
	ICollection<TKey> IDictionary<TKey, TValue>.Keys => this.Keys.AsCollection();

	/// <inheritdoc/>
	IEnumerable<TKey> IReadOnlyDictionary<TKey, TValue>.Keys => this.Keys.AsCollection();

	/// <summary>
	/// This type facilitates reading only the keys of a dictionary.
	/// </summary>
	/// <remarks>
	/// This is a stack-allocated enumerator over the keys in the dictionary.
	///
	/// To prevent accidental boxing, KeysEnumerator does not implement any interface.
	/// If you want to use the keys as a collection (e.g. <see cref="IEnumerable{TKey}"/>,
	/// <see cref="IReadOnlyCollection{TKey}"/>, etc.) you can still manually box
	/// it by calling <see cref="AsCollection"/>.
	/// </remarks>
	[StructLayout(LayoutKind.Auto)]
	public struct KeysEnumerator : IEnumeratorLike<TKey>
	{
		private readonly ValueDictionary<TKey, TValue> dictionary;
		private ValueDictionary<TKey, TValue>.Enumerator inner;

		internal KeysEnumerator(ValueDictionary<TKey, TValue> dictionary)
		{
			this.dictionary = dictionary;
			this.inner = dictionary.GetEnumerator();
		}

		/// <summary>
		/// Create a new heap-allocated view over the keys in the dictionary.
		/// </summary>
		/// <remarks>
		/// This method is an <c>O(1)</c> operation and allocates a new fixed-size
		/// collection instance. The items are not copied.
		/// </remarks>
		public KeysCollection AsCollection() => this.dictionary.Count == 0 ? KeysCollection.Empty : new KeysCollection(this.dictionary);

		/// <summary>
		/// Returns a new KeysEnumerator.
		///
		/// Typically, you don't need to manually call this method, but instead use
		/// the built-in <c>foreach</c> syntax.
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public KeysEnumerator GetEnumerator() => new(this.dictionary);

		/// <inheritdoc/>
		public readonly TKey Current
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => this.inner.Current.Key;
		}

		/// <inheritdoc/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool MoveNext() => this.inner.MoveNext();
	}

	/// <summary>
	/// A heap-allocated read-only view over all the keys in the dictionary.
	/// </summary>
	public sealed class KeysCollection : ISet<TKey>, IReadOnlyCollection<TKey>, IReadOnlySet<TKey>
	{
		internal static readonly KeysCollection Empty = new KeysCollection(ValueDictionary<TKey, TValue>.Empty);

		private readonly ValueDictionary<TKey, TValue> dictionary;

		internal KeysCollection(ValueDictionary<TKey, TValue> dictionary)
		{
			this.dictionary = dictionary;
		}

		/// <inheritdoc/>
		IEnumerator<TKey> IEnumerable<TKey>.GetEnumerator()
		{
			if (this.dictionary.Count == 0)
			{
				return EnumeratorLike.Empty<TKey>();
			}
			else
			{
				return EnumeratorLike.AsIEnumerator<TKey, KeysEnumerator>(this.dictionary.Keys);
			}
		}

		/// <inheritdoc/>
		IEnumerator IEnumerable.GetEnumerator() => (this as IEnumerable<TKey>).GetEnumerator();

		/// <inheritdoc/>
		int ICollection<TKey>.Count => this.dictionary.Count;

		/// <inheritdoc/>
		int IReadOnlyCollection<TKey>.Count => this.dictionary.Count;

		/// <inheritdoc/>
		bool ICollection<TKey>.IsReadOnly => true;

		/// <inheritdoc/>
		bool ICollection<TKey>.Contains(TKey item) => this.dictionary.ContainsKey(item);

		/// <inheritdoc/>
		void ICollection<TKey>.CopyTo(TKey[] array, int index) => this.dictionary.inner.Keys_CopyTo(array, index);

		/// <inheritdoc/>
		bool ISet<TKey>.IsProperSubsetOf(IEnumerable<TKey> other) => this.dictionary.inner.Keys_IsProperSubsetOf(other);

		/// <inheritdoc/>
		bool ISet<TKey>.IsProperSupersetOf(IEnumerable<TKey> other) => this.dictionary.inner.Keys_IsProperSupersetOf(other);

		/// <inheritdoc/>
		bool ISet<TKey>.IsSubsetOf(IEnumerable<TKey> other) => this.dictionary.inner.Keys_IsSubsetOf(other);

		/// <inheritdoc/>
		bool ISet<TKey>.IsSupersetOf(IEnumerable<TKey> other) => this.dictionary.inner.Keys_IsSupersetOf(other);

		/// <inheritdoc/>
		bool ISet<TKey>.Overlaps(IEnumerable<TKey> other) => this.dictionary.inner.Keys_Overlaps(other);

		/// <inheritdoc/>
		bool ISet<TKey>.SetEquals(IEnumerable<TKey> other) => this.dictionary.inner.Keys_SetEquals(other);

		/// <inheritdoc/>
		bool IReadOnlySet<TKey>.Contains(TKey item) => this.dictionary.ContainsKey(item);

		/// <inheritdoc/>
		bool IReadOnlySet<TKey>.IsProperSubsetOf(IEnumerable<TKey> other) => this.dictionary.inner.Keys_IsProperSubsetOf(other);

		/// <inheritdoc/>
		bool IReadOnlySet<TKey>.IsProperSupersetOf(IEnumerable<TKey> other) => this.dictionary.inner.Keys_IsProperSupersetOf(other);

		/// <inheritdoc/>
		bool IReadOnlySet<TKey>.IsSubsetOf(IEnumerable<TKey> other) => this.dictionary.inner.Keys_IsSubsetOf(other);

		/// <inheritdoc/>
		bool IReadOnlySet<TKey>.IsSupersetOf(IEnumerable<TKey> other) => this.dictionary.inner.Keys_IsSupersetOf(other);

		/// <inheritdoc/>
		bool IReadOnlySet<TKey>.Overlaps(IEnumerable<TKey> other) => this.dictionary.inner.Keys_Overlaps(other);

		/// <inheritdoc/>
		bool IReadOnlySet<TKey>.SetEquals(IEnumerable<TKey> other) => this.dictionary.inner.Keys_SetEquals(other);

		/// <inheritdoc/>
		void ICollection<TKey>.Add(TKey item) => throw ImmutableException();

		/// <inheritdoc/>
		bool ICollection<TKey>.Remove(TKey item) => throw ImmutableException();

		/// <inheritdoc/>
		void ICollection<TKey>.Clear() => throw ImmutableException();

		/// <inheritdoc/>
		bool ISet<TKey>.Add(TKey item) => throw ImmutableException();

		/// <inheritdoc/>
		void ISet<TKey>.ExceptWith(IEnumerable<TKey> other) => throw ImmutableException();

		/// <inheritdoc/>
		void ISet<TKey>.IntersectWith(IEnumerable<TKey> other) => throw ImmutableException();

		/// <inheritdoc/>
		void ISet<TKey>.SymmetricExceptWith(IEnumerable<TKey> other) => throw ImmutableException();

		/// <inheritdoc/>
		void ISet<TKey>.UnionWith(IEnumerable<TKey> other) => throw ImmutableException();
	}
}
