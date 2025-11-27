namespace FinancialAdvisor.Application.DTOs;

public class TransactionDto
{
    public int TransactionId { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

