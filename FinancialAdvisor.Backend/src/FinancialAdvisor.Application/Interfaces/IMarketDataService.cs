namespace FinancialAdvisor.Application.Interfaces;

public interface IMarketDataService
{
    Task<object> GetMarketDataAsync(string symbol);
}

