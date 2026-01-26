// See https://aka.ms/new-console-template for more information
using TsqlRefine.Cli;

return await CliApp.RunAsync(args, Console.OpenStandardInput(), Console.Out, Console.Error);
