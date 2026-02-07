namespace TsqlRefine.Cli;

/// <summary>
/// Exception thrown when configuration loading or validation fails.
/// </summary>
public sealed class ConfigException(string message) : Exception(message);

