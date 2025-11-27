namespace FinancialAdvisor.Application.Interfaces;

public interface IPortfolioService
{
    Task<object> GetPortfolioAsync(int userId);
}

