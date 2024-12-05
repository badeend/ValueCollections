using System.Collections;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Badeend.ValueCollections.Internals;

namespace Badeend.ValueCollections;

/// <summary>
/// Initialization methods for <see cref="ValueSet{T}"/>.
/// </summary>
public static class ValueSet
{
	/// <summary>
	/// Copy the <paramref name="items"/> into a new <see cref="ValueSet{T}"/>.
	/// </summary>
	[Pure]
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static ValueSet<T> Create<T>(scoped ReadOnlySpan<T> items) => ValueSet<T>.CreateImmutableUnsafe(new(items));

	/// <summary>
	/// Create a new empty <see cref="ValueSet{T}.Builder"/>. This builder can
	/// then be used to efficiently construct an immutable <see cref="ValueSet{T}"/>.
	/// </summary>
	[Pure]
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static ValueSet<T>.Builder CreateBuilder<T>() => ValueSet<T>.Builder.CreateUnsafe(new());

	/// <summary>
	/// Create a new empty <see cref="ValueSet{T}.Builder"/> with the specified
	/// initial <paramref name="minimumCapacity"/>. This builder can then be
	/// used to efficiently construct an immutable <see cref="ValueSet{T}"/>.
	/// </summary>
	/// <exception cref="ArgumentOutOfRangeException">
	/// <paramref name="minimumCapacity"/> is less than 0.
	/// </exception>
	[Pure]
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static ValueSet<T>.Builder CreateBuilderWithCapacity<T>(int minimumCapacity) => ValueSet<T>.Builder.CreateUnsafe(new(minimumCapacity));

	/// <summary>
	/// Create a new <see cref="ValueSet{T}.Builder"/> with the provided
	/// <paramref name="items"/> as its initial content.
	/// </summary>
	[Pure]
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static ValueSet<T>.Builder CreateBuilder<T>(scoped ReadOnlySpan<T> items) => ValueSet<T>.Builder.CreateUnsafe(new(items));
}

/// <summary>
/// An immutable, thread-safe set with value semantics.
/// </summary>
/// <remarks>
/// A set is a collection of unique elements (i.e. no duplicates) in no
/// particular order.
///
/// Constructing new instances can be done using
/// <see cref="ValueSet.CreateBuilder{T}()"/> or <see cref="ValueSet{T}.ToBuilder()"/>.
/// For creating ValueSets, <see cref="ValueSet{T}.Builder"/> is generally more
/// efficient than <see cref="HashSet{T}"/>.
///
/// ValueSets have "structural equality". This means that two sets
/// are considered equal only when their contents are equal. As long as a value
/// is present in a ValueSet, its hash code may not change.
///
/// The order in which the elements are enumerated is undefined.
/// </remarks>
/// <typeparam name="T">The type of items in the set.</typeparam>
[CollectionBuilder(typeof(ValueSet), nameof(ValueSet.Create))]
public sealed partial class ValueSet<T> : IReadOnlyCollection<T>, ISet<T>, IEquatable<ValueSet<T>>, IReadOnlySet<T>
{
	/// <summary>
	/// Get an empty set.
	///
	/// This does not allocate any memory.
	/// </summary>
	[Pure]
	public static ValueSet<T> Empty { get; } = new(new(), BuilderState.InitialImmutable);

#pragma warning disable SA1307 // Accessible fields should begin with upper-case letter
#pragma warning disable SA1401 // Fields should be private

	internal RawSet<T> inner;

	// See the BuilderState utility class for more info.
	private int state;

#pragma warning restore SA1401 // Fields should be private
#pragma warning restore SA1307 // Accessible fields should begin with upper-case letter

	/// <summary>
	/// Number of items in the set.
	/// </summary>
	[Pure]
	public int Count
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => this.inner.Count;
	}

	/// <summary>
	/// Shortcut for <c>.Count == 0</c>.
	/// </summary>
	[Pure]
	public bool IsEmpty
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => this.inner.Count == 0;
	}

	/// <inheritdoc/>
	bool ICollection<T>.IsReadOnly => true;

	private ValueSet(RawSet<T> inner, int state)
	{
		this.inner = inner;
		this.state = state;
	}

	internal static ValueSet<T> CreateImmutableUnsafe(RawSet<T> inner)
	{
		if (inner.Count == 0)
		{
			return Empty;
		}

		return new(inner, BuilderState.InitialImmutable);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static ValueSet<T> CreateMutableUnsafe(RawSet<T> inner) => new(inner, BuilderState.InitialMutable);

	// The RawSet is expected to be immutable.
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static ValueSet<T> CreateCowUnsafe(RawSet<T> inner) => new(inner, BuilderState.Cow);

	/// <summary>
	/// Create a new <see cref="Builder"/> with this set as its
	/// initial content. This builder can then be used to efficiently construct
	/// a new immutable <see cref="ValueSet{T}"/>.
	/// </summary>
	/// <remarks>
	/// The capacity of the returned builder may be larger than the size of this
	/// set. How much larger exactly is undefined.
	/// </remarks>
	[Pure]
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public Builder ToBuilder()
	{
		if (Utilities.IsReuseWorthwhile(this.inner.Capacity, this.inner.Count))
		{
			return Builder.CreateCowUnsafe(this.inner);
		}
		else
		{
			return Builder.CreateUnsafe(new(ref this.inner));
		}
	}

	/// <summary>
	/// Copy the contents of the set into an existing <see cref="Span{T}"/>.
	/// </summary>
	/// <exception cref="ArgumentException">
	///   <paramref name="destination"/> is shorter than the source slice.
	/// </exception>
	/// <remarks>
	/// The order in which the elements are copied is undefined.
	/// </remarks>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void CopyTo(Span<T> destination) => this.inner.CopyTo(destination);

	/// <inheritdoc/>
	void ICollection<T>.CopyTo(T[] array, int arrayIndex)
	{
		if (array is null)
		{
			throw new ArgumentNullException(nameof(array));
		}

		this.CopyTo(array.AsSpan(arrayIndex));
	}

	/// <summary>
	/// Returns <see langword="true"/> when the set contains the specified
	/// <paramref name="item"/>.
	/// </summary>
	[Pure]
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool Contains(T item) => this.inner.Contains(item);

	/// <summary>
	/// Check whether <c>this</c> set is a subset of the provided collection.
	/// </summary>
	/// <remarks>
	/// This is an <c>O(n)</c> operation, where <c>n</c> is the number of elements in <c>this</c>.
	///
	/// An overload that takes any <c>IEnumerable&lt;T&gt;</c> exists as an
	/// <see cref="ValueCollectionExtensions.IsSubsetOf">extension method</see>.
	/// Beware of the performance implications though.
	/// </remarks>
	public bool IsSubsetOf(ValueSet<T> other)
	{
		if (other is null)
		{
			ThrowHelpers.ThrowArgumentNullException(ThrowHelpers.Argument.other);
		}

		return this.inner.IsSubsetOf(ref other.inner);
	}

	// Accessible through extension method.
	internal bool IsSubsetOfEnumerable(IEnumerable<T> other)
	{
		if (other.AsValueSetUnsafe() is { } otherSet)
		{
			return this.inner.IsSubsetOf(ref otherSet.inner);
		}
		else
		{
			return this.inner.IsSubsetOf(other);
		}
	}

	/// <inheritdoc/>
	bool ISet<T>.IsSubsetOf(IEnumerable<T> other) => this.IsSubsetOfEnumerable(other);

	/// <inheritdoc/>
	bool IReadOnlySet<T>.IsSubsetOf(IEnumerable<T> other) => this.IsSubsetOfEnumerable(other);

	/// <summary>
	/// Check whether <c>this</c> set is a proper subset of the provided collection.
	/// </summary>
	/// <remarks>
	/// This is an <c>O(n)</c> operation, where <c>n</c> is the number of elements in <c>this</c>.
	///
	/// An overload that takes any <c>IEnumerable&lt;T&gt;</c> exists as an
	/// <see cref="ValueCollectionExtensions.IsProperSubsetOf">extension method</see>.
	/// Beware of the performance implications though.
	/// </remarks>
	public bool IsProperSubsetOf(ValueSet<T> other)
	{
		if (other is null)
		{
			ThrowHelpers.ThrowArgumentNullException(ThrowHelpers.Argument.other);
		}

		return this.inner.IsProperSubsetOf(ref other.inner);
	}

	// Accessible through extension method.
	internal bool IsProperSubsetOfEnumerable(IEnumerable<T> other)
	{
		if (other.AsValueSetUnsafe() is { } otherSet)
		{
			return this.inner.IsProperSubsetOf(ref otherSet.inner);
		}
		else
		{
			return this.inner.IsProperSubsetOf(other);
		}
	}

	/// <inheritdoc/>
	bool ISet<T>.IsProperSubsetOf(IEnumerable<T> other) => this.IsProperSubsetOfEnumerable(other);

	/// <inheritdoc/>
	bool IReadOnlySet<T>.IsProperSubsetOf(IEnumerable<T> other) => this.IsProperSubsetOfEnumerable(other);

	/// <summary>
	/// Check whether <c>this</c> set is a superset of the provided collection.
	/// </summary>
	/// <remarks>
	/// This is an <c>O(n)</c> operation, where <c>n</c> is the number of elements in <paramref name="other"/>.
	///
	/// An overload that takes any <c>IEnumerable&lt;T&gt;</c> exists as an
	/// <see cref="ValueCollectionExtensions.IsSupersetOf">extension method</see>.
	/// </remarks>
	public bool IsSupersetOf(ValueSet<T> other)
	{
		if (other is null)
		{
			ThrowHelpers.ThrowArgumentNullException(ThrowHelpers.Argument.other);
		}

		return this.inner.IsSupersetOf(ref other.inner);
	}

	/// <summary>
	/// Check whether <c>this</c> set is a superset of the provided sequence.
	/// </summary>
	/// <remarks>
	/// This is an <c>O(n)</c> operation, where <c>n</c> is the number of elements in <paramref name="other"/>.
	/// </remarks>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool IsSupersetOf(scoped ReadOnlySpan<T> other) => this.inner.IsSupersetOf(other);

	// Accessible through extension method.
	internal bool IsSupersetOfEnumerable(IEnumerable<T> other)
	{
		if (other.AsValueSetUnsafe() is { } otherSet)
		{
			return this.inner.IsSupersetOf(ref otherSet.inner);
		}
		else
		{
			return this.inner.IsSupersetOf(other);
		}
	}

	/// <inheritdoc/>
	bool ISet<T>.IsSupersetOf(IEnumerable<T> other) => this.IsSupersetOfEnumerable(other);

	/// <inheritdoc/>
	bool IReadOnlySet<T>.IsSupersetOf(IEnumerable<T> other) => this.IsSupersetOfEnumerable(other);

	/// <summary>
	/// Check whether <c>this</c> set is a proper superset of the provided collection.
	/// </summary>
	/// <remarks>
	/// This is an <c>O(n)</c> operation, where <c>n</c> is the number of elements in <c>this</c>.
	///
	/// An overload that takes any <c>IEnumerable&lt;T&gt;</c> exists as an
	/// <see cref="ValueCollectionExtensions.IsProperSupersetOf">extension method</see>.
	/// Beware of the performance implications though.
	/// </remarks>
	public bool IsProperSupersetOf(ValueSet<T> other)
	{
		if (other is null)
		{
			ThrowHelpers.ThrowArgumentNullException(ThrowHelpers.Argument.other);
		}

		return this.inner.IsProperSupersetOf(ref other.inner);
	}

	internal bool IsProperSupersetOfEnumerable(IEnumerable<T> other)
	{
		if (other.AsValueSetUnsafe() is { } otherSet)
		{
			return this.inner.IsProperSupersetOf(ref otherSet.inner);
		}
		else
		{
			return this.inner.IsProperSupersetOf(other);
		}
	}

	/// <inheritdoc/>
	bool ISet<T>.IsProperSupersetOf(IEnumerable<T> other) => this.IsProperSupersetOfEnumerable(other);

	/// <inheritdoc/>
	bool IReadOnlySet<T>.IsProperSupersetOf(IEnumerable<T> other) => this.IsProperSupersetOfEnumerable(other);

	/// <summary>
	/// Check whether <c>this</c> set and the provided collection share any common elements.
	/// </summary>
	/// <remarks>
	/// This is an <c>O(n)</c> operation, where <c>n</c> is the number of elements in <paramref name="other"/>.
	///
	/// An overload that takes any <c>IEnumerable&lt;T&gt;</c> exists as an
	/// <see cref="ValueCollectionExtensions.Overlaps">extension method</see>.
	/// </remarks>
	public bool Overlaps(ValueSet<T> other)
	{
		if (other is null)
		{
			ThrowHelpers.ThrowArgumentNullException(ThrowHelpers.Argument.other);
		}

		return this.inner.Overlaps(ref other.inner);
	}

	/// <summary>
	/// Check whether <c>this</c> set and the provided sequence share any common elements.
	/// </summary>
	/// <remarks>
	/// This is an <c>O(n)</c> operation, where <c>n</c> is the number of elements in <paramref name="other"/>.
	/// </remarks>
	public bool Overlaps(scoped ReadOnlySpan<T> other)
	{
		return this.inner.Overlaps(other);
	}

	// Accessible through extension method.
	internal bool OverlapsEnumerable(IEnumerable<T> other)
	{
		if (other.AsValueSetUnsafe() is { } otherSet)
		{
			return this.inner.Overlaps(ref otherSet.inner);
		}
		else
		{
			return this.inner.Overlaps(other);
		}
	}

	/// <inheritdoc/>
	bool ISet<T>.Overlaps(IEnumerable<T> other) => this.OverlapsEnumerable(other);

	/// <inheritdoc/>
	bool IReadOnlySet<T>.Overlaps(IEnumerable<T> other) => this.OverlapsEnumerable(other);

	/// <summary>
	/// Check whether <c>this</c> set and the provided collection contain
	/// the same elements, ignoring the order of the elements.
	/// </summary>
	/// <remarks>
	/// This is an <c>O(n)</c> operation, where <c>n</c> is the number of elements in <paramref name="other"/>.
	///
	/// An overload that takes any <c>IEnumerable&lt;T&gt;</c> exists as an
	/// <see cref="ValueCollectionExtensions.SetEquals">extension method</see>.
	/// Beware of the performance implications though.
	/// </remarks>
	public bool SetEquals(ValueSet<T> other)
	{
		if (other is null)
		{
			ThrowHelpers.ThrowArgumentNullException(ThrowHelpers.Argument.other);
		}

		return this.inner.SetEquals(ref other.inner);
	}

	// Accessible through extension method.
	internal bool SetEqualsEnumerable(IEnumerable<T> other)
	{
		if (other.AsValueSetUnsafe() is { } otherSet)
		{
			return this.inner.SetEquals(ref otherSet.inner);
		}
		else
		{
			return this.inner.SetEquals(other);
		}
	}

	/// <inheritdoc/>
	bool ISet<T>.SetEquals(IEnumerable<T> other) => this.SetEqualsEnumerable(other);

	/// <inheritdoc/>
	bool IReadOnlySet<T>.SetEquals(IEnumerable<T> other) => this.SetEqualsEnumerable(other);

	/// <inheritdoc/>
	[Pure]
	public sealed override int GetHashCode()
	{
		if (BuilderState.ReadHashCode(ref this.state, out var hashCode))
		{
			return hashCode;
		}

		return BuilderState.AdjustAndStoreHashCode(ref this.state, RawSet.GetSequenceHashCode(ref this.inner));
	}

	/// <summary>
	/// Returns <see langword="true"/> when the two sets have identical length
	/// and content.
	/// </summary>
	[Pure]
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool Equals(ValueSet<T>? other) => EqualsUtil(this, other);

#pragma warning disable CS0809 // Obsolete member overrides non-obsolete member
	/// <inheritdoc/>
	[Pure]
	[Obsolete("Use == instead.")]
	[EditorBrowsable(EditorBrowsableState.Never)]
	public sealed override bool Equals(object? obj) => obj is ValueSet<T> other && EqualsUtil(this, other);
#pragma warning restore CS0809 // Obsolete member overrides non-obsolete member

	/// <summary>
	/// Check for equality.
	/// </summary>
	[Pure]
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool operator ==(ValueSet<T>? left, ValueSet<T>? right) => EqualsUtil(left, right);

	/// <summary>
	/// Check for inequality.
	/// </summary>
	[Pure]
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool operator !=(ValueSet<T>? left, ValueSet<T>? right) => !EqualsUtil(left, right);

	private static bool EqualsUtil(ValueSet<T>? left, ValueSet<T>? right)
	{
		if (left is null)
		{
			return right is null;
		}

		if (right is null)
		{
			return false;
		}

		return left.inner.SetEquals(ref right.inner);
	}

	/// <summary>
	/// Returns an enumerator for this <see cref="ValueSet{T}"/>.
	///
	/// Typically, you don't need to manually call this method, but instead use
	/// the built-in <c>foreach</c> syntax.
	/// </summary>
	[Pure]
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public Enumerator GetEnumerator() => new Enumerator(this);

	/// <inheritdoc/>
	IEnumerator<T> IEnumerable<T>.GetEnumerator()
	{
		if (this.Count == 0)
		{
			return EnumeratorLike.Empty<T>();
		}
		else
		{
			return EnumeratorLike.AsIEnumerator<T, Enumerator>(new Enumerator(this));
		}
	}

	/// <inheritdoc/>
	IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable<T>)this).GetEnumerator();

	/// <summary>
	/// Enumerator for <see cref="ValueSet{T}"/>.
	/// </summary>
#pragma warning disable CA1034 // Nested types should not be visible
#pragma warning disable CA1815 // Override equals and operator equals on value types
	[StructLayout(LayoutKind.Auto)]
	public struct Enumerator : IRefEnumeratorLike<T>
	{
		private RawSet<T>.Enumerator inner;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal Enumerator(ValueSet<T> set)
		{
			this.inner = set.inner.GetEnumerator();
		}

		/// <inheritdoc/>
		public readonly ref readonly T Current
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => ref this.inner.Current;
		}

		/// <inheritdoc/>
		readonly T IEnumeratorLike<T>.Current => this.Current;

		/// <inheritdoc/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool MoveNext() => this.inner.MoveNext();
	}
#pragma warning restore CA1815 // Override equals and operator equals on value types
#pragma warning restore CA1034 // Nested types should not be visible

	/// <summary>
	/// Get a string representation of the collection for debugging purposes.
	/// The format is not stable and may change without prior notice.
	/// </summary>
	[Pure]
	public override string ToString() => this.inner.ToString();

	/// <inheritdoc/>
	void ICollection<T>.Add(T item) => throw CreateImmutableException();

	/// <inheritdoc/>
	void ICollection<T>.Clear() => throw CreateImmutableException();

	/// <inheritdoc/>
	bool ICollection<T>.Remove(T item) => throw CreateImmutableException();

	/// <inheritdoc/>
	bool ISet<T>.Add(T item) => throw CreateImmutableException();

	/// <inheritdoc/>
	void ISet<T>.UnionWith(IEnumerable<T> other) => throw CreateImmutableException();

	/// <inheritdoc/>
	void ISet<T>.IntersectWith(IEnumerable<T> other) => throw CreateImmutableException();

	/// <inheritdoc/>
	void ISet<T>.ExceptWith(IEnumerable<T> other) => throw CreateImmutableException();

	/// <inheritdoc/>
	void ISet<T>.SymmetricExceptWith(IEnumerable<T> other) => throw CreateImmutableException();

	private static NotSupportedException CreateImmutableException() => new NotSupportedException("Collection is immutable");
}
