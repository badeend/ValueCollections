using System.Collections;
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
	/// Values collections.
	/// </content>
	public partial struct Builder
	{
		/// <summary>
		/// All values in the dictionary in no particular order.
		/// </summary>
		/// <remarks>
		/// Every modification to the builder invalidates any <see cref="ValuesEnumerator"/>
		/// obtained before that moment.
		/// </remarks>
		[Pure]
		public ValuesEnumerator Values
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => new(this.Read());
		}

		/// <summary>
		/// This type facilitates reading only the values of a dictionary.
		/// </summary>
		/// <remarks>
		/// This is a stack-allocated enumerator over the values in the dictionary.
		/// Any <see cref="ValuesEnumerator">ValuesEnumerator</see> is only valid unti
		/// the next mutation performed on the builder. After which, a new
		/// <see cref="ValuesEnumerator">ValuesEnumerator</see> must be obtained.
		///
		/// To prevent accidental boxing, ValuesEnumerator does not implement any interface.
		/// If you want to use the values as a collection (e.g. <see cref="IEnumerable{TValue}"/>,
		/// <see cref="IReadOnlyCollection{TValue}"/>, etc.) you can still manually box
		/// it by calling <see cref="AsCollection"/>.
		/// </remarks>
		[StructLayout(LayoutKind.Auto)]
		public struct ValuesEnumerator : IEnumeratorLike<TValue>
		{
			private readonly Snapshot snapshot;
			private RawDictionary<TKey, TValue>.Enumerator inner;

			internal ValuesEnumerator(Snapshot snapshot)
			{
				this.snapshot = snapshot;
				this.inner = snapshot.AssertAlive().GetEnumerator();
			}

			/// <summary>
			/// Create a new heap-allocated temporary view over the values in the dictionary.
			/// </summary>
			/// <remarks>
			/// This method is an <c>O(1)</c> operation and allocates a new fixed-size
			/// collection instance. The items are not copied.
			///
			/// Every modification to the builder invalidates any <see cref="ValuesCollection"/>
			/// obtained before that moment.
			/// </remarks>
			public readonly ValuesCollection AsCollection() => this.snapshot.GetDictionaryUnsafe().GetBuilderCollection().GetBuilderValuesCollection(this.snapshot);

			/// <summary>
			/// Returns a new ValuesEnumerator.
			///
			/// Typically, you don't need to manually call this method, but instead use
			/// the built-in <c>foreach</c> syntax.
			/// </summary>
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public readonly ValuesEnumerator GetEnumerator() => new(this.snapshot);

			/// <inheritdoc/>
			public readonly TValue Current
			{
				[MethodImpl(MethodImplOptions.AggressiveInlining)]
				get => this.inner.CurrentValue;
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
		/// A heap-allocated read-only view over all the values in the dictionary.
		/// </summary>
		/// <remarks>
		/// Any <see cref="ValuesCollection">ValuesCollection</see> is only valid until
		/// the next mutation performed on the builder. As long as the ValuesCollection
		/// is usable, it effectively represents an immutable set of values.
		/// </remarks>
		public sealed class ValuesCollection : ICollection<TValue>, IReadOnlyCollection<TValue>
		{
			internal static readonly ValuesCollection Empty = new(default(ValueDictionary<TKey, TValue>.Builder).Read());

			internal Snapshot Snapshot { get; }

			internal ValuesCollection(Snapshot snapshot)
			{
				this.Snapshot = snapshot;
			}

			/// <inheritdoc/>
			IEnumerator<TValue> IEnumerable<TValue>.GetEnumerator()
			{
				ref readonly var builder = ref this.Snapshot.AssertAlive();
				if (builder.Count == 0)
				{
					return EnumeratorLike.Empty<TValue>();
				}
				else
				{
					return EnumeratorLike.AsIEnumerator<TValue, ValuesEnumerator>(new(this.Snapshot));
				}
			}

			/// <inheritdoc/>
			IEnumerator IEnumerable.GetEnumerator() => (this as IEnumerable<TValue>).GetEnumerator();

			/// <inheritdoc/>
			int ICollection<TValue>.Count => this.Snapshot.AssertAlive().Count;

			/// <inheritdoc/>
			int IReadOnlyCollection<TValue>.Count => this.Snapshot.AssertAlive().Count;

			/// <inheritdoc/>
			bool ICollection<TValue>.IsReadOnly => true;

			/// <inheritdoc/>
			bool ICollection<TValue>.Contains(TValue item) => this.Snapshot.AssertAlive().ContainsValue(item);

			/// <inheritdoc/>
			void ICollection<TValue>.CopyTo(TValue[] array, int index) => this.Snapshot.AssertAlive().Values_CopyTo(array, index);

			/// <inheritdoc/>
			void ICollection<TValue>.Add(TValue item) => throw ImmutableException();

			/// <inheritdoc/>
			bool ICollection<TValue>.Remove(TValue item) => throw ImmutableException();

			/// <inheritdoc/>
			void ICollection<TValue>.Clear() => throw ImmutableException();
		}
	}
}
