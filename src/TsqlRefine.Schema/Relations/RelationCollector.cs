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
    public static RelationProfile Collect(
        IEnumerable<(string Sql, string FilePath)> inputs,
        int compatLevel)
    {
        ArgumentNullException.ThrowIfNull(inputs);

        var inputList = inputs as IList<(string Sql, string FilePath)> ?? inputs.ToList();
        var allJoins = new List<RawJoinInfo>();

        foreach (var (sql, filePath) in inputList)
        {
            var fragment = SqlParser.Parse(sql, compatLevel);
            if (fragment is null)
            {
                continue;
            }

            var joins = RelationExtractor.Extract(fragment, filePath);
            allJoins.AddRange(joins);
        }

        return RelationAggregator.Aggregate(allJoins, inputList.Count);
    }
}
