using BenchmarkDotNet.Running;

// Run in Release: dotnet run -c Release --project benchmarks/CellCascade.Benchmarks -- --filter '*'
BenchmarkSwitcher.FromAssembly(typeof(CellCascade.Benchmarks.ChainBenchmarks).Assembly).Run(args);
