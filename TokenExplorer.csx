#!/usr/bin/env dotnet script
#r "nuget: Microsoft.SqlServer.TransactSql.ScriptDom, 161.9142.0"

using Microsoft.SqlServer.TransactSql.ScriptDom;
using System.IO;

var sql = File.ReadAllText("test_tokens.sql");
var parser = new TSql150Parser(initialQuotedIdentifiers: true);

using var reader = new StringReader(sql);
var tokens = parser.GetTokenStream(reader, out _);

Console.WriteLine("Token Analysis:");
Console.WriteLine("================");

foreach (var token in tokens)
{
    if (token.TokenType == TSqlTokenType.EndOfFile) break;

    var text = token.Text ?? "";
    if (string.IsNullOrWhiteSpace(text)) continue;

    Console.WriteLine($"{token.TokenType,-30} | {text}");
}
