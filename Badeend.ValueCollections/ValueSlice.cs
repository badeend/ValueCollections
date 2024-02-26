using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Badeend.ValueCollections;

/// <summary>
/// A set of initialization methods for <see cref="ValueSlice{T}"/>.
/// </summary>
public static class ValueSlice
{
	/// <summary>
	/// Create a new empty slice.
	///
	/// This does not allocate any memory.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static ValueSlice<T> Empty<T>() => default;

	/// <summary>
	/// Copy the <paramref name="items"/> into a new <see cref="ValueSlice{T}"/>.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static ValueSlice<T> Create<T>(this ReadOnlySpan<T> items) => new(items.ToArray());
}

/// <summary>
/// An immutable span with value semantics.
///
/// This type is very similar to <see cref="ReadOnlySpan{T}"/> and
/// <see cref="ReadOnlyMemory{T}"/> in that this too is just a view
/// into an existing allocation at a specified offset+length. Taking a subslice
/// with <see cref="ValueSlice{T}.Slice(int)"/> and
/// <see cref="ValueSlice{T}.Slice(int, int)"/> is very cheap as it reuses the
/// same allocation and only adjusts the internal offset+length fields.
///
/// Unlike ReadOnlySpan/Memory, the data is not just read-only but it is also
/// guaranteed to be immutable.
///
/// Additionally, ValueSlice has "value semantics". This means that two slices
/// are considered equal only when their contents are equal. Due to technical
/// reasons, the type parameter <typeparamref name="T"/> is currently not restricted
/// to implement <see cref="IEquatable{T}"/>, but it is highly encouraged to
/// only use ValueSlices on types that implement it nonetheless.
///
/// To prevent accidental boxing, ValueSlice does not implement commonly used
/// interfaces such as <see cref="IEnumerable{T}"/> and
/// <see cref="IReadOnlyList{T}"/>. You can still use these interfaces by
/// manually calling <see cref="ValueSliceExtensions.AsEnumerable"/> or
/// <see cref="ValueSliceExtensions.AsReadOnlyList"/> instead.
///
/// The <c>default</c> value of every ValueSlice is an empty slice.
/// </summary>
/// <typeparam name="T">The type of items in the slice.</typeparam>
[StructLayout(LayoutKind.Auto)]
[CollectionBuilder(typeof(ValueSlice), nameof(ValueSlice.Create))]
public readonly struct ValueSlice<T> : IEquatable<ValueSlice<T>>
{
	/// <summary>
	/// Get a new empty slice.
	///
	/// This does not allocate any memory.
	/// </summary>
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
	public int Length
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => this.length;
	}

	/// <summary>
	/// Shortcut for <c>.Length == 0</c>.
	/// </summary>
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
	public ReadOnlySpan<T> Span
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => this.items.AsSpan(this.offset, this.length);
	}

	/// <summary>
	/// Access the slice's contents using a <see cref="ReadOnlyMemory{T}"/>.
	/// </summary>
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
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get
		{
			// This check indirectly also ensures that _items is not null.
			if (index < 0 || index >= this.length)
			{
				throw new ArgumentOutOfRangeException(nameof(index));
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
	public ValueSlice<T> Slice(int offset)
	{
		if (offset < 0 || offset > this.length)
		{
			throw new ArgumentOutOfRangeException(nameof(offset));
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
	public ValueSlice<T> Slice(int offset, int length)
	{
		if (offset < 0 || offset > this.length)
		{
			throw new ArgumentOutOfRangeException(nameof(offset));
		}

		if (length < 0 || length > (this.length - offset))
		{
			throw new ArgumentOutOfRangeException(nameof(length));
		}

		return new ValueSlice<T>(this.items, this.offset + offset, length);
	}

	/// <summary>
	/// Copy the contents of the slice into a new array.
	///
	/// Similar to <see cref="ReadOnlySpan{T}.ToArray"/>.
	/// </summary>
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
	/// Returns an enumerator for this <see cref="ValueSlice{T}"/>.
	///
	/// Typically, you don't need to manually call this method, but instead use
	/// the built-in <c>foreach</c> syntax.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public Enumerator GetEnumerator() => new Enumerator(this);

	/// <summary>
	/// Enumerator for <see cref="ValueSlice{T}"/>.
	/// </summary>
#pragma warning disable CA1034 // Nested types should not be visible
#pragma warning disable CA1815 // Override equals and operator equals on value types
	public struct Enumerator
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

		/// <summary>
		/// Gets the currently enumerated value.
		/// </summary>
		public readonly ref readonly T Current
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => ref this.items![this.current];
		}

		/// <summary>
		/// Advances to the next value to be enumerated.
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool MoveNext() => ++this.current < this.end;
	}
#pragma warning restore CA1815 // Override equals and operator equals on value types
#pragma warning restore CA1034 // Nested types should not be visible

	/// <inheritdoc/>
	public override int GetHashCode()
	{
		int hashCode = typeof(T).GetHashCode();
		hashCode = (hashCode * -1521134295) + this.length;

		if (this.length > 0)
		{
			hashCode = (hashCode * -1521134295) + (this[0]?.GetHashCode() ?? 0);
		}

		if (this.length > 1)
		{
			hashCode = (hashCode * -1521134295) + (this[this.length - 1]?.GetHashCode() ?? 0);
		}

		return hashCode;
	}

	/// <summary>
	/// Returns <see langword="true"/> when the two slices have identical length
	/// and content.
	/// </summary>
	public bool Equals(ValueSlice<T> other) => this.SequenceEqual(other);

	private static bool SequenceEqualNullable(ValueSlice<T>? left, ValueSlice<T>? right)
	{
		if (left is null)
		{
			return right is null;
		}

		if (right is null)
		{
			return false;
		}

		return left.Value.SequenceEqual(right.Value);
	}

#pragma warning disable CS0809 // Obsolete member overrides non-obsolete member
	/// <inheritdoc/>
	[Obsolete("Avoid boxing. Use == instead.")]
	[EditorBrowsable(EditorBrowsableState.Never)]
	public override bool Equals(object? obj) => obj is ValueSlice<T> slice && this.SequenceEqual(slice);
#pragma warning restore CS0809 // Obsolete member overrides non-obsolete member

	/// <summary>
	/// Check for equality.
	/// </summary>
	public static bool operator ==(ValueSlice<T> left, ValueSlice<T> right) => left.SequenceEqual(right);

	/// <summary>
	/// Check for equality.
	/// </summary>
	public static bool operator ==(ValueSlice<T>? left, ValueSlice<T>? right) => SequenceEqualNullable(left, right);

	/// <summary>
	/// Check for inequality.
	/// </summary>
	public static bool operator !=(ValueSlice<T> left, ValueSlice<T> right) => !left.SequenceEqual(right);

	/// <summary>
	/// Check for inequality.
	/// </summary>
	public static bool operator !=(ValueSlice<T>? left, ValueSlice<T>? right) => !SequenceEqualNullable(left, right);
}
