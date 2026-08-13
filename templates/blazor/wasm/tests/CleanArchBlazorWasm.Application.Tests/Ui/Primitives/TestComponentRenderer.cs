using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace CleanArchBlazorWasm.Application.Tests.Ui.Primitives;

/// <summary>
/// Minimal component-rendering harness for Application-tier tests that must exercise real
/// Blazor parameter/cascading-parameter binding before the Functional (bUnit) tier exists
/// (design: bUnit ships with Dialog in PR4). Drives <c>SetParametersAsync</c> and cascading
/// values with no DOM — the smallest surface that satisfies <see cref="Renderer"/>'s abstract
/// contract.
/// </summary>
internal sealed class TestComponentRenderer()
    : Renderer(new ServiceCollection().BuildServiceProvider(), NullLoggerFactory.Instance)
{
    public override Dispatcher Dispatcher { get; } = Dispatcher.CreateDefault();

    protected override void HandleException(Exception exception) => throw exception;

    protected override Task UpdateDisplayAsync(in RenderBatch renderBatch) => Task.CompletedTask;

    public Task RenderAsync(RenderFragment fragment) =>
        Dispatcher.InvokeAsync(() =>
            RenderRootComponentAsync(
                AssignRootComponentId(new FragmentHost(fragment)),
                ParameterView.Empty
            )
        );

    public Task InvokeAsync(Action callback) => Dispatcher.InvokeAsync(callback);

    private sealed class FragmentHost(RenderFragment fragment) : IComponent
    {
        private RenderHandle _renderHandle;

        public void Attach(RenderHandle renderHandle) => _renderHandle = renderHandle;

        public Task SetParametersAsync(ParameterView parameters)
        {
            _renderHandle.Render(fragment);
            return Task.CompletedTask;
        }
    }
}
