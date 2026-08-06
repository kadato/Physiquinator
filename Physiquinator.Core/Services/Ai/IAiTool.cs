using System.Text.Json;

namespace Physiquinator.Core.Services.Ai;

public interface IAiTool
{
    string Name { get; }
    string Description { get; }
    object ParametersSchema { get; }
    Task<string> ExecuteAsync(string argumentsJson);
}
