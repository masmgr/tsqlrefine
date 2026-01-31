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
    /// <summary>
    /// Valid SQL Server compatibility levels.
    /// </summary>
    public static readonly IReadOnlySet<int> ValidCompatLevels = new HashSet<int>
    {
        100, // SQL Server 2008
        110, // SQL Server 2012
        120, // SQL Server 2014
        130, // SQL Server 2016
        140, // SQL Server 2017
        150, // SQL Server 2019
        160  // SQL Server 2022
    };

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

