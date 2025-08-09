using Newtonsoft.Json.Linq;

namespace ChatCompletionsSharp;

public class CompletionRequest
{
    public List<Message> Messages { get; set; } = new();
    public string Model { get; set; }
    public float Temperature { get; set; } = 1.0f;
    public int MaxTokens { get; set; } = -1;
    public string? Prediction { get; set; }
    public string? ReasoningEffort { get; set; }
    public string? Verbosity { get; set; }
    public string? Detail { get; set; }
    public ICompletionEventCallbacks Callbacks { get; set; }

    public CompletionRequest(List<Message> messages, string model, ICompletionEventCallbacks callbacks)
    {
        Messages = messages;
        Model = model;
        Callbacks = callbacks;
    }

    internal JObject ToJson()
    {
        var data = new JObject
        {
            ["model"] = Model,
            ["messages"] = new JArray(Messages.Select(m => m.ToJson(Detail))),
            ["temperature"] = Temperature,
            ["max_completion_tokens"] = MaxTokens < 0 ? null : MaxTokens,
            ["n"] = 1,
            ["stream"] = false,
            ["tools"] = Tool.AllAsJson()
        };

        if (Prediction != null)
            data["prediction"] = Prediction;
        if (ReasoningEffort != null)
            data["reasoning_effort"] = ReasoningEffort;
        if (Verbosity != null)
            data["verbosity"] = Verbosity;

        return data;
    }
}

public interface ICompletionEventCallbacks
{
    void OnCompletionStarted(CompletionRequest r);
    Message OnTool(CompletionRequest r, ToolCall toolCall);
    void OnCompletionDelta(CompletionRequest r, Message delta);
    void OnCompletionEnded(CompletionRequest r, List<Message> newMessages);
    void OnCompletionError(CompletionRequest r, Exception e);
    void OnCompletionError(CompletionRequest r, string e);
}