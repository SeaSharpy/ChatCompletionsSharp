using System.ComponentModel;
using System.Text;
using Newtonsoft.Json.Linq;

namespace ChatCompletionsSharp;

/// <summary>
/// Provides a thin wrapper around the OpenAI Chat Completions endpoint, including tool registration and request orchestration.
/// </summary>
public class OpenAI
{
    /// <summary>
    /// API key used to authenticate against the OpenAI service.
    /// </summary>
    private string ApiKey;

    /// <summary>
    /// Base URL of the chat completions endpoint. Can be overridden for Azure OpenAI or custom gateways.
    /// </summary>
    public string BaseUrl = "https://api.openai.com/v1/chat/completions";

    /// <summary>
    /// Registry of tool names to tool metadata for lookup during tool call execution.
    /// </summary>
    private Dictionary<string, Tool> ToolTypes = new();

    /// <summary>
    /// Creates a new OpenAI client, resolving the API key from the provided argument or the OPENAI_API_KEY environment variable.
    /// </summary>
    /// <param name="apiKey">API key value to use when authenticating; if null, the environment variable is used instead.</param>
    /// <exception cref="ArgumentNullException">Thrown when no API key is provided or discoverable via environment variable.</exception>
    public OpenAI(string? apiKey = null)
    {
        ApiKey = apiKey ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? throw new ArgumentNullException("OpenAI API key not found.");
        client.DefaultRequestHeaders.Add("Authorization", "Bearer " + ApiKey);
    }

    /// <summary>
    /// HTTP client instance responsible for issuing requests to the OpenAI service.
    /// </summary>
    private HttpClient client = new();

    /// <summary>
    /// Executes a chat completion request, invoking callbacks and handling tool calls until the model stops or callbacks signal termination.
    /// </summary>
    /// <param name="request">The request payload and callback bundle to process.</param>
    /// <param name="cancellationToken">Optional token used to cancel the operation between network calls and callback invocations.</param>
    /// <returns>A task that completes when the completion finishes or is cancelled.</returns>
    public async Task SendCompletion(CompletionRequest request, CancellationToken? cancellationToken = null)
    {
        void CancellationTokenCheck()
        {
            if (cancellationToken != null)
                cancellationToken.Value.ThrowIfCancellationRequested();
        }
        ICompletionEventCallbacks callbacks = request.Callbacks;
        Exception? error = null;
        List<Message> extraMessages = new();
        try
        {
            callbacks.OnCompletionStarted(request);
            CancellationTokenCheck();
            while (true)
            {
                string jsonRequest = request.ToJson(ToolTypes).ToString();
                StringContent content = new(jsonRequest, Encoding.UTF8, "application/json");

                HttpResponseMessage response = await (cancellationToken != null ? client.PostAsync(BaseUrl, content, cancellationToken.Value) : client.PostAsync(BaseUrl, content));
                string body = await (cancellationToken != null ? response.Content.ReadAsStringAsync(cancellationToken.Value) : response.Content.ReadAsStringAsync());
                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"HTTP error {(int)response.StatusCode} {response.ReasonPhrase}");
                    Console.WriteLine($"Error body: {body}");
                    Console.WriteLine($"Request body: {jsonRequest}");
                    callbacks.OnCompletionError(request, body);
                    goto EndTry;
                }

                JObject responseBody = JObject.Parse(body);
                JToken? choices = responseBody["choices"];
                choices = choices?[0];
                if (choices == null)
                {
                    Console.WriteLine("No choices found in response");
                    CancellationTokenCheck();
                    callbacks.OnCompletionError(request, "No choices found in response");
                    goto EndTry;
                }
                JToken? messageToken = choices["message"];
                if (messageToken == null)
                {
                    Console.WriteLine("No message found in response");
                    CancellationTokenCheck();
                    callbacks.OnCompletionError(request, "No message found in response");
                    goto EndTry;
                }
                Message message = Message.FromJson(ToolTypes, messageToken);
                CancellationTokenCheck();
                callbacks.OnCompletionDelta(request, message);
                CancellationTokenCheck();
                request.Messages.Add(message);
                CancellationTokenCheck();
                extraMessages.Add(message);
                CancellationTokenCheck();
                if (message.ToolCalls != null)
                {
                    foreach (ToolCall toolCall in message.ToolCalls)
                    {
                        CancellationTokenCheck();
                        CompletionToolResponse toolResponse = callbacks.OnTool(request, toolCall);
                        switch (toolResponse.Type)
                        {
                            case CompletionToolResponseType.AskTool:
                                if (toolCall.Tool.Callback != null)
                                {
                                    CancellationTokenCheck();
                                    CompletionToolCallbackResponse callbackResponse = toolCall.Tool.Callback(request, toolCall);
                                    switch (callbackResponse.Type)
                                    {
                                        case CompletionToolCallbackResponseType.Stop:
                                            goto EndTry;
                                        case CompletionToolCallbackResponseType.Message:
                                            CancellationTokenCheck();
                                            request.Messages.Add(callbackResponse.ToolResponse!);
                                            CancellationTokenCheck();
                                            extraMessages.Add(callbackResponse.ToolResponse!);
                                            CancellationTokenCheck();
                                            break;
                                    }
                                }
                                break;
                            case CompletionToolResponseType.Stop:
                                goto EndTry;
                            case CompletionToolResponseType.Message:
                                CancellationTokenCheck();
                                request.Messages.Add(toolResponse.ToolResponse!);
                                CancellationTokenCheck();
                                extraMessages.Add(toolResponse.ToolResponse!);
                                CancellationTokenCheck();
                                break;
                        }
                    }
                }
                else break;
            }
        EndTry:;
            CancellationTokenCheck();
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"HTTP request exception: {ex.Message}");
            error = ex;
        }
        catch (UriFormatException ex)
        {
            Console.WriteLine($"URL formatting error: {ex.Message}");
            error = ex;
        }
        catch (InvalidOperationException ex)
        {
            Console.WriteLine($"Invalid operation: {ex.Message}");
            error = ex;
        }
        catch (TaskCanceledException ex)
        {
            Console.WriteLine($"Task cancelled: {ex.Message}");
            error = ex;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"{ex.GetType().Name} error: {ex.Message}");
            error = ex;
        }
        finally
        {
            CancellationTokenCheck();
            if (error != null)
                callbacks.OnCompletionError(request, error);
            else
                callbacks.OnCompletionSuccess(request);
            CancellationTokenCheck();
            callbacks.OnCompletionEnded(request, extraMessages);
        }
    }

    /// <summary>
    /// Registers a tool definition by name based on the provided CLR type. Throws if the tool name already exists.
    /// </summary>
    /// <param name="name">Unique identifier for the tool that the model will reference.</param>
    /// <param name="description">Description shown to the model when choosing tools.</param>
    /// <param name="type">CLR type used to generate a JSON schema for the tool arguments.</param>
    /// <exception cref="ArgumentException">Thrown when a tool with the specified name already exists.</exception>
    public void AddTool(string name, string description, Type type)
    {
        if (ToolTypes.ContainsKey(name))
            throw new ArgumentException($"Tool with name '{name}' already exists.");
        ToolTypes.Add(name, new Tool(name, description, type));
    }

    /// <summary>
    /// Registers a fully constructed tool instance. Throws if a tool with the same name already exists.
    /// </summary>
    /// <param name="tool">Tool instance containing metadata, schema, and optional callback.</param>
    /// <exception cref="ArgumentException">Thrown when a tool with the specified name already exists.</exception>
    public void AddTool(Tool tool)
    {
        if (ToolTypes.ContainsKey(tool.Name))
            throw new ArgumentException($"Tool with name '{tool.Name}' already exists.");
        ToolTypes.Add(tool.Name, tool);
    }
}
