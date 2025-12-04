namespace FinancialAdvisor.MarketData.Config;

public class AlphaVantageConfiguration
{
    public string ApiKey { get; set; } = string.Empty;
    public int RateLimitPerMinute { get; set; } = 5;
}

