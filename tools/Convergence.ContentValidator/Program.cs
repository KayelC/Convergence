namespace Convergence.ContentValidator;

internal static class Program
{
    public static int Main(string[] args) =>
        ContentValidatorApplication.Run(args, Console.Out, Console.Error);
}
