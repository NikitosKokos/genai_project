namespace FinancialAdvisor.MarketData.Models;

public class NewsArticle
{
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime PublishedAt { get; set; }
}

