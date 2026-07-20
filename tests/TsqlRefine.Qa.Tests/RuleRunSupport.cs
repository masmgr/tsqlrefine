using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Qa.Tests;

/// <summary>
/// Builds <see cref="RuleContext"/> instances for running rules directly,
/// bypassing engine normalization so raw rule output can be inspected.
/// </summary>
internal static class RuleRunSupport
{
    private static readonly ISchemaContext SchemaContext = QaSchemaSupport.CreateContext();

    public static IEnumerable<(string Path, string Sql, int CompatLevel)> EnumerateSampleAndCorpusInputs()
    {
        var samplesRoot = Path.Combine(CorpusSupport.RepositoryRoot, "samples", "sql");
        foreach (var samplePath in Directory.EnumerateFiles(samplesRoot, "*.sql").Order(StringComparer.Ordinal))
        {
            yield return ($"samples/sql/{Path.GetFileName(samplePath)}", File.ReadAllText(samplePath), 160);
        }

        foreach (var file in CorpusSupport.LoadFiles())
        {
            yield return (file.Path, CorpusSupport.Read(file), file.MinCompatLevel);
        }
    }

    public static RuleContext CreateContext(string path, string sql, int compatLevel)
    {
        var parser = CorpusSupport.CreateParser(compatLevel);

        using var fragmentReader = new StringReader(sql);
        var fragment = parser.Parse(fragmentReader, out var parseErrors);

        using var tokenReader = new StringReader(sql);
        var tokenStream = parser.GetTokenStream(tokenReader, out IList<ParseError> tokenErrors);

        var tokens = tokenStream
            .Where(token => token.TokenType != TSqlTokenType.EndOfFile)
            .Select(token =>
            {
                var text = token.Text ?? string.Empty;
                return new Token(
                    text,
                    new Position(Math.Max(0, token.Line - 1), Math.Max(0, token.Column - 1)),
                    text.Length,
                    token.TokenType.ToString());
            })
            .ToArray();

        var ast = new ScriptDomAst(sql, fragment, parseErrors as IReadOnlyList<ParseError>, tokenErrors.ToArray());
        return new RuleContext(path, compatLevel, ast, tokens, new RuleSettings(), SchemaContext);
    }

    public static IReadOnlyList<string> SplitLines(string sql) =>
        sql.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
}
