// See https://aka.ms/new-console-template for more information
using System.Text;
using TsqlRefine.Cli;

if (args.Contains("--utf8", StringComparer.OrdinalIgnoreCase))
{
    Console.InputEncoding = Encoding.UTF8;
    Console.OutputEncoding = Encoding.UTF8;
}

#pragma warning disable CA2000 // Console standard streams are process-scoped and do not require disposal
using var cancellationSource = new CancellationTokenSource();
ConsoleCancelEventHandler cancelHandler = (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellationSource.Cancel();
};

Console.CancelKeyPress += cancelHandler;
try
{
    return await CliApp.RunAsync(
        args,
        Console.OpenStandardInput(),
        Console.Out,
        Console.Error,
        cancellationSource.Token);
}
finally
{
    Console.CancelKeyPress -= cancelHandler;
}
#pragma warning restore CA2000
