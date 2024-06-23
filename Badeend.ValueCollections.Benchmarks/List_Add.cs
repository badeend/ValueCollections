using System.Collections.Immutable;
using BenchmarkDotNet.Attributes;

namespace Badeend.ValueCollections.Benchmarks;

[MemoryDiagnoser]
[DisassemblyDiagnoser]
public class List_Add
{
	private const int Iterations = 10_000_000;

	[Benchmark(Description = "List<T>.Add()")]
	public IReadOnlyList<int> List()
	{
		var list = new List<int>();

		for (int i = 0; i < Iterations; i++)
		{
			list.Add(i);
		}

		return list;
	}

	[Benchmark(Description = "ValueList<T>.Builder.Add()")]
	public IReadOnlyList<int> ValueListBuilder()
	{
		var builder = ValueList.CreateBuilder<int>();

		for (int i = 0; i < Iterations; i++)
		{
			builder.Add(i);
		}

		return builder.Build();
	}

	[Benchmark(Description = "ImmutableList<T>.Builder.Add()")]
	public IReadOnlyList<int> ImmutableListBuilder()
	{
		var builder = ImmutableList.CreateBuilder<int>();

		for (int i = 0; i < Iterations; i++)
		{
			builder.Add(i);
		}

		return builder.ToImmutable();
	}
}
