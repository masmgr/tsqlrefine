using System.Text;

namespace TsqlRefine.Cli;

/// <summary>
/// Registers the CodePagesEncodingProvider to support legacy encodings (e.g., Shift-JIS, Windows-1252).
/// </summary>
internal static class EncodingProviderRegistry
{
    private static int _registered;

    public static void EnsureRegistered()
    {
        if (Interlocked.Exchange(ref _registered, 1) == 1)
        {
            return;
        }

        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }
}
