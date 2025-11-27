namespace FinancialAdvisor.Application.Services;

public class RagService
{
    public Task<object> QueryAsync(string query)
    {
        return Task.FromResult<object>(new { query });
    }
}

