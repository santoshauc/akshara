using MudBlazor;

namespace SchoolErp.AdminPortal.Shared.Ui;

/// <summary>
/// Opens the shared confirmation dialog. Callers state the action and its
/// consequence rather than asking "Are you sure?", so the dialog is worth
/// reading instead of being clicked through reflexively.
/// </summary>
public static class AkConfirm
{
    /// <summary>
    /// Returns true only when the user explicitly confirms; dismissing with
    /// Escape, the backdrop or Cancel all return false.
    /// </summary>
    public static async Task<bool> AskAsync(
        IDialogService dialogs,
        string title,
        string action,
        string? consequence = null,
        string confirmLabel = "Confirm",
        bool destructive = false)
    {
        var parameters = new DialogParameters
        {
            [nameof(AkConfirmDialog.Title)] = title,
            [nameof(AkConfirmDialog.Action)] = action,
            [nameof(AkConfirmDialog.Consequence)] = consequence,
            [nameof(AkConfirmDialog.ConfirmLabel)] = confirmLabel,
            [nameof(AkConfirmDialog.Destructive)] = destructive,
        };

        var options = new DialogOptions
        {
            MaxWidth = MaxWidth.ExtraSmall,
            FullWidth = true,
            CloseOnEscapeKey = true,
        };

        var dialog = await dialogs.ShowAsync<AkConfirmDialog>(title, parameters, options);
        var result = await dialog.Result;
        return result is { Canceled: false, Data: true };
    }
}
