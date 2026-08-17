using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;

namespace Physiquinator.Web.Services;

/// <summary>
/// JSON-only auth endpoints for the Blazor login page. CSRF is mitigated by requiring
/// an application/json content type (cross-origin form posts cannot set it, and the
/// endpoints enable no CORS) plus SameSite=Lax cookies; login is rate limited.
/// </summary>
public static class WebAuthEndpoints
{
    public static IEndpointRouteBuilder MapPhysiquinatorAuth(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/auth/login", LoginAsync).RequireRateLimiting("auth");
        endpoints.MapPost("/api/auth/register", RegisterAsync).RequireRateLimiting("auth");
        endpoints.MapPost("/api/auth/demo", DemoLoginAsync).RequireRateLimiting("auth");
        endpoints.MapPost("/api/auth/logout", LogoutAsync);
        return endpoints;
    }

    /// <summary>One-click sign-in as the seeded demo account for portfolio visitors.</summary>
    private static async Task<IResult> DemoLoginAsync(HttpContext context, WebUserStore users)
    {
        WebUser? user = await users.FindByUsernameAsync(users.DemoUsername);
        if (user is null)
            return Results.Problem("The demo account is not configured.", statusCode: 404);

        await SignInAsync(context, user);
        return Results.Ok();
    }

    private static async Task<IResult> LoginAsync(HttpContext context, WebUserStore users)
    {
        AuthCredentials? credentials = await ReadCredentialsAsync(context);
        if (credentials is null)
            return Error("Expected a JSON body with username and password.", StatusCodes.Status400BadRequest);

        WebUser? user = await users.ValidateCredentialsAsync(credentials.Username, credentials.Password);
        if (user is null)
            return Error("Invalid username or password.", StatusCodes.Status401Unauthorized);

        await SignInAsync(context, user);
        return Results.Ok();
    }

    private static async Task<IResult> RegisterAsync(HttpContext context, WebUserStore users)
    {
        AuthCredentials? credentials = await ReadCredentialsAsync(context);
        if (credentials is null)
            return Error("Expected a JSON body with username and password.", StatusCodes.Status400BadRequest);

        var username = credentials.Username.Trim();
        if (username.Length is < 3 or > 64 || !IsAllowedUsername(username))
            return Error("Username must be 3-64 characters using letters, digits, dot, dash, or underscore.", StatusCodes.Status400BadRequest);

        if (credentials.Password.Length < 8)
            return Error("Password must be at least 8 characters.", StatusCodes.Status400BadRequest);

        if (!await users.TryCreateAsync(username, credentials.Password))
            return Error("That username is already taken.", StatusCodes.Status409Conflict);

        WebUser? user = await users.ValidateCredentialsAsync(username, credentials.Password);
        if (user is null)
            return Error("Account was created but could not be signed in.", StatusCodes.Status500InternalServerError);

        await SignInAsync(context, user);
        return Results.Ok();
    }

    private static IResult Error(string message, int statusCode) =>
        Results.Json(new { message }, statusCode: statusCode);

    private static async Task LogoutAsync(HttpContext context)
    {
        await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    }

    private static async Task SignInAsync(HttpContext context, WebUser user)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id),
            new Claim(ClaimTypes.Name, user.Username)
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await context.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity));
    }

    private static bool IsAllowedUsername(string username) =>
        username.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '_');

    private static async Task<AuthCredentials?> ReadCredentialsAsync(HttpContext context)
    {
        if (!context.Request.ContentType?.StartsWith("application/json", StringComparison.OrdinalIgnoreCase) == true)
            return null;

        try
        {
            return await context.Request.ReadFromJsonAsync<AuthCredentials>(
                cancellationToken: context.RequestAborted);
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }

    private sealed record AuthCredentials(string Username, string Password);
}
