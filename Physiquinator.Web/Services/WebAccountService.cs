using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Physiquinator.Core.Services;

namespace Physiquinator.Web.Services;

/// <summary>
/// Web-host account actions: posts to the auth logout endpoint via the
/// bootstrap JS (so the Set-Cookie reaches the browser) and reloads the page
/// back to the login gate.
/// </summary>
public sealed class WebAccountService(IJSRuntime jsRuntime, NavigationManager navigation) : IAccountService
{
    public bool IsSignOutSupported => true;

    public async Task SignOutAsync()
    {
        try
        {
            await jsRuntime.InvokeVoidAsync("physiquinatorAuth.logout");
        }
        finally
        {
            navigation.NavigateTo("/", forceLoad: true);
        }
    }
}
