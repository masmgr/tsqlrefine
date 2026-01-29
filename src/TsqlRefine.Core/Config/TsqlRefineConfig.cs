using System.Text.Json;

namespace TsqlRefine.Core.Config;

public sealed record PluginConfig(string Path, bool Enabled = true);

public sealed record FormattingConfig(
    string IndentStyle = "spaces",
    int IndentSize = 4,
    string KeywordCasing = "upper",
    string FunctionCasing = "upper",
    string DataTypeCasing = "lower",
    string SchemaCasing = "lower",
    string TableCasing = "upper",
    string ColumnCasing = "upper",
    string VariableCasing = "lower",
    string CommaStyle = "trailing",
    int MaxLineLength = 0,
    bool InsertFinalNewline = true,
    bool TrimTrailingWhitespace = true
);

public sealed record TsqlRefineConfig(
    int CompatLevel = 150,
    string? Ruleset = null,
    IReadOnlyList<PluginConfig>? Plugins = null,
    FormattingConfig? Formatting = null
)
{
    public static readonly TsqlRefineConfig Default = new();

    public static TsqlRefineConfig Load(string path)
    {
        var json = File.ReadAllText(path);
        var config = JsonSerializer.Deserialize<TsqlRefineConfig>(json, JsonDefaults.Options);
        return config ?? Default;
    }
}

