using TsqlRefine.Formatting;

namespace TsqlRefine.Cli.Services;

/// <summary>
/// Reads formatting-related options from .editorconfig files.
/// </summary>
internal sealed class EditorConfigReader
{
    /// <summary>
    /// Result of reading .editorconfig options.
    /// </summary>
    public sealed record EditorConfigResult(
        IndentStyle? IndentStyle,
        int? IndentSize,
        LineEnding? LineEnding,
        string? Path);

    /// <summary>
    /// Tries to read formatting options from .editorconfig for the given file path.
    /// </summary>
    public EditorConfigResult TryRead(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) ||
            string.Equals(filePath, "<stdin>", StringComparison.Ordinal))
        {
            return new EditorConfigResult(null, null, null, null);
        }

        // For directory paths, create a dummy .sql file path for .editorconfig lookup
        var lookupPath = filePath;
        if (Directory.Exists(filePath))
        {
            lookupPath = Path.Combine(filePath, "dummy.sql");
        }
        else if (!string.Equals(Path.GetExtension(filePath), ".sql", StringComparison.OrdinalIgnoreCase))
        {
            return new EditorConfigResult(null, null, null, null);
        }

        try
        {
            var parser = new EditorConfig.Core.EditorConfigParser();
            var fullPath = Path.GetFullPath(lookupPath);
            var config = parser.Parse(fullPath);
            if (config?.Properties is null)
            {
                return new EditorConfigResult(null, null, null, null);
            }

            var properties = NormalizeProperties(config.Properties);
            var indentStyleValue = ParseIndentStyle(properties);
            var indentSizeValue = ParseIndentSize(properties);
            var lineEndingValue = ParseLineEnding(properties);

            if (indentStyleValue is null && indentSizeValue is null && lineEndingValue is null)
            {
                return new EditorConfigResult(null, null, null, null);
            }

            var editorConfigPath = FindEditorConfigPath(fullPath);
            return new EditorConfigResult(indentStyleValue, indentSizeValue, lineEndingValue, editorConfigPath);
        }
        catch (Exception)
        {
            return new EditorConfigResult(null, null, null, null);
        }
    }

    private static Dictionary<string, string> NormalizeProperties(IEnumerable<KeyValuePair<string, string>> properties)
    {
        var normalized = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in properties)
        {
            if (!string.IsNullOrWhiteSpace(entry.Key))
            {
                normalized[entry.Key] = entry.Value ?? string.Empty;
            }
        }
        return normalized;
    }

    private static string? FindEditorConfigPath(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        while (!string.IsNullOrEmpty(directory))
        {
            var editorConfigPath = Path.Combine(directory, ".editorconfig");
            if (File.Exists(editorConfigPath))
            {
                return editorConfigPath;
            }
            directory = Path.GetDirectoryName(directory);
        }
        return null;
    }

    private static IndentStyle? ParseIndentStyle(IReadOnlyDictionary<string, string> properties)
    {
        if (!properties.TryGetValue("indent_style", out var styleValue))
        {
            return null;
        }

        return styleValue.Trim().ToLowerInvariant() switch
        {
            "tab" => IndentStyle.Tabs,
            "space" => IndentStyle.Spaces,
            _ => null
        };
    }

    private static int? ParseIndentSize(IReadOnlyDictionary<string, string> properties)
    {
        if (!properties.TryGetValue("indent_size", out var sizeValue) || string.IsNullOrWhiteSpace(sizeValue))
        {
            return null;
        }

        sizeValue = sizeValue.Trim();
        if (string.Equals(sizeValue, "tab", StringComparison.OrdinalIgnoreCase))
        {
            if (properties.TryGetValue("tab_width", out var tabWidthValue) &&
                int.TryParse(tabWidthValue.Trim(), out var tabWidth) &&
                tabWidth > 0)
            {
                return tabWidth;
            }

            return null;
        }

        return int.TryParse(sizeValue, out var parsed) && parsed > 0 ? parsed : null;
    }

    private static LineEnding? ParseLineEnding(IReadOnlyDictionary<string, string> properties)
    {
        if (!properties.TryGetValue("end_of_line", out var value))
        {
            return null;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "lf" => LineEnding.Lf,
            "crlf" => LineEnding.CrLf,
            "cr" => LineEnding.Lf, // CR-only is rare, normalize to LF
            _ => null
        };
    }
}
