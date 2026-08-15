using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using Dorn.WebUI.Primitives;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Rendering;
using Xunit;

namespace Dorn.WebUI.Primitives.Tests;

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

    internal static void BuildStandaloneInput(
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

    // Annotations are inert here (no validator attached); UiInputBaseInEditFormTests reuses this model with a real one.
    internal sealed class TestModel
    {
        [Required]
        [MinLength(3)]
        public string Name { get; set; } = "";
    }

    internal sealed class TestUiInput : UiInputBase<string>
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
