using CleanArchBlazorServer.Web.Components.Ui.Primitives;
using Microsoft.AspNetCore.Components;
using Xunit;

namespace CleanArchBlazorServer.Application.Tests.Ui.Primitives;

public class UiValueComponentTests
{
    [Fact]
    public async Task SetValueAsync_WhenControlled_IgnoresInternalWriteAndInvokesCallback()
    {
        var receivedValues = new List<bool>();
        var callback = EventCallback.Factory.Create<bool>(
            new object(),
            (bool v) => receivedValues.Add(v)
        );
        var sut = new UiValueComponent<bool>();
        sut.SetParameters(value: true, valueChanged: callback, defaultValue: false);

        await sut.SetValueAsync(false);

        Assert.True(sut.CurrentValue);
        Assert.Equal([false], receivedValues);
    }

    [Fact]
    public void SetParameters_WhenUncontrolled_SeedsCurrentValueFromDefaultValue()
    {
        var sut = new UiValueComponent<string>();

        sut.SetParameters(value: null, valueChanged: default, defaultValue: "seeded");

        Assert.Equal("seeded", sut.CurrentValue);
    }

    [Fact]
    public async Task SetValueAsync_WhenUncontrolled_UpdatesInternalCurrentValue()
    {
        var sut = new UiValueComponent<string>();
        sut.SetParameters(value: null, valueChanged: default, defaultValue: "seeded");

        await sut.SetValueAsync("typed");

        Assert.Equal("typed", sut.CurrentValue);
    }

    [Fact]
    public void SetParameters_WhenValueChangedDelegateAppearsMidLife_FlipsToControlled()
    {
        var sut = new UiValueComponent<bool>();
        sut.SetParameters(value: false, valueChanged: default, defaultValue: false);
        Assert.False(sut.IsControlled);

        var callback = EventCallback.Factory.Create<bool>(new object(), (bool _) => { });
        sut.SetParameters(value: true, valueChanged: callback, defaultValue: false);

        Assert.True(sut.IsControlled);
        Assert.True(sut.CurrentValue);
    }
}
