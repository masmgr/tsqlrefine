using System.Text.Json;
using TsqlRefine.Core;

namespace TsqlRefine.Cli.Services;

public sealed class OutputWriter
{
    public async Task<int> WriteErrorAsync(TextWriter stderr, string message)
    {
        await stderr.WriteLineAsync(message);
        return ExitCodes.Fatal;
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
        if (!outputJson && !args.Write && inputCount > 1)
        {
            return await WriteErrorAsync(stderr, "Multiple inputs require --write.");
        }

        return null;
    }
}
