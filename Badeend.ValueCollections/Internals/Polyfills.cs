using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;

namespace Badeend.ValueCollections.Internals;

internal static class Polyfills
{
#if NET6_0_OR_GREATER
	internal static int ArrayMaxLength { get; } = Array.MaxLength;
#else
	internal static int ArrayMaxLength { get; } = 0X7FFFFFC7;
#endif

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static unsafe ref T NullRef<T>() => ref Unsafe.AsRef<T>(null);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static unsafe bool IsNullRef<T>(ref readonly T value) => Unsafe.AsPointer(ref Unsafe.AsRef(in value)) == null;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool TryGetNonEnumeratedCount<T>(IEnumerable<T> source, out int count)
	{
#if NET6_0_OR_GREATER
		return System.Linq.Enumerable.TryGetNonEnumeratedCount(source, out count);
#else
		if (source is ICollection<T> collection)
		{
			count = collection.Count;
			return true;
		}

		count = 0;
		return false;
#endif
	}

	public static string? GetEnumName<TEnum>(TEnum value)
		where TEnum : struct, Enum
	{
#if NET5_0_OR_GREATER
		return Enum.GetName(value);
#else
		return Enum.GetName(typeof(TEnum), value);
#endif
	}

	// Adapted from: https://github.com/dotnet/runtime/blob/6be24fd37e7d9f04c7fa903b8b6912c3eafe7198/src/libraries/System.Security.Cryptography/src/System/Security/Cryptography/RandomNumberGenerator.cs#L289
	internal static void Shuffle<T>(Span<T> values)
	{
#if NET8_0_OR_GREATER
		RandomNumberGenerator.Shuffle(values);
#else
		int n = values.Length;

		for (int i = 0; i < n - 1; i++)
		{
			int j = GetRandomInt32(i, n);

			if (i != j)
			{
				var temp = values[i];
				values[i] = values[j];
				values[j] = temp;
			}
		}
#endif
	}

#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_0_OR_GREATER
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static int GetRandomInt32(int fromInclusive, int toExclusive) => RandomNumberGenerator.GetInt32(fromInclusive, toExclusive);
#else
	[ThreadStatic]
	private static Csprng? csprng;

	// Adapted from: https://github.com/dotnet/runtime/blob/6be24fd37e7d9f04c7fa903b8b6912c3eafe7198/src/libraries/System.Security.Cryptography/src/System/Security/Cryptography/RandomNumberGenerator.cs#L103
	private static int GetRandomInt32(int fromInclusive, int toExclusive)
	{
		Debug.Assert(fromInclusive < toExclusive);

		// The total possible range is [0, 4,294,967,295).
		// Subtract one to account for zero being an actual possibility.
		uint range = (uint)toExclusive - (uint)fromInclusive - 1;

		// If there is only one possible choice, nothing random will actually happen, so return
		// the only possibility.
		if (range == 0)
		{
			return fromInclusive;
		}

		// Create a mask for the bits that we care about for the range. The other bits will be
		// masked away.
		uint mask = range;
		mask |= mask >> 1;
		mask |= mask >> 2;
		mask |= mask >> 4;
		mask |= mask >> 8;
		mask |= mask >> 16;

		csprng ??= new Csprng();

		uint result;
		do
		{
			result = mask & (uint)csprng.Next();
		}
		while (result > range);

		return (int)result + fromInclusive;
	}

	private sealed class Csprng
	{
		private const int BufferSize = 256;
		private const int ElementSize = sizeof(int);

		private readonly RNGCryptoServiceProvider provider = new();
		private readonly byte[] buffer = new byte[BufferSize * ElementSize];
		private int nextIndex = BufferSize;

		internal int Next()
		{
			if (this.nextIndex >= BufferSize)
			{
				this.provider.GetBytes(this.buffer);
				this.nextIndex = 0;
			}

			var result = BitConverter.ToInt32(this.buffer, this.nextIndex * ElementSize);
			this.nextIndex++;
			return result;
		}
	}
#endif

#if NETSTANDARD2_0
	/*
	The following polyfill for System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<T>()
	has been adapted from the .NET runtime:
	https://github.com/dotnet/corefx/blob/v2.2.8/src/System.Memory/src/System/SpanHelpers.cs#L129
	*/
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static bool IsReferenceOrContainsReferences<T>() => PerTypeValues<T>.IsReferenceOrContainsReferences;

	private static bool IsReferenceOrContainsReferencesCore(Type type)
	{
		if (type.GetTypeInfo().IsPrimitive)
		{
			return false;
		}

		if (!type.GetTypeInfo().IsValueType)
		{
			return true;
		}

		// If type is a Nullable<> of something, unwrap it first.
		var underlyingNullable = Nullable.GetUnderlyingType(type);
		if (underlyingNullable != null)
		{
			type = underlyingNullable;
		}

		if (type.GetTypeInfo().IsEnum)
		{
			return false;
		}

		foreach (var field in type.GetTypeInfo().DeclaredFields)
		{
			if (field.IsStatic)
			{
				continue;
			}

			if (IsReferenceOrContainsReferencesCore(field.FieldType))
			{
				return true;
			}
		}

		return false;
	}

	private static class PerTypeValues<T>
	{
		internal static readonly bool IsReferenceOrContainsReferences = IsReferenceOrContainsReferencesCore(typeof(T));
	}

#else

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static bool IsReferenceOrContainsReferences<T>() => System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<T>();

#endif

	[Conditional("DEBUG")]
	internal static void DebugAssert([DoesNotReturnIf(false)] bool condition) => Debug.Assert(condition);

	[Conditional("DEBUG")]
	internal static void DebugAssert([DoesNotReturnIf(false)] bool condition, string message) => Debug.Assert(condition, message);
}
