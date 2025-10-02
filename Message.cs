using Newtonsoft.Json.Linq;

namespace ChatCompletionsSharp;

/// <summary>
/// Represents a message within a chat transcript, including helper factories for each supported role.
/// </summary>
public class Message
{
    /// <summary>
    /// Role associated with the message (user, assistant, system, developer, or tool).
    /// </summary>
    public required string Role;

    /// <summary>
    /// Primary textual content of the message.
    /// </summary>
    public string? Content;

    /// <summary>
    /// Optional collection of image URLs attached to the message.
    /// </summary>
    public string[]? ImageUrls;

    /// <summary>
    /// Tool calls emitted by the assistant message, if any.
    /// </summary>
    public ToolCall[]? ToolCalls;

    /// <summary>
    /// Identifier linking a tool response message back to the originating tool call.
    /// </summary>
    public string? ToolCallId;

    /// <summary>
    /// Metadata slot for whatever you want.
    /// </summary>
    public string? Meta1;

    /// <summary>
    /// Metadata slot for whatever you want.
    /// </summary>
    public string? Meta2;

    /// <summary>
    /// Metadata slot for whatever you want.
    /// </summary>
    public string? Meta3;

    /// <summary>
    /// Optional name for the message to differentiate participants.
    /// </summary>
    public string? Name;

    /// <summary>
    /// Prevents direct construction; use factory methods or <see cref="FromJson"/> to ensure a valid role is set.
    /// </summary>
    internal Message() { }

    /// <summary>
    /// Creates a user message containing text and optional image URLs.
    /// </summary>
    /// <param name="content">Textual content of the user message.</param>
    /// <param name="imageUrls">Optional image URLs attached to the message.</param>
    /// <returns>A configured user message.</returns>
    public static Message User(string content, params string[] imageUrls)
    {
        return new Message
        {
            Role = "user",
            Content = content,
            ImageUrls = imageUrls.Length > 0 ? imageUrls : null
        };
    }

    /// <summary>
    /// Creates an assistant message with optional tool call descriptors.
    /// </summary>
    /// <param name="content">Assistant response body.</param>
    /// <param name="toolCalls">Optional tool calls emitted by the assistant.</param>
    /// <returns>A configured assistant message.</returns>
    public static Message Assistant(string content, ToolCall[]? toolCalls = null)
    {
        return new Message
        {
            Role = "assistant",
            Content = content,
            ToolCalls = toolCalls
        };
    }

    /// <summary>
    /// Creates a system message used to prime the assistant.
    /// </summary>
    /// <param name="content">System instruction content.</param>
    /// <returns>A configured system message.</returns>
    public static Message System(string content)
    {
        return new Message
        {
            Role = "system",
            Content = content
        };
    }

    /// <summary>
    /// Creates a developer message, typically used for instructions that differ from system messages.
    /// </summary>
    /// <param name="content">Developer instruction content.</param>
    /// <returns>A configured developer message.</returns>
    public static Message Developer(string content)
    {
        return new Message
        {
            Role = "developer",
            Content = content
        };
    }

    /// <summary>
    /// Creates a tool message that references the originating tool call.
    /// </summary>
    /// <param name="content">Tool response payload.</param>
    /// <param name="call">Tool call that produced the response.</param>
    /// <returns>A configured tool message.</returns>
    public static Message Tool(string content, ToolCall call)
    {
        return new Message
        {
            Role = "tool",
            Content = content,
            ToolCallId = call.Id
        };
    }

    /// <summary>
    /// Creates a tool message referencing a tool call by identifier.
    /// </summary>
    /// <param name="content">Tool response payload.</param>
    /// <param name="id">Identifier of the originating tool call.</param>
    /// <returns>A configured tool message.</returns>
    public static Message Tool(string content, string id)
    {
        return new Message
        {
            Role = "tool",
            Content = content,
            ToolCallId = id
        };
    }

    /// <summary>
    /// Parses a JSON payload returned by the OpenAI API into a message instance, resolving tool calls when present.
    /// </summary>
    /// <param name="toolTypes">Tool registry used to resolve tool call details.</param>
    /// <param name="json">JSON token representing the message.</param>
    /// <returns>A message object mirroring the OpenAI response.</returns>
    /// <exception cref="ArgumentNullException">Thrown when required fields are missing from the JSON payload.</exception>
    public static Message FromJson(Dictionary<string, Tool> toolTypes, JToken json)
    {
        Message message = new Message
        {
            Role = json["role"]?.ToString()
                ?? throw new ArgumentNullException("Message must have a role."),
        };
        JToken contentToken = json["content"] ?? throw new ArgumentNullException("Message must have a content.");
        if (contentToken is JArray contentArray)
        {
            message.Content = string.Join("", contentArray
                .Where(t => t["type"]?.ToString() == "text")
                .Select(t => t["text"]?.ToString()));

            if (message.Role == "user")
            {
                string[] urls = contentArray
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
            message.Content = contentToken.ToString();
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

    /// <summary>
    /// Returns a human-readable representation of the message for debugging.
    /// </summary>
    /// <returns>String representation of the message.</returns>
    public override string ToString()
    {
        string toolCallsStr = ToolCalls != null
            ? $"[{string.Join(", ", ToolCalls.Select(t => t.ToString()))}]"
            : "null";

        string contentStr = ImageUrls != null
            ? $"{Content} + [{string.Join(", ", ImageUrls)}]"
            : (Content ?? "null");

        return $"Role: {Role}, Name: {Name}, Content: {contentStr}, ToolCalls: {toolCallsStr}, ToolCallId: {ToolCallId}";
    }

    /// <summary>
    /// Serializes the message into the format expected by the OpenAI chat completions endpoint.
    /// </summary>
    /// <param name="detail">Optional detail level applied to user message images.</param>
    /// <returns>Serialized representation of the message.</returns>
    internal JObject ToJson(string? detail = null)
    {
        JObject data = new JObject
        {
            ["role"] = Role,
        };

        if (ImageUrls != null && Role == "user")
        {
            JArray array = new JArray();
            if (Content != null)
                array.Add(new JObject { ["type"] = "text", ["text"] = Content });
            foreach (string url in ImageUrls)
            {
                JObject imageObj = new JObject
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
