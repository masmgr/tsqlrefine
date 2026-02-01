using TsqlRefine.Cli.Services;
using TsqlRefine.Formatting;

namespace TsqlRefine.Cli.Tests.Services;

public sealed class EditorConfigReaderTests
{
    private readonly EditorConfigReader _reader = new();

    [Fact]
    public void TryRead_WithNullPath_ReturnsEmptyResult()
    {
        var result = _reader.TryRead(null);

        Assert.Null(result.IndentStyle);
        Assert.Null(result.IndentSize);
        Assert.Null(result.LineEnding);
        Assert.Null(result.Path);
    }

    [Fact]
    public void TryRead_WithEmptyPath_ReturnsEmptyResult()
    {
        var result = _reader.TryRead(string.Empty);

        Assert.Null(result.IndentStyle);
        Assert.Null(result.IndentSize);
        Assert.Null(result.LineEnding);
        Assert.Null(result.Path);
    }

    [Fact]
    public void TryRead_WithStdinPath_ReturnsEmptyResult()
    {
        var result = _reader.TryRead("<stdin>");

        Assert.Null(result.IndentStyle);
        Assert.Null(result.IndentSize);
        Assert.Null(result.LineEnding);
        Assert.Null(result.Path);
    }

    [Fact]
    public void TryRead_WithNonSqlExtension_ReturnsEmptyResult()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            var editorConfigPath = Path.Combine(tempDir, ".editorconfig");
            File.WriteAllText(editorConfigPath, "root = true\n[*]\nindent_style = tab\n");

            var txtPath = Path.Combine(tempDir, "test.txt");
            File.WriteAllText(txtPath, "content");

            var result = _reader.TryRead(txtPath);

            Assert.Null(result.IndentStyle);
            Assert.Null(result.IndentSize);
            Assert.Null(result.LineEnding);
            Assert.Null(result.Path);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void TryRead_WithValidEditorConfig_ReturnsIndentStyleTabs()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            var editorConfigPath = Path.Combine(tempDir, ".editorconfig");
            File.WriteAllText(editorConfigPath, "root = true\n[*.sql]\nindent_style = tab\n");

            var sqlPath = Path.Combine(tempDir, "test.sql");
            File.WriteAllText(sqlPath, "SELECT 1");

            var result = _reader.TryRead(sqlPath);

            Assert.Equal(IndentStyle.Tabs, result.IndentStyle);
            Assert.Equal(editorConfigPath, result.Path);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void TryRead_WithValidEditorConfig_ReturnsIndentStyleSpaces()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            var editorConfigPath = Path.Combine(tempDir, ".editorconfig");
            File.WriteAllText(editorConfigPath, "root = true\n[*.sql]\nindent_style = space\n");

            var sqlPath = Path.Combine(tempDir, "test.sql");
            File.WriteAllText(sqlPath, "SELECT 1");

            var result = _reader.TryRead(sqlPath);

            Assert.Equal(IndentStyle.Spaces, result.IndentStyle);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void TryRead_WithValidEditorConfig_ReturnsIndentSize()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            var editorConfigPath = Path.Combine(tempDir, ".editorconfig");
            File.WriteAllText(editorConfigPath, "root = true\n[*.sql]\nindent_size = 2\n");

            var sqlPath = Path.Combine(tempDir, "test.sql");
            File.WriteAllText(sqlPath, "SELECT 1");

            var result = _reader.TryRead(sqlPath);

            Assert.Equal(2, result.IndentSize);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void TryRead_WithValidEditorConfig_ReturnsLineEndingLf()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            var editorConfigPath = Path.Combine(tempDir, ".editorconfig");
            File.WriteAllText(editorConfigPath, "root = true\n[*.sql]\nend_of_line = lf\n");

            var sqlPath = Path.Combine(tempDir, "test.sql");
            File.WriteAllText(sqlPath, "SELECT 1");

            var result = _reader.TryRead(sqlPath);

            Assert.Equal(LineEnding.Lf, result.LineEnding);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void TryRead_WithValidEditorConfig_ReturnsLineEndingCrLf()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            var editorConfigPath = Path.Combine(tempDir, ".editorconfig");
            File.WriteAllText(editorConfigPath, "root = true\n[*.sql]\nend_of_line = crlf\n");

            var sqlPath = Path.Combine(tempDir, "test.sql");
            File.WriteAllText(sqlPath, "SELECT 1");

            var result = _reader.TryRead(sqlPath);

            Assert.Equal(LineEnding.CrLf, result.LineEnding);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void TryRead_WithCrLineEnding_NormalizesToLf()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            var editorConfigPath = Path.Combine(tempDir, ".editorconfig");
            File.WriteAllText(editorConfigPath, "root = true\n[*.sql]\nend_of_line = cr\n");

            var sqlPath = Path.Combine(tempDir, "test.sql");
            File.WriteAllText(sqlPath, "SELECT 1");

            var result = _reader.TryRead(sqlPath);

            Assert.Equal(LineEnding.Lf, result.LineEnding);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void TryRead_WithTabIndentSize_UsesTabWidth()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            var editorConfigPath = Path.Combine(tempDir, ".editorconfig");
            File.WriteAllText(editorConfigPath, "root = true\n[*.sql]\nindent_size = tab\ntab_width = 8\n");

            var sqlPath = Path.Combine(tempDir, "test.sql");
            File.WriteAllText(sqlPath, "SELECT 1");

            var result = _reader.TryRead(sqlPath);

            Assert.Equal(8, result.IndentSize);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void TryRead_WithDirectory_LooksUpDummySql()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            var editorConfigPath = Path.Combine(tempDir, ".editorconfig");
            File.WriteAllText(editorConfigPath, "root = true\n[*.sql]\nindent_style = tab\nindent_size = 2\n");

            var result = _reader.TryRead(tempDir);

            Assert.Equal(IndentStyle.Tabs, result.IndentStyle);
            Assert.Equal(2, result.IndentSize);
            Assert.Equal(editorConfigPath, result.Path);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void TryRead_WithAllOptions_ReturnsAllValues()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            var editorConfigPath = Path.Combine(tempDir, ".editorconfig");
            File.WriteAllText(editorConfigPath, "root = true\n[*.sql]\nindent_style = tab\nindent_size = 2\nend_of_line = lf\n");

            var sqlPath = Path.Combine(tempDir, "test.sql");
            File.WriteAllText(sqlPath, "SELECT 1");

            var result = _reader.TryRead(sqlPath);

            Assert.Equal(IndentStyle.Tabs, result.IndentStyle);
            Assert.Equal(2, result.IndentSize);
            Assert.Equal(LineEnding.Lf, result.LineEnding);
            Assert.Equal(editorConfigPath, result.Path);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void TryRead_WithNoMatchingSection_ReturnsEmptyResult()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            var editorConfigPath = Path.Combine(tempDir, ".editorconfig");
            File.WriteAllText(editorConfigPath, "root = true\n[*.cs]\nindent_style = tab\n");

            var sqlPath = Path.Combine(tempDir, "test.sql");
            File.WriteAllText(sqlPath, "SELECT 1");

            var result = _reader.TryRead(sqlPath);

            Assert.Null(result.IndentStyle);
            Assert.Null(result.IndentSize);
            Assert.Null(result.LineEnding);
            Assert.Null(result.Path);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void TryRead_WithInvalidIndentSize_ReturnsNull()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            var editorConfigPath = Path.Combine(tempDir, ".editorconfig");
            File.WriteAllText(editorConfigPath, "root = true\n[*.sql]\nindent_size = invalid\n");

            var sqlPath = Path.Combine(tempDir, "test.sql");
            File.WriteAllText(sqlPath, "SELECT 1");

            var result = _reader.TryRead(sqlPath);

            Assert.Null(result.IndentSize);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void TryRead_WithZeroIndentSize_ReturnsNull()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            var editorConfigPath = Path.Combine(tempDir, ".editorconfig");
            File.WriteAllText(editorConfigPath, "root = true\n[*.sql]\nindent_size = 0\n");

            var sqlPath = Path.Combine(tempDir, "test.sql");
            File.WriteAllText(sqlPath, "SELECT 1");

            var result = _reader.TryRead(sqlPath);

            Assert.Null(result.IndentSize);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    private static string CreateTempDirectory()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"tsqlrefine-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        return tempDir;
    }
}
