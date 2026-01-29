namespace TsqlRefine.Formatting.Helpers;

/// <summary>
/// Helper utilities for applying casing transformations to SQL text.
/// Eliminates code duplication between KeywordCasing and IdentifierCasing.
/// </summary>
public static class CasingHelpers
{
    /// <summary>
    /// Applies the specified casing transformation to text.
    /// Supports KeywordCasing, IdentifierCasing, and ElementCasing enum types.
    /// </summary>
    /// <typeparam name="T">The casing enum type</typeparam>
    /// <param name="text">The text to transform</param>
    /// <param name="casing">The casing style to apply</param>
    /// <returns>The transformed text</returns>
    public static string ApplyCasing<T>(string text, T casing) where T : Enum
    {
        return casing switch
        {
            // Handle KeywordCasing
            KeywordCasing keywordCasing => keywordCasing switch
            {
                KeywordCasing.Upper => text.ToUpperInvariant(),
                KeywordCasing.Lower => text.ToLowerInvariant(),
                KeywordCasing.Pascal => ToPascalCase(text),
                KeywordCasing.Preserve => text,
                _ => text
            },

            // Handle IdentifierCasing
            IdentifierCasing identifierCasing => identifierCasing switch
            {
                IdentifierCasing.Upper => text.ToUpperInvariant(),
                IdentifierCasing.Lower => text.ToLowerInvariant(),
                IdentifierCasing.Pascal => ToPascalCase(text),
                IdentifierCasing.Camel => ToCamelCase(text),
                IdentifierCasing.Preserve => text,
                _ => text
            },

            // Handle ElementCasing
            ElementCasing elementCasing => elementCasing switch
            {
                ElementCasing.Upper => text.ToUpperInvariant(),
                ElementCasing.Lower => text.ToLowerInvariant(),
                ElementCasing.None => text,
                _ => text
            },

            _ => text
        };
    }

    /// <summary>
    /// Converts text to PascalCase (first letter uppercase, rest lowercase).
    /// </summary>
    /// <param name="text">The text to convert</param>
    /// <returns>PascalCase text</returns>
    public static string ToPascalCase(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        var lower = text.ToLowerInvariant();
        return char.ToUpperInvariant(lower[0]) + lower[1..];
    }

    /// <summary>
    /// Converts text to camelCase (first letter lowercase, rest lowercase).
    /// </summary>
    /// <param name="text">The text to convert</param>
    /// <returns>camelCase text</returns>
    public static string ToCamelCase(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        var lower = text.ToLowerInvariant();
        return char.ToLowerInvariant(lower[0]) + lower[1..];
    }
}
