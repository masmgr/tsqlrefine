using TsqlRefine.Cli.Services;
using TsqlRefine.Formatting;

namespace TsqlRefine.Cli.Tests.Services;

public sealed class ResolvedFormattingOptionsBuilderTests
{
    [Fact]
    public void Build_WithDefaults_ReturnsAllDefaultValues()
    {
        var builder = new ResolvedFormattingOptionsBuilder();
        var defaults = new FormattingOptions();

        var result = builder.Build();

        Assert.Equal(defaults.CompatLevel, result.CompatLevel.Value);
        Assert.Equal(FormattingOptionSource.Default, result.CompatLevel.Source);

        Assert.Equal(defaults.IndentStyle, result.IndentStyle.Value);
        Assert.Equal(FormattingOptionSource.Default, result.IndentStyle.Source);

        Assert.Equal(defaults.IndentSize, result.IndentSize.Value);
        Assert.Equal(FormattingOptionSource.Default, result.IndentSize.Source);

        Assert.Equal(defaults.KeywordElementCasing, result.KeywordCasing.Value);
        Assert.Equal(FormattingOptionSource.Default, result.KeywordCasing.Source);

        Assert.Equal(defaults.BuiltInFunctionCasing, result.BuiltInFunctionCasing.Value);
        Assert.Equal(FormattingOptionSource.Default, result.BuiltInFunctionCasing.Source);

        Assert.Equal(defaults.DataTypeCasing, result.DataTypeCasing.Value);
        Assert.Equal(FormattingOptionSource.Default, result.DataTypeCasing.Source);

        Assert.Equal(defaults.SchemaCasing, result.SchemaCasing.Value);
        Assert.Equal(FormattingOptionSource.Default, result.SchemaCasing.Source);

        Assert.Equal(defaults.TableCasing, result.TableCasing.Value);
        Assert.Equal(FormattingOptionSource.Default, result.TableCasing.Source);

        Assert.Equal(defaults.ColumnCasing, result.ColumnCasing.Value);
        Assert.Equal(FormattingOptionSource.Default, result.ColumnCasing.Source);

        Assert.Equal(defaults.VariableCasing, result.VariableCasing.Value);
        Assert.Equal(FormattingOptionSource.Default, result.VariableCasing.Source);

        Assert.Equal(defaults.CommaStyle, result.CommaStyle.Value);
        Assert.Equal(FormattingOptionSource.Default, result.CommaStyle.Source);

        Assert.Equal(defaults.MaxLineLength, result.MaxLineLength.Value);
        Assert.Equal(FormattingOptionSource.Default, result.MaxLineLength.Source);

        Assert.Equal(defaults.InsertFinalNewline, result.InsertFinalNewline.Value);
        Assert.Equal(FormattingOptionSource.Default, result.InsertFinalNewline.Source);

        Assert.Equal(defaults.TrimTrailingWhitespace, result.TrimTrailingWhitespace.Value);
        Assert.Equal(FormattingOptionSource.Default, result.TrimTrailingWhitespace.Source);

        Assert.Equal(defaults.NormalizeInlineSpacing, result.NormalizeInlineSpacing.Value);
        Assert.Equal(FormattingOptionSource.Default, result.NormalizeInlineSpacing.Source);

        Assert.Equal(defaults.LineEnding, result.LineEnding.Value);
        Assert.Equal(FormattingOptionSource.Default, result.LineEnding.Source);
    }

    [Fact]
    public void ApplyConfig_OverridesValuesFromDefaults()
    {
        var builder = new ResolvedFormattingOptionsBuilder();
        var configOptions = new FormattingOptions
        {
            IndentStyle = IndentStyle.Tabs,
            IndentSize = 2,
            KeywordElementCasing = ElementCasing.Lower
        };

        builder.ApplyConfig(configOptions);
        var result = builder.Build();

        Assert.Equal(IndentStyle.Tabs, result.IndentStyle.Value);
        Assert.Equal(FormattingOptionSource.Config, result.IndentStyle.Source);

        Assert.Equal(2, result.IndentSize.Value);
        Assert.Equal(FormattingOptionSource.Config, result.IndentSize.Source);

        Assert.Equal(ElementCasing.Lower, result.KeywordCasing.Value);
        Assert.Equal(FormattingOptionSource.Config, result.KeywordCasing.Source);
    }

    [Fact]
    public void ApplyConfig_OnlyOverridesDifferentValues()
    {
        var builder = new ResolvedFormattingOptionsBuilder();
        var defaults = new FormattingOptions();
        var configOptions = new FormattingOptions
        {
            IndentStyle = defaults.IndentStyle, // Same as default
            IndentSize = 2 // Different from default
        };

        builder.ApplyConfig(configOptions);
        var result = builder.Build();

        // Same as default - should stay as Default source
        Assert.Equal(defaults.IndentStyle, result.IndentStyle.Value);
        Assert.Equal(FormattingOptionSource.Default, result.IndentStyle.Source);

        // Different from default - should be Config source
        Assert.Equal(2, result.IndentSize.Value);
        Assert.Equal(FormattingOptionSource.Config, result.IndentSize.Source);
    }

    [Fact]
    public void ApplyEditorConfig_OverridesIndentStyle()
    {
        var builder = new ResolvedFormattingOptionsBuilder();

        builder.ApplyEditorConfig(IndentStyle.Tabs, null, null);
        var result = builder.Build();

        Assert.Equal(IndentStyle.Tabs, result.IndentStyle.Value);
        Assert.Equal(FormattingOptionSource.EditorConfig, result.IndentStyle.Source);
    }

    [Fact]
    public void ApplyEditorConfig_OverridesIndentSize()
    {
        var builder = new ResolvedFormattingOptionsBuilder();

        builder.ApplyEditorConfig(null, 2, null);
        var result = builder.Build();

        Assert.Equal(2, result.IndentSize.Value);
        Assert.Equal(FormattingOptionSource.EditorConfig, result.IndentSize.Source);
    }

    [Fact]
    public void ApplyEditorConfig_OverridesLineEnding()
    {
        var builder = new ResolvedFormattingOptionsBuilder();

        builder.ApplyEditorConfig(null, null, LineEnding.Lf);
        var result = builder.Build();

        Assert.Equal(LineEnding.Lf, result.LineEnding.Value);
        Assert.Equal(FormattingOptionSource.EditorConfig, result.LineEnding.Source);
    }

    [Fact]
    public void ApplyEditorConfig_WithNullValues_DoesNotOverride()
    {
        var builder = new ResolvedFormattingOptionsBuilder();
        var defaults = new FormattingOptions();

        builder.ApplyEditorConfig(null, null, null);
        var result = builder.Build();

        Assert.Equal(defaults.IndentStyle, result.IndentStyle.Value);
        Assert.Equal(FormattingOptionSource.Default, result.IndentStyle.Source);

        Assert.Equal(defaults.IndentSize, result.IndentSize.Value);
        Assert.Equal(FormattingOptionSource.Default, result.IndentSize.Source);

        Assert.Equal(defaults.LineEnding, result.LineEnding.Value);
        Assert.Equal(FormattingOptionSource.Default, result.LineEnding.Source);
    }

    [Fact]
    public void ApplyCliArgs_HasHighestPriority()
    {
        var builder = new ResolvedFormattingOptionsBuilder();
        var configOptions = new FormattingOptions
        {
            IndentStyle = IndentStyle.Tabs,
            IndentSize = 2,
            LineEnding = LineEnding.Lf
        };

        builder
            .ApplyConfig(configOptions)
            .ApplyEditorConfig(IndentStyle.Spaces, 8, LineEnding.CrLf)
            .ApplyCliArgs(IndentStyle.Tabs, 4, LineEnding.Lf);

        var result = builder.Build();

        Assert.Equal(IndentStyle.Tabs, result.IndentStyle.Value);
        Assert.Equal(FormattingOptionSource.CliArg, result.IndentStyle.Source);

        Assert.Equal(4, result.IndentSize.Value);
        Assert.Equal(FormattingOptionSource.CliArg, result.IndentSize.Source);

        Assert.Equal(LineEnding.Lf, result.LineEnding.Value);
        Assert.Equal(FormattingOptionSource.CliArg, result.LineEnding.Source);
    }

    [Fact]
    public void ApplyCliArgs_WithNullValues_DoesNotOverride()
    {
        var builder = new ResolvedFormattingOptionsBuilder();

        builder
            .ApplyEditorConfig(IndentStyle.Tabs, 2, LineEnding.Lf)
            .ApplyCliArgs(null, null, null);

        var result = builder.Build();

        Assert.Equal(IndentStyle.Tabs, result.IndentStyle.Value);
        Assert.Equal(FormattingOptionSource.EditorConfig, result.IndentStyle.Source);

        Assert.Equal(2, result.IndentSize.Value);
        Assert.Equal(FormattingOptionSource.EditorConfig, result.IndentSize.Source);

        Assert.Equal(LineEnding.Lf, result.LineEnding.Value);
        Assert.Equal(FormattingOptionSource.EditorConfig, result.LineEnding.Source);
    }

    [Fact]
    public void Build_TracksConfigPath()
    {
        var builder = new ResolvedFormattingOptionsBuilder
        {
            ConfigPath = "/path/to/tsqlrefine.json"
        };

        var result = builder.Build();

        Assert.Equal("/path/to/tsqlrefine.json", result.ConfigPath);
    }

    [Fact]
    public void Build_TracksEditorConfigPath()
    {
        var builder = new ResolvedFormattingOptionsBuilder
        {
            EditorConfigPath = "/path/to/.editorconfig"
        };

        var result = builder.Build();

        Assert.Equal("/path/to/.editorconfig", result.EditorConfigPath);
    }

    [Fact]
    public void Build_TracksResolvedForPath()
    {
        var builder = new ResolvedFormattingOptionsBuilder
        {
            ResolvedForPath = "/path/to/query.sql"
        };

        var result = builder.Build();

        Assert.Equal("/path/to/query.sql", result.ResolvedForPath);
    }

    [Fact]
    public void FluentApi_ChainsCorrectly()
    {
        var builder = new ResolvedFormattingOptionsBuilder();
        var configOptions = new FormattingOptions { IndentSize = 2 };

        var returnedBuilder = builder
            .ApplyConfig(configOptions)
            .ApplyEditorConfig(IndentStyle.Tabs, null, null)
            .ApplyCliArgs(null, 4, null);

        Assert.Same(builder, returnedBuilder);
    }

    [Fact]
    public void LayeringPriority_ConfigThenEditorConfigThenCliArgs()
    {
        var builder = new ResolvedFormattingOptionsBuilder();
        var configOptions = new FormattingOptions
        {
            IndentStyle = IndentStyle.Tabs,
            IndentSize = 2,
            KeywordElementCasing = ElementCasing.Lower
        };

        builder
            .ApplyConfig(configOptions)
            .ApplyEditorConfig(IndentStyle.Spaces, 8, null)
            .ApplyCliArgs(null, 4, null);

        var result = builder.Build();

        // IndentStyle: Config -> EditorConfig (overrides)
        Assert.Equal(IndentStyle.Spaces, result.IndentStyle.Value);
        Assert.Equal(FormattingOptionSource.EditorConfig, result.IndentStyle.Source);

        // IndentSize: Config -> EditorConfig -> CliArg (overrides)
        Assert.Equal(4, result.IndentSize.Value);
        Assert.Equal(FormattingOptionSource.CliArg, result.IndentSize.Source);

        // KeywordCasing: Only Config (no override from EditorConfig or CliArg)
        Assert.Equal(ElementCasing.Lower, result.KeywordCasing.Value);
        Assert.Equal(FormattingOptionSource.Config, result.KeywordCasing.Source);
    }
}
