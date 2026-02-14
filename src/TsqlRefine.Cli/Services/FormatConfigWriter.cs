using System.Globalization;
using System.Text.Json;
using TsqlRefine.Core;

namespace TsqlRefine.Cli.Services;

/// <summary>
/// Writes formatting configuration output in text or JSON format.
/// </summary>
public static class FormatConfigWriter
{
    public static async Task WriteJsonAsync(TextWriter stdout, ResolvedFormattingOptions resolved)
    {
        var output = new
        {
            options = new
            {
                compatLevel = new { value = resolved.CompatLevel.Value, source = FormatSource(resolved.CompatLevel.Source) },
                indentStyle = new { value = resolved.IndentStyle.Value.ToString().ToLowerInvariant(), source = FormatSource(resolved.IndentStyle.Source) },
                indentSize = new { value = resolved.IndentSize.Value, source = FormatSource(resolved.IndentSize.Source) },
                keywordCasing = new { value = resolved.KeywordCasing.Value.ToString().ToLowerInvariant(), source = FormatSource(resolved.KeywordCasing.Source) },
                builtInFunctionCasing = new { value = resolved.BuiltInFunctionCasing.Value.ToString().ToLowerInvariant(), source = FormatSource(resolved.BuiltInFunctionCasing.Source) },
                dataTypeCasing = new { value = resolved.DataTypeCasing.Value.ToString().ToLowerInvariant(), source = FormatSource(resolved.DataTypeCasing.Source) },
                schemaCasing = new { value = resolved.SchemaCasing.Value.ToString().ToLowerInvariant(), source = FormatSource(resolved.SchemaCasing.Source) },
                tableCasing = new { value = resolved.TableCasing.Value.ToString().ToLowerInvariant(), source = FormatSource(resolved.TableCasing.Source) },
                columnCasing = new { value = resolved.ColumnCasing.Value.ToString().ToLowerInvariant(), source = FormatSource(resolved.ColumnCasing.Source) },
                variableCasing = new { value = resolved.VariableCasing.Value.ToString().ToLowerInvariant(), source = FormatSource(resolved.VariableCasing.Source) },
                systemTableCasing = new { value = resolved.SystemTableCasing.Value.ToString().ToLowerInvariant(), source = FormatSource(resolved.SystemTableCasing.Source) },
                storedProcedureCasing = new { value = resolved.StoredProcedureCasing.Value.ToString().ToLowerInvariant(), source = FormatSource(resolved.StoredProcedureCasing.Source) },
                userDefinedFunctionCasing = new { value = resolved.UserDefinedFunctionCasing.Value.ToString().ToLowerInvariant(), source = FormatSource(resolved.UserDefinedFunctionCasing.Source) },
                commaStyle = new { value = resolved.CommaStyle.Value.ToString().ToLowerInvariant(), source = FormatSource(resolved.CommaStyle.Source) },
                maxLineLength = new { value = resolved.MaxLineLength.Value, source = FormatSource(resolved.MaxLineLength.Source) },
                insertFinalNewline = new { value = resolved.InsertFinalNewline.Value, source = FormatSource(resolved.InsertFinalNewline.Source) },
                trimTrailingWhitespace = new { value = resolved.TrimTrailingWhitespace.Value, source = FormatSource(resolved.TrimTrailingWhitespace.Source) },
                normalizeInlineSpacing = new { value = resolved.NormalizeInlineSpacing.Value, source = FormatSource(resolved.NormalizeInlineSpacing.Source) },
                normalizeOperatorSpacing = new { value = resolved.NormalizeOperatorSpacing.Value, source = FormatSource(resolved.NormalizeOperatorSpacing.Source) },
                normalizeKeywordSpacing = new { value = resolved.NormalizeKeywordSpacing.Value, source = FormatSource(resolved.NormalizeKeywordSpacing.Source) },
                normalizeFunctionSpacing = new { value = resolved.NormalizeFunctionSpacing.Value, source = FormatSource(resolved.NormalizeFunctionSpacing.Source) },
                lineEnding = new { value = resolved.LineEnding.Value.ToString().ToLowerInvariant(), source = FormatSource(resolved.LineEnding.Source) },
                maxConsecutiveBlankLines = new { value = resolved.MaxConsecutiveBlankLines.Value, source = FormatSource(resolved.MaxConsecutiveBlankLines.Source) },
                trimLeadingBlankLines = new { value = resolved.TrimLeadingBlankLines.Value, source = FormatSource(resolved.TrimLeadingBlankLines.Source) }
            },
            sourcePaths = new
            {
                config = resolved.ConfigPath,
                editorconfig = resolved.EditorConfigPath
            },
            resolvedForPath = resolved.ResolvedForPath
        };

        var json = JsonSerializer.Serialize(output, JsonDefaults.Options);
        await stdout.WriteLineAsync(json);
    }

    public static async Task WriteTextAsync(TextWriter stdout, ResolvedFormattingOptions resolved, bool showSources)
    {
        await stdout.WriteLineAsync("Effective Formatting Options:");

        await WriteOptionLineAsync(stdout, "compatLevel", resolved.CompatLevel.Value.ToString(CultureInfo.InvariantCulture), resolved.CompatLevel.Source, showSources);
        await WriteOptionLineAsync(stdout, "indentStyle", resolved.IndentStyle.Value.ToString().ToLowerInvariant(), resolved.IndentStyle.Source, showSources);
        await WriteOptionLineAsync(stdout, "indentSize", resolved.IndentSize.Value.ToString(CultureInfo.InvariantCulture), resolved.IndentSize.Source, showSources);
        await WriteOptionLineAsync(stdout, "keywordCasing", resolved.KeywordCasing.Value.ToString().ToLowerInvariant(), resolved.KeywordCasing.Source, showSources);
        await WriteOptionLineAsync(stdout, "builtInFunctionCasing", resolved.BuiltInFunctionCasing.Value.ToString().ToLowerInvariant(), resolved.BuiltInFunctionCasing.Source, showSources);
        await WriteOptionLineAsync(stdout, "dataTypeCasing", resolved.DataTypeCasing.Value.ToString().ToLowerInvariant(), resolved.DataTypeCasing.Source, showSources);
        await WriteOptionLineAsync(stdout, "schemaCasing", resolved.SchemaCasing.Value.ToString().ToLowerInvariant(), resolved.SchemaCasing.Source, showSources);
        await WriteOptionLineAsync(stdout, "tableCasing", resolved.TableCasing.Value.ToString().ToLowerInvariant(), resolved.TableCasing.Source, showSources);
        await WriteOptionLineAsync(stdout, "columnCasing", resolved.ColumnCasing.Value.ToString().ToLowerInvariant(), resolved.ColumnCasing.Source, showSources);
        await WriteOptionLineAsync(stdout, "variableCasing", resolved.VariableCasing.Value.ToString().ToLowerInvariant(), resolved.VariableCasing.Source, showSources);
        await WriteOptionLineAsync(stdout, "systemTableCasing", resolved.SystemTableCasing.Value.ToString().ToLowerInvariant(), resolved.SystemTableCasing.Source, showSources);
        await WriteOptionLineAsync(stdout, "storedProcedureCasing", resolved.StoredProcedureCasing.Value.ToString().ToLowerInvariant(), resolved.StoredProcedureCasing.Source, showSources);
        await WriteOptionLineAsync(stdout, "userDefinedFunctionCasing", resolved.UserDefinedFunctionCasing.Value.ToString().ToLowerInvariant(), resolved.UserDefinedFunctionCasing.Source, showSources);
        await WriteOptionLineAsync(stdout, "commaStyle", resolved.CommaStyle.Value.ToString().ToLowerInvariant(), resolved.CommaStyle.Source, showSources);
        await WriteOptionLineAsync(stdout, "maxLineLength", resolved.MaxLineLength.Value.ToString(CultureInfo.InvariantCulture), resolved.MaxLineLength.Source, showSources);
        await WriteOptionLineAsync(stdout, "insertFinalNewline", resolved.InsertFinalNewline.Value.ToString().ToLowerInvariant(), resolved.InsertFinalNewline.Source, showSources);
        await WriteOptionLineAsync(stdout, "trimTrailingWhitespace", resolved.TrimTrailingWhitespace.Value.ToString().ToLowerInvariant(), resolved.TrimTrailingWhitespace.Source, showSources);
        await WriteOptionLineAsync(stdout, "normalizeInlineSpacing", resolved.NormalizeInlineSpacing.Value.ToString().ToLowerInvariant(), resolved.NormalizeInlineSpacing.Source, showSources);
        await WriteOptionLineAsync(stdout, "normalizeOperatorSpacing", resolved.NormalizeOperatorSpacing.Value.ToString().ToLowerInvariant(), resolved.NormalizeOperatorSpacing.Source, showSources);
        await WriteOptionLineAsync(stdout, "normalizeKeywordSpacing", resolved.NormalizeKeywordSpacing.Value.ToString().ToLowerInvariant(), resolved.NormalizeKeywordSpacing.Source, showSources);
        await WriteOptionLineAsync(stdout, "normalizeFunctionSpacing", resolved.NormalizeFunctionSpacing.Value.ToString().ToLowerInvariant(), resolved.NormalizeFunctionSpacing.Source, showSources);
        await WriteOptionLineAsync(stdout, "lineEnding", resolved.LineEnding.Value.ToString().ToLowerInvariant(), resolved.LineEnding.Source, showSources);
        await WriteOptionLineAsync(stdout, "maxConsecutiveBlankLines", resolved.MaxConsecutiveBlankLines.Value.ToString(CultureInfo.InvariantCulture), resolved.MaxConsecutiveBlankLines.Source, showSources);
        await WriteOptionLineAsync(stdout, "trimLeadingBlankLines", resolved.TrimLeadingBlankLines.Value.ToString().ToLowerInvariant(), resolved.TrimLeadingBlankLines.Source, showSources);

        if (showSources)
        {
            await stdout.WriteLineAsync();
            await stdout.WriteLineAsync("Sources:");
            if (resolved.ConfigPath is not null)
                await stdout.WriteLineAsync($"  config:       {resolved.ConfigPath}");
            if (resolved.EditorConfigPath is not null)
                await stdout.WriteLineAsync($"  editorconfig: {resolved.EditorConfigPath}");
            if (resolved.ResolvedForPath is not null)
                await stdout.WriteLineAsync($"  resolvedFor:  {resolved.ResolvedForPath}");
        }
    }

    private static string FormatSource(FormattingOptionSource source) => source switch
    {
        FormattingOptionSource.Default => "default",
        FormattingOptionSource.Config => "config",
        FormattingOptionSource.EditorConfig => "editorconfig",
        FormattingOptionSource.CliArg => "cli",
        _ => "unknown"
    };

    private static async Task WriteOptionLineAsync(TextWriter stdout, string name, string value, FormattingOptionSource source, bool showSources)
    {
        var paddedName = name.PadRight(30);
        var paddedValue = value.PadRight(12);

        if (showSources)
        {
            var sourceText = source switch
            {
                FormattingOptionSource.Default => "(default)",
                FormattingOptionSource.Config => "(tsqlrefine.json)",
                FormattingOptionSource.EditorConfig => "(.editorconfig)",
                FormattingOptionSource.CliArg => "(CLI arg)",
                _ => ""
            };
            await stdout.WriteLineAsync($"  {paddedName}{paddedValue}{sourceText}");
        }
        else
        {
            await stdout.WriteLineAsync($"  {paddedName}{value}");
        }
    }
}
