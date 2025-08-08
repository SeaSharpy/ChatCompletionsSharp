using System.Text;
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
        try
        {
            using var client = new HttpClient();

            // Add headers
            client.DefaultRequestHeaders.Add("Authorization", "Bearer " + ApiKey);

            // Convert JObject to StringContent
            var content = new StringContent(request.ToJson().ToString(), Encoding.UTF8, "application/json");

            // Send POST request
            var response = await client.PostAsync(BaseUrl, content);
            response.EnsureSuccessStatusCode();

            // Optionally read response
            string responseBody = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"Response: {responseBody}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"HTTP error: {ex.Message}");
        }
    }
}
