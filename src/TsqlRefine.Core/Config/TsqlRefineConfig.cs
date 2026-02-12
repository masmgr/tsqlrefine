using System.Collections.Frozen;
using System.Text.Json;

namespace TsqlRefine.Core.Config;

/// <summary>
/// Result of a configuration load operation.
/// </summary>
public sealed record ConfigLoadResult<T>(
    bool Success,
    T? Value,
    string? ErrorMessage = null,
    Exception? Exception = null
) where T : class
{
    public static ConfigLoadResult<T> Ok(T value) => new(true, value);

    public static ConfigLoadResult<T> Fail(string message, Exception? exception = null) =>
        new(false, null, message, exception);
}

/// <summary>
/// Configuration for a plugin to be loaded.
/// </summary>
/// <param name="Path">The file system path to the plugin assembly.</param>
/// <param name="Enabled">Whether the plugin is enabled. Default is true.</param>
public sealed record PluginConfig(string Path, bool Enabled = true);

/// <summary>
/// Formatting configuration options for SQL code.
/// </summary>
/// <param name="IndentStyle">Indentation style: "spaces" or "tabs". Default is "spaces".</param>
/// <param name="IndentSize">Number of spaces per indent level. Default is 4.</param>
/// <param name="KeywordCasing">Casing for SQL keywords: "upper", "lower", "pascal", or "none". Default is "upper".</param>
/// <param name="FunctionCasing">Casing for built-in functions: "upper", "lower", "pascal", or "none". Default is "upper".</param>
/// <param name="DataTypeCasing">Casing for data types: "upper", "lower", "pascal", or "none". Default is "lower".</param>
/// <param name="SchemaCasing">Casing for schema names: "upper", "lower", "pascal", or "none". Default is "lower".</param>
/// <param name="TableCasing">Casing for table names: "upper", "lower", "pascal", or "none". Default is "upper".</param>
/// <param name="ColumnCasing">Casing for column names: "upper", "lower", "pascal", or "none". Default is "upper".</param>
/// <param name="VariableCasing">Casing for variables: "upper", "lower", "pascal", or "none". Default is "lower".</param>
/// <param name="SystemTableCasing">Casing for system tables (sys.*, information_schema.*): "upper", "lower", "pascal", or "none". Default is "lower".</param>
/// <param name="StoredProcedureCasing">Casing for stored procedures: "upper", "lower", "pascal", or "none". Default is "none".</param>
/// <param name="UserDefinedFunctionCasing">Casing for user-defined functions: "upper", "lower", "pascal", or "none". Default is "none".</param>
/// <param name="CommaStyle">Comma placement: "trailing" or "leading". Default is "trailing".</param>
/// <param name="MaxLineLength">Maximum line length (0 for no limit). Default is 0.</param>
/// <param name="InsertFinalNewline">Whether to insert a final newline. Default is true.</param>
/// <param name="TrimTrailingWhitespace">Whether to trim trailing whitespace. Default is true.</param>
/// <param name="NormalizeInlineSpacing">Whether to normalize inline spacing (space after commas). Default is true.</param>
/// <param name="NormalizeOperatorSpacing">Whether to normalize operator spacing. Default is true.</param>
/// <param name="NormalizeKeywordSpacing">Whether to normalize compound keyword spacing. Default is true.</param>
/// <param name="NormalizeFunctionSpacing">Whether to remove space between function name and opening parenthesis. Default is true.</param>
/// <param name="LineEnding">Line ending style: "auto", "lf", or "crlf". Default is "auto".</param>
/// <param name="MaxConsecutiveBlankLines">Maximum consecutive blank lines allowed (0 for no limit). Default is 0.</param>
/// <param name="TrimLeadingBlankLines">Whether to remove leading blank lines at the start of file. Default is true.</param>
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
    string SystemTableCasing = "lower",
    string StoredProcedureCasing = "none",
    string UserDefinedFunctionCasing = "none",
    string CommaStyle = "trailing",
    int MaxLineLength = 0,
    bool InsertFinalNewline = true,
    bool TrimTrailingWhitespace = true,
    bool NormalizeInlineSpacing = true,
    bool NormalizeOperatorSpacing = true,
    bool NormalizeKeywordSpacing = true,
    bool NormalizeFunctionSpacing = true,
    string LineEnding = "auto",
    int MaxConsecutiveBlankLines = 0,
    bool TrimLeadingBlankLines = true
);

/// <summary>
/// Main configuration for TsqlRefine behavior and analysis options.
/// </summary>
/// <param name="CompatLevel">SQL Server compatibility level (100-160). Default is 150 (SQL Server 2019).</param>
/// <param name="Ruleset">Path to a ruleset JSON file specifying which rules to enable.</param>
/// <param name="Preset">Name of a built-in preset ruleset (e.g. "recommended", "strict").</param>
/// <param name="Plugins">List of plugin configurations to load.</param>
/// <param name="Formatting">Formatting configuration options.</param>
/// <param name="Rules">Per-rule severity overrides. Keys are rule IDs, values are severity strings
/// ("error", "warning", "info", "inherit", "none").</param>
public sealed record TsqlRefineConfig(
    int CompatLevel = 150,
    string? Ruleset = null,
    string? Preset = null,
    IReadOnlyList<PluginConfig>? Plugins = null,
    FormattingConfig? Formatting = null,
    IReadOnlyDictionary<string, string>? Rules = null
)
{
    /// <summary>
    /// Valid SQL Server compatibility levels.
    /// </summary>
    public static readonly FrozenSet<int> ValidCompatLevels = FrozenSet.ToFrozenSet(
    [
        100, // SQL Server 2008
        110, // SQL Server 2012
        120, // SQL Server 2014
        130, // SQL Server 2016
        140, // SQL Server 2017
        150, // SQL Server 2019
        160  // SQL Server 2022
    ]);

    public static readonly TsqlRefineConfig Default = new();

    /// <summary>
    /// Loads configuration from the specified path.
    /// </summary>
    /// <exception cref="FileNotFoundException">The configuration file was not found.</exception>
    /// <exception cref="JsonException">The configuration file contains invalid JSON.</exception>
    /// <exception cref="ConfigValidationException">The configuration values are invalid.</exception>
    public static TsqlRefineConfig Load(string path)
    {
        var result = TryLoad(path);
        if (!result.Success)
        {
            throw result.Exception ?? new InvalidOperationException(result.ErrorMessage);
        }
        return result.Value!;
    }

    /// <summary>
    /// Attempts to load configuration from the specified path.
    /// </summary>
    public static ConfigLoadResult<TsqlRefineConfig> TryLoad(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return ConfigLoadResult<TsqlRefineConfig>.Fail(
                    $"Configuration file not found: {path}",
                    new FileNotFoundException("Configuration file not found.", path));
            }

            var json = File.ReadAllText(path);
            var config = JsonSerializer.Deserialize<TsqlRefineConfig>(json, JsonDefaults.Options);

            if (config is null)
            {
                return ConfigLoadResult<TsqlRefineConfig>.Fail("Failed to deserialize configuration: result was null.");
            }

            var validationError = config.Validate();
            if (validationError is not null)
            {
                return ConfigLoadResult<TsqlRefineConfig>.Fail(
                    validationError,
                    new ConfigValidationException(validationError));
            }

            return ConfigLoadResult<TsqlRefineConfig>.Ok(config);
        }
        catch (JsonException ex)
        {
            return ConfigLoadResult<TsqlRefineConfig>.Fail(
                $"Invalid JSON in configuration file: {ex.Message}",
                ex);
        }
        catch (IOException ex)
        {
            return ConfigLoadResult<TsqlRefineConfig>.Fail(
                $"Failed to read configuration file: {ex.Message}",
                ex);
        }
    }

    /// <summary>
    /// Validates the configuration and returns an error message if invalid, or null if valid.
    /// </summary>
    public string? Validate()
    {
        if (!ValidCompatLevels.Contains(CompatLevel))
        {
            var validValues = string.Join(", ", ValidCompatLevels.OrderBy(x => x));
            return $"Invalid compatLevel: {CompatLevel}. Valid values are: {validValues}.";
        }

        if (Formatting is not null)
        {
            if (Formatting.IndentSize < 1 || Formatting.IndentSize > 16)
            {
                return $"Invalid indentSize: {Formatting.IndentSize}. Must be between 1 and 16.";
            }

            if (Formatting.MaxLineLength < 0)
            {
                return $"Invalid maxLineLength: {Formatting.MaxLineLength}. Must be non-negative.";
            }
        }

        if (Rules is not null)
        {
            foreach (var (ruleId, severity) in Rules)
            {
                try
                {
                    Config.Ruleset.ParseSeverityLevel(severity);
                }
                catch (ConfigValidationException)
                {
                    return $"Invalid severity '{severity}' for rule '{ruleId}'. Valid values: error, warning, info, inherit, none.";
                }
            }
        }

        return null;
    }
}

/// <summary>
/// Exception thrown when configuration validation fails.
/// </summary>
public sealed class ConfigValidationException : Exception
{
    public ConfigValidationException(string message) : base(message) { }
    public ConfigValidationException(string message, Exception innerException) : base(message, innerException) { }
}

