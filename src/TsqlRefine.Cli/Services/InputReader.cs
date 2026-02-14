using System.Text;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;
using TsqlRefine.Core.Engine;

namespace TsqlRefine.Cli.Services;

/// <summary>
/// Reads SQL input from files, stdin, or glob patterns while respecting ignore patterns and encoding detection.
/// </summary>
public sealed class InputReader
{
    private Matcher? _cachedIgnoreMatcher;
    private IReadOnlyList<string>? _cachedIgnorePatterns;

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

        var paths = new List<string>(args.Paths.Count);
        var readFromStdin = args.Stdin;
        foreach (var path in args.Paths)
        {
            if (path == "-")
            {
                readFromStdin = true;
                continue;
            }

            paths.Add(path);
        }

        if (readFromStdin)
        {
            var sql = args.MaxFileSize > 0
                ? await ReadBoundedAsync(stdin, args.MaxFileSize)
                : await stdin.ReadToEndAsync();
            if (sql is null)
            {
                await stderr.WriteLineAsync(
                    $"Stdin input exceeds maximum size of {args.MaxFileSize / (1024 * 1024)} MB. Use --max-file-size to increase.");
            }
            else
            {
                inputs.Add(new SqlInput("<stdin>", sql));
            }
        }

        var ignoreList = ignorePatterns as IReadOnlyList<string> ?? ignorePatterns.ToArray();
        foreach (var path in ExpandPaths(paths, ignoreList))
        {
            if (!File.Exists(path))
            {
                await stderr.WriteLineAsync($"File not found: {path}");
                continue;
            }

            // Check file size before reading
            if (args.MaxFileSize > 0)
            {
                var fileInfo = new FileInfo(path);
                if (fileInfo.Length > args.MaxFileSize)
                {
                    await stderr.WriteLineAsync(
                        $"Skipped {path}: file size ({fileInfo.Length / (1024 * 1024)} MB) exceeds maximum ({args.MaxFileSize / (1024 * 1024)} MB). Use --max-file-size to increase.");
                    continue;
                }
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

    private IEnumerable<string> ExpandPaths(IEnumerable<string> paths, IReadOnlyList<string> ignorePatterns)
    {
        foreach (var path in paths)
        {
            if (Directory.Exists(path))
            {
                var matcher = new Matcher(StringComparison.OrdinalIgnoreCase);
                matcher.AddInclude("**/*.sql");

                for (var i = 0; i < ignorePatterns.Count; i++)
                {
                    matcher.AddExclude(ignorePatterns[i]);
                }

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

    private bool ShouldIgnoreFile(string filePath, IReadOnlyList<string> ignorePatterns)
    {
        if (ignorePatterns.Count == 0)
            return false;

        var matcher = GetIgnoreMatcher(ignorePatterns);
        var fullPath = Path.GetFullPath(filePath);
        var fileName = Path.GetFileName(fullPath);
        var directoryPath = Path.GetDirectoryName(fullPath)!;

        var result = matcher.Match(directoryPath, [fileName]);
        return result.HasMatches;
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
        for (var i = 0; i < ignorePatterns.Count; i++)
        {
            matcher.AddInclude(ignorePatterns[i]);
        }

        _cachedIgnoreMatcher = matcher;
        _cachedIgnorePatterns = ignorePatterns;
        return matcher;
    }

    private static async Task<string?> ReadBoundedAsync(TextReader reader, long maxBytes)
    {
        var sb = new StringBuilder();
        var buffer = new char[8192];
        long totalBytes = 0;

        int charsRead;
        while ((charsRead = await reader.ReadAsync(buffer, 0, buffer.Length)) > 0)
        {
            totalBytes += Encoding.UTF8.GetByteCount(buffer, 0, charsRead);
            if (totalBytes > maxBytes)
            {
                return null;
            }

            sb.Append(buffer, 0, charsRead);
        }

        return sb.ToString();
    }

    private static bool HasUtf8Bom(byte[] bytes)
    {
        return bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
    }
}
