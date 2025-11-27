namespace FinancialAdvisor.RAG.Services;

public class RetrievalOrchestrator
{
    public Task<object> RetrieveAsync(string query)
    {
        return Task.FromResult<object>(new { query });
    }
}

