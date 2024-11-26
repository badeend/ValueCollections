using System.Collections.Immutable;
using BenchmarkDotNet.Attributes;

namespace Badeend.ValueCollections.Benchmarks;

[MemoryDiagnoser]
[DisassemblyDiagnoser]
public class Set_FromEnumerable
{
	private const int Size = 1_000_000;

	private static readonly IEnumerable<int> items_slow = Enumerable.Range(1, Size).Where(_ => true);
	private static readonly IEnumerable<int> items_fast = items_slow.ToArray();

	[Benchmark(Description = ".ToHashSet() [collection]")]
	public IReadOnlyCollection<int> HashSetFast() => items_fast.ToHashSet();

	[Benchmark(Description = ".ToValueSet() [collection]")]
	public IReadOnlyCollection<int> ValueSetFast() => items_fast.ToValueSet();

	[Benchmark(Description = ".ToValueSetBuilder() [collection]")]
	public ValueSet<int>.Builder ValueSetBuilderFast() => items_fast.ToValueSetBuilder();

	[Benchmark(Description = ".ToHashSet() [non-collection]")]
	public IReadOnlyCollection<int> HashSetSlow() => items_slow.ToHashSet();

	[Benchmark(Description = ".ToValueSet() [non-collection]")]
	public IReadOnlyCollection<int> ValueSetSlow() => items_slow.ToValueSet();

	[Benchmark(Description = ".ToValueSetBuilder() [non-collection]")]
	public ValueSet<int>.Builder ValueSetBuilderSlow() => items_slow.ToValueSetBuilder();

	[Benchmark(Description = ".ToImmutableHashSet()")]
	public IReadOnlyCollection<int> ImmutableHashSet() => items_fast.ToImmutableHashSet();
}
