namespace FinancialAdvisor.Infrastructure.CacheProviders;

public class RedisCacheProvider
{
    public Task<T?> GetAsync<T>(string key)
    {
        return Task.FromResult<T?>(default);
    }

    public Task SetAsync<T>(string key, T value, TimeSpan expiration)
    {
        return Task.CompletedTask;
    }
}

