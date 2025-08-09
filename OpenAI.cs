using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ChatCompletionsSharp;

public class OpenAI
{
    public string ApiKey { get; set; }
    public string BaseUrl { get; set; } = "https://api.openai.com/v1/chat/completions";

    public OpenAI(string? apiKey = null)
    {
        ApiKey = apiKey ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? throw new ArgumentNullException("OpenAI API key not found.");
    }

    public async Task SendCompletion(CompletionRequest request)
    {
        ICompletionEventCallbacks callbacks = request.Callbacks;
        Exception? error = null;
        List<Message> extraMessages = new();
        try
        {
            using var client = new HttpClient();

            client.DefaultRequestHeaders.Add("Authorization", "Bearer " + ApiKey);

            callbacks.OnCompletionStarted(request);

            while (true)
            {

                var content = new StringContent(request.ToJson().ToString(), Encoding.UTF8, "application/json");

                var response = await client.PostAsync(BaseUrl, content);

                if (!response.IsSuccessStatusCode)
                {
                    string errorBody = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"HTTP error {(int)response.StatusCode} {response.ReasonPhrase}");
                    Console.WriteLine($"Error body: {errorBody}");
                    Console.WriteLine($"Request body: {request.ToJson().ToString()}");
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
                Message message = Message.FromJson(messageToken);
                callbacks.OnCompletionDelta(request, message);
                request.Messages.Add(message);
                extraMessages.Add(message);
                if (message.ToolCalls != null)
                {
                    foreach (ToolCall toolCall in message.ToolCalls)
                    {
                        Message toolResponse = callbacks.OnTool(request, toolCall);
                        request.Messages.Add(toolResponse);
                        extraMessages.Add(toolResponse);
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
}
