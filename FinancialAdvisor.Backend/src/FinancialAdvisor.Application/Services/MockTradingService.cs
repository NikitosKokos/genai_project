namespace FinancialAdvisor.Application.Services;

public class MockTradingService
{
    public Task<object> ExecuteTradeAsync(string symbol, int quantity)
    {
        return Task.FromResult<object>(new { symbol, quantity });
    }
}

