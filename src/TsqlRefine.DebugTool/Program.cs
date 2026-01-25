using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.Core.Engine;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.DebugTool;

class Program
{
    static void Main(string[] args)
    {
        // Test cases
        var testCases = new[]
        {
            "SELECT 1",
            "SELECT 1;",
            "SELECT 1; SELECT 2",
            "SELECT 1; SELECT 2;",
            "SELECT 1\nGO\nSELECT 2",
            "CREATE TABLE t (id INT); SELECT * FROM t;",
            "-- comment\nSELECT 1"
        };

        foreach (var sql in testCases)
        {
            Console.WriteLine("=".PadRight(80, '='));
            Console.WriteLine($"SQL: {sql.Replace("\n", "\\n")}");
            Console.WriteLine("=".PadRight(80, '='));
            AnalyzeSql(sql);
            Console.WriteLine();
        }
    }

    static void AnalyzeSql(string sql)
    {
        var result = ScriptDomTokenizer.Analyze(sql, compatLevel: 150);

        if (result.Ast?.ParseErrors?.Count > 0)
        {
            Console.WriteLine("PARSE ERRORS:");
            foreach (var error in result.Ast.ParseErrors)
            {
                Console.WriteLine($"  {error}");
            }
            return;
        }

        Console.WriteLine("\n--- AST STRUCTURE ---");
        if (result.Ast?.Fragment is TSqlScript script)
        {
            Console.WriteLine($"Fragment Type: TSqlScript");
            Console.WriteLine($"Batches: {script.Batches.Count}");

            for (int batchIdx = 0; batchIdx < script.Batches.Count; batchIdx++)
            {
                var batch = script.Batches[batchIdx];
                Console.WriteLine($"\nBatch {batchIdx}:");
                Console.WriteLine($"  Type: {batch.GetType().Name}");
                Console.WriteLine($"  Statements: {batch.Statements.Count}");

                for (int stmtIdx = 0; stmtIdx < batch.Statements.Count; stmtIdx++)
                {
                    var stmt = batch.Statements[stmtIdx];
                    Console.WriteLine($"\n  Statement {stmtIdx}:");
                    Console.WriteLine($"    Type: {stmt.GetType().Name}");
                    Console.WriteLine($"    StartOffset: {stmt.StartOffset}");
                    Console.WriteLine($"    FragmentLength: {stmt.FragmentLength}");
                    Console.WriteLine($"    StartLine: {stmt.StartLine}");
                    Console.WriteLine($"    StartColumn: {stmt.StartColumn}");
                    Console.WriteLine($"    LastTokenIndex: {stmt.LastTokenIndex}");
                    Console.WriteLine($"    ScriptTokenStream index range: {stmt.FirstTokenIndex} - {stmt.LastTokenIndex}");

                    // Extract statement text
                    var stmtText = sql.Substring(stmt.StartOffset, stmt.FragmentLength);
                    Console.WriteLine($"    Text: [{stmtText}]");
                }
            }
        }
        else
        {
            Console.WriteLine($"Fragment Type: {result.Ast?.Fragment?.GetType().Name ?? "null"}");
        }

        Console.WriteLine("\n--- TOKENS ---");
        Console.WriteLine($"Total tokens: {result.Tokens.Count}");

        if (result.Ast?.Fragment is TSqlScript script2)
        {
            Console.WriteLine($"\nScriptTokenStream: {script2.ScriptTokenStream?.Count ?? 0} tokens");

            if (script2.ScriptTokenStream != null)
            {
                for (int i = 0; i < script2.ScriptTokenStream.Count; i++)
                {
                    var token = script2.ScriptTokenStream[i];
                    var tokenText = token.Text ?? "";
                    var tokenType = token.TokenType;
                    Console.WriteLine($"  [{i}] {tokenType,-20} [{tokenText.Replace("\n", "\\n").Replace("\r", "\\r")}]");
                }
            }
        }

        Console.WriteLine("\nPluginSdk Tokens:");
        for (int i = 0; i < result.Tokens.Count; i++)
        {
            var token = result.Tokens[i];
            Console.WriteLine($"  [{i}] Line:{token.Start.Line} Char:{token.Start.Character} Len:{token.Length} [{token.Text.Replace("\n", "\\n").Replace("\r", "\\r")}]");
        }

        // Check for semicolons
        Console.WriteLine("\n--- SEMICOLON ANALYSIS ---");
        if (result.Ast?.Fragment is TSqlScript script3 && script3.ScriptTokenStream != null)
        {
            foreach (var batch in script3.Batches)
            {
                foreach (var stmt in batch.Statements)
                {
                    Console.WriteLine($"\nStatement at token index {stmt.FirstTokenIndex}-{stmt.LastTokenIndex}:");
                    Console.WriteLine($"  Statement type: {stmt.GetType().Name}");

                    if (stmt.LastTokenIndex >= 0 && stmt.LastTokenIndex < script3.ScriptTokenStream.Count)
                    {
                        var lastToken = script3.ScriptTokenStream[stmt.LastTokenIndex];
                        Console.WriteLine($"  Last token: {lastToken.TokenType} [{lastToken.Text}]");

                        // Check next token
                        if (stmt.LastTokenIndex + 1 < script3.ScriptTokenStream.Count)
                        {
                            var nextToken = script3.ScriptTokenStream[stmt.LastTokenIndex + 1];
                            Console.WriteLine($"  Next token: {nextToken.TokenType} [{nextToken.Text}]");
                            Console.WriteLine($"  Has semicolon after: {nextToken.TokenType == TSqlTokenType.Semicolon}");
                        }
                        else
                        {
                            Console.WriteLine($"  No next token (end of stream)");
                            Console.WriteLine($"  Has semicolon after: false");
                        }
                    }
                }
            }
        }
    }
}
