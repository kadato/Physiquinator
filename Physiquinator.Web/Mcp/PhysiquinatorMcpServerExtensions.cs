using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System.Security.Cryptography;

namespace Physiquinator.Web.Mcp;

/// <summary>
/// Wires the Physiquinator MCP server: stateless Streamable HTTP at /mcp,
/// registry-backed tools, optional API key auth, and optional browser CORS.
/// </summary>
public static class PhysiquinatorMcpServerExtensions
{
    public const string CorsPolicyName = "PhysiquinatorMcp";

    private const string McpPathPrefix = "/mcp";

    public static IServiceCollection AddPhysiquinatorMcpServer(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddMcpServer(options =>
            {
                options.ServerInstructions =
                    "You are the agent interface for Physiquinator, a workout and fitness tracking application. " +
                    "Use the tools to read and modify workout plans, log and query bodyweight, inspect workout history " +
                    "and statistics, and adjust app settings. Tools whose names start with 'delete_' permanently remove " +
                    "data; the client will prompt the user for confirmation before they run.";

                options.Capabilities ??= new ServerCapabilities();
                options.Capabilities.Tools = new ToolsCapability();

                options.Filters ??= new McpServerFilters();
                options.Filters.Request ??= new McpRequestFilters();
                options.Filters.Request.CallToolFilters.Add(PhysiquinatorMcpTools.CallToolTelemetryFilter);
            })
            .WithHttpTransport()
            .WithListToolsHandler(PhysiquinatorMcpTools.ListToolsAsync)
            .WithCallToolHandler(PhysiquinatorMcpTools.CallToolAsync);

        var corsOrigins = configuration["Mcp:CorsOrigins"];
        if (!string.IsNullOrWhiteSpace(corsOrigins))
        {
            var origins = corsOrigins.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            services.AddCors(cors => cors.AddPolicy(CorsPolicyName, policy =>
                policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod().AllowCredentials()));
        }

        return services;
    }

    public static IEndpointRouteBuilder MapPhysiquinatorMcp(
        this IEndpointRouteBuilder endpoints,
        IConfiguration configuration)
    {
        IEndpointConventionBuilder endpoint = endpoints.MapMcp(McpPathPrefix);
        endpoint.RequireRateLimiting("mcp");

        if (!string.IsNullOrWhiteSpace(configuration["Mcp:CorsOrigins"]))
        {
            endpoint.RequireCors(CorsPolicyName);
        }

        return endpoints;
    }

    /// <summary>
    /// Requires Mcp:ApiKey on every request to the MCP endpoint via the X-Api-Key
    /// header or the Authorization: Bearer scheme. In production the key is mandatory
    /// and requests are rejected when it is not configured.
    /// </summary>
    public static void UsePhysiquinatorMcpApiKey(this IApplicationBuilder app)
    {
        IConfiguration configuration = app.ApplicationServices.GetRequiredService<IConfiguration>();
        var apiKey = configuration["Mcp:ApiKey"];
        var requireApiKey = !string.IsNullOrWhiteSpace(apiKey)
            || app.ApplicationServices.GetRequiredService<IHostEnvironment>().IsProduction();

        if (!requireApiKey)
        {
            return;
        }

        app.Use(async (httpContext, next) =>
        {
            if (httpContext.Request.Path.StartsWithSegments(McpPathPrefix) &&
                !IsValidApiKey(httpContext, apiKey ?? string.Empty))
            {
                httpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await httpContext.Response.WriteAsync("A valid Mcp:ApiKey is required for the agent API.");
                return;
            }

            await next(httpContext);
        });
    }

    private static bool IsValidApiKey(HttpContext httpContext, string expected)
    {
        var provided = httpContext.Request.Headers["X-Api-Key"].ToString();

        if (string.IsNullOrEmpty(provided))
        {
            var authorization = httpContext.Request.Headers.Authorization.ToString();
            const string bearerPrefix = "Bearer ";

            if (authorization.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase))
            {
                provided = authorization[bearerPrefix.Length..];
            }
        }

        if (string.IsNullOrEmpty(provided))
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.UTF8.GetBytes(provided),
            System.Text.Encoding.UTF8.GetBytes(expected));
    }
}
