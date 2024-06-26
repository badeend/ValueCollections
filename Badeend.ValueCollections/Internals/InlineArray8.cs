using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Badeend.ValueCollections.Internals;

[SuppressMessage("Performance", "CA1823:Avoid unused private fields", Justification = "They're accessed using Unsafe")]
[SuppressMessage("Style", "IDE0044:Add readonly modifier", Justification = "They're modified using Unsafe")]
internal struct InlineArray8<T>
{
	internal const int Length = 8;

#pragma warning disable CS0169 // Private field is never used
#pragma warning disable SA1306 // Field names should begin with lower-case letter
#pragma warning disable SA1309 // Field names should not begin with underscore
	private T _0;
	private T _1;
	private T _2;
	private T _3;
	private T _4;
	private T _5;
	private T _6;
	private T _7;
#pragma warning restore SA1309 // Field names should not begin with underscore
#pragma warning restore SA1306 // Field names should begin with lower-case letter
#pragma warning restore CS0169 // Private field is never used

	internal T this[int index]
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get
		{
			Debug.Assert(index >= 0 && index < Length);
			return Unsafe.Add(ref this._0, index);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		set
		{
			Debug.Assert(index >= 0 && index < Length);
			Unsafe.Add(ref this._0, index) = value;
		}
	}
}
