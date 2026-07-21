using System.Globalization;
using TsqlRefine.Schema.Snapshot;

namespace TsqlRefine.Schema.Resolution;

/// <summary>
/// Compares SQL identifiers using the database collation metadata captured in a schema snapshot.
/// </summary>
internal sealed class SqlIdentifierComparer : IEqualityComparer<string>
{
    private const int IgnoreCaseComparisonStyle = 1;
    private const int IgnoreAccentComparisonStyle = 2;
    private const int IgnoreKanaComparisonStyle = 65536;
    private const int IgnoreWidthComparisonStyle = 131072;

    private readonly CompareInfo? _compareInfo;
    private readonly CompareOptions _options;
    private readonly StringComparer? _ordinalComparer;

    private SqlIdentifierComparer(CompareInfo compareInfo, CompareOptions options)
    {
        _compareInfo = compareInfo;
        _options = options;
    }

    private SqlIdentifierComparer(StringComparer ordinalComparer)
    {
        _ordinalComparer = ordinalComparer;
    }

    internal static IEqualityComparer<string> Create(SnapshotMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        if (metadata.CollationLcid is not { } lcid ||
            metadata.CollationComparisonStyle is not { } comparisonStyle)
        {
            return StringComparer.OrdinalIgnoreCase;
        }

        if (IsBinaryCollation(metadata.DatabaseCollation))
        {
            return new SqlIdentifierComparer(StringComparer.Ordinal);
        }

        return new SqlIdentifierComparer(
            CultureInfo.GetCultureInfo(lcid).CompareInfo,
            GetCompareOptions(comparisonStyle));
    }

    public bool Equals(string? x, string? y)
    {
        if (ReferenceEquals(x, y))
        {
            return true;
        }

        if (x is null || y is null)
        {
            return false;
        }

        return _ordinalComparer is not null
            ? _ordinalComparer.Equals(x, y)
            : _compareInfo!.Compare(x, y, _options) == 0;
    }

    public int GetHashCode(string obj)
    {
        ArgumentNullException.ThrowIfNull(obj);
        return _ordinalComparer is not null
            ? _ordinalComparer.GetHashCode(obj)
            : _compareInfo!.GetHashCode(obj, _options);
    }

    private static bool IsBinaryCollation(string? collation) =>
        collation?.Contains("_BIN", StringComparison.OrdinalIgnoreCase) == true;

    private static CompareOptions GetCompareOptions(int comparisonStyle)
    {
        var options = CompareOptions.None;
        if ((comparisonStyle & IgnoreCaseComparisonStyle) != 0)
        {
            options |= CompareOptions.IgnoreCase;
        }

        if ((comparisonStyle & IgnoreAccentComparisonStyle) != 0)
        {
            options |= CompareOptions.IgnoreNonSpace;
        }

        if ((comparisonStyle & IgnoreKanaComparisonStyle) != 0)
        {
            options |= CompareOptions.IgnoreKanaType;
        }

        if ((comparisonStyle & IgnoreWidthComparisonStyle) != 0)
        {
            options |= CompareOptions.IgnoreWidth;
        }

        return options;
    }
}
