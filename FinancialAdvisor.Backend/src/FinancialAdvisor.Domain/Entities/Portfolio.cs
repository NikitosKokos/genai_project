namespace FinancialAdvisor.Domain.Entities;

public class Portfolio
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public decimal TotalValue { get; set; }
}

