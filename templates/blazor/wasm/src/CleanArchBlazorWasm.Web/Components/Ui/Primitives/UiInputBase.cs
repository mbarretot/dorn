using Microsoft.AspNetCore.Components.Forms;

namespace CleanArchBlazorWasm.Web.Components.Ui.Primitives;

/// <summary>
/// Shared base for value-bound Ui inputs (design C4), e.g. <c>Input</c> and
/// <c>Select&lt;TValue&gt;</c>. Inherits the framework's own
/// <see cref="InputBase{TValue}"/> rather than an owned reimplementation: verified empirically
/// (decompiled <c>Microsoft.AspNetCore.Components.Web</c> 10.0.10) that
/// <c>InputBase&lt;TValue&gt;.SetParametersAsync</c> no longer throws when its cascading
/// <see cref="EditContext"/> is null — it only requires <c>ValueExpression</c>, which
/// <c>@bind-Value</c> always supplies. This satisfies the design's own stated fallback for that
/// exact scenario, and reuses the framework's already-tested standalone/EditContext-integrated
/// two-way binding instead of duplicating it.
/// </summary>
public abstract class UiInputBase<TValue> : InputBase<TValue>
{
    /// <summary>
    /// True only when an ambient <see cref="EditContext"/> reports a validation error for this
    /// field. The framework already reflects this into <c>aria-invalid</c> on
    /// <c>AdditionalAttributes</c>; this exposes the same signal so a concrete Ui input can
    /// additionally merge an invalid-ring Tailwind class via <see cref="Cn"/>.
    /// </summary>
    protected bool IsInvalid => IsFieldInvalid(EditContext, FieldIdentifier);

    internal static bool IsFieldInvalid(
        EditContext? editContext,
        FieldIdentifier fieldIdentifier
    ) => editContext is not null && editContext.GetValidationMessages(fieldIdentifier).Any();
}
