namespace FinancialAdvisor.MarketData.Services;

public class CacheStrategy
{
    public Task<T?> GetCachedAsync<T>(string key)
    {
        return Task.FromResult<T?>(default);
    }

    public Task CacheAsync<T>(string key, T value, TimeSpan ttl)
    {
        return Task.CompletedTask;
    }
}

