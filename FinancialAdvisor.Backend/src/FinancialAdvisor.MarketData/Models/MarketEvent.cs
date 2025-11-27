namespace FinancialAdvisor.MarketData.Models;

public class MarketEvent
{
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; }
}

