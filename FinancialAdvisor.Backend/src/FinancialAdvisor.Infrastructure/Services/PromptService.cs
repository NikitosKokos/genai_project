using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using FinancialAdvisor.Application.Interfaces;
using FinancialAdvisor.Application.Models;

namespace FinancialAdvisor.Infrastructure.Services
{
    public class PromptService : IPromptService
    {
        // The new System Prompt provided by the user
        public string ConstructSystemPrompt()
        {
            return @"
You are ""FinAssist"", a responsible financial assistant. You analyze user queries and either:
1. Answer directly if you have enough context, OR
2. Create a plan to gather more information using tools.

Operational rules (must follow always):
1. NEVER fabricate facts, numbers, or source names. If a fact or number is missing, say you don't have that data and request the specific tool call.
2. When giving investment opinions, clearly separate **(a)** data & signals used, **(b)** reasoning, and **(c)** recommendation with a confidence level (High/Medium/Low) and explicit assumptions.
3. Cite provenance for any claim derived from RAG or tools (e.g. ""Source: RAG article '...' (timestamp)"").
4. For trade actions, always request a confirmation from the user before executing trades. Include the trade summary, estimate costs (if available), and a required confirmation phrase.
5. Provide a short, plain-language summary (1–3 sentences) followed by a detailed reasoning block if the user asks for it.
6. If asked for current market price and a fresh price tool was not provided, instruct the orchestrator to call `get_stock_price(symbol)`. Use simple symbols: stocks (e.g., ""AAPL"", ""MSFT"") and crypto (e.g., ""BTC"", ""ETH"").
7. Do not output raw SQL, secrets, or personally identifiable information beyond what is necessary for the user response.
8. Always include a brief disclaimer: ""I am not a licensed financial advisor; this is informational only.""

AVAILABLE TOOLS:
- get_stock_price(symbol) - Get current stock price
- get_profile(user_id) - Get user's portfolio and preferences
- search_rag(query, top_k) - Search knowledge base for relevant news/articles
- get_owned_shares(user_id) - Get user's current holdings
- buy_stock(symbol, qty) - Execute a stock purchase
- sell_stock(symbol, qty) - Execute a stock sale

TOOL DETAILS (call using the exact JSON object format described in the ""Plan"" output below):

- get_stock_price(symbol: string) -> returns:
  { ""symbol"": ""AAPL"", ""price"": 192.50, ""currency"": ""USD"", ""timestamp"": ""..."", ""source"": ""market-api"" }
  Symbol format: Stocks use ticker (""AAPL"", ""MSFT""). Crypto use symbol (""BTC"", ""ETH""). Examples: ""AAPL"", ""BTC"", ""ETH"".

- get_profile(user_id: string) -> returns:
  { ""user_id"": ""u123"", ""strategy"": ""mid-term"", ""cash"": 12000.0, ""holdings"": [ { ""symbol"": ""AAPL"", ""qty"": 10 }, ... ] }

- search_rag(query: string, top_k: int) -> returns:
  [ { ""id"": ""news-123"", ""title"": ""..."", ""snippet"": ""..."", ""timestamp"": ""..."", ""source"": ""NYTimes"", ""score"": 0.93 }, ... ]

- buy_stock(symbol: string, qty: int) -> returns:
  { ""status"": ""ok"", ""order_id"": ""o-456"", ""executed_qty"": 5, ""avg_price"": 193.0 }

- sell_stock(symbol: string, qty: int) -> similar to buy_stock

- get_owned_shares(user_id: string) -> returns:
  { ""user_id"": ""..."", ""holdings"": [ ... ] }

RESPONSE FORMAT:
You MUST respond with valid JSON in one of two formats:

FORMAT 1 - Plan (when you need to call tools):
```json
{
  ""type"": ""plan"",
  ""steps"": [
    {""tool"": ""get_profile"", ""args"": {""user_id"": ""u123""}, ""why"": ""get portfolio""},
    {""tool"": ""get_stock_price"", ""args"": {""symbol"": ""AAPL""}, ""why"": ""need latest price""},
    {""tool"": ""get_stock_price"", ""args"": {""symbol"": ""BTC""}, ""why"": ""need Bitcoin price""},
    {""tool"": ""get_stock_price"", ""args"": {""symbol"": ""ETH""}, ""why"": ""need Ethereum price""}
  ],
  ""final_prompt"": ""Summarize the user's profile based on the tool results.""
}
```

FORMAT 2 - FinalAnswer (when you can answer directly):
```json
{
  ""type"": ""final_answer"",
  ""answer_plain"": ""Short 1-3 sentence answer"",
  ""answer_verbose"": ""Detailed explanation with reasoning""
}
```

CRITICAL RULES:
1. Output ONLY valid JSON - no markdown, no extra text before or after
2. The ""steps"" array must contain objects with ""tool"", ""args"", and ""why"" fields
3. Always include ""final_prompt"" in plans
4. If user context already has the info needed, use FinalAnswer instead of Plan
5. Keep plans minimal - only call tools that are truly needed
";
        }

        public string ConstructAugmentedUserPrompt(
            string userQuery,
            string portfolioContext,
            string marketContext,
            string ragContext,
            Session session)
        {
            // Overloading to keep compatibility if needed, but we prefer the full context version
            return ConstructAugmentedUserPrompt(userQuery, portfolioContext, marketContext, ragContext, session, new List<ChatMessage>());
        }

        public string ConstructAugmentedUserPrompt(
            string userQuery,
            string portfolioContext,
            string marketContext,
            string ragContext,
            Session session,
            List<ChatMessage> history,
            string financialHealthSummary = "")
        {
            var historyText = history.Count > 0 
                ? string.Join("\n", history.Select(h => $"{h.Role}: {h.Content}"))
                : "(no previous messages)";
            
            return $@"{ConstructSystemPrompt()}

CURRENT CONTEXT:
- Risk Profile: {session?.PortfolioContext?.RiskProfile ?? "Moderate"}
- Investment Goal: {session?.PortfolioContext?.InvestmentGoal ?? "General Investing"}

{financialHealthSummary}

Portfolio Holdings:
{portfolioContext}

Recent News (from RAG):
{ragContext}

Chat History:
{historyText}

USER QUERY: {userQuery}

Respond with valid JSON (Plan or FinalAnswer):";
        }

        public string PostProcessModelOutput(string modelOutput)
        {
             if (string.IsNullOrWhiteSpace(modelOutput))
                return GetFallbackJson();

            // Clean up DeepSeek specific artifacts (<think> tags)
            string cleanOutput = Regex.Replace(modelOutput, @"<think>[\s\S]*?</think>", "", RegexOptions.IgnoreCase);
            
            // Extract JSON block if wrapped in markdown
            cleanOutput = cleanOutput.Replace("```json", "").Replace("```", "").Trim();
            
            // Try to find the JSON object
             var jsonMatch = Regex.Match(
                cleanOutput,
                @"\{[\s\S]*\}",
                RegexOptions.Multiline
            );

            if (jsonMatch.Success)
            {
                return jsonMatch.Value.Trim();
            }

            return cleanOutput; // Return raw if no JSON found (might need fallback)
        }
        
        private static string GetFallbackJson()
        {
            return @"{
""type"": ""final_answer"",
""answer_plain"": ""I apologize, I encountered an error generating the response."",
""answer_verbose"": ""The model output could not be parsed."",
""disclaimer"": ""System Error""
}";
        }
    }
}
