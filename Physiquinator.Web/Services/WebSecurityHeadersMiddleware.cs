namespace Physiquinator.Web.Services;

/// <summary>
/// Baseline security headers for every response. The CSP allows only same-origin
/// scripts (no inline code anywhere) while keeping 'unsafe-inline' for styles,
/// which MudBlazor needs for its inline style attributes.
/// </summary>
public static class WebSecurityHeadersMiddleware
{
    // S7039 flags 'unsafe-inline' in style-src. It is required by the MudBlazor
    // component library (inline style attributes), and scripts remain fully
    // same-origin, which is what the policy is meant to constrain.
    // Google Fonts origins are allowed because app-overrides.css and index.html
    // load JetBrains Mono / Share Tech Mono from fonts.googleapis.com.
#pragma warning disable S7039
    private const string ContentSecurityPolicy =
        "default-src 'self'; " +
        "script-src 'self'; " +
        "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; " +
        "img-src 'self' data:; " +
        "font-src 'self' data: https://fonts.gstatic.com; " +
        "connect-src 'self' ws: wss:; " +
        "frame-ancestors 'none'; " +
        "base-uri 'self'; " +
        "form-action 'self'";
#pragma warning restore S7039

    public static IApplicationBuilder UsePhysiquinatorSecurityHeaders(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            context.Response.Headers["X-Frame-Options"] = "DENY";
            context.Response.Headers["Referrer-Policy"] = "no-referrer";
#pragma warning disable S7039 // the CSP is defined above. S7039 re-flags the assignment site
            context.Response.Headers["Content-Security-Policy"] = ContentSecurityPolicy;
#pragma warning restore S7039
            context.Response.Headers["Permissions-Policy"] = "geolocation=(), camera=(), microphone=(), payment=(), usb=(), display-capture=()";
            context.Response.Headers["Cross-Origin-Opener-Policy"] = "same-origin";
            await next(context);
        });
}
