using System.Text;
using System.Text.Json;
using DiffPlex;
using DiffPlex.DiffBuilder;
using DiffPlex.DiffBuilder.Model;
using TsqlRefine.Core;

namespace TsqlRefine.Cli.Services;

public sealed class OutputWriter
{
    public async Task<int> WriteErrorAsync(TextWriter stderr, string message)
    {
        await stderr.WriteLineAsync(message);
        return ExitCodes.Fatal;
    }

    public string GenerateUnifiedDiff(string filePath, string original, string formatted)
    {
        if (string.Equals(original, formatted, StringComparison.Ordinal))
            return string.Empty;

        var differ = new Differ();
        var builder = new InlineDiffBuilder(differ);

        var diff = builder.BuildDiffModel(original, formatted, ignoreWhitespace: false);

        var result = new StringBuilder();
        result.AppendLine($"--- {filePath}");
        result.AppendLine($"+++ {filePath}");

        // Generate line-by-line diff
        foreach (var line in diff.Lines)
        {
            var prefix = line.Type switch
            {
                ChangeType.Deleted => "-",
                ChangeType.Inserted => "+",
                ChangeType.Modified => "!",
                _ => " "
            };

            result.AppendLine($"{prefix}{line.Text}");
        }

        return result.ToString();
    }

    public async Task WriteJsonOutputAsync<T>(TextWriter stdout, T data)
    {
        await stdout.WriteLineAsync(JsonSerializer.Serialize(data, JsonDefaults.Options));
    }

    public async Task<int?> ValidateFormatFixOptionsAsync(
        CliArgs args,
        int inputCount,
        bool outputJson,
        TextWriter stderr)
    {
        if (args.Diff && args.Write)
        {
            return await WriteErrorAsync(stderr, "--diff and --write are mutually exclusive.");
        }

        if (outputJson && args.Diff)
        {
            return await WriteErrorAsync(stderr, "--diff cannot be used with --output json.");
        }

        if (!outputJson && !args.Write && !args.Diff && inputCount > 1)
        {
            return await WriteErrorAsync(stderr, "Multiple inputs require --write or --diff.");
        }

        return null;
    }
}
