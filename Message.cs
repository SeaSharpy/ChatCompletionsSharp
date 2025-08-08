using Newtonsoft.Json.Linq;

namespace ChatCompletionSharp;

public class Message
{
    public required string Role { get; set; }
    public required string Content { get; set; }
    public ToolCall[]? ToolCalls { get; set; }
    public string? ToolCallId { get; set; }

    public static Message User(string content)
    {
        return new Message
        {
            Role = "user",
            Content = content
        };
    }

    public static Message Assistant(string content, ToolCall[]? toolCalls = null)
    {
        return new Message
        {
            Role = "assistant",
            Content = content,
            ToolCalls = toolCalls
        };
    }

    public static Message Developer(string content)
    {
        return new Message
        {
            Role = "developer",
            Content = content
        };
    }

    public static Message Tool(string content, ToolCall call)
    {
        return new Message
        {
            Role = "tool",
            Content = content,
            ToolCallId = call.Id
        };
    }
    public static Message Tool(string content, string id)
    {
        return new Message
        {
            Role = "tool",
            Content = content,
            ToolCallId = id
        };
    }

    internal JObject ToJson()
    {
        var data = new JObject
        {
            ["role"] = Role,
            ["content"] = Content
        };

        if (ToolCalls != null)
            data["tool_calls"] = new JArray(ToolCalls.Select(t => t.ToJson()));

        if (ToolCallId != null)
            data["tool_call_id"] = ToolCallId;

        return data;
    }
}