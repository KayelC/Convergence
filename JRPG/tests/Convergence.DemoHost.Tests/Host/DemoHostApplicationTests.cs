using Convergence.DemoHost;
using Xunit;

namespace Convergence.DemoHost.Tests.Host;

public sealed class DemoHostApplicationTests
{
    [Fact]
    public async Task Help_PrintsEverySupportedCommandAndSucceeds()
    {
        using var output = new StringWriter();

        int exitCode = await DemoHostApplication.RunAsync(["--help"], TextReader.Null, output);

        Assert.Equal(0, exitCode);
        Assert.Contains("Convergence DemoHost", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("--clean-training-annex-play", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("--clean-training-annex-demo", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("--clean-battle-demo", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("--clean-field-demo", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("--clean-save-demo", output.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData()]
    [InlineData("--unknown")]
    [InlineData("--help", "--clean-save-demo")]
    public async Task InvalidInvocation_PrintsUsageAndReturnsNonzero(params string[] args)
    {
        using var output = new StringWriter();

        int exitCode = await DemoHostApplication.RunAsync(args, TextReader.Null, output);

        Assert.NotEqual(0, exitCode);
        Assert.Contains("Usage:", output.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("--clean-battle-demo", "Outcome: Victory")]
    [InlineData("--clean-field-demo", "Shared field effects demo completed successfully")]
    [InlineData("--clean-save-demo", "Clean save demo completed successfully")]
    [InlineData("--clean-training-annex-demo", "Training Annex runtime slice completed successfully")]
    public async Task NoninteractiveCommands_RunWithoutInput(string command, string expectedOutput)
    {
        using var output = new StringWriter();

        int exitCode = await DemoHostApplication.RunAsync([command], TextReader.Null, output);

        Assert.Equal(0, exitCode);
        Assert.Contains(expectedOutput, output.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TrainingAnnexPlay_AcceptsHostInputAndCanExitImmediately()
    {
        using var input = new StringReader("10\n");
        using var output = new StringWriter();

        int exitCode = await DemoHostApplication.RunAsync(
            ["--clean-training-annex-play"],
            input,
            output);

        Assert.Equal(0, exitCode);
        Assert.Contains("Clean Training Annex session booted.", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("Exit", output.ToString(), StringComparison.Ordinal);
    }
}
