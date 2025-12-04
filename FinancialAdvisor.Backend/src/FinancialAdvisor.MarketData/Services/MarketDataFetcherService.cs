namespace FinancialAdvisor.MarketData.Services;

public class MarketDataFetcherService
{
    public Task<object> FetchQuoteAsync(string symbol)
    {
        return Task.FromResult<object>(new { symbol });
    }
}

