using TsqlRefine.Core;
using TsqlRefine.Core.Config;
using TsqlRefine.Core.Engine;
using TsqlRefine.Formatting;

namespace TsqlRefine.Cli.Services;

public sealed class FormattingOptionsResolver
{
    private readonly ConfigLoader _configLoader;
    private readonly EditorConfigReader _editorConfigReader = new();

    public FormattingOptionsResolver(ConfigLoader configLoader)
    {
        _configLoader = configLoader;
    }

    public FormattingOptions ResolveFormattingOptions(CliArgs args, SqlInput input)
    {
        // Priority: CLI args > .editorconfig > tsqlrefine.json > defaults
        var options = new FormattingOptions();

        // Load from tsqlrefine.json if available
        options = TryApplyConfigOptions(args, options);

        // Override with .editorconfig
        var editorConfig = _editorConfigReader.TryRead(input.FilePath);
        options = ApplyEditorConfigOptions(options, editorConfig);

        // Override with CLI args
        return ApplyCliArgOptions(options, args);
    }

    /// <summary>
    /// Resolves formatting options with source tracking for display purposes.
    /// </summary>
    public ResolvedFormattingOptions ResolveFormattingOptionsWithSources(CliArgs args, string? filePath)
    {
        var builder = new ResolvedFormattingOptionsBuilder
        {
            ResolvedForPath = filePath
        };

        // Layer tsqlrefine.json values
        TryApplyConfigToBuilder(args, builder);

        // Layer .editorconfig values
        var editorConfig = _editorConfigReader.TryRead(filePath);
        if (editorConfig.Path is not null)
        {
            builder.EditorConfigPath = editorConfig.Path;
            builder.ApplyEditorConfig(editorConfig.IndentStyle, editorConfig.IndentSize, editorConfig.LineEnding);
        }

        // Layer CLI args
        builder.ApplyCliArgs(args.IndentStyle, args.IndentSize, args.LineEnding);

        return builder.Build();
    }

    private FormattingOptions TryApplyConfigOptions(CliArgs args, FormattingOptions options)
    {
        try
        {
            var config = _configLoader.LoadConfig(args);
            if (config.Formatting is not null)
            {
                return FormattingConfigMapper.ToFormattingOptions(config.Formatting);
            }
        }
        catch (ConfigException)
        {
            // Config load failure is expected when no config file exists - use defaults
        }
        return options;
    }

    private void TryApplyConfigToBuilder(CliArgs args, ResolvedFormattingOptionsBuilder builder)
    {
        try
        {
            var config = _configLoader.LoadConfig(args);
            builder.ConfigPath = _configLoader.GetConfigPath(args);

            if (config.Formatting is not null)
            {
                var configOptions = FormattingConfigMapper.ToFormattingOptions(config.Formatting);
                builder.ApplyConfig(configOptions);
            }
        }
        catch (ConfigException)
        {
            // Config load failure is expected when no config file exists
        }
    }

    private static FormattingOptions ApplyEditorConfigOptions(
        FormattingOptions options,
        EditorConfigReader.EditorConfigResult editorConfig)
    {
        if (editorConfig.IndentStyle is null && editorConfig.IndentSize is null && editorConfig.LineEnding is null)
        {
            return options;
        }

        return options with
        {
            IndentStyle = editorConfig.IndentStyle ?? options.IndentStyle,
            IndentSize = editorConfig.IndentSize ?? options.IndentSize,
            LineEnding = editorConfig.LineEnding ?? options.LineEnding
        };
    }

    private static FormattingOptions ApplyCliArgOptions(FormattingOptions options, CliArgs args)
    {
        return options with
        {
            IndentStyle = args.IndentStyle ?? options.IndentStyle,
            IndentSize = args.IndentSize ?? options.IndentSize,
            LineEnding = args.LineEnding ?? options.LineEnding
        };
    }
}
