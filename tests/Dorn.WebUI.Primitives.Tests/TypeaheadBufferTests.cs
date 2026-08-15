using Dorn.WebUI.Primitives;
using Xunit;

namespace Dorn.WebUI.Primitives.Tests;

public class TypeaheadBufferTests
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Append_MultipleCharsWithinWindow_Accumulates()
    {
        var sut = new TypeaheadBuffer();

        sut.Append('a', T0);
        var result = sut.Append('b', T0.AddMilliseconds(200));

        Assert.Equal("ab", result);
    }

    [Fact]
    public void Append_AfterOneSecondGap_ResetsBuffer()
    {
        var sut = new TypeaheadBuffer();
        sut.Append('a', T0);

        var result = sut.Append('b', T0.AddSeconds(1.5));

        Assert.Equal("b", result);
    }

    [Fact]
    public void Append_RepeatingSameCharacter_CyclesInsteadOfAccumulating()
    {
        var sut = new TypeaheadBuffer();
        sut.Append('a', T0);

        var result = sut.Append('a', T0.AddMilliseconds(200));

        Assert.Equal("a", result);
    }

    [Fact]
    public void Append_DifferentCharacterAfterRepeat_StartsNewAccumulation()
    {
        var sut = new TypeaheadBuffer();
        sut.Append('a', T0);
        sut.Append('a', T0.AddMilliseconds(200));

        var result = sut.Append('b', T0.AddMilliseconds(300));

        Assert.Equal("ab", result);
    }

    [Fact]
    public void Reset_ClearsBufferImmediately()
    {
        var sut = new TypeaheadBuffer();
        sut.Append('a', T0);

        sut.Reset();
        var result = sut.Append('z', T0.AddMilliseconds(50));

        Assert.Equal("z", result);
    }
}
