namespace TsqlRefine.Cli.Tests;

/// <summary>
/// xUnit collection for tests that change the current directory.
/// Tests in this collection run serially to avoid race conditions.
/// </summary>
[CollectionDefinition("DirectoryChanging", DisableParallelization = true)]
public sealed class DirectoryChangingCollection
{
}
