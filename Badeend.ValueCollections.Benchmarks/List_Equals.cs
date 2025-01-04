using System.Collections.Immutable;
using BenchmarkDotNet.Attributes;

namespace Badeend.ValueCollections.Benchmarks;

[MemoryDiagnoser]
[DisassemblyDiagnoser]
public class List_Equals
{
	private const int Size = 10_000_000;

	private static readonly IEnumerable<int> items = Enumerable.Range(1, Size);
	private static readonly ValueList<int> valueListA = items.ToValueList();
	private static readonly ValueList<int> valueListB = items.ToValueList();

	[Benchmark(Description = "ValueList<T>.Equals()")]
	public bool ValueList()
	{
		return valueListA == valueListB;
	}
}
