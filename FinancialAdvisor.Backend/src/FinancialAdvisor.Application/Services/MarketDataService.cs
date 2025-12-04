namespace FinancialAdvisor.Application.Services;

public class MarketDataService
{
    public Task<object> GetMarketDataAsync(string symbol)
    {
        return Task.FromResult<object>(new { symbol });
    }
}

