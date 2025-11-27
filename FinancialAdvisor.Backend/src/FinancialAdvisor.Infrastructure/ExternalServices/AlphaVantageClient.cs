namespace FinancialAdvisor.Infrastructure.ExternalServices;

public class AlphaVantageClient
{
    public Task<object> GetQuoteAsync(string symbol)
    {
        return Task.FromResult<object>(new { symbol });
    }
}

