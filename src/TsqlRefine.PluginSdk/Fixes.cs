namespace TsqlRefine.PluginSdk;

public sealed record TextEdit(Range Range, string NewText);

public sealed record Fix(string Title, IReadOnlyList<TextEdit> Edits);

