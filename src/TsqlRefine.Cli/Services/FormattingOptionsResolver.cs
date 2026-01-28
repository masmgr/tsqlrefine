using TsqlRefine.Core;
using TsqlRefine.Core.Config;
using TsqlRefine.Core.Engine;
using TsqlRefine.Formatting;

namespace TsqlRefine.Cli.Services;

public sealed class FormattingOptionsResolver
{
    private sealed record EditorConfigFormattingOptions(
        IndentStyle? IndentStyle,
        int? IndentSize);

    private readonly ConfigLoader _configLoader;

    public FormattingOptionsResolver(ConfigLoader configLoader)
    {
        _configLoader = configLoader;
    }

    public FormattingOptions ResolveFormattingOptions(CliArgs args, SqlInput input)
    {
        // Priority: CLI args > .editorconfig > tsqlrefine.json > defaults
        var options = new FormattingOptions();

        // Load from tsqlrefine.json if available
        try
        {
            var config = _configLoader.LoadConfig(args);
            if (config.Formatting is not null)
            {
                options = FormattingConfigMapper.ToFormattingOptions(config.Formatting);
            }
        }
        catch
        {
            // Ignore config load errors for formatting - use defaults
        }

        // Override with .editorconfig
        var editorConfig = TryReadEditorConfigOptions(input.FilePath);
        if (editorConfig is not null)
        {
            options = options with
            {
                IndentStyle = editorConfig.IndentStyle ?? options.IndentStyle,
                IndentSize = editorConfig.IndentSize ?? options.IndentSize
            };
        }

        // Override with CLI args
        return options with
        {
            IndentStyle = args.IndentStyle ?? options.IndentStyle,
            IndentSize = args.IndentSize ?? options.IndentSize
        };
    }

    private EditorConfigFormattingOptions? TryReadEditorConfigOptions(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) ||
            string.Equals(filePath, "<stdin>", StringComparison.Ordinal))
        {
            return null;
        }

        if (!string.Equals(Path.GetExtension(filePath), ".sql", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        try
        {
            var parser = new EditorConfig.Core.EditorConfigParser();
            var config = parser.Parse(Path.GetFullPath(filePath));
            if (config?.Properties is null)
            {
                return null;
            }

            var properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in config.Properties)
            {
                if (string.IsNullOrWhiteSpace(entry.Key))
                {
                    continue;
                }

                properties[entry.Key] = entry.Value ?? string.Empty;
            }

            var indentStyle = ParseEditorConfigIndentStyle(properties);
            var indentSize = ParseEditorConfigIndentSize(properties);
            if (indentStyle is null && indentSize is null)
            {
                return null;
            }

            return new EditorConfigFormattingOptions(indentStyle, indentSize);
        }
        catch (Exception ex)
        {
            throw new ConfigException($"Failed to parse .editorconfig: {ex.Message}");
        }
    }

    private IndentStyle? ParseEditorConfigIndentStyle(IReadOnlyDictionary<string, string> properties)
    {
        if (!properties.TryGetValue("indent_style", out var styleValue))
        {
            return null;
        }

        return styleValue.Trim().ToLowerInvariant() switch
        {
            "tab" => IndentStyle.Tabs,
            "space" => IndentStyle.Spaces,
            _ => null
        };
    }

    private int? ParseEditorConfigIndentSize(IReadOnlyDictionary<string, string> properties)
    {
        if (!properties.TryGetValue("indent_size", out var sizeValue) || string.IsNullOrWhiteSpace(sizeValue))
        {
            return null;
        }

        sizeValue = sizeValue.Trim();
        if (string.Equals(sizeValue, "tab", StringComparison.OrdinalIgnoreCase))
        {
            if (properties.TryGetValue("tab_width", out var tabWidthValue) &&
                int.TryParse(tabWidthValue.Trim(), out var tabWidth) &&
                tabWidth > 0)
            {
                return tabWidth;
            }

            return null;
        }

        return int.TryParse(sizeValue, out var parsed) && parsed > 0 ? parsed : null;
    }
}
