using System.Collections.Immutable;
using BenchmarkDotNet.Attributes;

namespace Badeend.ValueCollections.Benchmarks;

[MemoryDiagnoser]
[DisassemblyDiagnoser]
public class Set_Add
{
	private const int Iterations = 1_000_000;

	[Benchmark(Description = "HashSet<T>.Add()")]
	public IReadOnlySet<int> HashSet()
	{
		var list = new HashSet<int>();

		for (int i = 0; i < Iterations; i++)
		{
			list.Add(i);
		}

		return list;
	}

	[Benchmark(Description = "ValueSet<T>.Builder.Add()")]
	public IReadOnlySet<int> ValueSetBuilder()
	{
		var builder = ValueSet.CreateBuilder<int>();

		for (int i = 0; i < Iterations; i++)
		{
			builder.Add(i);
		}

		return builder.Build();
	}

	[Benchmark(Description = "ImmutableHashSet<T>.Builder.Add()")]
	public IReadOnlySet<int> ImmutableHashSetBuilder()
	{
		var builder = ImmutableHashSet.CreateBuilder<int>();

		for (int i = 0; i < Iterations; i++)
		{
			builder.Add(i);
		}

		return builder.ToImmutable();
	}
}
