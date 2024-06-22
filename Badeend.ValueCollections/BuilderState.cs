using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Badeend.ValueCollections;

/// <summary>
/// A `state` field represents one of two things depending on its sign:
///
/// ## Zero or above: The collection is still mutable.
/// During this phase, the collection is only used from within Builders and the
/// collection does not need to be thread-safe yet. The actual value is a
/// version number used to invalidate enumerators.
///
/// ## Below zero: The collection is immutable.
/// During this phase, the collection has been exposed to the user and must be
/// thread-safe. The state field is used to cache the hash code of the collection's
/// contents. `-1` is used as a sentinel value to indicate that the hash code has
/// never been computed yet.
/// </summary>
internal static class BuilderState
{
	/// <summary>
	/// The initial state for a mutable collection.
	/// </summary>
	internal const int InitialMutable = 0;

	/// <summary>
	/// The initial state for an immutable collection.
	/// </summary>
	internal const int InitialImmutable = -1;

	/// <summary>
	/// The last valid version number before incrementing it will overflow into the negative (immutable) state range.
	/// </summary>
	internal const uint LastMutableVersion = 0x7FFFFFFF;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static bool IsMutable(int state) => state >= 0;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static bool IsImmutable(int state) => state < 0;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static bool ReadHashCode(scoped ref int state, out int hashCode)
	{
		hashCode = Volatile.Read(ref state);

		Debug.Assert(IsImmutable(hashCode));

		return hashCode != InitialImmutable;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static int AdjustAndStoreHashCode(scoped ref int state, int hashCode)
	{
		if (hashCode > 0)
		{
			// Flip all bits. The result is guaranteed to be negative, and because
			// we only end up here in case it is _greater_ than zero, we can't
			// accidentally end up with `InitialImmutable` either.
			hashCode = ~hashCode;
		}
		else if (hashCode == 0)
		{
			// Special case for exactly zero, because its bitwise negation would
			// result in `InitialImmutable`.
			hashCode = -42; // Obviously :)
		}

		Debug.Assert(IsImmutable(hashCode));
		Debug.Assert(hashCode != InitialImmutable);

		Volatile.Write(ref state, hashCode);

		return hashCode;
	}

	[DoesNotReturn]
	internal static void ThrowBuiltException()
	{
		throw new InvalidOperationException("Builder has already been built");
	}
}
