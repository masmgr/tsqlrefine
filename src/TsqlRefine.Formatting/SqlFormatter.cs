namespace TsqlRefine.Formatting;

public static class SqlFormatter
{
    public static string Format(string sql, FormattingOptions options)
    {
        _ = options;
        return sql ?? string.Empty;
    }
}

