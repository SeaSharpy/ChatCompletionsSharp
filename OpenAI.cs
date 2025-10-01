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
    public async Task SendCompletion(CompletionRequest request)
    {
        ICompletionEventCallbacks callbacks = request.Callbacks;
        Exception? error = null;
        List<Message> extraMessages = new();
        try
        {

            callbacks.OnCompletionStarted(request);

            while (true)
            {
                var content = new StringContent(request.ToJson(ToolTypes).ToString(), Encoding.UTF8, "application/json");

                var response = await client.PostAsync(BaseUrl, content);

                if (!response.IsSuccessStatusCode)
                {
                    string errorBody = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"HTTP error {(int)response.StatusCode} {response.ReasonPhrase}");
                    Console.WriteLine($"Error body: {errorBody}");
                    Console.WriteLine($"Request body: {request.ToJson(ToolTypes).ToString()}");
                    callbacks.OnCompletionError(request, errorBody);
                    goto EndTry;
                }

                JObject responseBody = JObject.Parse(await response.Content.ReadAsStringAsync());
                JToken? choices = responseBody["choices"];
                choices = choices?[0];
                if (choices == null)
                {
                    Console.WriteLine("No choices found in response");
                    callbacks.OnCompletionError(request, "No choices found in response");
                    goto EndTry;
                }
                JToken? messageToken = choices["message"];
                if (messageToken == null)
                {
                    Console.WriteLine("No message found in response");
                    callbacks.OnCompletionError(request, "No message found in response");
                    goto EndTry;
                }
                Message message = Message.FromJson(ToolTypes, messageToken);
                callbacks.OnCompletionDelta(request, message);
                request.Messages.Add(message);
                extraMessages.Add(message);
                if (message.ToolCalls != null)
                {
                    foreach (ToolCall toolCall in message.ToolCalls)
                    {
                        CompletionToolResponse toolResponse = callbacks.OnTool(request, toolCall);
                        switch (toolResponse.Type)
                        {
                            case CompletionToolResponseType.AskTool:
                                if (toolCall.Tool.Callback != null)
                                {
                                    CompletionToolCallbackResponse callbackResponse = toolCall.Tool.Callback(request, toolCall);
                                    switch (callbackResponse.Type)
                                    {
                                        case CompletionToolCallbackResponseType.Stop:
                                            goto EndTry;
                                        case CompletionToolCallbackResponseType.Message:
                                            request.Messages.Add(callbackResponse.ToolResponse!);
                                            extraMessages.Add(callbackResponse.ToolResponse!);
                                            break;
                                    }
                                }
                                break;
                            case CompletionToolResponseType.Stop:
                                goto EndTry;
                            case CompletionToolResponseType.Message:
                                request.Messages.Add(toolResponse.ToolResponse!);
                                extraMessages.Add(toolResponse.ToolResponse!);
                                break;
                        }
                    }
                }
                else break;
            }

        EndTry:;
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
            if (error != null)
            {
                callbacks.OnCompletionError(request, error);
            }
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
