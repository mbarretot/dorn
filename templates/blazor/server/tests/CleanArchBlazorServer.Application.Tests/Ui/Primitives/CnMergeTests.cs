using CleanArchBlazorServer.Web.Components.Ui.Primitives;
using Xunit;

namespace CleanArchBlazorServer.Application.Tests.Ui.Primitives;

public class CnMergeTests
{
    [Fact]
    public void Merge_ConflictingUtilityFromLaterInput_TakesPrecedence()
    {
        var result = Cn.Merge("bg-red-500", "bg-blue-500");

        Assert.Equal("bg-blue-500", result);
    }

    [Fact]
    public void Merge_UnknownClass_IsNeverDropped()
    {
        var result = Cn.Merge("unknown-made-up-class bg-red-500", "bg-blue-500");

        Assert.Equal("unknown-made-up-class bg-blue-500", result);
    }

    [Fact]
    public void Merge_VariantPrefix_IsPartOfTheGroupKey()
    {
        var result = Cn.Merge("hover:bg-red-500", "bg-blue-500");

        Assert.Equal("hover:bg-red-500 bg-blue-500", result);
    }

    [Fact]
    public void Merge_ImportantMarker_IsStrippedBeforeGroupKeyComparison()
    {
        var result = Cn.Merge("!bg-red-500", "bg-blue-500");

        Assert.Equal("bg-blue-500", result);
    }

    [Fact]
    public void Merge_NonConflictingUtilities_KeepsOriginalRelativeOrder()
    {
        var result = Cn.Merge("p-4 bg-red-500 text-white", "bg-blue-500");

        Assert.Equal("p-4 text-white bg-blue-500", result);
    }

    [Fact]
    public void Merge_NullOrEmptyInputs_AreIgnored()
    {
        var result = Cn.Merge(null, "", "  ", "px-2");

        Assert.Equal("px-2", result);
    }
}
