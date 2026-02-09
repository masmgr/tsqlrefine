// See https://aka.ms/new-console-template for more information
using System.Text;
using TsqlRefine.Cli;

if (args.Contains("--utf8", StringComparer.OrdinalIgnoreCase))
{
    Console.InputEncoding = Encoding.UTF8;
    Console.OutputEncoding = Encoding.UTF8;
}

return await CliApp.RunAsync(args, Console.OpenStandardInput(), Console.Out, Console.Error);
