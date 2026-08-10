using Dorn.Cli.Commands.New;
using Xunit;

namespace Dorn.Cli.Tests.Commands.New;

public class AuthChoiceProviderTests
{
    [Fact]
    public void ForOrm_WithDapper_ExcludesCustomAuth()
    {
        var choices = AuthChoiceProvider.ForOrm("dapper");

        Assert.DoesNotContain("custom", choices);
        Assert.Contains("none", choices);
        Assert.Contains("azure-ad", choices);
    }

    [Fact]
    public void ForOrm_WithEfCore_KeepsFullChoiceList()
    {
        var choices = AuthChoiceProvider.ForOrm("efcore");

        Assert.Equal(["none", "custom", "azure-ad"], choices);
    }
}
