using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Rendering;
using Xunit;

namespace Dorn.WebUI.Primitives.Tests;

// Covers the with-EditContext half; UiInputBaseTests only covers the null-EditContext half.
public class UiInputBaseInEditFormTests
{
    [Fact]
    public async Task Binds_InsideRealEditFormWithDataAnnotationsValidator()
    {
        var model = new UiInputBaseTests.TestModel { Name = "" };
        var editContext = new EditContext(model);
        UiInputBaseTests.TestUiInput? captured = null;
        var renderer = new TestComponentRenderer();

        await renderer.RenderAsync(builder =>
            BuildEditForm(builder, editContext, model, c => captured = c)
        );

        Assert.NotNull(captured);
        await renderer.InvokeAsync(() => captured!.SimulateTyping("abcdef"));

        Assert.Equal("abcdef", captured!.BoundValue);
        Assert.Equal("abcdef", model.Name);
    }

    [Fact]
    public async Task IsInvalid_ReflectsDataAnnotationsValidator_InsideRealEditForm()
    {
        var model = new UiInputBaseTests.TestModel { Name = "" };
        var editContext = new EditContext(model);
        UiInputBaseTests.TestUiInput? captured = null;
        var renderer = new TestComponentRenderer();

        await renderer.RenderAsync(builder =>
            BuildEditForm(builder, editContext, model, c => captured = c)
        );

        await renderer.InvokeAsync(() => editContext.Validate());

        Assert.True(captured!.InvalidState);
    }

    [Fact]
    public async Task IsInvalid_ClearsAfterValidValueRevalidates()
    {
        var model = new UiInputBaseTests.TestModel { Name = "" };
        var editContext = new EditContext(model);
        UiInputBaseTests.TestUiInput? captured = null;
        var renderer = new TestComponentRenderer();

        await renderer.RenderAsync(builder =>
            BuildEditForm(builder, editContext, model, c => captured = c)
        );
        await renderer.InvokeAsync(() => editContext.Validate());
        Assert.True(captured!.InvalidState);

        await renderer.InvokeAsync(() => captured!.SimulateTyping("abcdef"));
        await renderer.InvokeAsync(() => editContext.Validate());

        Assert.False(captured!.InvalidState);
    }

    private static void BuildEditForm(
        RenderTreeBuilder builder,
        EditContext editContext,
        UiInputBaseTests.TestModel model,
        Action<UiInputBaseTests.TestUiInput> capture
    )
    {
        builder.OpenComponent<EditForm>(0);
        builder.AddComponentParameter(1, "EditContext", editContext);
        builder.AddComponentParameter(
            2,
            "ChildContent",
            (RenderFragment<EditContext>)(
                _ =>
                    childBuilder =>
                    {
                        childBuilder.OpenComponent<DataAnnotationsValidator>(0);
                        childBuilder.CloseComponent();
                        UiInputBaseTests.BuildStandaloneInput(childBuilder, model, capture);
                    }
            )
        );
        builder.CloseComponent();
    }
}
