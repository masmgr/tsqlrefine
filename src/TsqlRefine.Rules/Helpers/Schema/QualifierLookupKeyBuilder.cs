using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace TsqlRefine.Rules.Helpers.Schema;

/// <summary>
/// Builds lookup keys from multipart column qualifiers, ordered from most to least specific.
/// </summary>
public static class QualifierLookupKeyBuilder
{
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
}
