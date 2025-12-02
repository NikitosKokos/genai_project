using FinancialAdvisor.Application.Interfaces;
using System.Threading.Tasks;

namespace FinancialAdvisor.Application.Services;

public class MarketDataService : IMarketDataService
{
    public Task<object> GetMarketDataAsync(string symbol)
    {
        return Task.FromResult<object>(new { symbol, price = 100.0m });
    }
}
