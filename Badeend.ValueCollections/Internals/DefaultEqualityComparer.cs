using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Badeend.ValueCollections.Internals;

internal readonly struct DefaultEqualityComparer<T>
{
	private readonly EqualityComparer<T>? comparer;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public DefaultEqualityComparer()
	{
#if NETCOREAPP2_1_OR_GREATER
		if (default(T) is null)
		{
			this.comparer = EqualityComparer<T>.Default;
		}
#else
		this.comparer = EqualityComparer<T>.Default;
#endif
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	[SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Emulate the EqualityComparer API.")]
	public int GetHashCode(T value)
	{
		if (value is null)
		{
			return 0;
		}
		else
		{
			return value.GetHashCode();
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool Equals(T left, T right)
	{
#if NETCOREAPP2_1_OR_GREATER
		if (default(T) is null)
		{
			return this.comparer!.Equals(left, right);
		}
		else
		{
			return EqualityComparer<T>.Default.Equals(left, right); // TODO: now that this has been moved into a method, is the JIT still able to devirtualize it?
		}
#else
		return this.comparer!.Equals(left, right);
#endif
	}
}
