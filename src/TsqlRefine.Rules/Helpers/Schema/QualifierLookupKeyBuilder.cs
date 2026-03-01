using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Helpers.Schema;

/// <summary>
/// Builds lookup keys from multipart column qualifiers, ordered from most to least specific.
/// </summary>
public static class QualifierLookupKeyBuilder
{
    /// <summary>
    /// Resolves the qualifier part of a multipart column identifier against the alias map.
    /// Lookup order is most to least specific: db.schema.table, schema.table, table/alias.
    /// </summary>
    public static bool TryResolve(
        AliasMap aliasMap,
        IList<Identifier> identifiers,
        out ResolvedTable? resolvedTable)
    {
        ArgumentNullException.ThrowIfNull(aliasMap);
        ArgumentNullException.ThrowIfNull(identifiers);

        var qualifierCount = identifiers.Count - 1;
        if (qualifierCount <= 0)
        {
            resolvedTable = null;
            return false;
        }

        if (qualifierCount == 1)
        {
            return aliasMap.TryResolve(identifiers[0].Value, out resolvedTable);
        }

        var fullQualifier = BuildQualifierKey(identifiers, qualifierCount);
        if (aliasMap.TryResolve(fullQualifier, out resolvedTable))
        {
            return true;
        }

        var twoPart = $"{identifiers[qualifierCount - 2].Value}.{identifiers[qualifierCount - 1].Value}";
        if (aliasMap.TryResolve(twoPart, out resolvedTable))
        {
            return true;
        }

        return aliasMap.TryResolve(identifiers[qualifierCount - 1].Value, out resolvedTable);
    }

    /// <summary>
    /// Builds alias/table lookup keys from the qualifier segments of a column identifier.
    /// The last identifier is treated as the column name and excluded from key generation.
    /// </summary>
    public static IEnumerable<string> Build(IList<Identifier> identifiers)
    {
        ArgumentNullException.ThrowIfNull(identifiers);

        var qualifierCount = identifiers.Count - 1;
        if (qualifierCount <= 0)
        {
            yield break;
        }

        var parts = new string[qualifierCount];
        for (var i = 0; i < qualifierCount; i++)
        {
            parts[i] = identifiers[i].Value;
        }

        if (parts.Length == 1)
        {
            yield return parts[0];
            yield break;
        }

        yield return string.Join(".", parts);

        if (parts.Length >= 2)
        {
            yield return $"{parts[^2]}.{parts[^1]}";
        }

        yield return parts[^1];
    }

    private static string BuildQualifierKey(IList<Identifier> identifiers, int qualifierCount)
    {
        return qualifierCount switch
        {
            2 => $"{identifiers[0].Value}.{identifiers[1].Value}",
            3 => $"{identifiers[0].Value}.{identifiers[1].Value}.{identifiers[2].Value}",
            _ => string.Join(".", identifiers.Take(qualifierCount).Select(static i => i.Value)),
        };
    }
}
