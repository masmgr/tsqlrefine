namespace TsqlRefine.Cli;

/// <summary>
/// Exit codes used by the CLI application to indicate different failure modes.
/// </summary>
public static class ExitCodes
{
    /// <summary>Exit code 1: Rule violations were found.</summary>
    public const int Violations = 1;

    /// <summary>Exit code 2: Analysis error occurred (parsing failure, etc.).</summary>
    public const int AnalysisError = 2;

    /// <summary>Exit code 3: Configuration error (invalid config file, etc.).</summary>
    public const int ConfigError = 3;

    /// <summary>Exit code 4: Fatal error (unexpected exception, etc.).</summary>
    public const int Fatal = 4;
}

