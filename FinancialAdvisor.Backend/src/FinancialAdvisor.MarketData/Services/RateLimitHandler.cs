namespace FinancialAdvisor.MarketData.Services;

public class RateLimitHandler
{
    public async Task<T> ExecuteWithRateLimitAsync<T>(Func<Task<T>> action)
    {
        return await action();
    }
}

