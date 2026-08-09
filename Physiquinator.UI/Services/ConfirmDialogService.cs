using Microsoft.Extensions.Localization;
using MudBlazor;
using Physiquinator.UI.Components.Shared;
using Physiquinator.UI.Localization;

namespace Physiquinator.UI.Services;

public static class ConfirmDialogService
{
    /// <summary>Shared wording for discarding an in-progress workout (Home and Workout page).</summary>
    public static string DiscardWorkoutMessage(string planName, IStringLocalizer<UiText> loc) =>
        loc["Remove the in-progress session for '{0}' and all logged sets? This cannot be undone.", planName];

    public static async Task<bool> ConfirmAsync(
        this IDialogService dialogService,
        string title,
        string message,
        string confirmText = "Confirm",
        MudBlazor.Color confirmColor = MudBlazor.Color.Error,
        string cancelText = "Cancel")
    {
        var parameters = new DialogParameters<ConfirmDialog>
        {
            { x => x.Message, message },
            { x => x.ConfirmText, confirmText },
            { x => x.ConfirmColor, confirmColor },
            { x => x.CancelText, cancelText },
        };

        var options = new DialogOptions
        {
            CloseButton = true,
            MaxWidth = MaxWidth.Small,
            FullWidth = true,
        };

        IDialogReference dialog = await dialogService.ShowAsync<ConfirmDialog>(title, parameters, options);
        DialogResult? result = await dialog.Result;
        return result is { Canceled: false };
    }
}
