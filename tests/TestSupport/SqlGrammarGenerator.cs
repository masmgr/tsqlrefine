using FsCheck;
using FsCheck.Fluent;

namespace TsqlRefine.TestSupport;

internal static class SqlGrammarGenerator
{
    private static readonly string[] Tables = ["dbo.Users", "dbo.Orders", "sales.Products", "#Work"];
    private static readonly string[] Columns = ["Id", "Name", "Amount", "CreatedAt", "IsActive"];
    private static readonly string[] Comparisons = ["=", "<>", ">", "<="];

    public static Gen<string> Scripts(int maximumStatements = 3) =>
        from statementCount in Gen.Choose(1, maximumStatements)
        from statements in Statement().ListOf(statementCount)
        select string.Join(Environment.NewLine, statements);

    private static Gen<string> Statement() =>
        Gen.OneOf(Select(), Update(), Delete());

    private static Gen<string> Select() =>
        from table in Gen.Elements(Tables)
        from column in Gen.Elements(Columns)
        from secondColumn in Gen.Elements(Columns)
        from comparison in Gen.Elements(Comparisons)
        from includeJoin in Gen.Elements(false, true)
        from includeWhere in Gen.Elements(false, true)
        from includeGroupBy in Gen.Elements(false, true)
        from includeSubquery in Gen.Elements(false, true)
        from terminate in Gen.Elements(false, true)
        select BuildSelect(
            table,
            column,
            secondColumn,
            comparison,
            includeJoin,
            includeWhere,
            includeGroupBy,
            includeSubquery,
            terminate);

    private static Gen<string> Update() =>
        from table in Gen.Elements(Tables)
        from column in Gen.Elements(Columns)
        from comparison in Gen.Elements(Comparisons)
        from includeWhere in Gen.Elements(false, true)
        from terminate in Gen.Elements(false, true)
        select $"UPDATE t SET t.{column} = 1 FROM {table} AS t" +
               (includeWhere ? $" WHERE t.Id {comparison} 1" : string.Empty) +
               (terminate ? ";" : string.Empty);

    private static Gen<string> Delete() =>
        from table in Gen.Elements(Tables)
        from comparison in Gen.Elements(Comparisons)
        from includeWhere in Gen.Elements(false, true)
        from terminate in Gen.Elements(false, true)
        select $"DELETE FROM {table}" +
               (includeWhere ? $" WHERE Id {comparison} 1" : string.Empty) +
               (terminate ? ";" : string.Empty);

    private static string BuildSelect(
        string table,
        string column,
        string secondColumn,
        string comparison,
        bool includeJoin,
        bool includeWhere,
        bool includeGroupBy,
        bool includeSubquery,
        bool terminate)
    {
        var projection = includeSubquery
            ? $"t.{column}, (SELECT MAX(i.{secondColumn}) FROM {table} AS i WHERE i.Id = t.Id) AS NestedValue"
            : includeGroupBy
                ? $"t.{column}, COUNT(*) AS ItemCount"
                : $"t.{column}, t.{secondColumn}";
        var join = includeJoin
            ? $" INNER JOIN {table} AS j ON j.Id = t.Id"
            : string.Empty;
        var where = includeWhere ? $" WHERE t.{column} {comparison} 1" : string.Empty;
        var groupBy = includeGroupBy ? $" GROUP BY t.{column}" : string.Empty;
        return $"SELECT {projection} FROM {table} AS t{join}{where}{groupBy}" +
               (terminate ? ";" : string.Empty);
    }
}
