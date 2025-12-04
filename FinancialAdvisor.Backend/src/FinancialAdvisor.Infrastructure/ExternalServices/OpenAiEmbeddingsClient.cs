using System.Text;
using System.Text.Json;
using System.Net.Http.Headers;
using Microsoft.Extensions.Configuration;

namespace FinancialAdvisor.Infrastructure.ExternalServices;

public class OpenAiEmbeddingsClient
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _model;

    public OpenAiEmbeddingsClient(IConfiguration configuration)
    {
        _httpClient = new HttpClient();
        _apiKey = configuration["OpenAi:ApiKey"] ?? "";
        _model = configuration["OpenAi:EmbeddingModel"] ?? "text-embedding-3-small";

        if (!string.IsNullOrEmpty(_apiKey))
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        }
    }

    public async Task<float[]> GenerateEmbeddingAsync(string text)
    {
        if (string.IsNullOrEmpty(_apiKey))
        {
            // Return a dummy zero vector if no key, to prevent crashes (mock mode fallback)
            return new float[1536]; 
        }

        var requestBody = new
        {
            model = _model,
            input = text
        };

        var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync("https://api.openai.com/v1/embeddings", content);

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"Error generating embeddings: {response.StatusCode}");
        }

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var embeddingElement = doc.RootElement
            .GetProperty("data")[0]
            .GetProperty("embedding");

        var embedding = new List<float>();
        foreach (var item in embeddingElement.EnumerateArray())
        {
            embedding.Add(item.GetSingle());
        }

        return embedding.ToArray();
    }
}

