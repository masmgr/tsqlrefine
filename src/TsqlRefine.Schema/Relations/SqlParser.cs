using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace TsqlRefine.Schema.Relations;

/// <summary>
/// Minimal SQL parser wrapper for relation extraction.
/// </summary>
internal static class SqlParser
{
    /// <summary>
    /// Parses a SQL string and returns the AST fragment, or null if parsing fails.
    /// </summary>
    internal static TSqlFragment? Parse(string sql, int compatLevel)
    {
        var parser = CreateParser(compatLevel);
        using var reader = new StringReader(sql);
        var fragment = parser.Parse(reader, out _);
        return fragment;
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
