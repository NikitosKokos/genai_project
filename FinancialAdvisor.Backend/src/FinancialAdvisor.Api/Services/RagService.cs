using FinancialAdvisor.Application.Interfaces;
using FinancialAdvisor.RAG.Services;
using FinancialAdvisor.Infrastructure.ExternalServices;
using Microsoft.Extensions.Configuration;

namespace FinancialAdvisor.Api.Services;

public class RagService : IRagService
{
    private readonly RetrievalOrchestrator _orchestrator;
    private readonly OpenAiLlmClient _llmClient;
    private readonly string _apiKey;

    public RagService(RetrievalOrchestrator orchestrator, OpenAiLlmClient llmClient, IConfiguration configuration)
    {
        _orchestrator = orchestrator;
        _llmClient = llmClient;
        _apiKey = configuration["OpenAi:ApiKey"] ?? "";
    }

    public async Task<object> QueryAsync(string query)
    {
        // Fallback to Smart Mock if no API key is configured
        if (string.IsNullOrEmpty(_apiKey))
        {
            return GenerateMockResponse(query);
        }

        // Real RAG Pipeline
        var context = await _orchestrator.RetrieveContextAsync(query);
        var response = await _llmClient.GenerateResponseAsync(query, context);

        return new 
        { 
            message = response,
            context = context
        };
    }

    private object GenerateMockResponse(string query)
    {
        string responseText = "I can help you with your financial questions. Try asking about your portfolio, specific stocks like Apple or Microsoft, or market trends.";
        var q = query.ToLower();

        if (q.Contains("apple") || q.Contains("aapl"))
        {
            responseText = "Apple (AAPL) is currently trading at $185.00. Market sentiment is bullish due to strong services revenue growth and upcoming AI features in iOS. It remains a core holding in your portfolio (10 shares).";
        }
        else if (q.Contains("microsoft") || q.Contains("msft"))
        {
            responseText = "Microsoft (MSFT) is a leader in the AI space with its OpenAI partnership. Azure cloud growth is accelerating. It's a strong buy for long-term growth portfolios.";
        }
        else if (q.Contains("portfolio") || q.Contains("performance") || q.Contains("doing"))
        {
            responseText = "Your portfolio is performing well, up 12% YTD. Your tech allocation (AAPL, MSFT, NVDA) has driven most of the gains. However, you might consider rebalancing to reduce volatility as the tech sector is currently overweight.";
        }
        else if (q.Contains("risk") || q.Contains("safe"))
        {
            responseText = "Your current risk profile is 'Growth'. If you're looking for safety, consider increasing allocation to bonds or dividend aristocrats like Johnson & Johnson (JNJ) to hedge against market downturns.";
        }
        else if (q.Contains("buy") || q.Contains("trade"))
        {
            responseText = "To place a trade, use the 'Available Assets' panel on the left. I can analyze a stock for you before you buy if you provide the ticker symbol.";
        }
        else if (q.Contains("hello") || q.Contains("hi"))
        {
            responseText = "Hello! I am Nova, your AI WealthOS assistant. How can I help you manage your portfolio today?";
        }

        return new 
        { 
            message = responseText,
            context = "Simulated Knowledge Base"
        };
    }
}
