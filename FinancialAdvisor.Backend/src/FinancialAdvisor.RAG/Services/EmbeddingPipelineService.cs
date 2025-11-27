namespace FinancialAdvisor.RAG.Services;

public class EmbeddingPipelineService
{
    public Task<float[]> GenerateEmbeddingAsync(string text)
    {
        return Task.FromResult(new float[0]);
    }
}

