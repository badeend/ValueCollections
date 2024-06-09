using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Badeend.ValueCollections;

/// <summary>
/// An enumerator that deliberately messes up the enumeration order to prevent
/// users from inadvertently depending on it being the insertion order.
///
/// At the moment, this is done by first skipping a small semi-random amount of
/// elements from the inner enumerator at the start and then appending those at the end.
/// </summary>
[StructLayout(LayoutKind.Auto)]
internal struct ShufflingDictionaryEnumerator<TKey, TValue> : IEnumeratorLike<KeyValuePair<TKey, TValue>>
	where TKey : notnull
{
	private readonly Dictionary<TKey, TValue> dictionary;
	private readonly int initialSeed;
	private Dictionary<TKey, TValue>.Enumerator inner;
	private EnumeratorState state;
	private KeyValuePair<TKey, TValue> current;
	private int stashIndex;
	private InlineArray8<KeyValuePair<TKey, TValue>> stash;

	internal ShufflingDictionaryEnumerator(Dictionary<TKey, TValue> dictionary, int initialSeed)
	{
		this.dictionary = dictionary;
		this.initialSeed = initialSeed;
		this.inner = dictionary.GetEnumerator();
		this.state = EnumeratorState.Uninitialized;
	}

	/// <inheritdoc/>
	public readonly KeyValuePair<TKey, TValue> Current
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => this.current;
	}

	/// <inheritdoc/>
	public bool MoveNext()
	{
		if (this.state == EnumeratorState.Uninitialized)
		{
			this.stashIndex = InlineArray8<KeyValuePair<TKey, TValue>>.Length - 1;

			var dictionarySize = this.dictionary.Count;
			if (dictionarySize > 1)
			{
				// Use the hashcode of the Dictionary as a semi-random seed.
				// All we care about is educating developers that the order can't be
				// trusted. It doesn't have to cryptographically secure.
				// The hashcode doesn't change over the lifetime of the Dictionary,
				// so enumerating the exact same instance multiple times yields
				// the same results.
				var seed = unchecked(this.initialSeed + RuntimeHelpers.GetHashCode(this.dictionary));

				var maxStashSize = Math.Min(dictionarySize, InlineArray8<KeyValuePair<TKey, TValue>>.Length);
				var stashSize = unchecked((int)((uint)seed % (uint)maxStashSize));

				Debug.Assert(stashSize >= 0);
				Debug.Assert(stashSize <= dictionarySize);
				Debug.Assert(stashSize <= InlineArray8<KeyValuePair<TKey, TValue>>.Length);

				for (int i = 0; i < stashSize; i++)
				{
					this.inner.MoveNext();

					// Stash them in reverse order, just for fun and giggles.
					this.stash[this.stashIndex--] = this.inner.Current;
				}
			}

			this.state = EnumeratorState.Bulk;
		}

		if (this.state == EnumeratorState.Bulk && this.inner.MoveNext())
		{
			this.current = this.inner.Current;
			return true;
		}

		if (++this.stashIndex < InlineArray8<KeyValuePair<TKey, TValue>>.Length)
		{
			this.current = this.stash[this.stashIndex];
			this.state = EnumeratorState.Stash;
			return true;
		}
		else
		{
			this.state = EnumeratorState.Finished;
			return false;
		}
	}

	private enum EnumeratorState
	{
		Uninitialized,
		Bulk,
		Stash,
		Finished,
	}
}
