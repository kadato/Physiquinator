namespace Physiquinator.Core.Services.Ai;

/// <summary>Ready to copy MCP client configs for the Physiquinator agent endpoint.</summary>
public static class McpClientSnippets
{
    public const string FallbackEndpoint = "https://physiquinator.example/mcp";
    public const string EnvVarName = "PHYSIQUINATOR_MCP_KEY";

    public static string ExportEnv(string? token) =>
        $"export {EnvVarName}=\"{(string.IsNullOrWhiteSpace(token) ? "YOUR_API_KEY" : token)}\"";

    public static string Claude(string? endpoint, string? token)
    {
        var url = string.IsNullOrWhiteSpace(endpoint) ? FallbackEndpoint : endpoint;
        var bearer = string.IsNullOrWhiteSpace(token) ? "YOUR_API_KEY" : token;
        return $$"""
        {
          "mcpServers": {
            "physiquinator": {
              "url": "{{url}}",
              "headers": {
                "Authorization": "Bearer {{bearer}}"
              }
            }
          }
        }
        """;
    }

    public static string ClaudeCodeCli(string? endpoint, string? token)
    {
        var url = string.IsNullOrWhiteSpace(endpoint) ? FallbackEndpoint : endpoint;
        var bearer = string.IsNullOrWhiteSpace(token) ? $"${EnvVarName}" : token;
        return $"claude mcp add physiquinator {url} --header \"Authorization=Bearer {bearer}\"";
    }

    public static string OpenCode(string? endpoint, string? token, bool useEnvVar = false)
    {
        var url = string.IsNullOrWhiteSpace(endpoint) ? FallbackEndpoint : endpoint;
        var bearer = useEnvVar || string.IsNullOrWhiteSpace(token) ? $"{{env:{EnvVarName}}}" : token;
        return $$"""
        {
          "$schema": "https://opencode.ai/config.json",
          "mcp": {
            "servers": {
              "physiquinator": {
                "type": "remote",
                "url": "{{url}}",
                "headers": {
                  "Authorization": "Bearer {{bearer}}"
                }
              }
            }
          }
        }
        """;
    }

    public static string OpenCodeCli(string? endpoint, string? token, bool useEnvVar = false)
    {
        var url = string.IsNullOrWhiteSpace(endpoint) ? FallbackEndpoint : endpoint;
        var bearer = useEnvVar || string.IsNullOrWhiteSpace(token) ? $"{{env:{EnvVarName}}}" : token;
        return $"opencode mcp add physiquinator --url {url} --header 'Authorization=Bearer {bearer}'";
    }

    public static string Curl(string? endpoint, string? token)
    {
        var url = string.IsNullOrWhiteSpace(endpoint) ? FallbackEndpoint : endpoint;
        var bearer = string.IsNullOrWhiteSpace(token) ? "YOUR_API_KEY" : token;
        return "curl -s -X POST " + url + " \\\n" +
            "  -H \"Authorization: Bearer " + bearer + "\" \\\n" +
            "  -H 'Content-Type: application/json' \\\n" +
            "  -H 'Accept: application/json, text/event-stream' \\\n" +
            "  -d '{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"tools/list\",\"params\":{}}'";
    }

    public static string HealthCheckCurl(string? endpoint, string? token)
    {
        var url = string.IsNullOrWhiteSpace(endpoint) ? FallbackEndpoint : endpoint;
        var bearer = string.IsNullOrWhiteSpace(token) ? $"${EnvVarName}" : token;
        return $"curl -s -X POST {url} -H \"Authorization: Bearer {bearer}\" -H 'Content-Type: application/json' -H 'Accept: application/json, text/event-stream' -d '{{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"tools/list\",\"params\":{{}}}}' | head -c 2000";
    }
}
