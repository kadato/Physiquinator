namespace Physiquinator.Services;

/// <summary>
/// Shell-level "Set logged / Undo" toast shown after completing a set.
/// Rendered outside the scroll container so it stays viewport-fixed on mobile WebViews.
/// </summary>
public sealed class WorkoutUndoOverlayService
{
    private CancellationTokenSource? _dismissCts;
    private Func<Task>? _undoHandler;

    public bool IsVisible { get; private set; }

    public event Action? StateChanged;

    public void RegisterUndoHandler(Func<Task> handler) => _undoHandler = handler;

    public void UnregisterUndoHandler() => _undoHandler = null;

    public void Show(int autoHideMs = 5000)
    {
        _dismissCts?.Cancel();
        _dismissCts?.Dispose();
        _dismissCts = new CancellationTokenSource();
        IsVisible = true;
        NotifyChanged();
        _ = DismissAfterDelayAsync(_dismissCts.Token, autoHideMs);
    }

    public void Hide()
    {
        _dismissCts?.Cancel();
        IsVisible = false;
        NotifyChanged();
    }

    public async Task TriggerUndoAsync()
    {
        Hide();
        if (_undoHandler != null)
            await _undoHandler();
    }

    private async Task DismissAfterDelayAsync(CancellationToken cancellationToken, int autoHideMs)
    {
        try
        {
            await Task.Delay(autoHideMs, cancellationToken);
            IsVisible = false;
            NotifyChanged();
        }
        catch (OperationCanceledException) { }
    }

    private void NotifyChanged() => StateChanged?.Invoke();
}