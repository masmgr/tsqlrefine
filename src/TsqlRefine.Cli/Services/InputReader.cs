using System.Text;
using TsqlRefine.Core.Engine;

namespace TsqlRefine.Cli.Services;

/// <summary>
/// Reads SQL input from files, stdin, or glob patterns while respecting ignore patterns and encoding detection.
/// </summary>
public sealed class InputReader
{
    private readonly InputPathDiscovery _pathDiscovery = new();

    public sealed record ReadInputsResult(
        List<SqlInput> Inputs,
        IReadOnlyDictionary<string, Encoding> WriteEncodings);

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Maintainability", "CA1502", Justification = "Existing input discovery workflow; tracked as complexity baseline debt.")]
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

        var readablePaths = DiscoverPaths(args, paths, ignorePatterns, stderr);
        var files = await ReadPathsAsync(args, readablePaths);
        inputs.AddRange(files.Inputs);
        foreach (var pair in files.WriteEncodings)
        {
            encodings[pair.Key] = pair.Value;
        }

        return new ReadInputsResult(inputs, encodings);
    }

    public List<string> DiscoverPaths(
        CliArgs args,
        IEnumerable<string> paths,
        IEnumerable<string> ignorePatterns,
        TextWriter stderr)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(ignorePatterns);
        ArgumentNullException.ThrowIfNull(stderr);

        return _pathDiscovery.Discover(args, paths, ignorePatterns, stderr);
    }

    public static async Task<ReadInputsResult> ReadPathsAsync(
        CliArgs args,
        IReadOnlyList<string> readablePaths)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(readablePaths);

        var slots = new (SqlInput Input, Encoding WriteEncoding)?[readablePaths.Count];
        await Parallel.ForEachAsync(
            Enumerable.Range(0, readablePaths.Count),
            async (index, cancellationToken) =>
            {
                var path = readablePaths[index];
                var bytes = await File.ReadAllBytesAsync(path, cancellationToken);
                if (args.DetectEncoding || ShouldUseDetectedEncodingForWriteBack(args.Command))
                {
                    var decoded = CharsetDetection.Decode(bytes);
                    slots[index] = (new SqlInput(path, decoded.Text), decoded.WriteEncoding);
                }
                else
                {
                    var utf8Offset = HasUtf8Bom(bytes) ? 3 : 0;
                    var sql = Encoding.UTF8.GetString(bytes, utf8Offset, bytes.Length - utf8Offset);
                    slots[index] = (new SqlInput(path, sql), Encoding.UTF8);
                }
            });

        var inputs = new List<SqlInput>(readablePaths.Count);
        var encodings = new Dictionary<string, Encoding>(readablePaths.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var slot in slots)
        {
            if (slot is not { } value)
            {
                continue;
            }

            inputs.Add(value.Input);
            encodings[value.Input.FilePath] = value.WriteEncoding;
        }

        return new ReadInputsResult(inputs, encodings);
    }

    private static bool ShouldUseDetectedEncodingForWriteBack(string command)
    {
        return command is "format" or "fix";
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
