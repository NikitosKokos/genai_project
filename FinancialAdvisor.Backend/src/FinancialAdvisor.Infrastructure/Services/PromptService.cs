using FinancialAdvisor.Application.Interfaces;
using FinancialAdvisor.Application.Models;

namespace FinancialAdvisor.Infrastructure.Services
{
    public class PromptService : IPromptService
    {
        public string ConstructSystemPrompt()
        {
            return @"You are an expert financial advisor. 
Provide evidence-based advice grounded in the retrieved documents. 
Always consider the user's risk profile and portfolio context.
Format your response clearly using markdown.
If you recommend a trade, be specific about symbol, action (BUY/SELL), and quantity.
Do not recommend actions if you lack sufficient information.";
        }

        public string ConstructAugmentedUserPrompt(
            string userQuery,
            string portfolioContext,
            string marketContext,
            string ragContext,
            Session session)
        {
            var riskProfile = session?.PortfolioContext?.RiskProfile ?? "moderate";

            return $@"
=== FINANCIAL ADVISOR RAG CONTEXT ===

Current Portfolio:
{portfolioContext}

Market Prices:
{marketContext}

Risk Profile: {riskProfile}

Recent Financial News & Analysis:
{ragContext}

=== END CONTEXT ===

User Query: {userQuery}

Based on the above context, provide personalized financial advice. 
If suitable, suggest specific trades in JSON format at the end of your response like:
```json
{{ ""trades"": [ {{ ""symbol"": ""AAPL"", ""action"": ""BUY"", ""qty"": 10 }} ] }}
```
";
        }
    }
}
