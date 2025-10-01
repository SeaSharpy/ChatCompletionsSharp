using System.Diagnostics;
using Newtonsoft.Json.Linq;

namespace ChatCompletionsSharp;

public class CompletionRequest
{
    public List<Message> Messages = new();
    public string Model;
    public float Temperature = 1.0f;
    public int MaxTokens = -1;
    public string? Prediction;
    public string? ReasoningEffort;
    public string? Verbosity;
    public string? Detail;
    public ICompletionEventCallbacks Callbacks;

    public CompletionRequest(List<Message> messages, string model, ICompletionEventCallbacks callbacks)
    {
        Messages = messages;
        Model = model;
        Callbacks = callbacks;
    }

    internal JObject ToJson(Dictionary<string, Tool> toolTypes)
    {
        var data = new JObject
        {
            ["model"] = Model,
            ["messages"] = new JArray(Messages.Select(m => m.ToJson(Detail))),
            ["temperature"] = Temperature,
            ["n"] = 1,
            ["stream"] = false,
            ["tools"] = Tool.AllAsJson(toolTypes)
        };

        if (Prediction != null)
            data["prediction"] = Prediction;
        if (ReasoningEffort != null)
            data["reasoning_effort"] = ReasoningEffort;
        if (Verbosity != null)
            data["verbosity"] = Verbosity;
        if (MaxTokens > 0)
            data["max_tokens"] = MaxTokens;

        return data;
    }
}

internal enum CompletionToolResponseType
{
    AskTool,
    Stop,
    Message
}

public class CompletionToolResponse
{
    internal CompletionToolResponseType Type;
    internal Message? ToolResponse;
    private CompletionToolResponse() { }
    public CompletionToolResponse(Message message)
    {
        Type = CompletionToolResponseType.Message;
        ToolResponse = message;
    }
    public static CompletionToolResponse Stop()
    {
        return new CompletionToolResponse { Type = CompletionToolResponseType.Stop };
    }
    public static CompletionToolResponse AskTool()
    {
        return new CompletionToolResponse { Type = CompletionToolResponseType.AskTool };
    }
    public static CompletionToolResponse Message(Message message)
    {
        return new CompletionToolResponse { Type = CompletionToolResponseType.Message, ToolResponse = message };
    }
}

internal enum CompletionToolCallbackResponseType
{
    Stop,
    Message
}

public class CompletionToolCallbackResponse
{
    internal CompletionToolCallbackResponseType Type;
    internal Message? ToolResponse;
    private CompletionToolCallbackResponse() { }
    public CompletionToolCallbackResponse(Message message)
    {
        Type = CompletionToolCallbackResponseType.Message;
        ToolResponse = message;
    }
    public static CompletionToolCallbackResponse Stop()
    {
        return new CompletionToolCallbackResponse { Type = CompletionToolCallbackResponseType.Stop };
    }
    public static CompletionToolCallbackResponse Message(Message message)
    {
        return new CompletionToolCallbackResponse { Type = CompletionToolCallbackResponseType.Message, ToolResponse = message };
    }
}

public interface ICompletionEventCallbacks
{
    void OnCompletionStarted(CompletionRequest r);
    CompletionToolResponse OnTool(CompletionRequest r, ToolCall toolCall); // bool to say whether to use the default
    void OnCompletionDelta(CompletionRequest r, Message delta);
    void OnCompletionEnded(CompletionRequest r, List<Message> newMessages);
    void OnCompletionError(CompletionRequest r, Exception e);
    void OnCompletionError(CompletionRequest r, string e);
}