// See https://aka.ms/new-console-template for more information
using System.Text;
using TsqlRefine.Cli;

if (args.Contains("--utf8", StringComparer.OrdinalIgnoreCase))
{
    Console.InputEncoding = Encoding.UTF8;
    Console.OutputEncoding = Encoding.UTF8;
}

#pragma warning disable CA2000 // Console standard streams are process-scoped and do not require disposal
return await CliApp.RunAsync(args, Console.OpenStandardInput(), Console.Out, Console.Error);
#pragma warning restore CA2000
