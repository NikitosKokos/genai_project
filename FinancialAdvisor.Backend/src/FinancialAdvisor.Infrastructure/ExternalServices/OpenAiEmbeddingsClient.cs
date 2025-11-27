namespace FinancialAdvisor.Infrastructure.ExternalServices;

public class OpenAiEmbeddingsClient
{
    public Task<float[]> GenerateEmbeddingAsync(string text)
    {
        return Task.FromResult(new float[0]);
    }
}

