using System.Collections;
using System.Runtime.CompilerServices;

namespace Badeend.ValueCollections.Internals;

internal static class EnumeratorLike
{
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static IEnumerator<T> Empty<T>() => EmptyEnumerator<T>.Singleton;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static IEnumerator<T> AsIEnumerator<T, TImpl>(TImpl impl)
		where TImpl : struct, IEnumeratorLike<T> => new EnumeratorObject<T, TImpl>(impl);

	private sealed class EmptyEnumerator<T> : IEnumerator<T>
	{
		internal static readonly EmptyEnumerator<T> Singleton = new();

		private EmptyEnumerator()
		{
		}

		public T Current => throw new InvalidOperationException("Invalid enumerator usage.");

		object? IEnumerator.Current => this.Current;

		public bool MoveNext() => false;

		void IEnumerator.Reset() => throw new NotSupportedException();

		void IDisposable.Dispose()
		{
			// Nothing to dispose.
		}
	}

	private sealed class EnumeratorObject<T, TImpl> : IEnumerator<T>
		where TImpl : struct, IEnumeratorLike<T>
	{
		private TImpl impl;
		private EnumeratorState state;

		internal EnumeratorObject(TImpl impl)
		{
			this.impl = impl;
			this.state = EnumeratorState.Initial;
		}

		public T Current
		{
			get
			{
				if (this.state != EnumeratorState.Enumerating)
				{
					throw new InvalidOperationException("Invalid enumerator usage.");
				}

				return this.impl.Current;
			}
		}

		object? IEnumerator.Current => this.Current;

		public bool MoveNext()
		{
			if (this.state == EnumeratorState.Finished)
			{
				return false;
			}

			if (this.impl.MoveNext())
			{
				this.state = EnumeratorState.Enumerating;
				return true;
			}
			else
			{
				this.state = EnumeratorState.Finished;
				return false;
			}
		}

		void IEnumerator.Reset() => throw new NotSupportedException();

		void IDisposable.Dispose()
		{
			// Nothing to dispose.
		}

		private enum EnumeratorState
		{
			Initial,
			Enumerating,
			Finished,
		}
	}
}

/// <summary>
/// This interface satisfies C#'s enumerator duck typing rules without having to
/// implement IDisposable nor the non-generic IEnumerator.
/// </summary>
internal interface IEnumeratorLike<T>
{
	/// <summary>
	/// Gets the element in the collection at the current position of the enumerator.
	/// </summary>
	T Current { get; }

	/// <summary>
	/// Advances the enumerator to the next element of the collection.
	/// </summary>
	bool MoveNext();
}

/// <summary>
/// This interface satisfies C#'s enumerator duck typing rules without having to
/// implement IDisposable nor the non-generic IEnumerator.
/// </summary>
internal interface IRefEnumeratorLike<T> : IEnumeratorLike<T>
{
	/// <summary>
	/// Gets the element in the collection at the current position of the enumerator.
	/// </summary>
	new ref readonly T Current { get; }
}
