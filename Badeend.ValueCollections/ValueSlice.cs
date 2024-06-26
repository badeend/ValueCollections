using System.Collections;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace Badeend.ValueCollections;

/// <summary>
/// Initialization methods for <see cref="ValueSlice{T}"/>.
/// </summary>
public static class ValueSlice
{
	/// <summary>
	/// Copy the <paramref name="items"/> into a new <see cref="ValueSlice{T}"/>.
	/// </summary>
	[Pure]
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static ValueSlice<T> Create<T>(ReadOnlySpan<T> items) => new(items.ToArray());
}

/// <summary>
/// An immutable, thread-safe span with value semantics.
/// </summary>
/// <remarks>
/// This type is similar to <see cref="ReadOnlySpan{T}"/> and
/// <see cref="ReadOnlyMemory{T}"/> in that this too is just a view
/// into an existing allocation at a specified offset+length. Taking a subslice
/// with <see cref="ValueSlice{T}.Slice(int)"/> and
/// <see cref="ValueSlice{T}.Slice(int, int)"/> is very cheap as it reuses the
/// same allocation and only adjusts the internal offset+length fields.
///
/// Unlike ReadOnlySpan&amp;Memory, the data is not just read-only but it is also
/// guaranteed to be immutable.
///
/// Additionally, ValueSlice has "structural equality". This means that two slices
/// are considered equal only when their contents are equal. As long as a value
/// is present in a ValueSlice, its hash code may not change.
///
/// To prevent accidental boxing, ValueSlice does not implement commonly used
/// interfaces such as <see cref="IEnumerable{T}"/> and
/// <see cref="IReadOnlyList{T}"/>. You can still use these interfaces by
/// manually calling <see cref="ValueSlice{T}.AsCollection"/> instead.
///
/// The <c>default</c> value of every ValueSlice is an empty slice.
/// </remarks>
/// <typeparam name="T">The type of items in the slice.</typeparam>
[StructLayout(LayoutKind.Auto)]
[CollectionBuilder(typeof(ValueSlice), nameof(ValueSlice.Create))]
public readonly struct ValueSlice<T> : IEquatable<ValueSlice<T>>
{
	/// <summary>
	/// Get an empty slice.
	///
	/// This does not allocate any memory.
	/// </summary>
	[Pure]
	public static ValueSlice<T> Empty { get; } = default;

	/// <summary>
	/// `null` indicates an empty slice.
	/// </summary>
	private readonly T[]? items;
	private readonly int offset;
	private readonly int length;

	/// <summary>
	/// Length of the slice.
	/// </summary>
	[Pure]
	public int Length
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => this.length;
	}

	/// <summary>
	/// Shortcut for <c>.Length == 0</c>.
	/// </summary>
	[Pure]
	public bool IsEmpty
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => this.length == 0;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal ValueSlice(T[]? items)
		: this(items, 0, items?.Length ?? 0)
	{
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal ValueSlice(T[]? items, int offset, int length)
	{
		if (length == 0)
		{
			this.items = null;
			this.offset = 0;
			this.length = 0;
		}
		else
		{
			this.items = items;
			this.offset = offset;
			this.length = length;
		}
	}

	/// <summary>
	/// Access the slice's contents using a <see cref="ReadOnlySpan{T}"/>.
	/// </summary>
	[Pure]
	public ReadOnlySpan<T> Span
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => this.items.AsSpan(this.offset, this.length);
	}

	/// <summary>
	/// Access the slice's contents using a <see cref="ReadOnlyMemory{T}"/>.
	/// </summary>
	[Pure]
	public ReadOnlyMemory<T> Memory
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => this.items.AsMemory(this.offset, this.length);
	}

	/// <summary>
	/// Get an item from the slice at the specified <paramref name="index"/>.
	/// </summary>
	public ref readonly T this[int index]
	{
		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get
		{
			// This check indirectly also ensures that `items` is not null.
			if ((uint)index >= (uint)this.length)
			{
				ThrowHelpers.ThrowArgumentOutOfRangeException(ThrowHelpers.Argument.index);
			}

			return ref this.items![this.offset + index];
		}
	}

	/// <summary>
	/// Create a subslice, starting at <paramref name="offset"/>.
	///
	/// This does not allocate any memory.
	///
	/// Similar to <see cref="ReadOnlySpan{T}.Slice(int)"/>.
	/// </summary>
	/// <param name="offset">The index at which to begin the subslice.</param>
	/// <exception cref="ArgumentOutOfRangeException">
	///   <paramref name="offset"/> is below <c>0</c> or greater than the
	///   current slice's length.
	/// </exception>
	[Pure]
	public ValueSlice<T> Slice(int offset)
	{
		if ((uint)offset > (uint)this.length)
		{
			ThrowHelpers.ThrowArgumentOutOfRangeException(ThrowHelpers.Argument.offset);
		}

		return new ValueSlice<T>(this.items, this.offset + offset, this.length - offset);
	}

	/// <summary>
	/// Create a subslice with a specified <paramref name="length"/>,
	/// starting at <paramref name="offset"/>.
	///
	/// This does not allocate any memory.
	///
	/// Similar to <see cref="ReadOnlySpan{T}.Slice(int, int)"/>.
	/// </summary>
	/// <param name="offset">The index at which to begin the subslice.</param>
	/// <param name="length">The length of the new subslice.</param>
	/// <exception cref="ArgumentOutOfRangeException">
	///   <paramref name="offset"/> is below <c>0</c> or greater than the
	///   current slice's length.
	/// </exception>
	/// <exception cref="ArgumentOutOfRangeException">
	///   <paramref name="length"/> is below <c>0</c> or would extend beyond the
	///   current slice's length.
	/// </exception>
	[Pure]
	public ValueSlice<T> Slice(int offset, int length)
	{
		if ((uint)offset > (uint)this.length)
		{
			ThrowHelpers.ThrowArgumentOutOfRangeException(ThrowHelpers.Argument.offset);
		}

		var maxLength = this.length - offset;
		if ((uint)length > (uint)maxLength)
		{
			ThrowHelpers.ThrowArgumentOutOfRangeException(ThrowHelpers.Argument.length);
		}

		return new ValueSlice<T>(this.items, this.offset + offset, length);
	}

	/// <summary>
	/// Copy the slice into a new <see cref="ValueList{T}"/>.
	/// </summary>
	[Pure]
	public ValueList<T> ToValueList()
	{
		if (this.length == 0)
		{
			return ValueList<T>.Empty;
		}

		// Try to reuse the existing buffer
		if (this.offset == 0)
		{
			// TODO: check that the length meets a minimum threshold of the total
			// capacity, to prevent unnecessarily keeping large arrays alive.
			return ValueList<T>.CreateImmutableFromArrayUnsafe(this.items!, this.length);
		}

		return ValueList<T>.CreateImmutableFromSpan(this.Span);
	}

	/// <summary>
	/// Copy the contents of the slice into a new array.
	///
	/// Similar to <see cref="ReadOnlySpan{T}.ToArray"/>.
	/// </summary>
	[Pure]
	public T[] ToArray() => this.Span.ToArray();

	/// <summary>
	/// Copy the contents of the slice into an existing <see cref="Span{T}"/>.
	///
	/// Similar to <see cref="ReadOnlySpan{T}.CopyTo"/>.
	/// </summary>
	/// <exception cref="ArgumentException">
	///   <paramref name="destination"/> is shorter than the source slice.
	/// </exception>
	public void CopyTo(Span<T> destination) => this.Span.CopyTo(destination);

	/// <summary>
	/// Attempt to copy the contents of the slice into an existing
	/// <see cref="Span{T}"/>. If the <paramref name="destination"/> is too short,
	/// no items are copied and the method returns <see langword="false"/>.
	///
	/// Similar to <see cref="ReadOnlySpan{T}.TryCopyTo"/>.
	/// </summary>
	public bool TryCopyTo(Span<T> destination) => this.Span.TryCopyTo(destination);

	/// <summary>
	/// Return the index of the first occurrence of <paramref name="item"/> in
	/// <c>this</c>, or <c>-1</c> if not found.
	/// </summary>
	[Pure]
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public int IndexOf(T item)
	{
		if (this.items is null)
		{
			return -1;
		}

		return Array.IndexOf(this.items, item, this.offset, this.length);
	}

	/// <summary>
	/// Return the index of the last occurrence of <paramref name="item"/> in
	/// <c>this</c>, or <c>-1</c> if not found.
	/// </summary>
	[Pure]
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public int LastIndexOf(T item)
	{
		if (this.items is null)
		{
			return -1;
		}

		return Array.LastIndexOf(this.items, item, this.offset + this.length - 1, this.length);
	}

	/// <summary>
	/// Returns <see langword="true"/> when <c>this</c> slice
	/// contains the specified <paramref name="item"/>.
	/// </summary>
	[Pure]
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool Contains(T item) => this.IndexOf(item) >= 0;

	/// <summary>
	/// Perform a binary search for <paramref name="item"/> within the slice.
	/// The slice is assumed to already be sorted. This uses the
	/// <see cref="Comparer{T}.Default">Default</see> comparer and throws if
	/// <typeparamref name="T"/> is not comparable. If the item is found, its
	/// index is returned. Otherwise a negative value is returned representing
	/// the bitwise complement of the index where the item should be inserted.
	/// </summary>
	[Pure]
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public int BinarySearch(T item)
	{
		if (this.items is null)
		{
			return -1;
		}

		return Array.BinarySearch(this.items, this.offset, this.length, item);
	}

	/// <summary>
	/// Returns an enumerator for this <see cref="ValueSlice{T}"/>.
	///
	/// Typically, you don't need to manually call this method, but instead use
	/// the built-in <c>foreach</c> syntax.
	/// </summary>
	[Pure]
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public Enumerator GetEnumerator() => new Enumerator(this);

	/// <summary>
	/// Enumerator for <see cref="ValueSlice{T}"/>.
	/// </summary>
#pragma warning disable CA1034 // Nested types should not be visible
#pragma warning disable CA1815 // Override equals and operator equals on value types
	[StructLayout(LayoutKind.Auto)]
	public struct Enumerator : IRefEnumeratorLike<T>
	{
		private readonly T[]? items;
		private readonly int end;
		private int current;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal Enumerator(ValueSlice<T> slice)
		{
			this.items = slice.items;
			this.end = slice.offset + slice.length;
			this.current = slice.offset - 1;
		}

		/// <inheritdoc/>
		public readonly ref readonly T Current
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => ref this.items![this.current];
		}

		/// <inheritdoc/>
		readonly T IEnumeratorLike<T>.Current => this.Current;

		/// <inheritdoc/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool MoveNext() => (uint)++this.current < this.end;
	}
#pragma warning restore CA1815 // Override equals and operator equals on value types
#pragma warning restore CA1034 // Nested types should not be visible

	/// <inheritdoc/>
	[Pure]
	public override int GetHashCode()
	{
		var hasher = new HashCode();
		hasher.Add(typeof(ValueSlice<T>));
		hasher.Add(this.Length);

		foreach (var item in this)
		{
			hasher.Add(item);
		}

		return hasher.ToHashCode();
	}

	/// <summary>
	/// Returns <see langword="true"/> when the two slices have identical length
	/// and content.
	/// </summary>
	[Pure]
	public bool Equals(ValueSlice<T> other) => SequenceEqual(this, other);

#pragma warning disable CS0809 // Obsolete member overrides non-obsolete member
	/// <inheritdoc/>
	[Pure]
	[Obsolete("Avoid boxing. Use == instead.")]
	[EditorBrowsable(EditorBrowsableState.Never)]
	public override bool Equals(object? obj) => obj is ValueSlice<T> slice && SequenceEqual(this, slice);
#pragma warning restore CS0809 // Obsolete member overrides non-obsolete member

	/// <summary>
	/// Check for equality.
	/// </summary>
	[Pure]
	public static bool operator ==(ValueSlice<T> left, ValueSlice<T> right) => SequenceEqual(left, right);

	/// <summary>
	/// Check for inequality.
	/// </summary>
	[Pure]
	public static bool operator !=(ValueSlice<T> left, ValueSlice<T> right) => !SequenceEqual(left, right);

	private static readonly Collection EmptyCollection = new([]);

	/// <summary>
	/// Create a new heap-allocated view over the slice.
	/// </summary>
	/// <remarks>
	/// This method is an <c>O(1)</c> operation and allocates a new fixed-size
	/// collection instance. The items are not copied.
	/// </remarks>
	[Pure]
	public Collection AsCollection() => this.IsEmpty ? EmptyCollection : new Collection(this);

	/// <summary>
	/// A heap-allocated read-only view over a slice.
	/// </summary>
	/// <remarks>
	/// This type only exists for the interfaces it implements.
	/// </remarks>
#pragma warning disable CA1034 // Nested types should not be visible
#pragma warning disable CA1815 // Override equals and operator equals on value types
	public sealed class Collection : IEnumerable<T>, IReadOnlyList<T>, IList<T>
	{
		private readonly ValueSlice<T> slice;

		internal Collection(ValueSlice<T> slice)
		{
			this.slice = slice;
		}

		/// <summary>
		/// The slice that this collection represents.
		/// </summary>
		public ValueSlice<T> ValueSlice => this.slice;

		/// <inheritdoc/>
		T IReadOnlyList<T>.this[int index] => this.slice[index];

		/// <inheritdoc/>
		T IList<T>.this[int index]
		{
			get => this.slice[index];
			set => ThrowHelpers.ThrowNotSupportedException_CollectionImmutable();
		}

		/// <inheritdoc/>
		int IReadOnlyCollection<T>.Count => this.slice.Length;

		/// <inheritdoc/>
		int ICollection<T>.Count => this.slice.Length;

		/// <inheritdoc/>
		bool ICollection<T>.IsReadOnly => true;

		/// <inheritdoc/>
		int IList<T>.IndexOf(T item) => this.slice.IndexOf(item);

		/// <inheritdoc/>
		bool ICollection<T>.Contains(T item) => this.slice.Contains(item);

		/// <inheritdoc/>
		void ICollection<T>.CopyTo(T[] array, int arrayIndex)
		{
			if (array is null)
			{
				ThrowHelpers.ThrowArgumentNullException(ThrowHelpers.Argument.array);
			}

			this.slice.CopyTo(array.AsSpan(arrayIndex));
		}

		/// <inheritdoc/>
		IEnumerator<T> IEnumerable<T>.GetEnumerator()
		{
			foreach (var value in this.slice)
			{
				yield return value;
			}
		}

		/// <inheritdoc/>
		IEnumerator IEnumerable.GetEnumerator() => (this as IEnumerable<T>).GetEnumerator();

		/// <inheritdoc/>
		void ICollection<T>.Add(T item) => ThrowHelpers.ThrowNotSupportedException_CollectionImmutable();

		/// <inheritdoc/>
		void ICollection<T>.Clear() => ThrowHelpers.ThrowNotSupportedException_CollectionImmutable();

		/// <inheritdoc/>
		bool ICollection<T>.Remove(T item)
		{
			ThrowHelpers.ThrowNotSupportedException_CollectionImmutable();
			return false;
		}

		/// <inheritdoc/>
		void IList<T>.Insert(int index, T item) => ThrowHelpers.ThrowNotSupportedException_CollectionImmutable();

		/// <inheritdoc/>
		void IList<T>.RemoveAt(int index) => ThrowHelpers.ThrowNotSupportedException_CollectionImmutable();
	}
#pragma warning restore CA1034 // Nested types should not be visible
#pragma warning restore CA1815 // Override equals and operator equals on value types

	private static bool SequenceEqual(ValueSlice<T> left, ValueSlice<T> right)
	{
		var leftSpan = left.Span;
		var rightSpan = right.Span;

#if NET6_0_OR_GREATER
		return System.MemoryExtensions.SequenceEqual(leftSpan, rightSpan);
#else
		if (leftSpan == rightSpan)
		{
			return true;
		}

		if (leftSpan.Length != rightSpan.Length)
		{
			return false;
		}

		var length = leftSpan.Length;

		for (int i = 0; i < length; i++)
		{
			if (!EqualityComparer<T>.Default.Equals(leftSpan[i], rightSpan[i]))
			{
				return false;
			}
		}

		return true;
#endif
	}

	/// <summary>
	/// Get a string representation of the collection for debugging purposes.
	/// The format is not stable and may change without prior notice.
	/// </summary>
	[Pure]
	public override string ToString()
	{
		if (this.Length == 0)
		{
			return "[]";
		}

		var builder = new StringBuilder();
		builder.Append('[');

		var index = 0;
		foreach (var item in this)
		{
			if (index > 0)
			{
				builder.Append(", ");
			}

			var itemString = item?.ToString() ?? "null";
			builder.Append(itemString);

			index++;
		}

		builder.Append(']');
		return builder.ToString();
	}
}
