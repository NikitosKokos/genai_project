namespace FinancialAdvisor.Application.Services;

public class TransactionService
{
    public Task<object> GetTransactionsAsync(int userId)
    {
        return Task.FromResult<object>(new { userId });
    }
}

