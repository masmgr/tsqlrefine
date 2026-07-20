namespace TsqlRefine.Cli.Tests;

[Collection("DirectoryChanging")]
public sealed class ConnectionStringEnvironmentTests
{
    [Fact]
    public void Parse_WithoutConnectionString_UsesEnvironmentVariable()
    {
        const string environmentValue = "Server=environment;Database=Test;Integrated Security=true;";
        var originalValue = Environment.GetEnvironmentVariable(
            CliParser.ConnectionStringEnvironmentVariable);

        try
        {
            Environment.SetEnvironmentVariable(
                CliParser.ConnectionStringEnvironmentVariable,
                environmentValue);

            var args = CliParser.Parse(["schema", "snapshot", "--output", "schema.json"]);

            Assert.Equal(environmentValue, args.SchemaConnectionString);
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                CliParser.ConnectionStringEnvironmentVariable,
                originalValue);
        }
    }

    [Fact]
    public void Parse_WithConnectionString_PrefersCommandLineValue()
    {
        const string environmentValue = "Server=environment;Database=Test;";
        const string commandLineValue = "Server=command-line;Database=Test;";
        var originalValue = Environment.GetEnvironmentVariable(
            CliParser.ConnectionStringEnvironmentVariable);

        try
        {
            Environment.SetEnvironmentVariable(
                CliParser.ConnectionStringEnvironmentVariable,
                environmentValue);

            var args = CliParser.Parse([
                "schema", "snapshot",
                "--connection-string", commandLineValue,
                "--output", "schema.json"]);

            Assert.Equal(commandLineValue, args.SchemaConnectionString);
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                CliParser.ConnectionStringEnvironmentVariable,
                originalValue);
        }
    }
}
