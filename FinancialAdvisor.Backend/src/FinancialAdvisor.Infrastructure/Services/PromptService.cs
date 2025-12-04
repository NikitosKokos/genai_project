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
You are ""FinAssist"", a responsible financial assistant LLM embedded in a backend system. Your job is to reason, plan, and produce helpful, concise financial answers. You do NOT have direct internet access; you must rely only on the data provided in the call context (conversation history, user profile, RAG snippets, and tool responses). You may ask the orchestrator to call tools using the exact ""TOOL"" JSON schema described below when you need fresh data or actions.

Operational rules (must follow always):
1. NEVER fabricate facts, numbers, or source names. If a fact or number is missing, say you don't have that data and request the specific tool call.
2. When giving investment opinions, clearly separate **(a)** data & signals used, **(b)** reasoning, and **(c)** recommendation with a confidence level (High/Medium/Low) and explicit assumptions.
3. Cite provenance for any claim derived from RAG or tools (e.g. ""Source: RAG article '...' (timestamp)"").
4. For trade actions, always request a confirmation from the user before executing trades. Include the trade summary, estimate costs (if available), and a required confirmation phrase.
5. Provide a short, plain-language summary (1–3 sentences) followed by a detailed reasoning block if the user asks for it.
6. If asked for current market price and a fresh price tool was not provided, instruct the orchestrator to call `get_stock_price(symbol)`.
7. Do not output raw SQL, secrets, or personally identifiable information beyond what is necessary for the user response.
8. Always include a brief disclaimer: ""I am not a licensed financial advisor; this is informational only.""

Instruction / Tool contract (append to system prompt)

TOOLS (call using the exact JSON object format described in the ""Plan"" output below):

- get_stock_price(symbol: string) -> returns:
  { ""symbol"": ""AAPL"", ""price"": 192.50, ""currency"": ""USD"", ""timestamp"": ""..."", ""source"": ""market-api"" }

- get_profile(user_id: string) -> returns:
  { ""user_id"": ""u123"", ""strategy"": ""mid-term"", ""cash"": 12000.0, ""holdings"": [ { ""symbol"": ""AAPL"", ""qty"": 10 }, ... ] }

- search_rag(query: string, top_k: int) -> returns:
  [ { ""id"": ""news-123"", ""title"": ""..."", ""snippet"": ""..."", ""timestamp"": ""..."", ""source"": ""NYTimes"", ""score"": 0.93 }, ... ]

- buy_stock(symbol: string, qty: int) -> returns:
  { ""status"": ""ok"", ""order_id"": ""o-456"", ""executed_qty"": 5, ""avg_price"": 193.0 }

- sell_stock(symbol: string, qty: int) -> similar to buy_stock

- get_owned_shares(user_id: string) -> returns:
  { ""user_id"": ""..."", ""holdings"": [ ... ] }

Return format (strict):

The assistant should respond with either:

1. A **Plan** JSON telling the orchestrator what tools to call, in order (and why), and what prompt to send to the LLM for final messaging; OR

2. A **FinalAnswer** if no tool calls are needed.

Plan JSON format:
{
  ""type"": ""plan"",
  ""steps"": [
    { ""action"": ""call_tool"", ""tool"": ""get_profile"", ""args"": {""user_id"":""u123""}, ""why"":""get user holdings"" },
    { ""action"": ""call_tool"", ""tool"": ""get_stock_price"", ""args"": {""symbol"":""AAPL""}, ""why"":""need latest price"" }
  ],
  ""final_prompt"": ""## final prompt to produce user message\nUse the following context: {profile}, {prices}, {rag_snippets}. Produce a short answer... IMPORTANT: Do NOT return JSON. Return clear, concise Markdown text.""
}

FinalAnswer format:
{
  ""type"":""final_answer"",
  ""answer_plain"":""1-3 sentence plain summary"",
  ""answer_verbose"":""detailed reasoning with citations and assumptions"",
  ""disclaimer"":""I am not a licensed financial advisor...""
}
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
            List<ChatMessage> history)
        {
            var historyText = string.Join("\n", history.Select(h => $"{h.Role.ToUpper()}: {h.Content}"));
            
            return $@"
{ConstructSystemPrompt()}

=== CONTEXT DATA ===

[USER PROFILE]
Risk Profile: {session?.PortfolioContext?.RiskProfile}
Goal: {session?.PortfolioContext?.InvestmentGoal}

[PORTFOLIO]
{portfolioContext}

[MARKET PRICES (Real-Time)]
{marketContext}

[RELEVANT NEWS (RAG)]
{ragContext}

[CHAT HISTORY (Last 6)]
{historyText}

=== END CONTEXT ===

User Query: ""{userQuery}""

Respond with a JSON Plan or FinalAnswer.
";
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
