using System.ComponentModel;
using System.Text;
using Newtonsoft.Json.Linq;

namespace ChatCompletionsSharp;

public class OpenAI
{
    private string ApiKey;
    public string BaseUrl = "https://api.openai.com/v1/chat/completions";
    private Dictionary<string, Tool> ToolTypes = new();

    public OpenAI(string? apiKey = null)
    {
        ApiKey = apiKey ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? throw new ArgumentNullException("OpenAI API key not found.");
        client.DefaultRequestHeaders.Add("Authorization", "Bearer " + ApiKey);
    }

    private HttpClient client = new();
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

    public void AddTool(string name, string description, Type type)
    {
        if (ToolTypes.ContainsKey(name))
            throw new ArgumentException($"Tool with name '{name}' already exists.");
        ToolTypes.Add(name, new Tool(name, description, type));
    }

    public void AddTool(Tool tool)
    {
        if (ToolTypes.ContainsKey(tool.Name))
            throw new ArgumentException($"Tool with name '{tool.Name}' already exists.");
        ToolTypes.Add(tool.Name, tool);
    }
}
