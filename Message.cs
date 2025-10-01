using Newtonsoft.Json.Linq;

namespace ChatCompletionsSharp;

public class Message
{
    public required string Role;
    public string? Content;
    public string[]? ImageUrls;
    public ToolCall[]? ToolCalls;
    public string? ToolCallId;
    public string? Meta1;
    public string? Meta2;
    public string? Meta3;

    public string? Name;
    internal Message() { }

    public static Message User(string content, params string[] imageUrls)
    {
        return new Message
        {
            Role = "user",
            Content = content,
            ImageUrls = imageUrls.Length > 0 ? imageUrls : null
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

    public static Message FromJson(Dictionary<string, Tool> toolTypes, JToken json)
    {
        var message = new Message
        {
            Role = json["role"]?.ToString()
                ?? throw new ArgumentNullException("Message must have a role."),
        };
        var contentToken = json["content"];
        if (contentToken is JArray contentArray)
        {
            message.Content = string.Join("", contentArray
                .Where(t => t["type"]?.ToString() == "text")
                .Select(t => t["text"]?.ToString()));

            if (message.Role == "user")
            {
                var urls = contentArray
                    .Where(t => t["type"]?.ToString() == "image_url")
                    .Select(t => t["image_url"]?["url"]?.ToString())
                    .Where(u => !string.IsNullOrEmpty(u))
                    .Cast<string>()
                    .ToArray();
                if (urls.Length > 0)
                    message.ImageUrls = urls;
            }
        }
        else
        {
            message.Content = contentToken?.ToString() ?? throw new ArgumentNullException("Message must have a content.");
        }
        if (json["tool_calls"] is JArray toolCallsArray)
        {
            message.ToolCalls = toolCallsArray
                .Select(t => ToolCall.FromJson(toolTypes, t))
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

        var contentStr = ImageUrls != null
            ? $"{Content} + [{string.Join(", ", ImageUrls)}]"
            : Content;

        return $"Role: {Role}, Name: {Name}, Content: {contentStr}, ToolCalls: {toolCallsStr}, ToolCallId: {ToolCallId}";
    }

    internal JObject ToJson(string? detail = null)
    {
        var data = new JObject
        {
            ["role"] = Role,
        };

        if (ImageUrls != null && Role == "user")
        {
            var array = new JArray();
            if (Content != null)
                array.Add(new JObject { ["type"] = "text", ["text"] = Content });
            foreach (var url in ImageUrls)
            {
                var imageObj = new JObject
                {
                    ["type"] = "image_url",
                    ["image_url"] = new JObject { ["url"] = url }
                };
                if (detail != null)
                    ((JObject)imageObj["image_url"]!)["detail"] = detail;
                array.Add(imageObj);
            }
            data["content"] = array;
        }
        else
        {
            data["content"] = Content;
        }

        if (ToolCalls != null)
            data["tool_calls"] = new JArray(ToolCalls.Select(t => t.ToJson()));

        if (ToolCallId != null)
            data["tool_call_id"] = ToolCallId;

        if (Name != null)
            data["name"] = Name;

        return data;
    }
}