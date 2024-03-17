using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Badeend.ValueCollections;

[SuppressMessage("Performance", "CA1823:Avoid unused private fields", Justification = "They're accessed using Unsafe")]
[SuppressMessage("Style", "IDE0044:Add readonly modifier", Justification = "They're modified using Unsafe")]
internal struct InlineArray32<T>
{
	internal const int Length = 32;

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
	private T _8;
	private T _9;
	private T _10;
	private T _11;
	private T _12;
	private T _13;
	private T _14;
	private T _15;
	private T _16;
	private T _17;
	private T _18;
	private T _19;
	private T _20;
	private T _21;
	private T _22;
	private T _23;
	private T _24;
	private T _25;
	private T _26;
	private T _27;
	private T _28;
	private T _29;
	private T _30;
	private T _31;
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
