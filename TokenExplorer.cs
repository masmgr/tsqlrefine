using Microsoft.SqlServer.TransactSql.ScriptDom;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public class TokenExplorer
{
    public static void Main()
    {
        var sql = @"
DECLARE @userId INT = 1;
SELECT u.UserId, COUNT(*) AS OrderCount, GETDATE(), @@ROWCOUNT
FROM dbo.Users u
WHERE u.IsActive = 1
    AND o.OrderDate >= DATEADD(day, -30, GETDATE());
";

        var parser = new TSql150Parser(initialQuotedIdentifiers: true);
        using var reader = new StringReader(sql);
        var tokens = parser.GetTokenStream(reader, out _);

        Console.WriteLine("Token Type                     | Text");
        Console.WriteLine("====================================");

        foreach (var token in tokens)
        {
            if (token.TokenType == TSqlTokenType.EndOfFile) break;
            var text = (token.Text ?? "").Trim();
            if (string.IsNullOrWhiteSpace(text)) continue;

            Console.WriteLine($"{token.TokenType,-30} | {text}");
        }

        Console.WriteLine("\n\nBuilt-in Function Detection:");
        Console.WriteLine("====================================");

        var functionKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "COUNT", "SUM", "AVG", "MIN", "MAX", "GETDATE", "DATEADD", "DATEDIFF",
            "CAST", "CONVERT", "COALESCE", "ISNULL", "LEN", "SUBSTRING", "UPPER", "LOWER",
            "TRIM", "LTRIM", "RTRIM", "REPLACE", "ROW_NUMBER", "RANK", "DENSE_RANK"
        };

        var dataTypeKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "INT", "BIGINT", "SMALLINT", "TINYINT", "BIT", "DECIMAL", "NUMERIC", "MONEY", "SMALLMONEY",
            "FLOAT", "REAL", "DATE", "TIME", "DATETIME", "DATETIME2", "DATETIMEOFFSET", "SMALLDATETIME",
            "CHAR", "VARCHAR", "TEXT", "NCHAR", "NVARCHAR", "NTEXT", "BINARY", "VARBINARY", "IMAGE",
            "UNIQUEIDENTIFIER", "XML", "SQL_VARIANT", "CURSOR", "TABLE"
        };

        using var reader2 = new StringReader(sql);
        var tokens2 = parser.GetTokenStream(reader2, out _).ToList();

        for (int i = 0; i < tokens2.Count; i++)
        {
            var token = tokens2[i];
            var text = token.Text?.Trim() ?? "";

            if (functionKeywords.Contains(text))
            {
                Console.WriteLine($"Function: {text} (TokenType: {token.TokenType})");
            }

            if (dataTypeKeywords.Contains(text))
            {
                Console.WriteLine($"DataType: {text} (TokenType: {token.TokenType})");
            }
        }
    }
}
