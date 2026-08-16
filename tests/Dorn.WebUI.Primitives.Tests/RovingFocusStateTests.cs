using Dorn.WebUI.Primitives;
using Xunit;

namespace Dorn.WebUI.Primitives.Tests;

public class RovingFocusStateTests
{
    private static RovingFocusState CreateWithThreeItems(bool loop = true) =>
        new RovingFocusState(RovingFocusOrientation.Vertical, loop).WithItems("a", "b", "c");

    [Fact]
    public void SetItems_ActivatesTheFirstItemByDefault()
    {
        var sut = CreateWithThreeItems();

        Assert.True(sut.IsActive("a"));
        Assert.False(sut.IsActive("b"));
    }

    [Fact]
    public void MoveNext_ArrowDown_AdvancesToTheNextItem()
    {
        var sut = CreateWithThreeItems();

        sut.HandleKey("ArrowDown");

        Assert.True(sut.IsActive("b"));
    }

    [Fact]
    public void MoveNext_AtLastItem_WithLoopEnabled_WrapsToFirst()
    {
        var sut = CreateWithThreeItems(loop: true);
        sut.HandleKey("ArrowDown");
        sut.HandleKey("ArrowDown");

        sut.HandleKey("ArrowDown");

        Assert.True(sut.IsActive("a"));
    }

    [Fact]
    public void MoveNext_AtLastItem_WithLoopDisabled_ClampsAtLast()
    {
        var sut = CreateWithThreeItems(loop: false);
        sut.HandleKey("ArrowDown");
        sut.HandleKey("ArrowDown");

        sut.HandleKey("ArrowDown");

        Assert.True(sut.IsActive("c"));
    }

    [Fact]
    public void MovePrevious_SkipsDisabledItems()
    {
        var sut = new RovingFocusState(RovingFocusOrientation.Vertical, loop: true).WithItems(
            ("a", false),
            ("b", true),
            ("c", false)
        );

        sut.HandleKey("ArrowUp"); // from a, moving up wraps: skip disabled b, land on c

        Assert.True(sut.IsActive("c"));
    }

    [Fact]
    public void HandleKey_Home_ActivatesFirstEnabledItem()
    {
        var sut = new RovingFocusState(RovingFocusOrientation.Vertical, loop: true).WithItems(
            ("a", true),
            ("b", false),
            ("c", false)
        );
        sut.HandleKey("End");

        sut.HandleKey("Home");

        Assert.True(sut.IsActive("b"));
    }

    [Fact]
    public void HandleKey_End_ActivatesLastEnabledItem()
    {
        var sut = CreateWithThreeItems();

        sut.HandleKey("End");

        Assert.True(sut.IsActive("c"));
    }

    [Fact]
    public void HandleKey_HorizontalOrientation_IgnoresVerticalArrowKeys()
    {
        var sut = new RovingFocusState(RovingFocusOrientation.Horizontal, loop: true).WithItems(
            "a",
            "b",
            "c"
        );

        var handled = sut.HandleKey("ArrowDown");

        Assert.False(handled);
        Assert.True(sut.IsActive("a"));
    }

    [Fact]
    public void HandleKey_HorizontalOrientation_RespondsToArrowRight()
    {
        var sut = new RovingFocusState(RovingFocusOrientation.Horizontal, loop: true).WithItems(
            "a",
            "b",
            "c"
        );

        var handled = sut.HandleKey("ArrowRight");

        Assert.True(handled);
        Assert.True(sut.IsActive("b"));
    }

    [Fact]
    public void TabIndexFor_OnlyActiveItemIsZero_RestAreMinusOne()
    {
        var sut = CreateWithThreeItems();

        Assert.Equal(0, sut.TabIndexFor("a"));
        Assert.Equal(-1, sut.TabIndexFor("b"));
        Assert.Equal(-1, sut.TabIndexFor("c"));
    }

    [Fact]
    public void TrySetActive_ExistingEnabledItem_ActivatesIt_AndReturnsTrue()
    {
        var sut = CreateWithThreeItems();

        var result = sut.TrySetActive("c");

        Assert.True(result);
        Assert.True(sut.IsActive("c"));
    }

    [Fact]
    public void TrySetActive_DisabledOrMissingItem_LeavesActiveUnchanged_AndReturnsFalse()
    {
        var sut = new RovingFocusState(RovingFocusOrientation.Vertical, loop: true).WithItems(
            ("a", false),
            ("b", true)
        );

        var disabledResult = sut.TrySetActive("b");
        var missingResult = sut.TrySetActive("z");

        Assert.False(disabledResult);
        Assert.False(missingResult);
        Assert.True(sut.IsActive("a"));
    }
}
