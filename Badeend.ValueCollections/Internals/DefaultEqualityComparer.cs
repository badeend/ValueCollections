using System.Runtime.CompilerServices;

namespace Badeend.ValueCollections.Internals;

internal readonly struct DefaultEqualityComparer<T>
{
	private readonly EqualityComparer<T>? comparer;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public DefaultEqualityComparer()
	{
		if (default(T) is null)
		{
			this.comparer = EqualityComparer<T>.Default;
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public int GetHashCode(T value)
	{
		if (default(T) is null)
		{
			if (value is null)
			{
				return 0;
			}
			else
			{
				return this.comparer!.GetHashCode(value);
			}
		}
		else
		{
			return value!.GetHashCode();
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool Equals(T left, T right)
	{
		if (default(T) is null)
		{
			return this.comparer!.Equals(left, right);
		}
		else
		{
			return EqualityComparer<T>.Default.Equals(left, right); // TODO: now that this has been moved into a method, is the JIT still able to devirtualize it?
		}
	}
}
