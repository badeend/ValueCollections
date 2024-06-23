using System.Collections.Immutable;
using BenchmarkDotNet.Attributes;

namespace Badeend.ValueCollections.Benchmarks;

[MemoryDiagnoser]
[DisassemblyDiagnoser]
public class List_Foreach
{
	private const int Iterations = 10_000_000;

	private static readonly IEnumerable<int> items = Enumerable.Range(1, Iterations);
	private static readonly ImmutableList<int> immutableList = items.ToImmutableList();
	private static readonly List<int> list = items.ToList();
	private static readonly ValueList<int> valueList = items.ToValueList();
	private static readonly ValueList<int>.Builder valueListBuilder = items.ToValueListBuilder();

	[Benchmark(Description = "foreach: List<T>")]
	public int List()
	{
		int result = 0;

		foreach (var i in list)
		{
			result += i;
		}

		return result;
	}

	[Benchmark(Description = "foreach: ValueList<T>")]
	public int ValueList()
	{
		int result = 0;

		foreach (var i in valueList)
		{
			result += i;
		}

		return result;
	}

	[Benchmark(Description = "foreach: ValueList<T>.Builder")]
	public int ValueListBuilder()
	{
		int result = 0;

		foreach (var i in valueListBuilder)
		{
			result += i;
		}

		return result;
	}

	[Benchmark(Description = "foreach: ImmutableList<T>")]
	public int ImmutableList()
	{
		int result = 0;

		foreach (var i in immutableList)
		{
			result += i;
		}

		return result;
	}
}
