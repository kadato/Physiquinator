using MudBlazor;
using Physiquinator.Components.Shared;

namespace Physiquinator.Services;

public static class ConfirmDialogService
{
    public static async Task<bool> ConfirmAsync(
        IDialogService dialogService,
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

        var dialog = await dialogService.ShowAsync<ConfirmDialog>(title, parameters, options);
        var result = await dialog.Result;
        return result is { Canceled: false };
    }
}
