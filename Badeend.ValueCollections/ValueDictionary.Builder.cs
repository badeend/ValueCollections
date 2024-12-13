using System.Collections;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Badeend.ValueCollections.Internals;

namespace Badeend.ValueCollections;

/// <content>
/// Builder code.
/// </content>
public partial class ValueDictionary<TKey, TValue>
{
	/// <summary>
	/// A mutable dictionary that can be used to efficiently construct new immutable
	/// dictionaries.
	/// </summary>
	/// <remarks>
	/// Most mutating methods on this class return `this`, allowing the caller to
	/// chain multiple mutations in a row.
	///
	/// When you're done building, call <see cref="Build()"/> to extract the
	/// resulting dictionary.
	///
	/// For constructing <see cref="ValueDictionary{TKey, TValue}"/>s it is
	/// recommended to use this type over e.g. <see cref="Dictionary{TKey, TValue}"/>.
	/// This type can avoiding unnecessary copying by taking advantage of the
	/// immutability of its results. Whereas calling <c>.ToValueDictionary()</c> on
	/// a regular <see cref="Dictionary{TKey, TValue}"/> <em>always</em> performs a
	/// full copy.
	///
	/// The order in which the entries are enumerated is undefined.
	///
	/// To prevent accidental boxing, this type does not implement commonly used
	/// interfaces such as <see cref="IEnumerable{T}"/> and
	/// <see cref="IDictionary{TKey, TValue}"/>. You can still use these interfaces by
	/// manually calling <see cref="AsCollection"/> instead.
	///
	/// Unlike the resulting ValueDictionary, its Builder is <em>not</em> thread-safe.
	///
	/// The <c>default</c> value is an empty read-only builder.
	/// </remarks>
	public readonly partial struct Builder : IEquatable<Builder>
	{
		/// <summary>
		/// Only access this field through .Read() or .Mutate().
		/// </summary>
		private readonly ValueDictionary<TKey, TValue>? dictionary;

		/// <summary>
		/// Returns <see langword="true"/> when this instance has been built and is
		/// now read-only.
		/// </summary>
		[Pure]
		public bool IsReadOnly => this.dictionary is null || BuilderState.IsImmutable(this.dictionary.state);

		/// <summary>
		/// Finalize the builder and export its contents as a <see cref="ValueDictionary{TKey, TValue}"/>.
		/// This makes the builder read-only. Any future attempt to mutate the
		/// builder will throw.
		///
		/// This is an <c>O(1)</c> operation and performs no heap allocations.
		/// </summary>
		/// <remarks>
		/// If you need an intermediate snapshot of the contents while keeping the
		/// builder open for mutation, use <see cref="ToValueDictionary"/> instead.
		/// </remarks>
		/// <exception cref="InvalidOperationException">
		/// This instance has already been built.
		/// </exception>
		public ValueDictionary<TKey, TValue> Build()
		{
			var dictionary = this.dictionary;
			if (dictionary is null || BuilderState.BuildRequiresAttention(dictionary.state))
			{
				MutateUncommon(dictionary);
			}

			dictionary.state = BuilderState.InitialImmutable;

			return dictionary.IsEmpty ? Empty : dictionary;
		}

		/// <summary>
		/// Copy the current contents of the builder into a new <see cref="ValueDictionary{TKey, TValue}"/>.
		/// </summary>
		/// <remarks>
		/// If you don't need the builder anymore after this method, consider using
		/// <see cref="Build"/> instead.
		/// </remarks>
		[Pure]
		public ValueDictionary<TKey, TValue> ToValueDictionary()
		{
			var dictionary = this.dictionary;
			if (dictionary is null)
			{
				return Empty;
			}

			if (BuilderState.IsImmutable(dictionary.state))
			{
				return dictionary.IsEmpty ? Empty : dictionary;
			}
			else if (dictionary.state == BuilderState.Cow)
			{
				return ValueDictionary<TKey, TValue>.CreateImmutableUnsafe(dictionary.inner);
			}
			else
			{
				return ValueDictionary<TKey, TValue>.CreateImmutableUnsafe(EnumerableToDictionary(dictionary.inner));
			}
		}

		/// <summary>
		/// Copy the current contents of the builder into a new <see cref="ValueDictionary{TKey, TValue}.Builder"/>.
		/// </summary>
		[Pure]
		public Builder ToValueDictionaryBuilder()
		{
			return ValueDictionary<TKey, TValue>.Builder.CreateUnsafe(EnumerableToDictionary(this.ReadOnce()));
		}

		[StructLayout(LayoutKind.Auto)]
		internal readonly struct Snapshot
		{
			private readonly ValueDictionary<TKey, TValue> dictionary;
			private readonly int expectedState;

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			internal Snapshot(ValueDictionary<TKey, TValue> dictionary, int expectedState)
			{
				this.dictionary = dictionary;
				this.expectedState = expectedState;
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			internal ref readonly Dictionary<TKey, TValue> AssertAlive()
			{
				if (this.expectedState != this.dictionary.state)
				{
					this.AssertAliveUncommon();
				}

				return ref this.dictionary.inner;
			}

			private void AssertAliveUncommon()
			{
				// The only valid reason for ending up here is when the snapshot
				// was obtained in an already-built state and the hash code was
				// materialized afterwards.
				if (BuilderState.IsImmutable(this.expectedState))
				{
					Debug.Assert(BuilderState.IsImmutable(this.dictionary.state));

					return;
				}

				if (this.dictionary.state == BuilderState.ExclusiveMode)
				{
					ThrowHelpers.ThrowInvalidOperationException_Locked();
				}
				else
				{
					ThrowHelpers.ThrowInvalidOperationException_CollectionModifiedDuringEnumeration();
				}
			}
		}

		[StructLayout(LayoutKind.Auto)]
		private readonly ref struct MutationGuard
		{
			private readonly ValueDictionary<TKey, TValue> dictionary;
			private readonly int restoreState;

			internal readonly ref Dictionary<TKey, TValue> Inner
			{
				[MethodImpl(MethodImplOptions.AggressiveInlining)]
				get
				{
					Debug.Assert(this.dictionary.state == BuilderState.ExclusiveMode);

					return ref this.dictionary.inner;
				}
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			internal MutationGuard(ValueDictionary<TKey, TValue> dictionary, int restoreState)
			{
				this.dictionary = dictionary;
				this.restoreState = restoreState;
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public void Dispose()
			{
				Debug.Assert(this.dictionary.state == BuilderState.ExclusiveMode);

				this.dictionary.state = this.restoreState;
			}
		}

		private Snapshot Read()
		{
			var dictionary = this.dictionary ?? Empty;

			if (dictionary.state == BuilderState.ExclusiveMode)
			{
				ThrowHelpers.ThrowInvalidOperationException_Locked();
			}

			return new Snapshot(dictionary, dictionary.state);
		}

		private ref readonly Dictionary<TKey, TValue> ReadOnce()
		{
			var dictionary = this.dictionary ?? Empty;

			if (dictionary.state == BuilderState.ExclusiveMode)
			{
				ThrowHelpers.ThrowInvalidOperationException_Locked();
			}

			return ref dictionary.inner;
		}

		private MutationGuard Mutate()
		{
			var dictionary = this.dictionary;
			if (dictionary is null || BuilderState.MutateRequiresAttention(dictionary.state))
			{
				MutateUncommon(dictionary);
			}

			var stateToRestore = dictionary.state + 1;
			dictionary.state = BuilderState.ExclusiveMode;

			Debug.Assert(BuilderState.IsMutable(stateToRestore));

			return new MutationGuard(dictionary, stateToRestore);
		}

		// Only to be used if the mutation can be done at once (i.e. "atomically"),
		// and the outside world can not observe the builder in a temporary intermediate state.
		private ref Dictionary<TKey, TValue> MutateOnce()
		{
			var dictionary = this.dictionary;
			if (dictionary is null || BuilderState.MutateRequiresAttention(dictionary.state))
			{
				MutateUncommon(dictionary);
			}

			dictionary.state++;

			Debug.Assert(BuilderState.IsMutable(dictionary.state));

			return ref dictionary.inner;
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private static void MutateUncommon([NotNull] ValueDictionary<TKey, TValue>? dictionary)
		{
			if (dictionary is null)
			{
				ThrowHelpers.ThrowInvalidOperationException_UninitializedBuilder();
			}
			else if (dictionary.state == BuilderState.Cow)
			{
				// Make copy with at least the same amount of capacity.
				// var copy = new Dictionary<TKey, TValue>(dictionary.inner.Capacity); TODO
				// copy.UnionWith(ref dictionary.inner);
				var copy = EnumerableToDictionary(dictionary.inner);

				dictionary.inner = copy;
				dictionary.state = BuilderState.InitialMutable;
			}
			else if (dictionary.state == BuilderState.LastMutableVersion)
			{
				dictionary.state = BuilderState.InitialMutable;
			}
			else if (dictionary.state == BuilderState.ExclusiveMode)
			{
				ThrowHelpers.ThrowInvalidOperationException_Locked();
			}
			else
			{
				Debug.Assert(BuilderState.IsImmutable(dictionary.state));

				ThrowHelpers.ThrowInvalidOperationException_AlreadyBuilt();
			}
		}

		/// <summary>
		/// Current size of the dictionary.
		/// </summary>
		[Pure]
		public int Count => this.ReadOnce().Count;

		/// <summary>
		/// Shortcut for <c>.Count == 0</c>.
		/// </summary>
		[Pure]
		public bool IsEmpty
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => this.Count == 0;
		}

		/// <summary>
		/// Get the value associated with the specified key.
		/// </summary>
		/// <exception cref="ArgumentNullException">
		/// <paramref name="key"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="KeyNotFoundException">
		/// The <paramref name="key"/> does not exist.
		/// </exception>
		public TValue this[TKey key]
		{
			[Pure]
			get => this.ReadOnce()[key];
			set => this.MutateOnce()[key] = value;
		}

		/// <summary>
		/// Create a new uninitialized builder.
		///
		/// An uninitialized builder behaves the same as an already built dictionary
		/// with 0 items and 0 capacity. Reading from it will succeed, but
		/// mutating it will throw.
		///
		/// This is the same as the <c>default</c> value.
		/// </summary>
		[Pure]
		[Obsolete("This creates an uninitialized builder. Use ValueDictionary.CreateBuilder<TKey, TValue>() instead.")]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public Builder()
		{
		}

		// This takes ownership of the ValueDictionary
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private Builder(ValueDictionary<TKey, TValue> dictionary)
		{
			Debug.Assert(BuilderState.IsMutable(dictionary.state));

			this.dictionary = dictionary;
		}

		// This takes ownership of the RawDictionary
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal static Builder CreateUnsafe(Dictionary<TKey, TValue> inner) => new(ValueDictionary<TKey, TValue>.CreateMutableUnsafe(inner));

		// The RawDictionary is expected to be immutable.
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal static Builder CreateCowUnsafe(Dictionary<TKey, TValue> inner) => new(ValueDictionary<TKey, TValue>.CreateCowUnsafe(inner));

#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
		/// <summary>
		/// The total number of elements the internal data structure can hold without resizing.
		/// </summary>
		/// <remarks>
		/// Available on .NET Standard 2.1 and .NET Core 2.1 and higher.
		/// </remarks>
		public int Capacity
		{
			[Pure]
			get
			{
				var dictionary = this.ReadOnce();

#if NET9_0_OR_GREATER
				return dictionary.Capacity;
#else
				return dictionary.EnsureCapacity(0);
#endif
			}
		}

		/// <summary>
		/// Ensures that the capacity of this dictionary is at least the specified capacity.
		/// If the current capacity is less than capacity, it is increased to at
		/// least the specified capacity.
		/// </summary>
		/// <remarks>
		/// Available on .NET Standard 2.1 and .NET Core 2.1 and higher.
		/// </remarks>
		/// <exception cref="ArgumentOutOfRangeException">
		/// <paramref name="minimumCapacity"/> is less than 0.
		/// </exception>
		public Builder EnsureCapacity(int minimumCapacity)
		{
			this.MutateOnce().EnsureCapacity(minimumCapacity);
			return this;
		}

		/// <summary>
		/// Reduce the capacity of the dictionary to roughly the specified value. If the
		/// current capacity is already smaller than the requested capacity, this
		/// method does nothing. The specified <paramref name="targetCapacity"/> is only
		/// a hint. After this method returns, the <see cref="Capacity"/> may be
		/// rounded up to a nearby, implementation-specific value.
		/// </summary>
		/// <exception cref="ArgumentOutOfRangeException">
		/// <paramref name="targetCapacity"/> is less than <see cref="Count"/>.
		/// </exception>
		/// <remarks>
		/// Available on .NET Standard 2.1 and .NET Core 2.1 and higher.
		/// </remarks>
		public Builder TrimExcess(int targetCapacity)
		{
			this.MutateOnce().TrimExcess(targetCapacity);
			return this;
		}
#endif

		/// <summary>
		/// Determines whether this dictionary contains an element with the specified value.
		/// </summary>
		/// <remarks>
		/// This performs a linear scan through the dictionary.
		/// </remarks>
		[Pure]
		public bool ContainsValue(TValue value) => this.ReadOnce().ContainsValue(value);

		/// <summary>
		/// Determines whether this dictionary contains an element with the specified key.
		/// </summary>
		[Pure]
		public bool ContainsKey(TKey key) => this.ReadOnce().ContainsKey(key);

		/// <summary>
		/// Attempt to get the value associated with the specified <paramref name="key"/>.
		/// Returns <see langword="false"/> when the key was not found.
		/// </summary>
#pragma warning disable CS8767 // Nullability of reference types in type of parameter doesn't match implicitly implemented member (possibly because of nullability attributes).
		public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value) => this.ReadOnce().TryGetValue(key, out value);
#pragma warning restore CS8767 // Nullability of reference types in type of parameter doesn't match implicitly implemented member (possibly because of nullability attributes).

		/// <summary>
		/// Attempt to get the value associated with the specified <paramref name="key"/>.
		/// Returns <see langword="default"/> when the key was not found.
		/// </summary>
		[Pure]
		public TValue? GetValueOrDefault(TKey key) => this.GetValueOrDefault(key, default!);

		/// <summary>
		/// Attempt to get the value associated with the specified <paramref name="key"/>.
		/// Returns <paramref name="defaultValue"/> when the key was not found.
		/// </summary>
		[Pure]
		public TValue GetValueOrDefault(TKey key, TValue defaultValue)
		{
			return this.TryGetValue(key, out var value) ? value : defaultValue;
		}

		/// <summary>
		/// Get the value by the provided <paramref name="key"/>. If the key does
		/// not already exist, the <paramref name="valueFactory"/> is invoked to
		/// generate a new value, which will then be inserted and returned.
		/// </summary>
		public TValue GetOrAdd(TKey key, Func<TKey, TValue> valueFactory)
		{
			using (var guard = this.Mutate())
			{
				if (guard.Inner.TryGetValue(key, out var existingValue))
				{
					return existingValue;
				}

				var newValue = valueFactory(key);
				guard.Inner.Add(key, newValue);
				return newValue;
			}
		}

		/// <summary>
		/// Attempt to add the <paramref name="key"/> and <paramref name="value"/>
		/// to the dictionary. Returns <see langword="false"/> when the key was
		/// already present.
		/// </summary>
		public bool TryAdd(TKey key, TValue value)
		{
			var dictionary = this.MutateOnce();

#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_0_OR_GREATER
			return dictionary.TryAdd(key, value);
#else
			if (!dictionary.ContainsKey(key))
			{
				dictionary.Add(key, value);
				return true;
			}

			return false;
#endif
		}

		/// <summary>
		/// Add the <paramref name="key"/> and <paramref name="value"/> to the
		/// dictionary.
		/// </summary>
		/// <remarks>
		/// This method throws an exception when the key already exists. If this is
		/// not desired, you can use <see cref="TryAdd"/> or <see cref="SetItem"/>
		/// instead.
		/// </remarks>
		/// <exception cref="ArgumentException">
		/// The <paramref name="key"/> already exists.
		/// </exception>
		public Builder Add(TKey key, TValue value)
		{
			this.MutateOnce().Add(key, value);
			return this;
		}

		/// <summary>
		/// Add multiple entries to the dictionary.
		/// </summary>
		/// <remarks>
		/// <see cref="ValueCollectionExtensions.AddRange{TKey, TValue}(ValueDictionary{TKey, TValue}.Builder, ReadOnlySpan{KeyValuePair{TKey, TValue}})">More overloads</see>
		/// are available as extension methods.
		/// </remarks>
		/// <exception cref="ArgumentException">
		/// <paramref name="items"/> contains a duplicate key or a key that already
		/// exists in the dictionary.
		/// </exception>
		public Builder AddRange(IEnumerable<KeyValuePair<TKey, TValue>> items)
		{
			if (items is null)
			{
				throw new ArgumentNullException(nameof(items));
			}

			using (var guard = this.Mutate())
			{
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
				if (items is ICollection<KeyValuePair<TKey, TValue>> collection)
				{
					guard.Inner.EnsureCapacity(guard.Inner.Count + collection.Count);
				}
#endif

				foreach (var item in items)
				{
					guard.Inner.Add(item.Key, item.Value);
				}
			}

			return this;
		}

		// Accessible through an extension method.
		internal Builder AddRangeSpan(scoped ReadOnlySpan<KeyValuePair<TKey, TValue>> items)
		{
			var dictionary = this.MutateOnce();
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
			dictionary.EnsureCapacity(dictionary.Count + items.Length);
#endif
			foreach (var item in items)
			{
				dictionary.Add(item.Key, item.Value);
			}

			return this;
		}

		/// <summary>
		/// Set the specified <paramref name="key"/> in the dictionary, possibly
		/// overwriting an existing value for the key.
		/// </summary>
		/// <remarks>
		/// Functionally equivalent to:
		/// <code>
		/// builder[key] = value;
		/// </code>
		/// </remarks>
		public Builder SetItem(TKey key, TValue value)
		{
			this.MutateOnce()[key] = value;
			return this;
		}

		/// <summary>
		/// Sets the specified key/value pairs in the dictionary, possibly
		/// overwriting existing values for the keys.
		/// </summary>
		/// <remarks>
		/// When the same key appears multiple times in the <paramref name="items"/>,
		/// the last value overwrites any earlier values.
		///
		/// <see cref="ValueCollectionExtensions.SetItems{TKey, TValue}(ValueDictionary{TKey, TValue}.Builder, ReadOnlySpan{KeyValuePair{TKey, TValue}})">More overloads</see>
		/// are available as extension methods.
		/// </remarks>
		public Builder SetItems(IEnumerable<KeyValuePair<TKey, TValue>> items)
		{
			if (items is null)
			{
				throw new ArgumentNullException(nameof(items));
			}

			using (var guard = this.Mutate())
			{
				foreach (var item in items)
				{
					guard.Inner[item.Key] = item.Value;
				}
			}

			return this;
		}

		// Accessible through an extension method.
		internal Builder SetItemsSpan(scoped ReadOnlySpan<KeyValuePair<TKey, TValue>> items)
		{
			var dictionary = this.MutateOnce();

			foreach (var item in items)
			{
				dictionary[item.Key] = item.Value;
			}

			return this;
		}

		/// <summary>
		/// Attempt to remove a specific <paramref name="key"/> from the dictionary.
		/// Returns <see langword="false"/> when the key was not found.
		/// </summary>
		/// <remarks>
		/// This is the equivalent of <see cref="Dictionary{TKey, TValue}.Remove(TKey)">Dictionary.Remove</see>.
		/// </remarks>
		public bool TryRemove(TKey key) => this.MutateOnce().Remove(key);

		/// <summary>
		/// Attempt to remove a specific <paramref name="key"/> from the dictionary.
		/// Returns <see langword="false"/> when the key was not found. The removed
		/// value (if any) is stored in <paramref name="value"/>.
		/// </summary>
		public bool TryRemove(TKey key, [MaybeNullWhen(false)] out TValue value)
		{
			var dictionary = this.MutateOnce();

#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
			return dictionary.Remove(key, out value);
#else
			if (dictionary.TryGetValue(key, out value))
			{
				dictionary.Remove(key);
				return true;
			}

			value = default;
			return false;
#endif
		}

		/// <summary>
		/// Remove a specific key from the dictionary if it exists.
		/// </summary>
		/// <remarks>
		/// Use <c>TryRemove</c> if you want to know whether any element was
		/// actually removed.
		/// </remarks>
		public Builder Remove(TKey key)
		{
			this.TryRemove(key);
			return this;
		}

		/// <summary>
		/// Remove the provided <paramref name="keys"/> from the dictionary.
		/// </summary>
		/// <remarks>
		/// <see cref="ValueCollectionExtensions.RemoveRange{TKey, TValue}(ValueDictionary{TKey, TValue}.Builder, ReadOnlySpan{TKey})">More overloads</see>
		/// are available as extension methods.
		/// </remarks>
		public Builder RemoveRange(IEnumerable<TKey> keys)
		{
			if (keys is null)
			{
				throw new ArgumentNullException(nameof(keys));
			}

			using (var guard = this.Mutate())
			{
				foreach (var key in keys)
				{
					guard.Inner.Remove(key);
				}
			}

			return this;
		}

		// Accessible through an extension method.
		internal Builder RemoveRangeSpan(scoped ReadOnlySpan<TKey> keys)
		{
			var dictionary = this.MutateOnce();

			foreach (var key in keys)
			{
				dictionary.Remove(key);
			}

			return this;
		}

		/// <summary>
		/// Remove all elements from the dictionary.
		/// </summary>
		/// <remarks>
		/// The capacity remains unchanged until a call to <see cref="TrimExcess()"/> is made.
		/// </remarks>
		public Builder Clear()
		{
			this.MutateOnce().Clear();
			return this;
		}

		/// <summary>
		/// Reduce the capacity of this dictionary as much as possible. After calling this
		/// method, the <c>Capacity</c> of the dictionary may still be higher than
		/// the <see cref="Count"/>.
		/// </summary>
		/// <remarks>
		/// This method can be used to minimize the memory overhead of long-lived
		/// dictionaries. This method is most useful just before calling
		/// <see cref="Build"/>, e.g.:
		/// <code>
		/// var longLivedDictionary = builder.TrimExcess().Build()
		/// </code>
		/// Excessive use of this method most likely introduces more performance
		/// problems than it solves.
		/// </remarks>
		public Builder TrimExcess()
		{
			var dictionary = this.MutateOnce();

#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
			dictionary.TrimExcess();
#else
			var copy = new Dictionary<TKey, TValue>(dictionary.Count);

			foreach (var entry in dictionary)
			{
				copy.Add(entry.Key, entry.Value);
			}

			this.dictionary!.inner = copy;
#endif
			return this;
		}

		/// <summary>
		/// Create a new heap-allocated live view of the builder.
		/// </summary>
		/// <remarks>
		/// This method is an <c>O(1)</c> operation and allocates a new fixed-size
		/// collection instance. The items are not copied. Changes made to the
		/// builder are visible in the collection and vice versa.
		/// </remarks>
		public Collection AsCollection() => new Collection(this);

#pragma warning disable CA1034 // Nested types should not be visible
		/// <summary>
		/// A heap-allocated live view of a builder. Changes made to the
		/// collection are visible in the builder and vice versa.
		/// </summary>
		public sealed class Collection : IDictionary<TKey, TValue>, IReadOnlyDictionary<TKey, TValue>
		{
			private readonly Builder builder;

			/// <summary>
			/// The underlying builder.
			/// </summary>
			public Builder Builder => this.builder;

			internal Collection(Builder builder)
			{
				this.builder = builder;
			}

			/// <inheritdoc/>
			bool ICollection<KeyValuePair<TKey, TValue>>.IsReadOnly => this.builder.IsReadOnly;

			/// <inheritdoc/>
			int ICollection<KeyValuePair<TKey, TValue>>.Count => this.builder.Count;

			/// <inheritdoc/>
			int IReadOnlyCollection<KeyValuePair<TKey, TValue>>.Count => this.builder.Count;

			/// <inheritdoc/>
			ICollection<TKey> IDictionary<TKey, TValue>.Keys => this.builder.Keys.AsCollection();

			/// <inheritdoc/>
			IEnumerable<TKey> IReadOnlyDictionary<TKey, TValue>.Keys => this.builder.Keys.AsCollection();

			/// <inheritdoc/>
			ICollection<TValue> IDictionary<TKey, TValue>.Values => this.builder.Values.AsCollection();

			/// <inheritdoc/>
			IEnumerable<TValue> IReadOnlyDictionary<TKey, TValue>.Values => this.builder.Values.AsCollection();

			/// <inheritdoc/>
			TValue IReadOnlyDictionary<TKey, TValue>.this[TKey key] => this.builder[key];

			/// <inheritdoc/>
			TValue IDictionary<TKey, TValue>.this[TKey key]
			{
				get => this.builder[key];
				set => this.builder[key] = value;
			}

			/// <inheritdoc/>
			bool IReadOnlyDictionary<TKey, TValue>.ContainsKey(TKey key) => this.builder.ContainsKey(key);

			/// <inheritdoc/>
#pragma warning disable CS8601 // Possible null reference assignment.
			bool IReadOnlyDictionary<TKey, TValue>.TryGetValue(TKey key, out TValue value) => this.builder.TryGetValue(key, out value);
#pragma warning restore CS8601 // Possible null reference assignment.

			/// <inheritdoc/>
			void IDictionary<TKey, TValue>.Add(TKey key, TValue value) => this.builder.Add(key, value);

			/// <inheritdoc/>
			bool IDictionary<TKey, TValue>.ContainsKey(TKey key) => this.builder.ContainsKey(key);

			/// <inheritdoc/>
			bool IDictionary<TKey, TValue>.Remove(TKey key) => this.builder.TryRemove(key);

			/// <inheritdoc/>
#pragma warning disable CS8601 // Possible null reference assignment.
			bool IDictionary<TKey, TValue>.TryGetValue(TKey key, out TValue value) => this.builder.TryGetValue(key, out value);
#pragma warning restore CS8601 // Possible null reference assignment.

			/// <inheritdoc/>
			void ICollection<KeyValuePair<TKey, TValue>>.Add(KeyValuePair<TKey, TValue> item) => this.builder.Add(item.Key, item.Value);

			/// <inheritdoc/>
			void ICollection<KeyValuePair<TKey, TValue>>.Clear() => this.builder.Clear();

			/// <inheritdoc/>
			bool ICollection<KeyValuePair<TKey, TValue>>.Contains(KeyValuePair<TKey, TValue> item) => this.builder.ReadOnce().Contains(item);

			/// <inheritdoc/>
			void ICollection<KeyValuePair<TKey, TValue>>.CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
			{
				if (array is null)
				{
					throw new ArgumentNullException(nameof(array));
				}

				if (arrayIndex < 0 || arrayIndex > array.Length)
				{
					throw new ArgumentOutOfRangeException(nameof(arrayIndex));
				}

				if (array.Length - arrayIndex < this.Builder.Count)
				{
					throw new ArgumentException("Destination too short", nameof(arrayIndex));
				}

				var index = arrayIndex;
				foreach (var item in this)
				{
					array[index] = item;
					index++;
				}
			}

			/// <inheritdoc/>
			bool ICollection<KeyValuePair<TKey, TValue>>.Remove(KeyValuePair<TKey, TValue> item) => ((ICollection<KeyValuePair<TKey, TValue>>)this.Builder.MutateOnce()).Remove(item);

			/// <inheritdoc/>
			IEnumerator<KeyValuePair<TKey, TValue>> IEnumerable<KeyValuePair<TKey, TValue>>.GetEnumerator()
			{
				if (this.builder.Count == 0)
				{
					return EnumeratorLike.Empty<KeyValuePair<TKey, TValue>>();
				}
				else
				{
					return EnumeratorLike.AsIEnumerator<KeyValuePair<TKey, TValue>, Enumerator>(this.builder.GetEnumerator());
				}
			}

			/// <inheritdoc/>
			IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable<KeyValuePair<TKey, TValue>>)this).GetEnumerator();
		}
#pragma warning restore CA1034 // Nested types should not be visible

		/// <summary>
		/// Returns an enumerator for this <see cref="ValueDictionary{TKey, TValue}.Builder"/>.
		///
		/// Typically, you don't need to manually call this method, but instead use
		/// the built-in <c>foreach</c> syntax.
		/// </summary>
		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Enumerator GetEnumerator() => new Enumerator(this.Read());

		/// <summary>
		/// Enumerator for <see cref="ValueDictionary{TKey, TValue}.Builder"/>.
		/// </summary>
#pragma warning disable CA1034 // Nested types should not be visible
#pragma warning disable CA1815 // Override equals and operator equals on value types
		[StructLayout(LayoutKind.Auto)]
		public struct Enumerator : IEnumeratorLike<KeyValuePair<TKey, TValue>>
		{
			private readonly Snapshot snapshot;
			private ShufflingDictionaryEnumerator<TKey, TValue> inner;

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			internal Enumerator(Snapshot snapshot)
			{
				this.snapshot = snapshot;
				this.inner = new(snapshot.AssertAlive());
			}

			/// <inheritdoc/>
			public readonly KeyValuePair<TKey, TValue> Current
			{
				[MethodImpl(MethodImplOptions.AggressiveInlining)]
				get => this.inner.Current;
			}

			/// <inheritdoc/>
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public bool MoveNext()
			{
				this.snapshot.AssertAlive();

				return this.inner.MoveNext();
			}
		}

		/// <summary>
		/// Get a string representation of the collection for debugging purposes.
		/// The format is not stable and may change without prior notice.
		/// </summary>
		[Pure]
		public override string ToString()
		{
			if (this.Count == 0)
			{
				return "[]";
			}

			var builder = new StringBuilder();
			builder.Append('[');

			var index = 0;
			foreach (var entry in this)
			{
				if (index > 0)
				{
					builder.Append(", ");
				}

				var keyString = entry.Key?.ToString() ?? "null";
				var valueString = entry.Value?.ToString() ?? "null";
				builder.Append(keyString);
				builder.Append(": ");
				builder.Append(valueString);

				index++;
			}

			builder.Append(']');
			return builder.ToString();
		}

		/// <inheritdoc/>
		[Pure]
		public override int GetHashCode() => RuntimeHelpers.GetHashCode(this.dictionary);

		/// <summary>
		/// Returns <see langword="true"/> when the two builders refer to the same allocation.
		/// </summary>
		[Pure]
		public bool Equals(Builder other) => object.ReferenceEquals(this.dictionary, other.dictionary);

#pragma warning disable CS0809 // Obsolete member overrides non-obsolete member
		/// <inheritdoc/>
		[Pure]
		[Obsolete("Avoid boxing. Use == instead.")]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public override bool Equals(object? obj) => obj is Builder builder && obj.Equals(builder);
#pragma warning restore CS0809 // Obsolete member overrides non-obsolete member

		/// <summary>
		/// Check for equality.
		/// </summary>
		[Pure]
		public static bool operator ==(Builder left, Builder right) => left.Equals(right);

		/// <summary>
		/// Check for inequality.
		/// </summary>
		[Pure]
		public static bool operator !=(Builder left, Builder right) => !left.Equals(right);

		private static NotSupportedException ImmutableException() => new("Collection is immutable");
	}
}
