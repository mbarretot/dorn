namespace Dorn.Cli.Output;

public sealed class ConsoleCliOutputWriter : ICliOutputWriter
{
    public void WriteLine(string value)
    {
        Console.Out.WriteLine(value);
        Console.Out.Flush();
    }
}
