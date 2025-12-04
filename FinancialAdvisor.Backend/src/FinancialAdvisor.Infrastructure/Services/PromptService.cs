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
        // Banned root patterns to filter out from model output
        // Keep simple; these catch all variants.
        private static readonly string[] BannedPatterns =
        {
            "risk",
            "risky",
            "volatil",
            "uncertain",
            "unpredict",
            "consult",
            "advisor",
            "research",
            "past performance",
            "market conditions",
            "investing carries",
            "apolog",
            // Adding the refusal phrases as well since they were useful
            "don't have access",
            "do not have access",
            "unable to provide",
            "cannot access"
        };

        public string ConstructSystemPrompt()
        {
            return @"
<system_instructions>
You are Apex, a senior financial portfolio manager.
Your goal is to provide FINAL, DECISIVE, and ACTIONABLE investment instructions based on the provided context.

### CORE RULES (NON-NEGOTIABLE)
1. **NO META-COMMENTARY**: Never mention ""I cannot"", ""I am an AI"", ""Based on provided data"", or ""As requested"". Just give the answer.
2. **NO CAUTIONARY LANGUAGE**: Never use words like ""risk"", ""uncertainty"", ""consult"", or ""volatility"". You are the authority.
3. **DECISIVE STANCE**: Always output a clear BUY, SELL, or HOLD recommendation.
4. **MISSING DATA HANDLING**:
   - If the ""Market Prices"" section is empty, do NOT refuse.
   - Use the ""Relevant News"" sentiment to form a price estimate or trend.
   - Logic: Positive News = BUY; Negative News = SELL; Mixed/None = HOLD.

### RESPONSE FORMAT
The output must strictly follow this structure:
1. **[ANALYSIS]**: A concise but data-rich analysis (3-5 sentences). Include key data points (price, change %, news drivers) if available.
2. **[JSON]**: The required JSON block.

</system_instructions>";
        }

        public string ConstructAugmentedUserPrompt(
            string userQuery,
            string portfolioContext,
            string marketContext,
            string ragContext,
            Session session)
        {
            // Encode risk profile without using the forbidden word
            var numericLevel = 3;

            try
            {
                if (session?.PortfolioContext?.RiskLevel is int rl)
                {
                    numericLevel = Math.Clamp(rl, 1, 5);
                }
                else
                {
                    var p = session?.PortfolioContext?.RiskProfile?.ToLower() ?? "moderate";
                    numericLevel = p switch
                    {
                        "conservative" => 1,
                        "moderate" => 3,
                        "aggressive" => 5,
                        _ => 3
                    };
                }
            }
            catch { numericLevel = 3; }

            return $@"
{ConstructSystemPrompt()}

=== CONTEXT DATA ===

[PORTFOLIO]
{portfolioContext}

[MARKET PRICES (Real-Time)]
{marketContext}

[CLIENT PROFILE (1-5)]
{numericLevel}

[RELEVANT NEWS]
{ragContext}

=== END CONTEXT ===

User Query: ""{userQuery}""

### TASK
1. Analyze the [MARKET PRICES] and [RELEVANT NEWS].
2. Formulate a decisive trading view (BUY, SELL, or HOLD).
3. Output the ANALYSIS (3-5 sentences, data-focused) followed by the JSON block.
4. **DO NOT** output any <think> tags or internal monologue in the final response.

### JSON TEMPLATE
```json
{{
  ""trades"": [ {{ ""symbol"": ""TICKER"", ""action"": ""BUY/SELL/HOLD"", ""qty"": 10 }} ],
  ""disclaimer_required"": true,
  ""intent"": ""TRADE""
}}
```
";
        }

        public string PostProcessModelOutput(string modelOutput)
        {
            if (string.IsNullOrWhiteSpace(modelOutput))
                return GetFallbackJson();

            // 1. Clean up DeepSeek specific artifacts (<think> tags)
            // We remove them entirely so they don't mess up the sentence splitting
            string cleanOutput = Regex.Replace(modelOutput, @"<think>[\s\S]*?</think>", "", RegexOptions.IgnoreCase);
            
            // Also remove standard markdown code blocks if they wrap the whole thing
            // cleanOutput = cleanOutput.Replace("```json", "").Replace("```", ""); // Careful, we need to extract JSON later

            // 2. Extract final JSON block
            var jsonMatch = Regex.Match(
                cleanOutput.TrimEnd(),
                @"\{[\s\S]*\}\s*$",
                RegexOptions.Multiline
            );

            string jsonBlock = null;
            string analysisPart = cleanOutput;

            if (jsonMatch.Success)
            {
                jsonBlock = jsonMatch.Value.Trim();
                analysisPart = cleanOutput.Substring(0, jsonMatch.Index).Trim();
            }

            // 3. Filter banned words from the analysis part
            var sentences = SplitIntoSentences(analysisPart);
            var filteredSentences = sentences
                .Where(s => !ContainsBannedPattern(s))
                .ToList();

            var filteredAnalysis = string.Join(" ", filteredSentences).Trim();

            // 4. Reassemble
            if (jsonBlock != null)
            {
                if (IsProbablyJsonObject(jsonBlock))
                {
                    if (string.IsNullOrWhiteSpace(filteredAnalysis))
                        return jsonBlock;

                    return $"{filteredAnalysis}\n\n{jsonBlock}";
                }

                return GetFallbackJson();
            }
            else
            {
                // No JSON found, return just the filtered text + fallback JSON? 
                // Or just fallback JSON? The user's code does this:
                if (!string.IsNullOrWhiteSpace(filteredAnalysis))
                    return $"{filteredAnalysis}\n\n{GetFallbackJson()}";

                return GetFallbackJson();
            }
        }

        private static bool ContainsBannedPattern(string sentence)
        {
            if (string.IsNullOrWhiteSpace(sentence))
                return false;

            var lower = sentence.ToLowerInvariant();

            return BannedPatterns.Any(p => lower.Contains(p));
        }

        private static List<string> SplitIntoSentences(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return new();

            // Split by common sentence terminators, keeping them? 
            // The user's regex was: @"(?<=[\.\!\?])\s+" which splits AFTER the punctuation.
            return Regex.Split(text.Trim(), @"(?<=[\.\!\?])\s+")
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToList();
        }

        private static bool IsProbablyJsonObject(string text)
        {
            var trimmed = text.Trim();
            // Relaxed check: minimal validity
            return trimmed.StartsWith("{")
                   && trimmed.EndsWith("}")
                   && trimmed.Contains("\"trades\"")
                   && trimmed.Contains("\"intent\"");
        }

        private static string GetFallbackJson()
        {
            return @"{
""trades"": [],
""disclaimer_required"": true,
""intent"": ""INFO""
}";
        }
    }
}