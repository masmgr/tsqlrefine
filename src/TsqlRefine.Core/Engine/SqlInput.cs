namespace TsqlRefine.Core.Engine;

/// <summary>
/// Represents SQL input to be analyzed.
/// </summary>
/// <param name="FilePath">The path to the SQL file (may be a placeholder for stdin).</param>
/// <param name="Text">The SQL text content to analyze.</param>
public sealed record SqlInput(string FilePath, string Text);

