using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace TsqlRefine.Qa.Tests;

internal static class CorpusSupport
{
    public static string RepositoryRoot { get; } = FindRepositoryRoot();

    public static string CorpusRoot => Path.Combine(RepositoryRoot, "tests", "corpus");

    public static IReadOnlyList<CorpusFile> LoadFiles()
    {
        var manifestPath = Path.Combine(CorpusRoot, "manifest.json");
        using var document = JsonDocument.Parse(File.ReadAllText(manifestPath));
        return document.RootElement.GetProperty("files")
            .EnumerateArray()
            .Select(item => new CorpusFile(
                item.GetProperty("path").GetString()!,
                item.GetProperty("project").GetString()!,
                item.GetProperty("authors").GetString()!,
                item.GetProperty("sourceUrl").GetString()!,
                item.GetProperty("revision").GetString()!,
                item.GetProperty("license").GetString()!,
                item.GetProperty("modified").GetBoolean(),
                item.GetProperty("sha256").GetString()!,
                item.GetProperty("minCompatLevel").GetInt32()))
            .ToArray();
    }

    public static string Read(CorpusFile file) =>
        File.ReadAllText(Path.Combine(CorpusRoot, file.Path.Replace('/', Path.DirectorySeparatorChar)));

    public static string Sha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    public static TSqlParser CreateParser(int compatibilityLevel) => compatibilityLevel switch
    {
        >= 160 => new TSql160Parser(true),
        >= 150 => new TSql150Parser(true),
        >= 140 => new TSql140Parser(true),
        >= 130 => new TSql130Parser(true),
        >= 120 => new TSql120Parser(true),
        >= 110 => new TSql110Parser(true),
        _ => new TSql100Parser(true)
    };

    public static IReadOnlyList<ParseError> Parse(string sql, int compatibilityLevel)
    {
        using var reader = new StringReader(sql);
        _ = CreateParser(compatibilityLevel).Parse(reader, out IList<ParseError> errors);
        return errors.ToArray();
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "global.json")) &&
                Directory.Exists(Path.Combine(directory.FullName, ".git")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate the repository root.");
    }
}

internal sealed record CorpusFile(
    string Path,
    string Project,
    string Authors,
    string SourceUrl,
    string Revision,
    string License,
    bool Modified,
    string Sha256,
    int MinCompatLevel);
