using Newtonsoft.Json.Linq;

namespace ChatCompletionsSharp;

public class Message
{
    public required string Role { get; set; }
    public required string Content { get; set; }
    public ToolCall[]? ToolCalls { get; set; }
    public string? ToolCallId { get; set; }

    public string? Name { get; set; }

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

    public static Message FromJson(JToken json)
    {
        var message = new Message
        {
            Role = json["role"]?.ToString()
                ?? throw new ArgumentNullException("Message must have a role."),
            Content = json["content"]?.ToString()
                ?? throw new ArgumentNullException("Message must have a content.")
        };
        if (json["tool_calls"] is JArray toolCallsArray)
        {
            message.ToolCalls = toolCallsArray
                .Select(t => ToolCall.FromJson(t))
                .ToArray();
        }
        message.ToolCallId = json["tool_call_id"]?.ToString();
        message.Name = json["name"]?.ToString();
        return message;
    }

    public override string ToString()
    {
        var toolCallsStr = ToolCalls != null
            ? $"[{string.Join(", ", ToolCalls.Select(t => t.ToString()))}]"
            : "null";

        return $"Role: {Role}, Name: {Name}, Content: {Content}, ToolCalls: {toolCallsStr}, ToolCallId: {ToolCallId}";
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

        if (Name != null)
            data["name"] = Name;

        return data;
    }
}