using System.Text.Json;

namespace TsqlRefine.Core.Config;

public sealed record PluginConfig(string Path, bool Enabled = true);

public sealed record TsqlRefineConfig(
    int CompatLevel = 150,
    string? Ruleset = null,
    IReadOnlyList<PluginConfig>? Plugins = null
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

