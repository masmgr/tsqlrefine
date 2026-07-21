using BenchmarkDotNet.Running;
using TsqlRefine.Benchmarks;

BenchmarkSwitcher.FromAssembly(typeof(LintBenchmarks).Assembly).Run(args);
