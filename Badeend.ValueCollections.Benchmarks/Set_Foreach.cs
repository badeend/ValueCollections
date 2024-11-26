using System.Collections.Immutable;
using BenchmarkDotNet.Attributes;

namespace Badeend.ValueCollections.Benchmarks;

[MemoryDiagnoser]
[DisassemblyDiagnoser]
public class Set_Foreach
{
	private const int Iterations = 1_000_000;

	private static readonly IEnumerable<int> items = Enumerable.Range(1, Iterations);
	private static readonly ImmutableHashSet<int> immutableHashSet = items.ToImmutableHashSet();
	private static readonly HashSet<int> hashSet = items.ToHashSet();
	private static readonly ValueSet<int> valueSet = items.ToValueSet();
	private static readonly ValueSet<int>.Builder valueSetBuilder = items.ToValueSetBuilder();

	[Benchmark(Description = "foreach: HashSet<T>")]
	public int HashSet()
	{
		int result = 0;

		foreach (var i in hashSet)
		{
			result += i;
		}

		return result;
	}

	[Benchmark(Description = "foreach: ValueSet<T>")]
	public int ValueSet()
	{
		int result = 0;

		foreach (var i in valueSet)
		{
			result += i;
		}

		return result;
	}

	[Benchmark(Description = "foreach: ValueSet<T>.Builder")]
	public int ValueSetBuilder()
	{
		int result = 0;

		foreach (var i in valueSetBuilder)
		{
			result += i;
		}

		return result;
	}

	[Benchmark(Description = "foreach: ImmutableHashSet<T>")]
	public int ImmutableHashSet()
	{
		int result = 0;

		foreach (var i in immutableHashSet)
		{
			result += i;
		}

		return result;
	}
}
