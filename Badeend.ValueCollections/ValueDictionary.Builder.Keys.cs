using System.Collections;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Badeend.ValueCollections.Internals;

namespace Badeend.ValueCollections;

#pragma warning disable CA1815 // Override equals and operator equals on value types
#pragma warning disable CA1034 // Nested types should not be visible

/// <content>
/// Builder code.
/// </content>
public partial class ValueDictionary<TKey, TValue>
{
	/// <content>
	/// Keys collections.
	/// </content>
	public partial struct Builder
	{
		/// <summary>
		/// All keys in the dictionary in no particular order.
		/// </summary>
		/// <remarks>
		/// Every modification to the builder invalidates any <see cref="KeysEnumerator"/>
		/// obtained before that moment.
		/// </remarks>
		[Pure]
		public KeysEnumerator Keys
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => new(this.Read());
		}

		/// <summary>
		/// This type facilitates reading only the keys of a dictionary.
		/// </summary>
		/// <remarks>
		/// This is a stack-allocated enumerator over the keys in the dictionary.
		/// Any <see cref="KeysEnumerator">KeysEnumerator</see> is only valid unti
		/// the next mutation performed on the builder. After which, a new
		/// <see cref="KeysEnumerator">KeysEnumerator</see> must be obtained.
		///
		/// To prevent accidental boxing, KeysEnumerator does not implement any interface.
		/// If you want to use the keys as a collection (e.g. <see cref="IEnumerable{TKey}"/>,
		/// <see cref="IReadOnlyCollection{TKey}"/>, etc.) you can still manually box
		/// it by calling <see cref="AsCollection"/>.
		/// </remarks>
		[StructLayout(LayoutKind.Auto)]
		public struct KeysEnumerator : IEnumeratorLike<TKey>
		{
			private readonly Snapshot snapshot;
			private RawDictionary<TKey, TValue>.Enumerator inner;

			internal KeysEnumerator(Snapshot snapshot)
			{
				this.snapshot = snapshot;
				this.inner = snapshot.AssertAlive().GetEnumerator();
			}

			/// <summary>
			/// Create a new heap-allocated temporary view over the keys in the dictionary.
			/// </summary>
			/// <remarks>
			/// This method is an <c>O(1)</c> operation and allocates a new fixed-size
			/// collection instance. The items are not copied.
			///
			/// Every modification to the builder invalidates any <see cref="KeysCollection"/>
			/// obtained before that moment.
			/// </remarks>
			public readonly KeysCollection AsCollection() => this.snapshot.GetDictionaryUnsafe().GetBuilderCollection().GetBuilderKeysCollection(this.snapshot);

			/// <summary>
			/// Returns a new KeysEnumerator.
			///
			/// Typically, you don't need to manually call this method, but instead use
			/// the built-in <c>foreach</c> syntax.
			/// </summary>
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public readonly KeysEnumerator GetEnumerator() => new(this.snapshot);

			/// <inheritdoc/>
			public readonly TKey Current
			{
				[MethodImpl(MethodImplOptions.AggressiveInlining)]
				get => this.inner.CurrentKey;
			}

			/// <inheritdoc/>
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public bool MoveNext()
			{
				this.snapshot.AssertAlive();

				return this.inner.MoveNext();
			}
		}

		/// <summary>
		/// A heap-allocated read-only view over all the keys in the dictionary.
		/// </summary>
		/// <remarks>
		/// Any <see cref="KeysCollection">KeysCollection</see> is only valid until
		/// the next mutation performed on the builder. As long as the KeysCollection
		/// is usable, it effectively represents an immutable set of keys.
		/// </remarks>
		[DebuggerDisplay("Count = {Count}")]
		[DebuggerTypeProxy(typeof(ValueDictionary<,>.Builder.KeysCollection.DebugView))]
		public sealed class KeysCollection : ISet<TKey>, IReadOnlyCollection<TKey>, IReadOnlySet<TKey>
		{
			internal static readonly KeysCollection Empty = new(default(ValueDictionary<TKey, TValue>.Builder).Read());

			internal Snapshot Snapshot { get; }

			internal KeysCollection(Snapshot snapshot)
			{
				this.Snapshot = snapshot;
			}

			/// <inheritdoc/>
			IEnumerator<TKey> IEnumerable<TKey>.GetEnumerator()
			{
				ref readonly var builder = ref this.Snapshot.AssertAlive();
				if (builder.Count == 0)
				{
					return EnumeratorLike.Empty<TKey>();
				}
				else
				{
					return EnumeratorLike.AsIEnumerator<TKey, KeysEnumerator>(new(this.Snapshot));
				}
			}

			/// <inheritdoc/>
			IEnumerator IEnumerable.GetEnumerator() => (this as IEnumerable<TKey>).GetEnumerator();

			// Used by DebuggerDisplay attribute
			private int Count => this.Snapshot.AssertAlive().Count;

			/// <inheritdoc/>
			int ICollection<TKey>.Count => this.Count;

			/// <inheritdoc/>
			int IReadOnlyCollection<TKey>.Count => this.Count;

			/// <inheritdoc/>
			bool ICollection<TKey>.IsReadOnly => true;

			/// <inheritdoc/>
			bool ICollection<TKey>.Contains(TKey item) => this.Snapshot.AssertAlive().ContainsKey(item);

			/// <inheritdoc/>
			void ICollection<TKey>.CopyTo(TKey[] array, int index) => this.Snapshot.AssertAlive().Keys_CopyTo(array, index);

			/// <inheritdoc/>
			bool ISet<TKey>.IsProperSubsetOf(IEnumerable<TKey> other) => this.Snapshot.AssertAlive().Keys_IsProperSubsetOf(other);

			/// <inheritdoc/>
			bool ISet<TKey>.IsProperSupersetOf(IEnumerable<TKey> other) => this.Snapshot.AssertAlive().Keys_IsProperSupersetOf(other);

			/// <inheritdoc/>
			bool ISet<TKey>.IsSubsetOf(IEnumerable<TKey> other) => this.Snapshot.AssertAlive().Keys_IsSubsetOf(other);

			/// <inheritdoc/>
			bool ISet<TKey>.IsSupersetOf(IEnumerable<TKey> other) => this.Snapshot.AssertAlive().Keys_IsSupersetOf(other);

			/// <inheritdoc/>
			bool ISet<TKey>.Overlaps(IEnumerable<TKey> other) => this.Snapshot.AssertAlive().Keys_Overlaps(other);

			/// <inheritdoc/>
			bool ISet<TKey>.SetEquals(IEnumerable<TKey> other) => this.Snapshot.AssertAlive().Keys_SetEquals(other);

			/// <inheritdoc/>
			bool IReadOnlySet<TKey>.Contains(TKey item) => this.Snapshot.AssertAlive().ContainsKey(item);

			/// <inheritdoc/>
			bool IReadOnlySet<TKey>.IsProperSubsetOf(IEnumerable<TKey> other) => this.Snapshot.AssertAlive().Keys_IsProperSubsetOf(other);

			/// <inheritdoc/>
			bool IReadOnlySet<TKey>.IsProperSupersetOf(IEnumerable<TKey> other) => this.Snapshot.AssertAlive().Keys_IsProperSupersetOf(other);

			/// <inheritdoc/>
			bool IReadOnlySet<TKey>.IsSubsetOf(IEnumerable<TKey> other) => this.Snapshot.AssertAlive().Keys_IsSubsetOf(other);

			/// <inheritdoc/>
			bool IReadOnlySet<TKey>.IsSupersetOf(IEnumerable<TKey> other) => this.Snapshot.AssertAlive().Keys_IsSupersetOf(other);

			/// <inheritdoc/>
			bool IReadOnlySet<TKey>.Overlaps(IEnumerable<TKey> other) => this.Snapshot.AssertAlive().Keys_Overlaps(other);

			/// <inheritdoc/>
			bool IReadOnlySet<TKey>.SetEquals(IEnumerable<TKey> other) => this.Snapshot.AssertAlive().Keys_SetEquals(other);

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

			internal sealed class DebugView(KeysCollection collection)
			{
				[DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
				internal TKey[] Items => ValueDictionary<TKey, TValue>.DebugView.CreateKeys(in collection.Snapshot.AssertAlive());
			}
		}
	}
}
