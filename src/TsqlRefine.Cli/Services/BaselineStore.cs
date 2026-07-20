using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TsqlRefine.Core;
using TsqlRefine.Core.Engine;
using TsqlRefine.Core.Model;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Cli.Services;

public sealed record BaselineEntry(string Fingerprint, string RuleId, string File);

public sealed record BaselineDocument(
    int Version,
    int FingerprintVersion,
    DateTimeOffset GeneratedAt,
    string ToolVersion,
    string Root,
    IReadOnlyList<BaselineEntry> Entries);

public sealed record ClassifiedDiagnostic(Diagnostic Diagnostic, bool Suppressed, string? Fingerprint);

public sealed record ClassifiedFile(string FilePath, IReadOnlyList<ClassifiedDiagnostic> Diagnostics);

public sealed record BaselineClassification(
    IReadOnlyList<ClassifiedFile> Files,
    int ResolvedCount);

/// <summary>
/// Reads, writes, and applies CLI diagnostic baselines.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Maintainability", "CA1506", Justification = "Existing baseline persistence and classification service; tracked as coupling baseline debt.")]
public static class BaselineStore
{
    public const int CurrentVersion = 1;
    public const int CurrentFingerprintVersion = 1;

    public static BaselineDocument Load(string path)
    {
        if (!File.Exists(path))
        {
            throw new ConfigException($"Baseline file not found: {path}");
        }

        try
        {
            var json = File.ReadAllText(path);
            var document = JsonSerializer.Deserialize<BaselineDocument>(json, JsonDefaults.Options)
                ?? throw new ConfigException($"Baseline file is empty: {path}");
            if (document.Version != CurrentVersion)
            {
                throw new ConfigException(
                    $"Unsupported baseline version {document.Version}; expected {CurrentVersion}.");
            }
            if (document.FingerprintVersion != CurrentFingerprintVersion)
            {
                throw new ConfigException(
                    $"Unsupported baseline fingerprint version {document.FingerprintVersion}; " +
                    $"expected {CurrentFingerprintVersion}.");
            }
            if (string.IsNullOrWhiteSpace(document.Root) || document.Entries is null)
            {
                throw new ConfigException("Baseline is missing required root or entries data.");
            }
            return document;
        }
        catch (JsonException ex)
        {
            throw new ConfigException($"Failed to parse baseline: {ex.Message}");
        }
        catch (IOException ex)
        {
            throw new ConfigException($"Failed to read baseline: {ex.Message}");
        }
    }

    public static async Task WriteAsync(string path, BaselineDocument document)
    {
        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath)!;
        Directory.CreateDirectory(directory);
        var json = JsonSerializer.Serialize(document, JsonDefaults.Options);
        var temporaryPath = Path.Combine(directory, $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            await File.WriteAllTextAsync(
                temporaryPath,
                json + Environment.NewLine,
                new UTF8Encoding(false));
            File.Move(temporaryPath, fullPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    public static string ResolveRootForCreate(string? explicitRoot, IReadOnlyList<SqlInput> inputs)
    {
        if (!string.IsNullOrWhiteSpace(explicitRoot))
        {
            var resolved = Path.GetFullPath(explicitRoot);
            if (!Directory.Exists(resolved))
            {
                throw new ConfigException($"Baseline root directory not found: {resolved}");
            }
            return resolved;
        }

        var filePaths = inputs
            .Where(input => !IsStdin(input.FilePath))
            .Select(input => Path.GetFullPath(input.FilePath))
            .ToArray();
        var gitRoot = FindGitRoot(Directory.GetCurrentDirectory());
        if (gitRoot is not null && filePaths.All(path => IsUnderDirectory(gitRoot, path)))
        {
            return gitRoot;
        }

        return filePaths.Length == 0
            ? Directory.GetCurrentDirectory()
            : FindCommonDirectory(filePaths);
    }

    public static string ResolveStoredRoot(string baselinePath, BaselineDocument document)
    {
        var baselineDirectory = Path.GetDirectoryName(Path.GetFullPath(baselinePath))!;
        var root = Path.GetFullPath(document.Root, baselineDirectory);
        if (!Directory.Exists(root))
        {
            throw new ConfigException($"Baseline root directory not found: {root}");
        }
        return root;
    }

    public static void ValidateExplicitRoot(string? explicitRoot, string storedRoot)
    {
        if (string.IsNullOrWhiteSpace(explicitRoot))
        {
            return;
        }

        var resolved = Path.GetFullPath(explicitRoot);
        if (!string.Equals(
                Path.TrimEndingDirectorySeparator(resolved),
                Path.TrimEndingDirectorySeparator(storedRoot),
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
        {
            throw new ConfigException(
                $"Baseline root mismatch. Baseline uses '{storedRoot}', but --root resolved to '{resolved}'.");
        }
    }

    public static BaselineDocument Create(
        string baselinePath,
        string root,
        LintResult result,
        IReadOnlyDictionary<string, string> sources)
    {
        var entries = CreateEntries(result.Files, sources, root);
        var baselineDirectory = Path.GetDirectoryName(Path.GetFullPath(baselinePath))!;
        var relativeRoot = NormalizeSeparators(Path.GetRelativePath(baselineDirectory, root));
        return new BaselineDocument(
            CurrentVersion,
            CurrentFingerprintVersion,
            DateTimeOffset.UtcNow,
            result.Version,
            relativeRoot,
            entries);
    }

    public static IReadOnlyList<ClassifiedFile> Classify(
        IReadOnlyList<FileResult> files,
        IReadOnlyDictionary<string, string> sources,
        string root,
        BaselineDocument? baseline)
        => ClassifyWithSummary(files, sources, root, baseline).Files;

    public static BaselineClassification ClassifyWithSummary(
        IReadOnlyList<FileResult> files,
        IReadOnlyDictionary<string, string> sources,
        string root,
        BaselineDocument? baseline)
    {
        var analyzedFiles = files
            .Select(file => NormalizeFilePath(file.FilePath, root))
            .ToHashSet(GetPathComparer());
        var remaining = baseline?.Entries
            .Where(entry => analyzedFiles.Contains(entry.File))
            .GroupBy(entry => entry.Fingerprint, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal)
            ?? new Dictionary<string, int>(StringComparer.Ordinal);

        var output = new List<ClassifiedFile>(files.Count);
        foreach (var file in files)
        {
            sources.TryGetValue(file.FilePath, out var source);
            source ??= string.Empty;
            var sourceLines = GetSourceLines(source);
            var diagnostics = new List<ClassifiedDiagnostic>(file.Diagnostics.Count);
            foreach (var diagnostic in file.Diagnostics)
            {
                if (IsAnalysisFailure(diagnostic))
                {
                    diagnostics.Add(new ClassifiedDiagnostic(diagnostic, false, null));
                    continue;
                }

                var fingerprint = ComputeFingerprint(file.FilePath, sourceLines, diagnostic, root);
                var suppressed = remaining.TryGetValue(fingerprint, out var count) && count > 0;
                if (suppressed)
                {
                    remaining[fingerprint] = count - 1;
                }
                diagnostics.Add(new ClassifiedDiagnostic(diagnostic, suppressed, fingerprint));
            }
            output.Add(new ClassifiedFile(file.FilePath, diagnostics));
        }
        return new BaselineClassification(output, remaining.Values.Sum());
    }

    public static BaselineDocument Trim(
        BaselineDocument baseline,
        IReadOnlyList<FileResult> files,
        IReadOnlyDictionary<string, string> sources,
        string root,
        bool removeMissing)
    {
        var currentEntries = CreateEntries(files, sources, root);
        var currentCounts = currentEntries
            .GroupBy(entry => entry.Fingerprint, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        var analyzedFiles = files
            .Select(file => NormalizeFilePath(file.FilePath, root))
            .ToHashSet(GetPathComparer());

        var retained = new List<BaselineEntry>(baseline.Entries.Count);
        foreach (var entry in baseline.Entries)
        {
            var appliesToAnalyzedFile = analyzedFiles.Contains(entry.File);
            var fullEntryPath = Path.GetFullPath(entry.File.Replace('/', Path.DirectorySeparatorChar), root);
            if (!appliesToAnalyzedFile)
            {
                if (!removeMissing || File.Exists(fullEntryPath))
                {
                    retained.Add(entry);
                }
                continue;
            }

            if (currentCounts.TryGetValue(entry.Fingerprint, out var count) && count > 0)
            {
                retained.Add(entry);
                currentCounts[entry.Fingerprint] = count - 1;
            }
        }

        return baseline with
        {
            GeneratedAt = DateTimeOffset.UtcNow,
            Entries = retained
        };
    }

    public static bool IsAnalysisFailure(Diagnostic diagnostic) =>
        diagnostic.Code is TsqlRefineEngine.ParseErrorCode or TsqlRefineEngine.ParserExceptionCode;

    private static BaselineEntry[] CreateEntries(
        IReadOnlyList<FileResult> files,
        IReadOnlyDictionary<string, string> sources,
        string root)
    {
        var entries = new List<BaselineEntry>();
        foreach (var file in files)
        {
            sources.TryGetValue(file.FilePath, out var source);
            source ??= string.Empty;
            var sourceLines = GetSourceLines(source);
            var normalizedPath = NormalizeFilePath(file.FilePath, root);
            foreach (var diagnostic in file.Diagnostics)
            {
                if (IsAnalysisFailure(diagnostic))
                {
                    continue;
                }
                entries.Add(new BaselineEntry(
                    ComputeFingerprint(file.FilePath, sourceLines, diagnostic, root),
                    diagnostic.Data?.RuleId ?? diagnostic.Code ?? string.Empty,
                    normalizedPath));
            }
        }
        return entries
            .OrderBy(entry => entry.File, GetPathComparer())
            .ThenBy(entry => entry.RuleId, StringComparer.Ordinal)
            .ThenBy(entry => entry.Fingerprint, StringComparer.Ordinal)
            .ToArray();
    }

    private static string ComputeFingerprint(
        string filePath,
        string[] lines,
        Diagnostic diagnostic,
        string root)
    {
        var diagnosticText = ExtractDiagnosticText(lines, diagnostic.Range);
        var leading = FindContextLine(lines, diagnostic.Range.Start.Line, -1);
        var trailing = FindContextLine(lines, diagnostic.Range.End.Line, 1);
        var fields = new[]
        {
            CurrentFingerprintVersion.ToString(System.Globalization.CultureInfo.InvariantCulture),
            diagnostic.Data?.RuleId ?? diagnostic.Code ?? string.Empty,
            NormalizeFilePath(filePath, root),
            diagnosticText,
            leading,
            trailing
        };

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Span<byte> lengthBytes = stackalloc byte[sizeof(int)];
        foreach (var field in fields)
        {
            var bytes = Encoding.UTF8.GetBytes(field);
            BinaryPrimitives.WriteInt32BigEndian(lengthBytes, bytes.Length);
            hash.AppendData(lengthBytes);
            hash.AppendData(bytes);
        }
        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private static string ExtractDiagnosticText(string[] lines, TsqlRefine.PluginSdk.Range range)
    {
        if (lines.Length == 0)
        {
            return string.Empty;
        }

        var startLine = Math.Clamp(range.Start.Line, 0, lines.Length - 1);
        var endLine = Math.Clamp(range.End.Line, startLine, lines.Length - 1);
        if (startLine == endLine)
        {
            var line = lines[startLine];
            var start = Math.Clamp(range.Start.Character, 0, line.Length);
            var end = Math.Clamp(range.End.Character, start, line.Length);
            var text = line[start..end].Trim();
            return text.Length > 0 ? text : line.Trim();
        }

        var selected = new List<string>(endLine - startLine + 1);
        for (var i = startLine; i <= endLine; i++)
        {
            var line = lines[i];
            if (i == startLine)
            {
                line = line[Math.Clamp(range.Start.Character, 0, line.Length)..];
            }
            if (i == endLine)
            {
                line = line[..Math.Clamp(range.End.Character, 0, line.Length)];
            }
            selected.Add(line.TrimEnd());
        }
        return string.Join('\n', selected).Trim();
    }

    private static string FindContextLine(string[] lines, int line, int direction)
    {
        for (var i = line + direction; i >= 0 && i < lines.Length; i += direction)
        {
            var candidate = lines[i].Trim();
            if (candidate.Length > 0)
            {
                return candidate;
            }
        }
        return string.Empty;
    }

    private static string NormalizeFilePath(string filePath, string root)
    {
        if (IsStdin(filePath))
        {
            return filePath;
        }

        var fullPath = Path.GetFullPath(filePath);
        var relative = Path.GetRelativePath(root, fullPath);
        if (Path.IsPathRooted(relative) || relative == ".." || relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
        {
            throw new ConfigException($"Input file is outside baseline root '{root}': {fullPath}");
        }
        return NormalizeSeparators(relative);
    }

    private static string NormalizeNewlines(string value) =>
        value.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');

    private static string[] GetSourceLines(string source) => NormalizeNewlines(source).Split('\n');

    private static string NormalizeSeparators(string value) => value.Replace('\\', '/');

    private static bool IsStdin(string filePath) => string.Equals(filePath, "<stdin>", StringComparison.Ordinal);

    private static StringComparer GetPathComparer() =>
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    private static string? FindGitRoot(string startDirectory)
    {
        var directory = new DirectoryInfo(Path.GetFullPath(startDirectory));
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, ".git")) ||
                File.Exists(Path.Combine(directory.FullName, ".git")))
            {
                return directory.FullName;
            }
            directory = directory.Parent;
        }
        return null;
    }

    private static string FindCommonDirectory(string[] filePaths)
    {
        var candidate = Path.GetDirectoryName(filePaths[0])!;
        while (filePaths.Any(path => !IsUnderDirectory(candidate, path)))
        {
            candidate = Directory.GetParent(candidate)?.FullName
                ?? throw new ConfigException("Could not determine a common baseline root for input files.");
        }
        return candidate;
    }

    private static bool IsUnderDirectory(string directory, string path)
    {
        var relative = Path.GetRelativePath(directory, path);
        return !Path.IsPathRooted(relative) &&
               relative != ".." &&
               !relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal);
    }
}
