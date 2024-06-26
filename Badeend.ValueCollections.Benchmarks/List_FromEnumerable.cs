using System.Collections.Immutable;
using BenchmarkDotNet.Attributes;

namespace Badeend.ValueCollections.Benchmarks;

[MemoryDiagnoser]
[DisassemblyDiagnoser]
public class List_FromEnumerable
{
	private const int Size = 10_000_000;

	private static readonly IEnumerable<int> items_slow = Enumerable.Range(1, Size).Where(_ => true);
	private static readonly IEnumerable<int> items_fast = items_slow.ToArray();

	[Benchmark(Description = ".ToList() [collection]")]
	public IReadOnlyList<int> ListFast() => items_fast.ToList();

	[Benchmark(Description = ".ToValueList() [collection]")]
	public IReadOnlyList<int> ValueListFast() => items_fast.ToValueList();

	[Benchmark(Description = ".ToValueListBuilder() [collection]")]
	public ValueList<int>.Builder ValueListBuilderFast() => items_fast.ToValueListBuilder();

	[Benchmark(Description = ".ToList() [non-collection]")]
	public IReadOnlyList<int> ListSlow() => items_slow.ToList();

	[Benchmark(Description = ".ToValueList() [non-collection]")]
	public IReadOnlyList<int> ValueListSlow() => items_slow.ToValueList();

	[Benchmark(Description = ".ToValueListBuilder() [non-collection]")]
	public ValueList<int>.Builder ValueListBuilderSlow() => items_slow.ToValueListBuilder();

	[Benchmark(Description = ".ToImmutableList()")]
	public IReadOnlyList<int> ImmutableList() => items_fast.ToImmutableList();
}
