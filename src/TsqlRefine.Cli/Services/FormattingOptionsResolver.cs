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

    private sealed record EditorConfigResult(
        EditorConfigFormattingOptions? Options,
        string? Path);

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
        catch (ConfigException)
        {
            // Config load failure is expected when no config file exists - use defaults
        }

        // Override with .editorconfig
        var editorConfigResult = TryReadEditorConfigOptionsWithPath(input.FilePath);
        if (editorConfigResult.Options is not null)
        {
            options = options with
            {
                IndentStyle = editorConfigResult.Options.IndentStyle ?? options.IndentStyle,
                IndentSize = editorConfigResult.Options.IndentSize ?? options.IndentSize
            };
        }

        // Override with CLI args
        return options with
        {
            IndentStyle = args.IndentStyle ?? options.IndentStyle,
            IndentSize = args.IndentSize ?? options.IndentSize
        };
    }

    /// <summary>
    /// Resolves formatting options with source tracking for display purposes.
    /// </summary>
    public ResolvedFormattingOptions ResolveFormattingOptionsWithSources(CliArgs args, string? filePath)
    {
        // Start with defaults
        var defaults = new FormattingOptions();
        var compatLevel = new ResolvedFormattingOption<int>(defaults.CompatLevel, FormattingOptionSource.Default);
        var indentStyle = new ResolvedFormattingOption<IndentStyle>(defaults.IndentStyle, FormattingOptionSource.Default);
        var indentSize = new ResolvedFormattingOption<int>(defaults.IndentSize, FormattingOptionSource.Default);
        var keywordCasing = new ResolvedFormattingOption<ElementCasing>(defaults.KeywordElementCasing, FormattingOptionSource.Default);
        var builtInFunctionCasing = new ResolvedFormattingOption<ElementCasing>(defaults.BuiltInFunctionCasing, FormattingOptionSource.Default);
        var dataTypeCasing = new ResolvedFormattingOption<ElementCasing>(defaults.DataTypeCasing, FormattingOptionSource.Default);
        var schemaCasing = new ResolvedFormattingOption<ElementCasing>(defaults.SchemaCasing, FormattingOptionSource.Default);
        var tableCasing = new ResolvedFormattingOption<ElementCasing>(defaults.TableCasing, FormattingOptionSource.Default);
        var columnCasing = new ResolvedFormattingOption<ElementCasing>(defaults.ColumnCasing, FormattingOptionSource.Default);
        var variableCasing = new ResolvedFormattingOption<ElementCasing>(defaults.VariableCasing, FormattingOptionSource.Default);
        var commaStyle = new ResolvedFormattingOption<CommaStyle>(defaults.CommaStyle, FormattingOptionSource.Default);
        var maxLineLength = new ResolvedFormattingOption<int>(defaults.MaxLineLength, FormattingOptionSource.Default);
        var insertFinalNewline = new ResolvedFormattingOption<bool>(defaults.InsertFinalNewline, FormattingOptionSource.Default);
        var trimTrailingWhitespace = new ResolvedFormattingOption<bool>(defaults.TrimTrailingWhitespace, FormattingOptionSource.Default);
        var normalizeInlineSpacing = new ResolvedFormattingOption<bool>(defaults.NormalizeInlineSpacing, FormattingOptionSource.Default);

        string? configPath = null;
        string? editorConfigPath = null;

        // Layer tsqlrefine.json values
        try
        {
            var config = _configLoader.LoadConfig(args);
            configPath = _configLoader.GetConfigPath(args);

            if (config.Formatting is not null)
            {
                var configOptions = FormattingConfigMapper.ToFormattingOptions(config.Formatting);

                if (configOptions.CompatLevel != defaults.CompatLevel)
                    compatLevel = new ResolvedFormattingOption<int>(configOptions.CompatLevel, FormattingOptionSource.Config);
                if (configOptions.IndentStyle != defaults.IndentStyle)
                    indentStyle = new ResolvedFormattingOption<IndentStyle>(configOptions.IndentStyle, FormattingOptionSource.Config);
                if (configOptions.IndentSize != defaults.IndentSize)
                    indentSize = new ResolvedFormattingOption<int>(configOptions.IndentSize, FormattingOptionSource.Config);
                if (configOptions.KeywordElementCasing != defaults.KeywordElementCasing)
                    keywordCasing = new ResolvedFormattingOption<ElementCasing>(configOptions.KeywordElementCasing, FormattingOptionSource.Config);
                if (configOptions.BuiltInFunctionCasing != defaults.BuiltInFunctionCasing)
                    builtInFunctionCasing = new ResolvedFormattingOption<ElementCasing>(configOptions.BuiltInFunctionCasing, FormattingOptionSource.Config);
                if (configOptions.DataTypeCasing != defaults.DataTypeCasing)
                    dataTypeCasing = new ResolvedFormattingOption<ElementCasing>(configOptions.DataTypeCasing, FormattingOptionSource.Config);
                if (configOptions.SchemaCasing != defaults.SchemaCasing)
                    schemaCasing = new ResolvedFormattingOption<ElementCasing>(configOptions.SchemaCasing, FormattingOptionSource.Config);
                if (configOptions.TableCasing != defaults.TableCasing)
                    tableCasing = new ResolvedFormattingOption<ElementCasing>(configOptions.TableCasing, FormattingOptionSource.Config);
                if (configOptions.ColumnCasing != defaults.ColumnCasing)
                    columnCasing = new ResolvedFormattingOption<ElementCasing>(configOptions.ColumnCasing, FormattingOptionSource.Config);
                if (configOptions.VariableCasing != defaults.VariableCasing)
                    variableCasing = new ResolvedFormattingOption<ElementCasing>(configOptions.VariableCasing, FormattingOptionSource.Config);
                if (configOptions.CommaStyle != defaults.CommaStyle)
                    commaStyle = new ResolvedFormattingOption<CommaStyle>(configOptions.CommaStyle, FormattingOptionSource.Config);
                if (configOptions.MaxLineLength != defaults.MaxLineLength)
                    maxLineLength = new ResolvedFormattingOption<int>(configOptions.MaxLineLength, FormattingOptionSource.Config);
                if (configOptions.InsertFinalNewline != defaults.InsertFinalNewline)
                    insertFinalNewline = new ResolvedFormattingOption<bool>(configOptions.InsertFinalNewline, FormattingOptionSource.Config);
                if (configOptions.TrimTrailingWhitespace != defaults.TrimTrailingWhitespace)
                    trimTrailingWhitespace = new ResolvedFormattingOption<bool>(configOptions.TrimTrailingWhitespace, FormattingOptionSource.Config);
                if (configOptions.NormalizeInlineSpacing != defaults.NormalizeInlineSpacing)
                    normalizeInlineSpacing = new ResolvedFormattingOption<bool>(configOptions.NormalizeInlineSpacing, FormattingOptionSource.Config);
            }
        }
        catch (ConfigException)
        {
            // Config load failure is expected when no config file exists
        }

        // Layer .editorconfig values
        var editorConfigResult = TryReadEditorConfigOptionsWithPath(filePath);
        if (editorConfigResult.Options is not null)
        {
            editorConfigPath = editorConfigResult.Path;
            if (editorConfigResult.Options.IndentStyle.HasValue)
                indentStyle = new ResolvedFormattingOption<IndentStyle>(editorConfigResult.Options.IndentStyle.Value, FormattingOptionSource.EditorConfig);
            if (editorConfigResult.Options.IndentSize.HasValue)
                indentSize = new ResolvedFormattingOption<int>(editorConfigResult.Options.IndentSize.Value, FormattingOptionSource.EditorConfig);
        }

        // Layer CLI args
        if (args.IndentStyle.HasValue)
            indentStyle = new ResolvedFormattingOption<IndentStyle>(args.IndentStyle.Value, FormattingOptionSource.CliArg);
        if (args.IndentSize.HasValue)
            indentSize = new ResolvedFormattingOption<int>(args.IndentSize.Value, FormattingOptionSource.CliArg);

        return new ResolvedFormattingOptions(
            CompatLevel: compatLevel,
            IndentStyle: indentStyle,
            IndentSize: indentSize,
            KeywordCasing: keywordCasing,
            BuiltInFunctionCasing: builtInFunctionCasing,
            DataTypeCasing: dataTypeCasing,
            SchemaCasing: schemaCasing,
            TableCasing: tableCasing,
            ColumnCasing: columnCasing,
            VariableCasing: variableCasing,
            CommaStyle: commaStyle,
            MaxLineLength: maxLineLength,
            InsertFinalNewline: insertFinalNewline,
            TrimTrailingWhitespace: trimTrailingWhitespace,
            NormalizeInlineSpacing: normalizeInlineSpacing
        )
        {
            ConfigPath = configPath,
            EditorConfigPath = editorConfigPath,
            ResolvedForPath = filePath
        };
    }

    private EditorConfigResult TryReadEditorConfigOptionsWithPath(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) ||
            string.Equals(filePath, "<stdin>", StringComparison.Ordinal))
        {
            return new EditorConfigResult(null, null);
        }

        // For directory paths, create a dummy .sql file path for .editorconfig lookup
        var lookupPath = filePath;
        if (Directory.Exists(filePath))
        {
            lookupPath = Path.Combine(filePath, "dummy.sql");
        }
        else if (!string.Equals(Path.GetExtension(filePath), ".sql", StringComparison.OrdinalIgnoreCase))
        {
            return new EditorConfigResult(null, null);
        }

        try
        {
            var parser = new EditorConfig.Core.EditorConfigParser();
            var fullPath = Path.GetFullPath(lookupPath);
            var config = parser.Parse(fullPath);
            if (config?.Properties is null)
            {
                return new EditorConfigResult(null, null);
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

            var indentStyleValue = ParseEditorConfigIndentStyle(properties);
            var indentSizeValue = ParseEditorConfigIndentSize(properties);
            if (indentStyleValue is null && indentSizeValue is null)
            {
                return new EditorConfigResult(null, null);
            }

            // Try to find the .editorconfig file path
            var editorConfigPath = FindEditorConfigPath(fullPath);

            return new EditorConfigResult(
                new EditorConfigFormattingOptions(indentStyleValue, indentSizeValue),
                editorConfigPath);
        }
        catch (Exception)
        {
            return new EditorConfigResult(null, null);
        }
    }

    private static string? FindEditorConfigPath(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        while (!string.IsNullOrEmpty(directory))
        {
            var editorConfigPath = Path.Combine(directory, ".editorconfig");
            if (File.Exists(editorConfigPath))
            {
                return editorConfigPath;
            }
            directory = Path.GetDirectoryName(directory);
        }
        return null;
    }

    private static IndentStyle? ParseEditorConfigIndentStyle(IReadOnlyDictionary<string, string> properties)
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

    private static int? ParseEditorConfigIndentSize(IReadOnlyDictionary<string, string> properties)
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
