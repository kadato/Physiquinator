using System.Text.Json;

namespace Physiquinator.Core.Services.Ai;

public sealed class AiToolRegistry(IEnumerable<IAiTool> tools)
{
    private readonly Dictionary<string, IAiTool> _toolsByName = tools.ToDictionary(t => t.Name, StringComparer.OrdinalIgnoreCase);

    public IEnumerable<IAiTool> GetAllTools() => _toolsByName.Values;

    public bool TryGetTool(string name, out IAiTool? tool) => _toolsByName.TryGetValue(name, out tool);

    public object GetOpenAiToolsSchema()
    {
        return _toolsByName.Values.Select(t => new
        {
            type = "function",
            function = new
            {
                name = t.Name,
                description = t.Description,
                parameters = t.ParametersSchema
            }
        }).ToList();
    }

    public async Task<string> ExecuteToolAsync(string name, string argumentsJson)
    {
        if (_toolsByName.TryGetValue(name, out var tool))
        {
            try
            {
                return await tool.ExecuteAsync(argumentsJson);
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new { success = false, error = ex.Message });
            }
        }

        return JsonSerializer.Serialize(new { success = false, error = $"Tool '{name}' not found." });
    }
}
