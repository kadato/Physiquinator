using System.Security.Claims;

namespace Physiquinator.Web.Services;

/// <summary>
/// The authenticated account of the current Blazor circuit. Set by AuthGate before any
/// app service is resolved, so the database path provider can pick the user's database.
/// Requests that never pass through the gate (for example, MCP tool calls) get their own
/// isolated "agent" tenant instead.
/// </summary>
public sealed class WebUserContext
{
    public const string McpAgentTenant = "mcp-agent";

    private string? _tenantKey;

    public string TenantKey => _tenantKey ?? McpAgentTenant;

    public bool HasUser => _tenantKey is not null;

    public void SetFromPrincipal(ClaimsPrincipal principal)
    {
        var id = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!string.IsNullOrWhiteSpace(id))
        {
            _tenantKey = id;
        }
    }
}
