using System.Collections.Frozen;

namespace TsqlRefine.Rules.Helpers.Catalog;

internal static class SystemProcedureHelpers
{
    private static readonly FrozenSet<string> s_knownNames = new[]
    {
        "sp_executesql",
        "sp_xml_preparedocument",
        "sp_xml_removedocument",
        "sp_prepare",
        "sp_execute",
        "sp_unprepare",
        "sp_describe_first_result_set",
        "sp_describe_undeclared_parameters",
        "sp_getapplock",
        "sp_releaseapplock",
        "sp_addmessage",
        "sp_dropmessage",
        "xp_cmdshell",
        "xp_sendmail",
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    internal static bool IsKnownSystemProcedure(string name) => s_knownNames.Contains(name);

    internal static bool IsSystemProcedureReference(string name, string? schema = null)
    {
        if (IsKnownSystemProcedure(name))
        {
            return true;
        }

        return (string.IsNullOrWhiteSpace(schema) ||
                string.Equals(schema, "sys", StringComparison.OrdinalIgnoreCase)) &&
               (name.StartsWith("sp_", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("xp_", StringComparison.OrdinalIgnoreCase));
    }
}
