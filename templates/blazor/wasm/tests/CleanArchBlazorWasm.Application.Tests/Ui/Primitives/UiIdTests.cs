using CleanArchBlazorWasm.Web.Components.Ui.Primitives;
using Xunit;

namespace CleanArchBlazorWasm.Application.Tests.Ui.Primitives;

/// <summary>
/// Trivial structural helper — triangulated with two calls to prove uniqueness, not skipped,
/// since it does have one branching-free but stateful behavior (the counter) worth locking down.
/// </summary>
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
