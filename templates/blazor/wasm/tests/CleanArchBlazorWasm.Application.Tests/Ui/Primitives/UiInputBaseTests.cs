using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using CleanArchBlazorWasm.Web.Components.Ui.Primitives;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Rendering;
using Xunit;

namespace CleanArchBlazorWasm.Application.Tests.Ui.Primitives;

/// <summary>
/// <see cref="UiInputBase{TValue}"/> binding tests — distinct from Input's own bUnit tests
/// (design C4/PR5): this exercises the base class alone. Verified empirically before writing
/// this base (decompiled <c>Microsoft.AspNetCore.Components.Web</c> 10.0.10): .NET 10's
/// <c>InputBase&lt;TValue&gt;.SetParametersAsync</c> no longer throws when its cascading
/// <see cref="EditContext"/> is null — only a missing <c>ValueExpression</c> throws. Design's
/// own fallback ("prefer inheriting it over the owned UiInputBase&lt;T&gt;") therefore applies.
/// </summary>
public class UiInputBaseTests
{
    [Fact]
    public async Task Binds_WithNoAmbientEditContext()
    {
        var model = new TestModel { Name = "seed" };
        TestUiInput? captured = null;
        var renderer = new TestComponentRenderer();

        await renderer.RenderAsync(builder =>
            BuildStandaloneInput(builder, model, c => captured = c)
        );

        Assert.NotNull(captured);
        await renderer.InvokeAsync(() => captured!.SimulateTyping("typed-value"));

        Assert.Equal("typed-value", captured!.BoundValue);
        Assert.Equal("typed-value", model.Name);
    }

    [Fact]
    public async Task IsInvalid_IsFalse_WithNoAmbientEditContext()
    {
        var model = new TestModel { Name = "seed" };
        TestUiInput? captured = null;
        var renderer = new TestComponentRenderer();

        await renderer.RenderAsync(builder =>
            BuildStandaloneInput(builder, model, c => captured = c)
        );

        Assert.False(captured!.InvalidState);
    }

    [Fact]
    public async Task IsInvalid_ReflectsAmbientEditContextValidationState()
    {
        var model = new TestModel { Name = "seed" };
        var editContext = new EditContext(model);
        var fieldIdentifier = FieldIdentifier.Create(() => model.Name);
        var messageStore = new ValidationMessageStore(editContext);
        messageStore.Add(fieldIdentifier, "Name is required.");
        editContext.NotifyValidationStateChanged();

        TestUiInput? captured = null;
        var renderer = new TestComponentRenderer();

        await renderer.RenderAsync(builder =>
            BuildInputWithEditContext(builder, model, editContext, c => captured = c)
        );

        Assert.True(captured!.InvalidState);
    }

    private static void BuildStandaloneInput(
        RenderTreeBuilder builder,
        TestModel model,
        Action<TestUiInput> capture
    )
    {
        Expression<Func<string>> valueExpression = () => model.Name;
        builder.OpenComponent<TestUiInput>(0);
        builder.AddComponentParameter(1, "Value", model.Name);
        builder.AddComponentParameter(
            2,
            "ValueChanged",
            EventCallback.Factory.Create<string>(model, v => model.Name = v)
        );
        builder.AddComponentParameter(3, "ValueExpression", valueExpression);
        builder.AddComponentReferenceCapture(4, r => capture((TestUiInput)r));
        builder.CloseComponent();
    }

    private static void BuildInputWithEditContext(
        RenderTreeBuilder builder,
        TestModel model,
        EditContext editContext,
        Action<TestUiInput> capture
    )
    {
        builder.OpenComponent<CascadingValue<EditContext>>(0);
        builder.AddComponentParameter(1, "Value", editContext);
        builder.AddComponentParameter(
            2,
            "ChildContent",
            (RenderFragment)(childBuilder => BuildStandaloneInput(childBuilder, model, capture))
        );
        builder.CloseComponent();
    }

    private sealed class TestModel
    {
        public string Name { get; set; } = "";
    }

    private sealed class TestUiInput : UiInputBase<string>
    {
        public string BoundValue => CurrentValue ?? "";

        public bool InvalidState => IsInvalid;

        public void SimulateTyping(string raw) => CurrentValueAsString = raw;

        protected override bool TryParseValueFromString(
            string? value,
            [MaybeNullWhen(false)] out string result,
            [NotNullWhen(false)] out string? validationErrorMessage
        )
        {
            result = value ?? "";
            validationErrorMessage = null;
            return true;
        }
    }
}
