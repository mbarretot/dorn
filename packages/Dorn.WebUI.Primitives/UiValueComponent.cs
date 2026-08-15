using Microsoft.AspNetCore.Components;

namespace Dorn.WebUI.Primitives;

// Controlled/uncontrolled value pattern (design C3); composition, not inheritance, so consumers stay free to inherit ComponentBase themselves.
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
