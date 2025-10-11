# ChatCompletionsSharp

https://github.com/SeaSharpy/ChatCompletionsSharp

A lightweight .NET 8 client for OpenAI chat completions with a focus on typed tool calls and event-driven workflows.

## Features

- Event callbacks for every stage of the chat completion lifecycle
- Strongly-typed tool definitions with JSON Schema generated from your structs
- Helper factories for user, assistant, developer, and tool messages (including images)
- Simple cancellation support and rich error reporting hooks
- Minimal dependencies: Newtonsoft.Json and NJsonSchema

## Installation

```bash
dotnet add package ChatCompletionsSharp
```

Set your OpenAI key via the constructor or by exporting `OPENAI_API_KEY`:

```powershell
$env:OPENAI_API_KEY = "sk-your-key"
```

## Quick start

```csharp
using ChatCompletionsSharp;

var client = new OpenAI(); // reads OPENAI_API_KEY by default

var callbacks = new ConsoleCallbacks();

var request = new CompletionRequest(
    new List<Message>
    {
        Message.Developer("You are an upbeat travel planner."),
        Message.User("Plan a weekend walking tour in Porto."),
    },
    model: "gpt-5-mini",
    callbacks: callbacks)
{
    Temperature = 0.7f,
    MaxTokens = 500
};

await client.SendCompletion(request);
```

A minimal callback implementation might look like this:

```csharp
class ConsoleCallbacks : ICompletionEventCallbacks
{
    public void OnCompletionStarted(CompletionRequest r) =>
        Console.WriteLine("\n--- chat started ---");

    public CompletionToolResponse OnTool(CompletionRequest r, ToolCall toolCall)
    {
        // Delegate to the registered tool callback, if there is one.
        return CompletionToolResponse.AskTool();
    }

    public void OnCompletionDelta(CompletionRequest r, Message delta)
    {
        if (!string.IsNullOrWhiteSpace(delta.Content))
            Console.Write(delta.Content);
    }

    public void OnCompletionEnded(CompletionRequest r, List<Message> newMessages) =>
        Console.WriteLine("--- chat ended ---\n");

    public void OnCompletionSuccess(CompletionRequest r) {}
    public void OnCompletionError(CompletionRequest r, Exception e) => Console.Error.WriteLine(e);
    public void OnCompletionError(CompletionRequest r, string e) => Console.Error.WriteLine(e);
}
```

## Defining and handling tools

Tools are registered on the `OpenAI` client. The library builds the JSON schema from your struct and deserializes arguments before invoking your callback.

```csharp
struct WeatherArgs {
    string City; 
    string Units;
}

var weatherTool = new Tool(
    name: "get_weather",
    description: "Look up the current weather by city name.",
    type: typeof(WeatherArgs),
    callback: (request, call) =>
    {
        var args = (WeatherArgs)call.Arguments!;
        var data = WeatherService.Lookup(args.City, args.Units);

        return CompletionToolCallbackResponse.Message(
            Message.Tool(JsonConvert.SerializeObject(data), call));
    });

client.AddTool(weatherTool);
```

During a run, `OpenAI.SendCompletion` will:

- Invoke `OnTool` for each tool call the assistant emits.
- Run the tool's `Callback` when `OnTool` returns `CompletionToolResponse.AskTool()`.
- Append tool responses as additional `Message` objects so the assistant can continue.

Return `CompletionToolResponse.Stop()` (or `CompletionToolCallbackResponse.Stop()`) to terminate the turn early.

## Message helpers

- `Message.User(string content, params string[] imageUrls)` for text and images in user messages.
- `Message.Assistant(string content, ToolCall[]? toolCalls = null)` to inject tool call stubs in tests.
- `Message.Developer(string content)` to supply system instructions (you can also do Message.System if you're using the older models).
- `Message.Tool(string content, ToolCall call)` or `Message.Tool(string content, string id)` to send tool outputs back to the assistant.

You cannot instantiate like `new Message { Role = "...", Content = "..." }`.

## Request options

`CompletionRequest` exposes several OpenAI parameters:

- `Temperature` and `MaxTokens` for sampling control.
- `Prediction`, `ReasoningEffort`, and `Verbosity` for reasoning features.
- `Detail` to toggle image detail levels (`"auto"`, `"low"`, `"high"`).

## Cancellation and error handling

Pass a `CancellationToken` into `SendCompletion` to abort the run. The library checks the token between network calls, callback invocations, and tool handling. Any exception surfaced from the HTTP layer, JSON parsing, or callbacks is reported through `OnCompletionError` and re-signaled at the end of the run via `OnCompletionEnded`.

For non-success HTTP responses the request and response bodies are logged to the console to simplify troubleshooting.

## Custom endpoints and retry logic

Change `OpenAI.BaseUrl` before calling `SendCompletion` (e.g., to point at Azure OpenAI or a gateway).

```csharp
var client = new OpenAI(apiKey: "azure-key")
{
    BaseUrl = "https://my-azure-resource.openai.azure.com/openai/deployments/gpt-4/chat/completions?api-version=2024-02-15-preview"
};
```

The current client does not include automatic retries; wrap `SendCompletion` in your own policy (Polly, etc.) if you need them.

## API Reference

This section documents all publicly accessible types, fields, properties, and methods exposed by the library.

### OpenAI

- **Purpose**: Entry point for sending chat completion requests and managing tool registrations.
- **Constructor**: `OpenAI(string? apiKey = null)` loads the API key either from the argument or from the `OPENAI_API_KEY` environment variable.
- **Fields**:
  - `string BaseUrl` — Endpoint used for `POST` requests (default `https://api.openai.com/v1/chat/completions`). Update this to target Azure OpenAI or custom gateways.
- **Methods**:
  - `async Task SendCompletion(CompletionRequest request, CancellationToken? cancellationToken = null)` — Executes the request, drives callbacks, deserializes tool calls, and appends all new messages to the request.
  - `void AddTool(string name, string description, Type type)` — Registers a tool by CLR type with optional callback to be attached later.
  - `void AddTool(Tool tool)` — Registers a pre-constructed `Tool` instance.

### CompletionRequest

- **Purpose**: Represents an outbound completion request along with configuration flags and callbacks.
- **Constructor**: `CompletionRequest(List<Message> messages, string model, ICompletionEventCallbacks callbacks)` — Requires the initial message list, OpenAI model name, and callbacks implementation.
- **Members**:

  - `List<Message> Messages` (get) — Mutable transcript sent with the request; responses are appended here.
  - `string Model` — Target model deployment name (e.g., `gpt-4.1-mini`).
  - `float Temperature` — Sampling temperature (`1.0f` default). Set to `0` for deterministic runs.
  - `int MaxTokens` — Upper bound on response tokens; `-1` uses the service default.
  - `string? Prediction` — Experimental prediction hint forwarded to the API.
  - `string? ReasoningEffort` — Controls reasoning budget (`"medium"`, etc.).
  - `string? Verbosity` — Request verbosity level from the OpenAI API.
  - `string? Detail` — Optional image detail preference propagated to user message image URLs.
  - `ICompletionEventCallbacks Callbacks` (get) — Callback implementation invoked throughout the lifecycle.

### ICompletionEventCallbacks

- **Purpose**: Receives lifecycle notifications from `SendCompletion`.
- **Methods**:
  - `void OnCompletionStarted(CompletionRequest r)` — Raised before the first HTTP request is sent.
  - `CompletionToolResponse OnTool(CompletionRequest r, ToolCall toolCall)` — Invoked for each tool call emitted by the assistant.
  - `void OnCompletionDelta(CompletionRequest r, Message delta)` — Streams each assistant message or tool call response.
  - `void OnCompletionEnded(CompletionRequest r, List<Message> newMessages)` — Provides the list of messages added during the run.
  - `void OnCompletionSuccess(CompletionRequest r)` — Signals successful completion without errors.
  - `void OnCompletionError(CompletionRequest r, Exception e)` — Reports exceptions caught while processing the completion.
  - `void OnCompletionError(CompletionRequest r, string e)` — Reports error payloads returned by the API.

### CompletionToolResponse

- **Purpose**: Return value for `OnTool`, describing how the runtime should proceed.
- **Members**:
  - `CompletionToolResponse(Message message)` — Constructs a response that immediately enqueues a tool response message.
  - `static CompletionToolResponse Stop()` — Ends the completion loop.
  - `static CompletionToolResponse AskTool()` — Requests execution of the registered tool callback.
  - `static CompletionToolResponse Message(Message message)` — Shorthand for returning a message response.

### CompletionToolCallbackResponse

- **Purpose**: Return value for `Tool.Callback` implementations.
- **Members**:
  - `CompletionToolCallbackResponse(Message message)` — Creates a callback response that returns a message to the assistant.
  - `static CompletionToolCallbackResponse Stop()` — Signals that the tool should halt further processing.
  - `static CompletionToolCallbackResponse Message(Message message)` — Convenience factory mirroring the constructor.

### Tool

- **Purpose**: Encapsulates tool metadata, JSON schema, and optional callback for tool execution.
- **Constructor**: `Tool(string name, string description, Type type, bool strict = false, CompletionToolCallback? callback = null)` — Generates the JSON schema from the supplied CLR type and stores the optional callback.
- **Delegates**:
  - `Tool.CompletionToolCallback` — Signature `CompletionToolCallbackResponse CompletionToolCallback(CompletionRequest r, ToolCall toolCall)` used for handling tool requests.
- **Fields and properties**:
  - `string Name` (get) — Tool name used in the schema and tool calls.
  - `string Description` (get) — Text shown to the model when choosing tools.
  - `bool Strict` (get) — When true, the request enforces strict argument adherence (requires compatible API support).
  - `Type Type` (get) — CLR type used to generate the JSON schema and deserialize arguments.
  - `CompletionToolCallback? Callback` (get) — Optional delegate executed when requested.

### ToolCall

- **Purpose**: Represents a single tool call emitted by the assistant.
- **Constructor**: `ToolCall(object? arguments, string name, string id, Tool tool)` — Stores the raw arguments object, tool metadata, and call identifier.
- **Fields**:

  - `object? Arguments` — Deserialized arguments that match the tool schema (or null if validation failed).
  - `string Name` — Function name requested by the model.
  - `string Id` — Unique identifier for the tool call.
  - `Tool Tool` — Registered tool definition that supplied the schema and callback.

### Message

- **Purpose**: Models chat transcript entries, including system/developer prompts, user messages with images, tool responses, and assistant output.
- **Factories**:

  - `static Message User(string content, params string[] imageUrls)` — Creates a user message with optional images captured as URLs.
  - `static Message Assistant(string content, ToolCall[]? toolCalls = null)` — Creates an assistant message with optional tool call stubs.
  - `static Message System(string content)` — Helper for system prompts.
  - `static Message Developer(string content)` — Helper for developer role prompts.
  - `static Message Tool(string content, ToolCall call)` — Builds a tool response referencing the originating call.
  - `static Message Tool(string content, string id)` — Tool response factory when only the call ID is available.
- **Fields**:

  - `required string Role` — Role of the message (`user`, `assistant`, `system`, `developer`, `tool`).
  - `string? Content` — Textual content of the message.
  - `string[]? ImageUrls` — Optional image URLs attached to user messages.
  - `ToolCall[]? ToolCalls` — Tool call descriptors emitted by the assistant.
  - `string? ToolCallId` — Identifier linking tool responses to the originating call.
  - `string? Meta1`, `string? Meta2`, `string? Meta3` — Metadata slots for whatever you want.
  - `string? Name` — Optional name for the message to differentiate participants.

## Contributing

Issues and pull requests are welcome.
