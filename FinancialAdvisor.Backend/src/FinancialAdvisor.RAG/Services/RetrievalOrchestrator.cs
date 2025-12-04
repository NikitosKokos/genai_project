using FinancialAdvisor.Infrastructure.ExternalServices;
using System.Text;

namespace FinancialAdvisor.RAG.Services;

public class RetrievalOrchestrator
{
    private readonly OpenAiEmbeddingsClient _embeddingsClient;
    private readonly VectorDbManager _vectorDb;

    public RetrievalOrchestrator(OpenAiEmbeddingsClient embeddingsClient, VectorDbManager vectorDb)
    {
        _embeddingsClient = embeddingsClient;
        _vectorDb = vectorDb;
    }

    public async Task<string> RetrieveContextAsync(string query)
    {
        var queryEmbedding = await _embeddingsClient.GenerateEmbeddingAsync(query);
        var results = await _vectorDb.SearchAsync(queryEmbedding);

        var sb = new StringBuilder();
        foreach (var result in results)
        {
            sb.AppendLine($"--- Source: {result.Document.Metadata} ---");
            sb.AppendLine(result.Document.Content);
            sb.AppendLine();
        }

        return sb.ToString();
    }
}

