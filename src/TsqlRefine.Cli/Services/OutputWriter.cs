using System.Text.Json;
using TsqlRefine.Core;

namespace TsqlRefine.Cli.Services;

/// <summary>
/// Writes output in various formats (text, JSON) to stdout/stderr.
/// </summary>
public sealed class OutputWriter
{
    public static async Task<int> WriteErrorAsync(TextWriter stderr, string message)
    {
        await stderr.WriteLineAsync(message);
        return ExitCodes.Fatal;
    }

    public static async Task WriteJsonOutputAsync<T>(TextWriter stdout, T data)
    {
        await stdout.WriteLineAsync(JsonSerializer.Serialize(data, JsonDefaults.Options));
    }
}
