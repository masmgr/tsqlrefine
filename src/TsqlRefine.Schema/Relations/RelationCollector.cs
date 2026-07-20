namespace TsqlRefine.Schema.Relations;

/// <summary>
/// Public facade for collecting JOIN relation patterns from SQL inputs.
/// Parses SQL, extracts JOIN patterns, and aggregates them into a <see cref="RelationProfile"/>.
/// </summary>
public static class RelationCollector
{
    /// <summary>
    /// Collects JOIN relation patterns from a set of SQL inputs.
    /// </summary>
    /// <param name="inputs">SQL text and file path pairs.</param>
    /// <param name="compatLevel">SQL Server compatibility level for parsing (100-160).</param>
    /// <returns>An aggregated relation profile.</returns>
    /// <exception cref="InvalidDataException">One or more inputs contain SQL parse errors.</exception>
    public static RelationProfile Collect(
        IEnumerable<(string Sql, string FilePath)> inputs,
        int compatLevel)
    {
        ArgumentNullException.ThrowIfNull(inputs);

        var inputList = inputs as IList<(string Sql, string FilePath)> ?? inputs.ToList();
        var allJoins = new List<RawJoinInfo>();
        var parseErrors = new List<string>();

        foreach (var (sql, filePath) in inputList)
        {
            var parseResult = SqlParser.Parse(sql, compatLevel);
            if (parseResult.Errors.Count > 0)
            {
                parseErrors.AddRange(parseResult.Errors.Select(error => SqlParser.FormatError(filePath, error)));
                continue;
            }
            if (parseResult.Fragment is null)
            {
                parseErrors.Add($"{filePath}: SQL parser returned no syntax tree.");
                continue;
            }

            var joins = RelationExtractor.Extract(parseResult.Fragment, filePath);
            allJoins.AddRange(joins);
        }

        SqlParser.ThrowIfErrors(parseErrors);

        return RelationAggregator.Aggregate(allJoins, inputList.Count);
    }
}
