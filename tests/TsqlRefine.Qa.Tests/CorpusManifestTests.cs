namespace TsqlRefine.Qa.Tests;

public sealed class CorpusManifestTests
{
    [Fact]
    public void Manifest_CoversEverySqlFileWithValidMetadataAndChecksum()
    {
        var files = CorpusSupport.LoadFiles();
        var listedPaths = files.Select(file => file.Path).ToHashSet(StringComparer.Ordinal);
        var diskPaths = Directory.EnumerateFiles(CorpusSupport.CorpusRoot, "*.sql", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(CorpusSupport.CorpusRoot, path).Replace('\\', '/'))
            .ToHashSet(StringComparer.Ordinal);

        Assert.Equal(diskPaths.Order(), listedPaths.Order());
        Assert.All(files, file =>
        {
            Assert.False(string.IsNullOrWhiteSpace(file.Project));
            Assert.False(string.IsNullOrWhiteSpace(file.Authors));
            Assert.True(Uri.TryCreate(file.SourceUrl, UriKind.Absolute, out _));
            Assert.False(string.IsNullOrWhiteSpace(file.Revision));
            Assert.False(string.IsNullOrWhiteSpace(file.License));
            Assert.Contains(file.MinCompatLevel, new[] { 100, 110, 120, 130, 140, 150, 160 });
            var fullPath = Path.Combine(CorpusSupport.CorpusRoot, file.Path.Replace('/', Path.DirectorySeparatorChar));
            Assert.Equal(file.Sha256, CorpusSupport.Sha256(fullPath));
        });
    }
}
