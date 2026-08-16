using Dorn.Cli.Output;
using Xunit;

namespace Dorn.Cli.Tests.Output;

public class ConsoleCliOutputWriterTests
{
    [Fact]
    public void WriteLine_WritesValueFollowedByNewLineToConsoleOut()
    {
        var originalOut = Console.Out;
        var captured = new StringWriter();
        Console.SetOut(captured);
        try
        {
            var writer = new ConsoleCliOutputWriter();

            writer.WriteLine("hello world");
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        Assert.Equal("hello world" + Environment.NewLine, captured.ToString());
    }

    [Fact]
    public void WriteLine_WritesEachCallOnItsOwnLine()
    {
        var originalOut = Console.Out;
        var captured = new StringWriter();
        Console.SetOut(captured);
        try
        {
            var writer = new ConsoleCliOutputWriter();

            writer.WriteLine("first");
            writer.WriteLine("second");
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        Assert.Equal(
            "first" + Environment.NewLine + "second" + Environment.NewLine,
            captured.ToString()
        );
    }
}
