namespace TsqlRefine.PluginSdk;

/// <summary>
/// Represents a text replacement at a specific location.
/// </summary>
/// <param name="Range">The source range to replace.</param>
/// <param name="NewText">The replacement text. Use empty string to delete the range.</param>
public sealed record TextEdit(Range Range, string NewText);

/// <summary>
/// Represents an auto-fix that can be applied to resolve a diagnostic.
/// </summary>
/// <param name="Title">A human-readable description of what this fix does.</param>
/// <param name="Edits">One or more text edits to apply. Edits should not overlap.</param>
public sealed record Fix(string Title, IReadOnlyList<TextEdit> Edits);

