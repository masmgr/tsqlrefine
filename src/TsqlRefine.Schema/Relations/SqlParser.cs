using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace TsqlRefine.Schema.Relations;

/// <summary>
/// Minimal SQL parser wrapper for relation extraction.
/// </summary>
internal static class SqlParser
{
    /// <summary>
    /// Parses a SQL string and returns both the best-effort AST and all parse errors.
    /// </summary>
    internal static SqlParseResult Parse(string sql, int compatLevel)
    {
        var parser = CreateParser(compatLevel);
        using var reader = new StringReader(sql);
        var fragment = parser.Parse(reader, out var errors);
        return new SqlParseResult(fragment, errors.ToArray());
    }

    /// <summary>Formats a ScriptDOM parse error with its source file location.</summary>
    internal static string FormatError(string filePath, ParseError error) =>
        $"{filePath}({error.Line},{error.Column}): SQL{error.Number}: {error.Message}";

    /// <summary>Throws a single failure containing every collected parse error.</summary>
    internal static void ThrowIfErrors(IReadOnlyList<string> errors)
    {
        if (errors.Count > 0)
        {
            throw new InvalidDataException(
                $"SQL parsing failed:{Environment.NewLine}{string.Join(Environment.NewLine, errors)}");
        }
    }

    private static TSqlParser CreateParser(int compatLevel) =>
        compatLevel switch
        {
            >= 160 => new TSql160Parser(initialQuotedIdentifiers: true),
            >= 150 => new TSql150Parser(initialQuotedIdentifiers: true),
            >= 140 => new TSql140Parser(initialQuotedIdentifiers: true),
            >= 130 => new TSql130Parser(initialQuotedIdentifiers: true),
            >= 120 => new TSql120Parser(initialQuotedIdentifiers: true),
            >= 110 => new TSql110Parser(initialQuotedIdentifiers: true),
            _ => new TSql100Parser(initialQuotedIdentifiers: true)
        };
}

/// <summary>Best-effort ScriptDOM output together with any syntax errors.</summary>
internal sealed record SqlParseResult(TSqlFragment? Fragment, IReadOnlyList<ParseError> Errors);
