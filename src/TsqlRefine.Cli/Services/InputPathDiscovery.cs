using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;

namespace TsqlRefine.Cli.Services;

internal sealed class InputPathDiscovery
{
    private Matcher? _cachedIgnoreMatcher;
    private IReadOnlyList<string>? _cachedIgnorePatterns;

    public List<string> Discover(
        CliArgs args,
        IEnumerable<string> paths,
        IEnumerable<string> ignorePatterns,
        TextWriter stderr)
    {
        var ignoreList = ignorePatterns as IReadOnlyList<string> ?? ignorePatterns.ToArray();
        var ignoreBaseDirectory = ResolveIgnoreBaseDirectory(args.IgnoreListPath);
        var ignoreMatcher = GetIgnoreMatcher(ignoreList);
        var readablePaths = new List<string>();
        foreach (var path in ExpandPaths(paths, ignoreList, ignoreBaseDirectory, ignoreMatcher))
        {
            if (!File.Exists(path))
            {
                stderr.WriteLine($"File not found: {path}");
                continue;
            }

            if (args.MaxFileSize > 0)
            {
                var fileInfo = new FileInfo(path);
                if (fileInfo.Length > args.MaxFileSize)
                {
                    stderr.WriteLine(
                        $"Skipped {path}: file size ({fileInfo.Length / (1024 * 1024)} MB) exceeds maximum ({args.MaxFileSize / (1024 * 1024)} MB). Use --max-file-size to increase.");
                    continue;
                }
            }

            readablePaths.Add(path);
        }

        return readablePaths;
    }

    private static IEnumerable<string> ExpandPaths(
        IEnumerable<string> paths,
        IReadOnlyList<string> ignorePatterns,
        string ignoreBaseDirectory,
        Matcher ignoreMatcher)
    {
        foreach (var path in paths)
        {
            if (Directory.Exists(path))
            {
                var matcher = new Matcher(StringComparison.OrdinalIgnoreCase);
                matcher.AddInclude("**/*.sql");
                foreach (var pattern in ignorePatterns)
                {
                    matcher.AddExclude(pattern);
                }

                var result = matcher.Execute(new DirectoryInfoWrapper(new DirectoryInfo(path)));
                foreach (var file in result.Files)
                {
                    yield return Path.Combine(path, file.Path);
                }

                continue;
            }

            if (!ShouldIgnoreFile(path, ignorePatterns, ignoreBaseDirectory, ignoreMatcher))
            {
                yield return path;
            }
        }
    }

    private static bool ShouldIgnoreFile(
        string filePath,
        IReadOnlyList<string> ignorePatterns,
        string ignoreBaseDirectory,
        Matcher matcher)
    {
        if (ignorePatterns.Count == 0)
        {
            return false;
        }

        var fullPath = Path.GetFullPath(filePath);
        var candidatePaths = new[]
        {
            Path.GetFileName(fullPath),
            GetRelativePathIfUnderBase(Directory.GetCurrentDirectory(), fullPath),
            GetRelativePathIfUnderBase(ignoreBaseDirectory, fullPath)
        };

        return candidatePaths
            .Where(path => !string.IsNullOrEmpty(path))
            .Select(path => path!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Any(path => matcher.Match(ignoreBaseDirectory, [path]).HasMatches);
    }

    private Matcher GetIgnoreMatcher(IReadOnlyList<string> ignorePatterns)
    {
        if (_cachedIgnoreMatcher is not null &&
            _cachedIgnorePatterns is not null &&
            ReferenceEquals(_cachedIgnorePatterns, ignorePatterns))
        {
            return _cachedIgnoreMatcher;
        }

        var matcher = new Matcher(StringComparison.OrdinalIgnoreCase);
        foreach (var pattern in ignorePatterns)
        {
            matcher.AddInclude(pattern);
        }
        _cachedIgnoreMatcher = matcher;
        _cachedIgnorePatterns = ignorePatterns;
        return matcher;
    }

    private static string ResolveIgnoreBaseDirectory(string? ignoreListPath)
    {
        if (string.IsNullOrWhiteSpace(ignoreListPath))
        {
            return Directory.GetCurrentDirectory();
        }

        var fullPath = Path.GetFullPath(ignoreListPath);
        return Path.GetDirectoryName(fullPath) ?? Directory.GetCurrentDirectory();
    }

    private static string? GetRelativePathIfUnderBase(string baseDirectory, string fullPath)
    {
        var fullBase = Path.GetFullPath(baseDirectory);
        var relative = Path.GetRelativePath(fullBase, fullPath);
        return relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative)
            ? null
            : relative;
    }
}
