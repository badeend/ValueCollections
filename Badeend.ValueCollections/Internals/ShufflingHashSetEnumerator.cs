using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Badeend.ValueCollections.Internals;

/// <summary>
/// An enumerator that deliberately messes up the enumeration order to prevent
/// users from inadvertently depending on it being the insertion order.
///
/// At the moment, this is done by first skipping a small semi-random amount of
/// elements from the inner enumerator at the start and then appending those at the end.
/// </summary>
[StructLayout(LayoutKind.Auto)]
internal struct ShufflingHashSetEnumerator<T> : IEnumeratorLike<T>
{
	private readonly HashSet<T> set;
	private readonly int initialSeed;
	private HashSet<T>.Enumerator inner;
	private EnumeratorState state;
	private T current;
	private int stashIndex;
	private InlineArray8<T> stash;

	internal ShufflingHashSetEnumerator(HashSet<T> set, int initialSeed)
	{
		this.set = set;
		this.initialSeed = initialSeed;
		this.inner = set.GetEnumerator();
		this.state = EnumeratorState.Uninitialized;
		this.current = default!;
	}

	/// <inheritdoc/>
	public readonly T Current
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => this.current;
	}

	/// <inheritdoc/>
	public bool MoveNext()
	{
		if (this.state == EnumeratorState.Uninitialized)
		{
			this.stashIndex = InlineArray8<T>.Length - 1;

			var setSize = this.set.Count;
			if (setSize > 1)
			{
				// Use the hashcode of the HashSet as a semi-random seed.
				// All we care about is educating developers that the order can't be
				// trusted. It doesn't have to cryptographically secure.
				// The hashcode doesn't change over the lifetime of the HashSet,
				// so enumerating the exact same instance multiple times yields
				// the same results.
				var seed = unchecked(this.initialSeed + RuntimeHelpers.GetHashCode(this.set));

				var maxStashSize = Math.Min(setSize, InlineArray8<T>.Length);
				var stashSize = unchecked((int)((uint)seed % (uint)maxStashSize));

				Debug.Assert(stashSize >= 0);
				Debug.Assert(stashSize <= setSize);
				Debug.Assert(stashSize <= InlineArray8<T>.Length);

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

		if (++this.stashIndex < InlineArray8<T>.Length)
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