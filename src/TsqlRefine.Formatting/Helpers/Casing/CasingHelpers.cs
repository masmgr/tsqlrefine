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
            ElementCasing.None => text,
            _ => text
        };
    }
}
