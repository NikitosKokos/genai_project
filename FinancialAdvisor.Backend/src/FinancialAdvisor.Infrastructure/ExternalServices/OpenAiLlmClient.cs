using System.Text;
using System.Text.Json;
using System.Net.Http.Headers;
using Microsoft.Extensions.Configuration;

namespace FinancialAdvisor.Infrastructure.ExternalServices;

public class OpenAiLlmClient
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _model;

    public OpenAiLlmClient(IConfiguration configuration)
    {
        _httpClient = new HttpClient();
        _apiKey = configuration["OpenAi:ApiKey"] ?? "";
        _model = configuration["OpenAi:ChatModel"] ?? "gpt-3.5-turbo";
        
        if (!string.IsNullOrEmpty(_apiKey))
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        }
    }

    public async Task<string> GenerateResponseAsync(string prompt, string context)
    {
        if (string.IsNullOrEmpty(_apiKey))
        {
            return "Error: OpenAI API Key is missing in appsettings.json. Please configure 'OpenAi:ApiKey'.";
        }

        var systemMessage = @"You are a helpful financial advisor AI. 
Use the provided context to answer the user's question. 
If the answer is not in the context, use your general knowledge but mention that it's general advice.
Keep answers concise and professional.";

        var requestBody = new
        {
            model = _model,
            messages = new[]
            {
                new { role = "system", content = systemMessage },
                new { role = "user", content = $"Context:\n{context}\n\nQuestion: {prompt}" }
            },
            temperature = 0.7
        };

        var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync("https://api.openai.com/v1/chat/completions", content);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            return $"Error calling OpenAI: {response.StatusCode} - {error}";
        }

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var answer = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        return answer ?? "No response generated.";
    }
}

