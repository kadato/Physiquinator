namespace Physiquinator.Core.Models;

public enum AiMessageRole
{
    System,
    User,
    Assistant,
    Tool
}

public class AiToolCallInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ArgumentsJson { get; set; } = "{}";
}

public class AiChatMessage
{
    public AiMessageRole Role { get; set; } = AiMessageRole.User;
    public string Content { get; set; } = string.Empty;
    public List<AiToolCallInfo>? ToolCalls { get; set; }
    public string? ToolCallId { get; set; }
    public string? ToolName { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public bool IsError { get; set; }
    public bool IsThinking { get; set; }
    public string ReasoningContent { get; set; } = string.Empty;
}

