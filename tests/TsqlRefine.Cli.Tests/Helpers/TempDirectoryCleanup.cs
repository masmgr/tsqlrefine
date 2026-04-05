namespace TsqlRefine.Cli.Tests.Helpers;

/// <summary>
/// Utility for cleaning up temporary directories in tests with retry logic
/// to handle file-lock races on Windows.
/// </summary>
internal static class TempDirectoryCleanup
{
    /// <summary>
    /// Restores the working directory and attempts to delete the temp directory
    /// with retries to handle file-lock races on Windows.
    /// </summary>
    public static async Task CleanupAsync(string originalDir, string tempDir, int maxRetries = 3, int delayMs = 100)
    {
        Directory.SetCurrentDirectory(originalDir);

        for (var attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }

                return;
            }
            catch (IOException) when (attempt < maxRetries - 1)
            {
                await Task.Delay(delayMs);
            }
            catch (UnauthorizedAccessException) when (attempt < maxRetries - 1)
            {
                await Task.Delay(delayMs);
            }
        }
    }
}
