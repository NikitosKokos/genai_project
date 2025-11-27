namespace FinancialAdvisor.MarketData.Models;

public class StockQuote
{
    public string Symbol { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public DateTime Timestamp { get; set; }
}

