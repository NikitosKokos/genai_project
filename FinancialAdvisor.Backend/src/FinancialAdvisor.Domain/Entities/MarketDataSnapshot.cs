namespace FinancialAdvisor.Domain.Entities;

public class MarketDataSnapshot
{
    public int Id { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public DateTime Timestamp { get; set; }
}

