using System.Collections.Immutable;
using BenchmarkDotNet.Attributes;

namespace Badeend.ValueCollections.Benchmarks;

[MemoryDiagnoser]
[DisassemblyDiagnoser]
public class List_CompareTo
{
	private const int Size = 10_000_000;

	private static readonly IEnumerable<int> items = Enumerable.Range(1, Size);
	private static readonly ValueList<int> valueListA = items.ToValueList();
	private static readonly ValueList<int> valueListB = items.ToValueList();

	[Benchmark(Description = "ValueList<T>.CompareTo()")]
	public int ValueList()
	{
		return valueListA.CompareTo(valueListB);
	}
}
