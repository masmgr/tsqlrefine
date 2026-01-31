using System.Text;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;
using TsqlRefine.Core;
using TsqlRefine.Core.Engine;

namespace TsqlRefine.Cli.Services;

public sealed class InputReader
{
    private Matcher? _cachedIgnoreMatcher;
    private List<string>? _cachedIgnorePatterns;

    public sealed record ReadInputsResult(
        List<SqlInput> Inputs,
        IReadOnlyDictionary<string, Encoding> WriteEncodings);

    public async Task<ReadInputsResult> ReadInputsAsync(
        CliArgs args,
        TextReader stdin,
        IEnumerable<string> ignorePatterns,
        TextWriter stderr)
    {
        var inputs = new List<SqlInput>();
        var encodings = new Dictionary<string, Encoding>(StringComparer.OrdinalIgnoreCase);
        var paths = args.Paths.ToArray();

        if (args.Stdin || paths.Any(p => p == "-"))
        {
            var sql = await stdin.ReadToEndAsync();
            var filePath = args.StdinFilePath ?? "<stdin>";
            inputs.Add(new SqlInput(filePath, sql));
            paths = paths.Where(p => p != "-").ToArray();
        }

        var ignoreList = ignorePatterns.ToList();
        foreach (var path in ExpandPaths(paths, ignoreList))
        {
            if (!File.Exists(path))
            {
                await stderr.WriteLineAsync($"File not found: {path}");
                continue;
            }

            // Read bytes once
            var bytes = await File.ReadAllBytesAsync(path);

            // Always detect encoding for write-back purposes
            var decoded = CharsetDetection.Decode(bytes);
            encodings[path] = decoded.WriteEncoding;

            if (args.DetectEncoding)
            {
                // Use detected encoding for content
                inputs.Add(new SqlInput(path, decoded.Text));
            }
            else
            {
                // Decode as UTF-8 (default behavior), skipping BOM if present
                var offset = HasUtf8Bom(bytes) ? 3 : 0;
                var sql = Encoding.UTF8.GetString(bytes, offset, bytes.Length - offset);
                inputs.Add(new SqlInput(path, sql));
            }
        }

        return new ReadInputsResult(inputs, encodings);
    }

    private IEnumerable<string> ExpandPaths(IEnumerable<string> paths, List<string> ignorePatterns)
    {
        foreach (var path in paths)
        {
            if (Directory.Exists(path))
            {
                var matcher = new Matcher(StringComparison.OrdinalIgnoreCase);
                matcher.AddInclude("**/*.sql");

                foreach (var pattern in ignorePatterns)
                    matcher.AddExclude(pattern);

                var result = matcher.Execute(
                    new DirectoryInfoWrapper(new DirectoryInfo(path)));

                foreach (var file in result.Files)
                    yield return Path.Combine(path, file.Path);

                continue;
            }

            // For individual files, check if they match ignore patterns
            if (!ShouldIgnoreFile(path, ignorePatterns))
                yield return path;
        }
    }

    private bool ShouldIgnoreFile(string filePath, List<string> ignorePatterns)
    {
        if (ignorePatterns.Count == 0)
            return false;

        var matcher = GetIgnoreMatcher(ignorePatterns);
        var fileName = Path.GetFileName(filePath);
        var directoryPath = Path.GetDirectoryName(filePath) ?? string.Empty;

        var result = matcher.Match(directoryPath, new[] { fileName });
        return result.HasMatches;
    }

    private Matcher GetIgnoreMatcher(List<string> ignorePatterns)
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

    private static bool HasUtf8Bom(byte[] bytes)
    {
        return bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
    }
}
