using System.Text.Json;

namespace TsqlRefine.Cli.Tests;

public sealed class CliPrintFormatConfigTests
{
    [Fact]
    public async Task PrintFormatConfig_OutputsDefaultValuesAsText()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var code = await CliApp.RunAsync(["print-format-config"], TextReader.Null, stdout, stderr);

        Assert.Equal(0, code);

        var output = stdout.ToString();
        Assert.Contains("Effective Formatting Options:", output);
        Assert.Contains("indentStyle", output);
        Assert.Contains("spaces", output);
        Assert.Contains("indentSize", output);
        Assert.Contains("4", output);
    }

    [Fact]
    public async Task PrintFormatConfig_WithShowSources_IncludesSourceAnnotations()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var code = await CliApp.RunAsync(["print-format-config", "--show-sources"], TextReader.Null, stdout, stderr);

        Assert.Equal(0, code);

        var output = stdout.ToString();
        Assert.Contains("(default)", output);
        Assert.Contains("Sources:", output);
    }

    [Fact]
    public async Task PrintFormatConfig_WithCliArgs_ShowsCliSource()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var code = await CliApp.RunAsync(
            ["print-format-config", "--indent-size", "2", "--indent-style", "tabs", "--show-sources"],
            TextReader.Null, stdout, stderr);

        Assert.Equal(0, code);

        var output = stdout.ToString();
        Assert.Contains("indentStyle", output);
        Assert.Contains("tabs", output);
        Assert.Contains("(CLI arg)", output);
        Assert.Contains("indentSize", output);
        Assert.Contains("2", output);
    }

    [Fact]
    public async Task PrintFormatConfig_JsonOutput_IsValidJson()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var code = await CliApp.RunAsync(["print-format-config", "--output", "json"], TextReader.Null, stdout, stderr);

        Assert.Equal(0, code);

        var output = stdout.ToString();
        Assert.False(string.IsNullOrWhiteSpace(output));

        using var doc = JsonDocument.Parse(output);
        Assert.True(doc.RootElement.TryGetProperty("options", out var options));
        Assert.True(options.TryGetProperty("indentStyle", out var indentStyle));
        Assert.True(indentStyle.TryGetProperty("value", out _));
        Assert.True(indentStyle.TryGetProperty("source", out _));
    }

    [Fact]
    public async Task PrintFormatConfig_JsonOutput_IncludesAllOptions()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var code = await CliApp.RunAsync(["print-format-config", "--output", "json"], TextReader.Null, stdout, stderr);

        Assert.Equal(0, code);

        using var doc = JsonDocument.Parse(stdout.ToString());
        var options = doc.RootElement.GetProperty("options");

        // Verify all expected options are present
        Assert.True(options.TryGetProperty("compatLevel", out _));
        Assert.True(options.TryGetProperty("indentStyle", out _));
        Assert.True(options.TryGetProperty("indentSize", out _));
        Assert.True(options.TryGetProperty("keywordCasing", out _));
        Assert.True(options.TryGetProperty("builtInFunctionCasing", out _));
        Assert.True(options.TryGetProperty("dataTypeCasing", out _));
        Assert.True(options.TryGetProperty("schemaCasing", out _));
        Assert.True(options.TryGetProperty("tableCasing", out _));
        Assert.True(options.TryGetProperty("columnCasing", out _));
        Assert.True(options.TryGetProperty("variableCasing", out _));
        Assert.True(options.TryGetProperty("commaStyle", out _));
        Assert.True(options.TryGetProperty("maxLineLength", out _));
        Assert.True(options.TryGetProperty("insertFinalNewline", out _));
        Assert.True(options.TryGetProperty("trimTrailingWhitespace", out _));
        Assert.True(options.TryGetProperty("normalizeInlineSpacing", out _));
    }

    [Fact]
    public async Task PrintFormatConfig_JsonOutput_CliArgsOverride()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var code = await CliApp.RunAsync(
            ["print-format-config", "--indent-size", "8", "--output", "json"],
            TextReader.Null, stdout, stderr);

        Assert.Equal(0, code);

        using var doc = JsonDocument.Parse(stdout.ToString());
        var options = doc.RootElement.GetProperty("options");
        var indentSize = options.GetProperty("indentSize");

        Assert.Equal(8, indentSize.GetProperty("value").GetInt32());
        Assert.Equal("cli", indentSize.GetProperty("source").GetString());
    }

    [Fact]
    public async Task PrintFormatConfig_WithConfigFile_ShowsConfigSource()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        try
        {
            Directory.CreateDirectory(tempDir);
            var configPath = Path.Combine(tempDir, "tsqlrefine.json");
            await File.WriteAllTextAsync(configPath, """
                {
                    "formatting": {
                        "indentSize": 2,
                        "keywordCasing": "lower"
                    }
                }
                """);

            var stdout = new StringWriter();
            var stderr = new StringWriter();

            var code = await CliApp.RunAsync(
                ["print-format-config", "--config", configPath, "--show-sources"],
                TextReader.Null, stdout, stderr);

            Assert.Equal(0, code);

            var output = stdout.ToString();
            Assert.Contains("indentSize", output);
            Assert.Contains("2", output);
            Assert.Contains("(tsqlrefine.json)", output);
            Assert.Contains("keywordCasing", output);
            Assert.Contains("lower", output);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task PrintFormatConfig_CliOverridesConfig()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        try
        {
            Directory.CreateDirectory(tempDir);
            var configPath = Path.Combine(tempDir, "tsqlrefine.json");
            await File.WriteAllTextAsync(configPath, """
                {
                    "formatting": {
                        "indentSize": 2
                    }
                }
                """);

            var stdout = new StringWriter();
            var stderr = new StringWriter();

            // CLI arg should override config file
            var code = await CliApp.RunAsync(
                ["print-format-config", "--config", configPath, "--indent-size", "8", "--output", "json"],
                TextReader.Null, stdout, stderr);

            Assert.Equal(0, code);

            using var doc = JsonDocument.Parse(stdout.ToString());
            var options = doc.RootElement.GetProperty("options");
            var indentSize = options.GetProperty("indentSize");

            Assert.Equal(8, indentSize.GetProperty("value").GetInt32());
            Assert.Equal("cli", indentSize.GetProperty("source").GetString());
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task PrintFormatConfig_JsonOutput_IncludesSourcePaths()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        try
        {
            Directory.CreateDirectory(tempDir);
            var configPath = Path.Combine(tempDir, "tsqlrefine.json");
            await File.WriteAllTextAsync(configPath, """{"formatting": {"indentSize": 2}}""");

            var stdout = new StringWriter();
            var stderr = new StringWriter();

            var code = await CliApp.RunAsync(
                ["print-format-config", "--config", configPath, "--output", "json"],
                TextReader.Null, stdout, stderr);

            Assert.Equal(0, code);

            using var doc = JsonDocument.Parse(stdout.ToString());
            Assert.True(doc.RootElement.TryGetProperty("sourcePaths", out var sourcePaths));
            Assert.True(sourcePaths.TryGetProperty("config", out var config));
            Assert.Contains("tsqlrefine.json", config.GetString());
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task PrintFormatConfig_Help_ShowsOptions()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var code = await CliApp.RunAsync(["print-format-config", "--help"], TextReader.Null, stdout, stderr);

        Assert.Equal(0, code);

        var output = stdout.ToString();
        Assert.Contains("print-format-config", output);
        Assert.Contains("--output", output);
        Assert.Contains("--show-sources", output);
        Assert.Contains("--indent-style", output);
        Assert.Contains("--indent-size", output);
    }
}
