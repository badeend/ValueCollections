using System.Collections;
using System.Runtime.CompilerServices;

namespace Badeend.ValueCollections;

/// <summary>
/// Extension methods related to <see cref="ValueSlice{T}"/>.
/// </summary>
public static class ValueSliceExtensions
{
	/// <summary>
	/// Copy the <paramref name="items"/> into a new <see cref="ValueSlice{T}"/>.
	/// </summary>
	public static ValueSlice<T> ToValueSlice<T>(this ReadOnlySpan<T> items) => new(items.ToArray());

	/// <summary>
	/// Copy the <paramref name="items"/> into a new <see cref="ValueSlice{T}"/>.
	/// </summary>
	public static ValueSlice<T> ToValueSlice<T>(this Span<T> items) => new(items.ToArray());

	/// <summary>
	/// Copy the <paramref name="items"/> into a new <see cref="ValueSlice{T}"/>.
	/// </summary>
	public static ValueSlice<T> ToValueSlice<T>(this ReadOnlyMemory<T> items) => new(items.ToArray());

	/// <summary>
	/// Copy the <paramref name="items"/> into a new <see cref="ValueSlice{T}"/>.
	/// </summary>
	public static ValueSlice<T> ToValueSlice<T>(this Memory<T> items) => new(items.ToArray());

	/// <summary>
	/// Copy the <paramref name="items"/> into a new <see cref="ValueSlice{T}"/>.
	/// </summary>
	public static ValueSlice<T> ToValueSlice<T>(this List<T> items) => new(items.ToArray());

	/// <summary>
	/// Copy the <paramref name="items"/> into a new <see cref="ValueSlice{T}"/>.
	/// </summary>
	public static ValueSlice<T> ToValueSlice<T>(this IEnumerable<T> items) => new(items.ToArray());

	/// <summary>
	/// Create a new <see cref="IEnumerable{T}"/> view over the slice.
	///
	/// This method allocates a new fixed-size IEnumerable instance. The items
	/// are not copied.
	/// </summary>
	public static IEnumerable<T> AsEnumerable<T>(this ValueSlice<T> slice) => new ReadOnlyList<T>(slice);

	/// <summary>
	/// Create a new <see cref="IReadOnlyList{T}"/> view over the slice.
	///
	/// This method allocates a new fixed-size IReadOnlyList instance. The items
	/// are not copied.
	/// </summary>
	public static IReadOnlyList<T> AsReadOnlyList<T>(this ValueSlice<T> slice) => new ReadOnlyList<T>(slice);

	private sealed class ReadOnlyList<T> : IEnumerable<T>, IReadOnlyList<T>
	{
		private readonly ValueSlice<T> slice;

		internal ReadOnlyList(ValueSlice<T> slice)
		{
			this.slice = slice;
		}

		public T this[int index] => this.slice[index];

		public int Count => this.slice.Length;

		public IEnumerator<T> GetEnumerator()
		{
			foreach (var value in this.slice)
			{
				yield return value;
			}
		}

		IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
	}

	/// <summary>
	/// Returns <see langword="true"/> when the two slices have identical length
	/// and content.
	///
	/// This is the default comparison mechanism of ValueSlices.
	///
	/// Similar to <c>MemoryExtensions.SequenceEqual</c>.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool SequenceEqual<T>(this ValueSlice<T> slice, ValueSlice<T> other)
		where T : IEquatable<T>
	{
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
		return System.MemoryExtensions.SequenceEqual(slice.Span, other.Span);
#else
		return SequenceEqual(slice, other, comparer: default);
#endif
	}

	/// <summary>
	/// Returns <see langword="true"/> when the two slices have identical length
	/// and content.
	///
	/// This is the default comparison mechanism of ValueSlices.
	///
	/// Similar to <c>MemoryExtensions.SequenceEqual</c>.
	/// </summary>
	public static bool SequenceEqual<T>(this ValueSlice<T> slice, ValueSlice<T> other, IEqualityComparer<T>? comparer = default)
	{
		var sliceSpan = slice.Span;
		var otherSpan = other.Span;

#if NET6_0_OR_GREATER
		return System.MemoryExtensions.SequenceEqual(sliceSpan, otherSpan, comparer);
#else
		if (sliceSpan.Length != otherSpan.Length)
		{
			return false;
		}

		comparer ??= EqualityComparer<T>.Default;

		var length = sliceSpan.Length;

		for (int i = 0; i < length; i++)
		{
			if (!comparer.Equals(sliceSpan[i], otherSpan[i]))
			{
				return false;
			}
		}

		return true;
#endif
	}

	/// <summary>
	/// Return the index of the first occurrence of <paramref name="item"/> in
	/// <paramref name="slice"/>, or <c>-1</c> if not found.
	///
	/// Similar to <c>MemoryExtensions.IndexOf</c>.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static int IndexOf<T>(this ValueSlice<T> slice, T item)
		where T : IEquatable<T>
	{
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
		return System.MemoryExtensions.IndexOf(slice.Span, item);
#else
		return IndexOf(slice, item, comparer: default);
#endif
	}

	/// <summary>
	/// Return the index of the first occurrence of <paramref name="item"/> in
	/// <paramref name="slice"/>, or <c>-1</c> if not found.
	///
	/// Similar to <c>MemoryExtensions.IndexOf</c>.
	/// </summary>
	public static int IndexOf<T>(this ValueSlice<T> slice, T item, IEqualityComparer<T>? comparer = default)
	{
		var span = slice.Span;

		comparer ??= EqualityComparer<T>.Default;

		for (int i = 0; i < span.Length; i++)
		{
			if (comparer.Equals(item, span[i]))
			{
				return i;
			}
		}

		return -1;
	}

	/// <summary>
	/// Return the index of the last occurrence of <paramref name="item"/> in
	/// <paramref name="slice"/>, or <c>-1</c> if not found.
	///
	/// Similar to <c>MemoryExtensions.LastIndexOf</c>.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static int LastIndexOf<T>(this ValueSlice<T> slice, T item)
		where T : IEquatable<T>
	{
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
		return System.MemoryExtensions.LastIndexOf(slice.Span, item);
#else
		return LastIndexOf(slice, item, comparer: default);
#endif
	}

	/// <summary>
	/// Return the index of the last occurrence of <paramref name="item"/> in
	/// <paramref name="slice"/>, or <c>-1</c> if not found.
	///
	/// Similar to <c>MemoryExtensions.LastIndexOf</c>.
	/// </summary>
	public static int LastIndexOf<T>(this ValueSlice<T> slice, T item, IEqualityComparer<T>? comparer = default)
	{
		var span = slice.Span;

		comparer ??= EqualityComparer<T>.Default;

		for (int i = 0; i < span.Length; i++)
		{
			if (comparer.Equals(item, span[i]))
			{
				return i;
			}
		}

		return -1;
	}

	/// <summary>
	/// Returns <see langword="true"/> when the <paramref name="slice"/>
	/// contains the specified <paramref name="item"/>.
	///
	/// Similar to <c>MemoryExtensions.Contains</c>.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool Contains<T>(this ValueSlice<T> slice, T item)
		where T : IEquatable<T>
	{
#if NETCOREAPP3_0_OR_GREATER
		return System.MemoryExtensions.Contains(slice.Span, item);
#else
		return Contains(slice, item, comparer: default);
#endif
	}

	/// <summary>
	/// Returns <see langword="true"/> when the <paramref name="slice"/>
	/// contains the specified <paramref name="item"/>.
	///
	/// Similar to <c>MemoryExtensions.Contains</c>.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool Contains<T>(this ValueSlice<T> slice, T item, IEqualityComparer<T>? comparer = default)
	{
		return IndexOf(slice, item, comparer) >= 0;
	}

	/// <summary>
	/// Finds the length of any common prefix shared between
	/// <paramref name="slice"/> and <paramref name="other"/>. If there's no
	/// shared prefix, <c>0</c> is returned.
	///
	/// Similar to <c>MemoryExtensions.CommonPrefixLength</c>.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static int CommonPrefixLength<T>(this ValueSlice<T> slice, ValueSlice<T> other)
		where T : IEquatable<T>
	{
#if NET7_0_OR_GREATER
		return System.MemoryExtensions.CommonPrefixLength(slice.Span, other.Span);
#else
		return CommonPrefixLength(slice, other, comparer: default);
#endif
	}

	/// <summary>
	/// Finds the length of any common prefix shared between
	/// <paramref name="slice"/> and <paramref name="other"/>. If there's no
	/// shared prefix, <c>0</c> is returned.
	///
	/// Similar to <c>MemoryExtensions.CommonPrefixLength</c>.
	/// </summary>
	public static int CommonPrefixLength<T>(this ValueSlice<T> slice, ValueSlice<T> other, IEqualityComparer<T>? comparer = default)
	{
		var sliceSpan = slice.Span;
		var otherSpan = other.Span;

#if NET7_0_OR_GREATER
		return System.MemoryExtensions.CommonPrefixLength(sliceSpan, otherSpan, comparer);
#else
		comparer ??= EqualityComparer<T>.Default;
		var minLength = Math.Min(sliceSpan.Length, otherSpan.Length);

		for (int i = 0; i < minLength; i++)
		{
			if (!comparer.Equals(sliceSpan[i], otherSpan[i]))
			{
				return i;
			}
		}

		return minLength;
#endif
	}
}
