namespace TsqlRefine.Rules.Helpers.Catalog;

/// <summary>Provides safe file identity comparison for catalog-backed rules.</summary>
internal static class CatalogFilePathHelpers
{
    internal static bool SameFile(string left, string right)
    {
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (string.Equals(left, right, comparison))
        {
            return true;
        }
        if (left.StartsWith('<') || right.StartsWith('<'))
        {
            return false;
        }

        try
        {
            return string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), comparison);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }
}
