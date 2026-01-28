using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace TsqlRefine.Rules.Helpers;

internal static class DatePartHelper
{
    private static readonly HashSet<string> DatePartFunctions = new(StringComparer.OrdinalIgnoreCase)
    {
        "DATEADD",
        "DATEDIFF",
        "DATEPART",
        "DATENAME"
    };

    public static bool IsDatePartFunction(FunctionCall func)
    {
        var name = func.FunctionName?.Value;
        return name != null && DatePartFunctions.Contains(name);
    }

    public static bool IsDatePartLiteralParameter(FunctionCall func, int parameterIndex, ScalarExpression param)
    {
        if (parameterIndex != 0)
        {
            return false;
        }

        if (!IsDatePartFunction(func))
        {
            return false;
        }

        if (param is ColumnReferenceExpression colRef)
        {
            return colRef.MultiPartIdentifier != null && colRef.MultiPartIdentifier.Count == 1;
        }

        return false;
    }
}
