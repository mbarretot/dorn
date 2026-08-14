using Microsoft.AspNetCore.Components;

namespace CleanArchBlazorWasm.Web.Components.Ui.Primitives;

/// <summary>
/// Controlled/uncontrolled value pattern (design C3), used by composition rather than
/// inheritance so consuming components stay free to inherit <c>ComponentBase</c> (or a later
/// interop base) themselves. A component calls <see cref="SetParameters"/> from its own
/// <c>OnParametersSet</c> and reads/writes through <see cref="CurrentValue"/>/
/// <see cref="SetValueAsync"/> only.
/// </summary>
public sealed class UiValueComponent<TValue>
{
    private TValue? _internalValue;
    private bool _hasSeededDefault;
    private EventCallback<TValue> _valueChanged;

    public TValue? Value { get; private set; }

    public bool IsControlled { get; private set; }

    public TValue? CurrentValue => IsControlled ? Value : _internalValue;

    public void SetParameters(
        TValue? value,
        EventCallback<TValue> valueChanged,
        TValue? defaultValue
    )
    {
        Value = value;
        _valueChanged = valueChanged;
        IsControlled = valueChanged.HasDelegate;

        if (!IsControlled && !_hasSeededDefault)
        {
            _internalValue = defaultValue;
            _hasSeededDefault = true;
        }
    }

    public async Task SetValueAsync(TValue value)
    {
        if (!IsControlled)
        {
            _internalValue = value;
        }

        if (_valueChanged.HasDelegate)
        {
            await _valueChanged.InvokeAsync(value);
        }
    }
}
