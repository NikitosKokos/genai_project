using System.Collections.Generic;
using System.Threading.Tasks;
using FinancialAdvisor.Application.Models;

namespace FinancialAdvisor.Application.Interfaces
{
    public interface IMarketDataService
    {
        Task<List<MarketDataCache>> GetMarketDataAsync(List<string> symbols);
        string FormatMarketContext(List<MarketDataCache> marketData);
    }
}
