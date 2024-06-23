using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using BenchmarkDotNet.Running;

namespace Badeend.ValueCollections.Benchmarks;

public static class Benchmark
{
	public static void Main(string[] args) => BenchmarkSwitcher.FromAssembly(typeof(Benchmark).Assembly).Run(args);
}
