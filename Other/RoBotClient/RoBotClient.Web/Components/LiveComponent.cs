using Microsoft.AspNetCore.Components;

namespace RoBotClient.Web.Components;

/// <summary>
/// Base class for pages/components that should re-render periodically to reflect live bot state.
/// Inherit with <c>@inherits LiveComponent</c>; override <see cref="RefreshMs"/> to change the rate.
/// </summary>
public abstract class LiveComponent : ComponentBase, IDisposable
{
    private PeriodicTimer? _timer;

    protected virtual int RefreshMs => 500;

    protected override void OnInitialized()
    {
        _timer = new PeriodicTimer(TimeSpan.FromMilliseconds(RefreshMs));
        _ = LoopAsync();
    }

    private async Task LoopAsync()
    {
        try
        {
            while (await _timer!.WaitForNextTickAsync())
                await InvokeAsync(StateHasChanged);
        }
        catch (OperationCanceledException) { }
        catch (ObjectDisposedException) { }
    }

    public virtual void Dispose() => _timer?.Dispose();
}
