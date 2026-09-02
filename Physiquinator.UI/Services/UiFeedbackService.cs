using MudBlazor;

namespace Physiquinator.UI.Services;

/// <summary>
/// Central helper for snackbars and confirm dialogs so pages do not repeat the same try/catch and Snackbar.Add ladders.
/// Keeps success and error tone consistent and makes future theming of feedback one place.
/// </summary>
public sealed class UiFeedbackService(ISnackbar snackbar)
{
    private readonly ISnackbar _snackbar = snackbar;

    public void Success(string message) =>
        _snackbar.Add(message, Severity.Success);

    public void Info(string message) =>
        _snackbar.Add(message, Severity.Info);

    public void Warning(string message) =>
        _snackbar.Add(message, Severity.Warning);

    public void Error(string message) =>
        _snackbar.Add(message, Severity.Error);

    public void Error(string action, Exception ex) =>
        _snackbar.Add($"{action}: {ex.Message}", Severity.Error);

    /// <summary>Shows a success snackbar with an Undo action that invokes the callback when clicked.</summary>
    public void SuccessWithUndo(string message, Func<Task> onUndo)
    {
        _snackbar.Add(message, Severity.Success, options =>
        {
            options.Action = "Undo";
            options.ActionColor = Color.Primary;
            options.RequireInteraction = true;
            options.OnClick = _ => onUndo();
        });
    }

    /// <summary>Runs the action and shows a success or error snackbar, returning true on success.</summary>
    public async Task<bool> TryExecuteAsync(Func<Task> action, string successMessage, string errorPrefix)
    {
        try
        {
            await action();
            Success(successMessage);
            return true;
        }
        catch (Exception ex)
        {
            Error(errorPrefix, ex);
            return false;
        }
    }

    public async Task<bool> TryExecuteAsync<T>(Func<Task<T>> action, Func<T, string> successMessage, string errorPrefix)
    {
        try
        {
            var result = await action();
            Success(successMessage(result));
            return true;
        }
        catch (Exception ex)
        {
            Error(errorPrefix, ex);
            return false;
        }
    }
}
