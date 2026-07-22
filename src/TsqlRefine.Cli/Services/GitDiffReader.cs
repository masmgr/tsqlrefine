using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using TsqlRefine.Core;
using TsqlRefine.Core.Model;

namespace TsqlRefine.Cli.Services;

public sealed record ChangedLineRange(int StartLine, int EndLine);

public sealed record ChangedFileLines(string Path, IReadOnlyList<ChangedLineRange> Ranges);

public sealed record ChangedLinesDocument(int Version, IReadOnlyList<ChangedFileLines> Files);

public sealed class ChangedLineMap
{
    private readonly IReadOnlyDictionary<string, IReadOnlyList<ChangedLineRange>> _rangesByPath;

    internal ChangedLineMap(IReadOnlyDictionary<string, IReadOnlyList<ChangedLineRange>> rangesByPath)
    {
        _rangesByPath = rangesByPath;
    }

    public bool Intersects(string filePath, TsqlRefine.PluginSdk.Range diagnosticRange)
    {
        if (!_rangesByPath.TryGetValue(Path.GetFullPath(filePath), out var ranges))
        {
            return false;
        }
        var startLine = diagnosticRange.Start.Line + 1;
        var endLine = diagnosticRange.End.Line + 1;
        if (diagnosticRange.End.Character == 0 && endLine > startLine)
        {
            endLine--;
        }
        return ranges.Any(range => range.StartLine <= endLine && range.EndLine >= startLine);
    }

    public bool ContainsFile(string filePath) =>
        _rangesByPath.ContainsKey(Path.GetFullPath(filePath));
}

[System.Diagnostics.CodeAnalysis.SuppressMessage("Maintainability", "CA1506", Justification = "Existing Git diff integration service; tracked as coupling baseline debt.")]
public static class GitDiffReader
{
    private const int CurrentChangedLinesVersion = 1;
    private static readonly Regex HunkPattern = new(
        "^@@ -[0-9]+(?:,[0-9]+)? \\+(?<start>[0-9]+)(?:,(?<count>[0-9]+))? @@",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static async Task<ChangedLineMap> ReadAsync(
        CliArgs args,
        IReadOnlyList<string> inputPaths)
    {
        if (args.Stdin || args.Paths.Contains("-", StringComparer.Ordinal))
        {
            throw new ConfigException("Changed-only lint does not support stdin; use path inputs.");
        }
        return args.ChangedLinesFrom is not null
            ? ReadDocument(args.ChangedLinesFrom)
            : await ReadGitAsync(args.BaseRef ?? "origin/main", inputPaths);
    }

    public static LintResult Filter(LintResult result, ChangedLineMap changedLines)
    {
        var files = result.Files.Select(file => new FileResult(
            file.FilePath,
            file.Diagnostics.Where(diagnostic =>
                BaselineStore.IsAnalysisFailure(diagnostic) ||
                changedLines.Intersects(file.FilePath, diagnostic.Range)).ToArray())).ToArray();
        return result with { Files = files };
    }

    private static ChangedLineMap ReadDocument(string path)
    {
        if (!File.Exists(path))
        {
            throw new ConfigException($"Changed-lines file not found: {path}");
        }
        try
        {
            var document = JsonSerializer.Deserialize<ChangedLinesDocument>(
                File.ReadAllText(path),
                JsonDefaults.Options) ?? throw new ConfigException($"Changed-lines file is empty: {path}");
            if (document.Version != CurrentChangedLinesVersion)
            {
                throw new ConfigException(
                    $"Unsupported changed-lines version {document.Version}; expected {CurrentChangedLinesVersion}.");
            }
            if (document.Files is null)
            {
                throw new ConfigException("Changed-lines file is missing files data.");
            }
            var baseDirectory = Path.GetDirectoryName(Path.GetFullPath(path))!;
            var ranges = new Dictionary<string, List<ChangedLineRange>>(GetPathComparer());
            foreach (var file in document.Files)
            {
                if (string.IsNullOrWhiteSpace(file.Path) || file.Ranges is null)
                {
                    throw new ConfigException("Changed-lines entries require a path and ranges.");
                }
                var fullPath = Path.GetFullPath(file.Path, baseDirectory);
                foreach (var range in file.Ranges)
                {
                    if (range.StartLine < 1 || range.EndLine < range.StartLine)
                    {
                        throw new ConfigException(
                            $"Invalid changed-line range {range.StartLine}-{range.EndLine} for '{file.Path}'.");
                    }
                    AddRange(ranges, fullPath, range);
                }
            }
            return CreateMap(ranges);
        }
        catch (JsonException ex)
        {
            throw new ConfigException($"Failed to parse changed-lines file: {ex.Message}");
        }
        catch (IOException ex)
        {
            throw new ConfigException($"Failed to read changed-lines file: {ex.Message}");
        }
    }

    private static async Task<ChangedLineMap> ReadGitAsync(string baseRef, IReadOnlyList<string> inputPaths)
    {
        var rootResult = await RunGitAsync(["rev-parse", "--show-toplevel"]);
        var root = Path.GetFullPath(rootResult.Trim());
        var ranges = new Dictionary<string, List<ChangedLineRange>>(GetPathComparer());
        var mergeBase = (await RunGitAsync(["merge-base", baseRef, "HEAD"])).Trim();
        var working = await RunGitAsync([
            "-c", "core.quotepath=false", "diff", "--unified=0", "--no-color", "--no-ext-diff",
            "--no-prefix", mergeBase, "--"
        ]);
        ParsePatch(working, root, ranges);

        var untrackedOutput = await RunGitAsync([
            "-c", "core.quotepath=false", "ls-files", "--others", "--exclude-standard", "--"
        ]);
        var inputPathSet = inputPaths
            .Select(Path.GetFullPath)
            .ToHashSet(GetPathComparer());
        foreach (var relativePath in SplitLines(untrackedOutput).Where(line => line.Length > 0))
        {
            var fullPath = Path.GetFullPath(UnquoteGitPath(relativePath), root);
            if (inputPathSet.Contains(fullPath) && File.Exists(fullPath))
            {
                var text = await File.ReadAllTextAsync(fullPath);
                var lineCount = Math.Max(1, CountLines(text));
                AddRange(ranges, fullPath, new ChangedLineRange(1, lineCount));
            }
        }
        return CreateMap(ranges);
    }

    private static void ParsePatch(
        string patch,
        string root,
        Dictionary<string, List<ChangedLineRange>> ranges)
    {
        string? currentPath = null;
        foreach (var line in SplitLines(patch))
        {
            if (line.StartsWith("+++ ", StringComparison.Ordinal))
            {
                var path = line[4..];
                currentPath = path == "/dev/null"
                    ? null
                    : Path.GetFullPath(UnquoteGitPath(path), root);
                continue;
            }
            if (currentPath is null || !line.StartsWith("@@ ", StringComparison.Ordinal))
            {
                continue;
            }
            var match = HunkPattern.Match(line);
            if (!match.Success ||
                !int.TryParse(match.Groups["start"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var start))
            {
                continue;
            }
            var count = match.Groups["count"].Success &&
                        int.TryParse(match.Groups["count"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsedCount)
                ? parsedCount
                : 1;
            if (count > 0)
            {
                AddRange(ranges, currentPath, new ChangedLineRange(start, start + count - 1));
            }
        }
    }

    private static async Task<string> RunGitAsync(IReadOnlyList<string> arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }
        try
        {
            using var process = Process.Start(startInfo)
                ?? throw new ConfigException("Failed to start Git for changed-only lint.");
            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            var stdout = await stdoutTask;
            var stderr = await stderrTask;
            if (process.ExitCode != 0)
            {
                throw new ConfigException(
                    $"Git command failed for changed-only lint: {stderr.Trim()}");
            }
            return stdout;
        }
        catch (Win32Exception ex)
        {
            throw new ConfigException(
                $"Git is required for --changed-only; use --changed-lines-from when Git is unavailable: {ex.Message}");
        }
    }

    private static ChangedLineMap CreateMap(Dictionary<string, List<ChangedLineRange>> ranges)
    {
        var normalized = ranges.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<ChangedLineRange>)MergeRanges(pair.Value),
            GetPathComparer());
        return new ChangedLineMap(normalized);
    }

    private static ChangedLineRange[] MergeRanges(List<ChangedLineRange> ranges)
    {
        var ordered = ranges.OrderBy(range => range.StartLine).ThenBy(range => range.EndLine).ToArray();
        if (ordered.Length == 0)
        {
            return [];
        }
        var merged = new List<ChangedLineRange> { ordered[0] };
        foreach (var range in ordered.Skip(1))
        {
            var previous = merged[^1];
            if (range.StartLine <= previous.EndLine + 1)
            {
                merged[^1] = previous with { EndLine = Math.Max(previous.EndLine, range.EndLine) };
            }
            else
            {
                merged.Add(range);
            }
        }
        return merged.ToArray();
    }

    private static void AddRange(
        Dictionary<string, List<ChangedLineRange>> ranges,
        string fullPath,
        ChangedLineRange range)
    {
        if (!ranges.TryGetValue(fullPath, out var fileRanges))
        {
            fileRanges = [];
            ranges[fullPath] = fileRanges;
        }
        fileRanges.Add(range);
    }

    private static string[] SplitLines(string text) =>
        text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');

    private static int CountLines(string text) =>
        text.Length == 0 ? 1 : 1 + text.Count(character => character == '\n');

    private static string UnquoteGitPath(string path)
    {
        if (path.Length < 2 || path[0] != '"' || path[^1] != '"')
        {
            return path;
        }
        return Regex.Unescape(path[1..^1]);
    }

    private static StringComparer GetPathComparer() =>
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
}
