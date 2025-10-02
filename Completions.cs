using Newtonsoft.Json.Linq;

namespace ChatCompletionsSharp;

/// <summary>
/// Represents a chat completion request, including the transcript, target model, and configuration parameters.
/// </summary>
public class CompletionRequest
{
    /// <summary>
    /// Collection of messages that constitute the conversation context sent to the model.
    /// </summary>
    public List<Message> Messages { get; private set; } = new();

    /// <summary>
    /// Identifier for the OpenAI model or deployment to target.
    /// </summary>
    public string Model;

    /// <summary>
    /// Sampling temperature forwarded to the chat completion endpoint.
    /// </summary>
    public float Temperature = 1.0f;

    /// <summary>
    /// Optional upper bound on the number of tokens generated for the response (negative value uses API default).
    /// </summary>
    public int MaxTokens = -1;

    /// <summary>
    /// Optional prediction hint leveraged by the OpenAI reasoning APIs.
    /// </summary>
    public string? Prediction;

    /// <summary>
    /// Optional reasoning effort configuration value.
    /// </summary>
    public string? ReasoningEffort;

    /// <summary>
    /// Optional verbosity override forwarded to the API.
    /// </summary>
    public string? Verbosity;

    /// <summary>
    /// Optional image detail setting applied to every user-supplied image URL.
    /// </summary>
    public string? Detail;

    /// <summary>
    /// Completion lifecycle callbacks that are invoked during request processing.
    /// </summary>
    public ICompletionEventCallbacks Callbacks;

    /// <summary>
    /// Initializes a new request with the provided transcript, model name, and callback implementation.
    /// </summary>
    /// <param name="messages">Existing conversation history; any responses will be appended here.</param>
    /// <param name="model">OpenAI model identifier to use for the completion.</param>
    /// <param name="callbacks">Callback implementation that reacts to lifecycle events.</param>
    public CompletionRequest(List<Message> messages, string model, ICompletionEventCallbacks callbacks)
    {
        Messages = messages;
        Model = model;
        Callbacks = callbacks;
    }

    /// <summary>
    /// Converts the request into a JSON payload that can be submitted to the OpenAI chat completion endpoint.
    /// </summary>
    /// <param name="toolTypes">Tool registry used to attach tool definitions to the payload.</param>
    /// <returns>A JObject representing the serialized request body.</returns>
    internal JObject ToJson(Dictionary<string, Tool> toolTypes)
    {
        JObject data = new JObject
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

/// <summary>
/// Enumerates the possible actions that can be taken after <see cref="ICompletionEventCallbacks.OnTool"/> executes.
/// </summary>
internal enum CompletionToolResponseType
{
    /// <summary>
    /// Indicates that the runtime should invoke the registered tool callback.
    /// </summary>
    AskTool,

    /// <summary>
    /// Instructs the runtime to halt the completion loop immediately.
    /// </summary>
    Stop,

    /// <summary>
    /// Supplies a message that should be appended without running a tool callback.
    /// </summary>
    Message
}

/// <summary>
/// Represents the control flow decision returned from <see cref="ICompletionEventCallbacks.OnTool"/>.
/// </summary>
public class CompletionToolResponse
{
    /// <summary>
    /// Internal discriminator capturing the requested control flow action.
    /// </summary>
    internal CompletionToolResponseType Type;

    /// <summary>
    /// Optional message to be appended to the transcript.
    /// </summary>
    internal Message? ToolResponse;

    /// <summary>
    /// Prevents external callers from instantiating without setting the <see cref="Type"/> value.
    /// </summary>
    private CompletionToolResponse() { }

    /// <summary>
    /// Initializes a tool response that appends the supplied message to the transcript.
    /// </summary>
    /// <param name="message">Message to append to the conversation.</param>
    public CompletionToolResponse(Message message)
    {
        Type = CompletionToolResponseType.Message;
        ToolResponse = message;
    }

    /// <summary>
    /// Creates a response instructing the completion loop to stop immediately.
    /// </summary>
    /// <returns>A response signaling termination.</returns>
    public static CompletionToolResponse Stop()
    {
        return new CompletionToolResponse { Type = CompletionToolResponseType.Stop };
    }

    /// <summary>
    /// Creates a response requesting that the registered tool callback be executed.
    /// </summary>
    /// <returns>A response that defers execution to the tool callback.</returns>
    public static CompletionToolResponse AskTool()
    {
        return new CompletionToolResponse { Type = CompletionToolResponseType.AskTool };
    }

    /// <summary>
    /// Creates a response that appends the provided message to the transcript.
    /// </summary>
    /// <param name="message">Message to add to the request/response flow.</param>
    /// <returns>A response wrapping the message.</returns>
    public static CompletionToolResponse Message(Message message)
    {
        return new CompletionToolResponse { Type = CompletionToolResponseType.Message, ToolResponse = message };
    }
}

/// <summary>
/// Enumerates the possible actions a tool callback can request after it runs.
/// </summary>
internal enum CompletionToolCallbackResponseType
{
    /// <summary>
    /// Stops the completion loop immediately.
    /// </summary>
    Stop,

    /// <summary>
    /// Appends a message as part of the tool response.
    /// </summary>
    Message
}

/// <summary>
/// Represents the outcome of executing a tool callback.
/// </summary>
public class CompletionToolCallbackResponse
{
    /// <summary>
    /// Internal discriminator capturing whether execution should stop or append a message.
    /// </summary>
    internal CompletionToolCallbackResponseType Type;

    /// <summary>
    /// Optional message to append when <see cref="Type"/> equals <see cref="CompletionToolCallbackResponseType.Message"/>.
    /// </summary>
    internal Message? ToolResponse;

    /// <summary>
    /// Prevents direct instantiation without specifying the desired <see cref="Type"/>.
    /// </summary>
    private CompletionToolCallbackResponse() { }

    /// <summary>
    /// Initializes a callback response that appends the supplied message to the transcript.
    /// </summary>
    /// <param name="message">Message returned by the tool implementation.</param>
    public CompletionToolCallbackResponse(Message message)
    {
        Type = CompletionToolCallbackResponseType.Message;
        ToolResponse = message;
    }

    /// <summary>
    /// Creates a response directing the runtime to halt the completion loop.
    /// </summary>
    /// <returns>A response indicating that execution should stop.</returns>
    public static CompletionToolCallbackResponse Stop()
    {
        return new CompletionToolCallbackResponse { Type = CompletionToolCallbackResponseType.Stop };
    }

    /// <summary>
    /// Creates a response that appends the supplied message to the transcript.
    /// </summary>
    /// <param name="message">Message returned by the tool implementation.</param>
    /// <returns>A response wrapping the message for inclusion in the transcript.</returns>
    public static CompletionToolCallbackResponse Message(Message message)
    {
        return new CompletionToolCallbackResponse { Type = CompletionToolCallbackResponseType.Message, ToolResponse = message };
    }
}

/// <summary>
/// Defines the callback contract for receiving completion lifecycle events.
/// </summary>
public interface ICompletionEventCallbacks
{
    /// <summary>
    /// Invoked when a completion begins processing.
    /// </summary>
    /// <param name="r">Active completion request.</param>
    void OnCompletionStarted(CompletionRequest r);

    /// <summary>
    /// Invoked when the assistant emits a tool call.
    /// </summary>
    /// <param name="r">Active completion request.</param>
    /// <param name="toolCall">Tool invocation details emitted by the assistant.</param>
    /// <returns>A response instructing how the runtime should proceed.</returns>
    CompletionToolResponse OnTool(CompletionRequest r, ToolCall toolCall);

    /// <summary>
    /// Invoked whenever the assistant produces a message delta.
    /// </summary>
    /// <param name="r">Active completion request.</param>
    /// <param name="delta">Message snippet emitted by the assistant.</param>
    void OnCompletionDelta(CompletionRequest r, Message delta);

    /// <summary>
    /// Invoked at the end of the completion run with any new messages that were appended.
    /// </summary>
    /// <param name="r">Active completion request.</param>
    /// <param name="newMessages">Messages produced during processing.</param>
    void OnCompletionEnded(CompletionRequest r, List<Message> newMessages);

    /// <summary>
    /// Invoked when the completion finishes successfully without errors.
    /// </summary>
    /// <param name="r">Active completion request.</param>
    void OnCompletionSuccess(CompletionRequest r);

    /// <summary>
    /// Invoked when an exception is thrown while processing the completion.
    /// </summary>
    /// <param name="r">Active completion request.</param>
    /// <param name="e">Raised exception.</param>
    void OnCompletionError(CompletionRequest r, Exception e);

    /// <summary>
    /// Invoked when the completion API returns an error payload.
    /// </summary>
    /// <param name="r">Active completion request.</param>
    /// <param name="e">Error content returned by the API.</param>
    void OnCompletionError(CompletionRequest r, string e);
}
