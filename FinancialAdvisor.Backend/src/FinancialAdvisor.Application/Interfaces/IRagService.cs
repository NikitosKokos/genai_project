namespace FinancialAdvisor.Application.Interfaces;

public interface IRagService
{
    Task<object> QueryAsync(string query);
}

