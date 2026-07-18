using BenchmarkDotNet.Attributes;
using TsqlRefine.Core.Engine;
using TsqlRefine.Core.Model;
using TsqlRefine.Rules;

namespace TsqlRefine.Benchmarks;

[MemoryDiagnoser]
[JsonExporterAttribute.FullCompressed]
public class LintBenchmarks
{
    private TsqlRefineEngine _engine = null!;
    private SqlInput[] _inputs = null!;

    [GlobalSetup]
    public void Setup()
    {
        var repositoryRoot = FindRepositoryRoot();
        var corpusRoot = Path.Combine(repositoryRoot, "tests", "corpus", "sql");
        _inputs = Directory.EnumerateFiles(corpusRoot, "*.sql", SearchOption.AllDirectories)
            .Order(StringComparer.Ordinal)
            .Select(path => new SqlInput(
                Path.GetRelativePath(corpusRoot, path).Replace('\\', '/'),
                File.ReadAllText(path)))
            .ToArray();
        _engine = new TsqlRefineEngine(new BuiltinRuleProvider().GetRules());
    }

    [Benchmark]
    public LintResult LintAllRules() =>
        _engine.Run("lint", _inputs, new EngineOptions(CompatLevel: 160));

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "global.json")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate the repository root.");
    }
}
