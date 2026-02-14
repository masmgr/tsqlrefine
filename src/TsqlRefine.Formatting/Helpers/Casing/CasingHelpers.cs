namespace TsqlRefine.Formatting.Helpers.Casing;

/// <summary>
/// Helper utilities for applying casing transformations to SQL text.
/// </summary>
public static class CasingHelpers
{
    /// <summary>
    /// Applies the specified ElementCasing transformation to text.
    /// </summary>
    /// <param name="text">The text to transform</param>
    /// <param name="casing">The casing style to apply</param>
    /// <returns>The transformed text</returns>
    public static string ApplyCasing(string text, ElementCasing casing)
    {
        return casing switch
        {
            ElementCasing.Upper => text.ToUpperInvariant(),
            ElementCasing.Lower => text.ToLowerInvariant(),
            ElementCasing.Pascal => ToPascalCase(text),
            ElementCasing.None => text,
            _ => text
        };
    }

    private static string ToPascalCase(string text)
    {
        if (text.Length == 0)
        {
            return text;
        }

        var parts = text.Split('_');

        if (parts.Length == 1)
        {
            return string.Concat(char.ToUpperInvariant(text[0]), text[1..].ToLowerInvariant());
        }

        var builder = new System.Text.StringBuilder(text.Length);

        foreach (var part in parts)
        {
            if (part.Length == 0)
            {
                continue;
            }

            builder.Append(char.ToUpperInvariant(part[0]));
            builder.Append(part[1..].ToLowerInvariant());
        }

        return builder.ToString();
    }
}
