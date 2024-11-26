using System.Diagnostics.CodeAnalysis;

namespace Badeend.ValueCollections.Internals;

internal static class ThrowHelpers
{
	[SuppressMessage("StyleCop.CSharp.NamingRules", "SA1300:Element should begin with upper-case letter", Justification = "Can't. Uses reflection for exact name.")]
	internal enum Argument
	{
		minimumCapacity,
		targetCapacity,
		value,
		index,
		offset,
		count,
		length,
		collection,
		match,
		comparison,
		items,
		array,
		source,
		other,
		destination,
	}

	[DoesNotReturn]
	internal static void ThrowArgumentOutOfRangeException(Argument argument) => throw new ArgumentOutOfRangeException(Polyfills.GetEnumName(argument));

	[DoesNotReturn]
	internal static void ThrowArgumentNullException(Argument argument) => throw new ArgumentNullException(Polyfills.GetEnumName(argument));

	[DoesNotReturn]
	internal static void ThrowArgumentException_DestinationTooShort(Argument argument) => throw new ArgumentException("Destination too short", Polyfills.GetEnumName(argument));

	[DoesNotReturn]
	internal static void ThrowArgumentException_InvalidOffsetOrLength() => throw new ArgumentException("Invalid offset + length.");

	[DoesNotReturn]
	internal static void ThrowInvalidOperationException_AlreadyBuilt() => throw new InvalidOperationException("Builder has already been built");

	[DoesNotReturn]
	internal static void ThrowInvalidOperationException_UninitializedBuilder() => throw new InvalidOperationException("Uninitialized builder");

	[DoesNotReturn]
	internal static void ThrowInvalidOperationException_CollectionModifiedDuringEnumeration() => throw new InvalidOperationException("Collection was modified during enumeration.");

	[DoesNotReturn]
	internal static void ThrowInvalidOperationException_ConcurrentOperationsNotSupported() => throw new InvalidOperationException("Concurrent access detected. The collection's state is corrupted.");

	[DoesNotReturn]
	internal static void ThrowNotSupportedException_CollectionImmutable() => throw new NotSupportedException("Collection is immutable");

	[DoesNotReturn]
	internal static void ThrowNotSupportedException_CapacityOverflow() => throw new NotSupportedException("Capacity overflowed.");
}
