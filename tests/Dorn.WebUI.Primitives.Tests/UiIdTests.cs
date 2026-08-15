using Dorn.WebUI.Primitives;
using Xunit;

namespace Dorn.WebUI.Primitives.Tests;

public class UiIdTests
{
    [Fact]
    public void New_DefaultPrefix_ProducesAUiPrefixedId()
    {
        var id = UiId.New();

        Assert.StartsWith("ui-", id);
    }

    [Fact]
    public void New_ConsecutiveCalls_ProduceDistinctIds()
    {
        var first = UiId.New();
        var second = UiId.New();

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void New_CustomPrefix_IsUsedInsteadOfDefault()
    {
        var id = UiId.New("field");

        Assert.StartsWith("field-", id);
    }
}
