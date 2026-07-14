using JRPGPrototype.Host;
using JRPGPrototype.Host.CleanConsole.TrainingAnnex;

namespace Convergence.DemoHost;

internal static class Program
{
    private static Task<int> Main(string[] args) =>
        DemoHostApplication.RunAsync(args, Console.In, Console.Out);
}

internal static class DemoHostApplication
{
    private const int UsageError = 2;

    public static async Task<int> RunAsync(
        IReadOnlyList<string> args,
        TextReader input,
        TextWriter output,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(output);

        if (args.Count != 1)
        {
            await WriteUsageAsync(output, cancellationToken).ConfigureAwait(false);
            return UsageError;
        }

        string contentRoot = Path.Combine(AppContext.BaseDirectory, "Content");
        return args[0] switch
        {
            "--clean-battle-demo" => await new CleanBattleDemoHost(output, contentRoot)
                .RunAsync(cancellationToken).ConfigureAwait(false),
            "--clean-field-demo" => await new CleanFieldDemoHost(output, contentRoot)
                .RunAsync(cancellationToken).ConfigureAwait(false),
            "--clean-save-demo" => await new CleanSaveDemoHost(output, contentRoot)
                .RunAsync(cancellationToken).ConfigureAwait(false),
            "--clean-training-annex-demo" => await new CleanTrainingAnnexDemoHost(output, contentRoot)
                .RunAsync(cancellationToken).ConfigureAwait(false),
            "--clean-training-annex-play" => await RunTrainingAnnexAsync(
                input,
                output,
                contentRoot,
                cancellationToken).ConfigureAwait(false),
            "--help" => await WriteHelpAsync(output, cancellationToken).ConfigureAwait(false),
            _ => await WriteUnknownCommandAsync(output, args[0], cancellationToken).ConfigureAwait(false)
        };
    }

    private static Task<int> RunTrainingAnnexAsync(
        TextReader input,
        TextWriter output,
        string contentRoot,
        CancellationToken cancellationToken)
    {
        var host = new CleanTrainingAnnexPlayHost(
            new FileContentPackSource(contentRoot),
            new TextWriterEventSink(output),
            new TextReaderCommandSource<CleanTrainingAnnexPlayCommand>(input, output),
            new TrainingAnnexMinimumRandomSource());
        return host.RunAsync(cancellationToken);
    }

    private static async Task<int> WriteHelpAsync(TextWriter output, CancellationToken cancellationToken)
    {
        await WriteUsageAsync(output, cancellationToken).ConfigureAwait(false);
        return 0;
    }

    private static async Task<int> WriteUnknownCommandAsync(
        TextWriter output,
        string command,
        CancellationToken cancellationToken)
    {
        await output.WriteLineAsync($"Unknown command: {command}".AsMemory(), cancellationToken).ConfigureAwait(false);
        await WriteUsageAsync(output, cancellationToken).ConfigureAwait(false);
        return UsageError;
    }

    private static async Task WriteUsageAsync(TextWriter output, CancellationToken cancellationToken)
    {
        await output.WriteLineAsync("Convergence DemoHost".AsMemory(), cancellationToken).ConfigureAwait(false);
        await output.WriteLineAsync("Usage: dotnet run --project samples/Convergence.DemoHost -- <command>".AsMemory(), cancellationToken)
            .ConfigureAwait(false);
        await output.WriteLineAsync("  --clean-training-annex-play".AsMemory(), cancellationToken).ConfigureAwait(false);
        await output.WriteLineAsync("  --clean-training-annex-demo".AsMemory(), cancellationToken).ConfigureAwait(false);
        await output.WriteLineAsync("  --clean-battle-demo".AsMemory(), cancellationToken).ConfigureAwait(false);
        await output.WriteLineAsync("  --clean-field-demo".AsMemory(), cancellationToken).ConfigureAwait(false);
        await output.WriteLineAsync("  --clean-save-demo".AsMemory(), cancellationToken).ConfigureAwait(false);
        await output.WriteLineAsync("  --help".AsMemory(), cancellationToken).ConfigureAwait(false);
    }
}
