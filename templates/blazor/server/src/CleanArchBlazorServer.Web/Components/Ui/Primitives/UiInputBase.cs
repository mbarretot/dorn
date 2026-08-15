using Microsoft.AspNetCore.Components.Forms;

namespace CleanArchBlazorServer.Web.Components.Ui.Primitives;

// Inherits InputBase<TValue> directly: verified empirically that .NET 10's SetParametersAsync no longer throws on a null EditContext (only a missing ValueExpression does).
public abstract class UiInputBase<TValue> : InputBase<TValue>
{
    // Mirrors the framework's own aria-invalid signal so a concrete input can merge an invalid-ring class.
    protected bool IsInvalid => IsFieldInvalid(EditContext, FieldIdentifier);

    internal static bool IsFieldInvalid(
        EditContext? editContext,
        FieldIdentifier fieldIdentifier
    ) => editContext is not null && editContext.GetValidationMessages(fieldIdentifier).Any();
}
