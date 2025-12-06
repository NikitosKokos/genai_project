using FinancialAdvisor.Application.Interfaces;
using FinancialAdvisor.Application.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;
using FinancialAdvisor.Infrastructure.Tools;

namespace FinancialAdvisor.Infrastructure.Services.RAG
{
    public class AgentOrchestrator : IRagService
    {
        private readonly IContextService _contextService;
        private readonly IPromptService _promptService;
        private readonly ILLMService _llmService;
        private readonly IEnumerable<ITool> _tools;
        private readonly ILogger<AgentOrchestrator> _logger;
        
        public AgentOrchestrator(
            IContextService contextService,
            IPromptService promptService,
            ILLMService llmService,
            IEnumerable<ITool> tools,
            ILogger<AgentOrchestrator> logger)
        {
            _contextService = contextService;
            _promptService = promptService;
            _llmService = llmService;
            _tools = tools;
            _logger = logger;
        }

        /// <summary>
        /// Determines if a user query requires tool usage (e.g., current prices, portfolio data).
        /// Queries that require real-time data must go through the planning path to use tools.
        /// </summary>
        private bool RequiresToolUsage(string userQuery)
        {
            if (string.IsNullOrWhiteSpace(userQuery))
                return false;

            var queryLower = userQuery.ToLowerInvariant();
            
            // Keywords that indicate need for current price data
            var priceKeywords = new[]
            {
                "price", "cost", "worth", "value", "how much",
                "current price", "stock price", "crypto price",
                "what is the price", "what's the price", "price of",
                "trading at", "selling for", "buying at"
            };

            // Keywords that indicate need for portfolio/holdings data
            var portfolioKeywords = new[]
            {
                "portfolio", "holdings", "owned", "shares", "stocks i own",
                "my stocks", "my portfolio", "what do i own", "current holdings"
            };

            // Keywords that indicate need for profile data
            var profileKeywords = new[]
            {
                "my profile", "my account", "my balance", "cash balance",
                "available cash", "my strategy", "risk profile"
            };

            // Check if query contains any price-related keywords
            if (priceKeywords.Any(keyword => queryLower.Contains(keyword)))
            {
                return true;
            }

            // Check if query contains portfolio-related keywords
            if (portfolioKeywords.Any(keyword => queryLower.Contains(keyword)))
            {
                return true;
            }

            // Check if query contains profile-related keywords
            if (profileKeywords.Any(keyword => queryLower.Contains(keyword)))
            {
                return true;
            }

            // Check for specific stock/crypto symbols (likely price queries)
            var commonSymbols = new[] { "aapl", "msft", "googl", "amzn", "tsla", "btc", "eth", "stock", "crypto" };
            if (commonSymbols.Any(symbol => queryLower.Contains(symbol)))
            {
                // If it mentions a symbol AND asks about price/value, require tools
                if (priceKeywords.Any(keyword => queryLower.Contains(keyword)))
                {
                    return true;
                }
            }

            return false;
        }

        public async Task<object> QueryAsync(string query)
        {
            return await ProcessQueryAsync(query, "demo_session_001");
        }

        public async Task<ChatResponse> ProcessQueryAsync(string userQuery, string sessionId)
        {
            var responseContent = "";
            await foreach (var chunk in ProcessQueryStreamAsync(userQuery, sessionId))
            {
                // Only keep the final part which isn't a status update if we want to be pure,
                // but for now we just accumulate everything that isn't a status message?
                // Actually, for non-streaming, we want the final clean answer.
                // Since ProcessQueryStreamAsync mixes status and content, this is tricky to reuse directly
                // without parsing.
                // So we keep the logic separate or duplicate for now to be safe, OR we adapt ProcessQueryStreamAsync
                // to yield typed objects (Status vs Token). But the interface is IAsyncEnumerable<string>.
                
                if (!chunk.StartsWith("STATUS:")) 
                {
                    responseContent += chunk;
                }
            }

            return new ChatResponse 
            { 
                Advice = responseContent,
                Timestamp = DateTime.UtcNow
            };
        }

        public async IAsyncEnumerable<string> ProcessQueryStreamAsync(string userQuery, string sessionId, [EnumeratorCancellation] System.Threading.CancellationToken cancellationToken = default, bool enableReasoning = false, int documentCount = 3)
        {
            _logger.LogInformation($"[{sessionId}] Processing Agent Query: {userQuery}");
            yield return "<status>Analyzing request...</status>";

            // 1. Gather Initial Context (Parallel for better performance)
            _logger.LogInformation($"[{sessionId}] Fetching context: history, session, portfolio");
            var historyTask = _contextService.GetChatHistoryAsync(sessionId, 6);
            var sessionTask = _contextService.GetSessionAsync(sessionId);
            var portfolioTask = _contextService.GetPortfolioAsync(sessionId);
            
            await Task.WhenAll(historyTask, sessionTask, portfolioTask);
            
            var history = historyTask.Result;
            var session = sessionTask.Result;
            var portfolio = portfolioTask.Result;
            
            // Log portfolio retrieval for debugging
            if (portfolio == null)
            {
                _logger.LogWarning($"[{sessionId}] No portfolio found in database for sessionId: '{sessionId}'");
            }
            else
            {
                var holdingsCount = portfolio.Holdings?.Count ?? 0;
                _logger.LogInformation($"[{sessionId}] Portfolio retrieved: {holdingsCount} holdings, TotalValue: {portfolio.TotalValue}, CashBalance: {portfolio.CashBalance}");
            }
            
            // Build health summary from already-fetched data (avoids redundant DB calls)
            var healthSummary = _contextService.BuildFinancialHealthSummary(session, portfolio);

            // 2. Proactive RAG
            var ragTool = _tools.FirstOrDefault(t => t.Name == "search_rag");
            string ragContext = "";
            if (ragTool != null)
            {
                yield return "<status>Checking knowledge base...</status>";
                var ragResultJson = await ragTool.ExecuteAsync(JsonSerializer.Serialize(new { query = userQuery, top_k = 3 }));
                ragContext = ragResultJson;
            }

            // 3. Initial Prompt
            string marketContext = "[]"; 
            string portfolioContext = _contextService.FormatPortfolioContext(portfolio);

            var fullPrompt = _promptService.ConstructAugmentedUserPrompt(
                userQuery, 
                portfolioContext, 
                marketContext, 
                ragContext, 
                session, 
                history,
                healthSummary);

            // Check if query requires tools (current prices, portfolio data, etc.)
            bool requiresTools = RequiresToolUsage(userQuery);
            
            // FAST PATH: when reasoning is disabled AND query doesn't require tools, skip planning/tools
            // BUT: Always use planning path for queries that need current data (prices, portfolio, etc.)
            if (!enableReasoning && !requiresTools)
            {
                yield return "<status>Generating answer...</status>";

                // Create a direct-answer prompt that explicitly requests plain text
                var directAnswerPrompt = $@"You are FinAssist, a helpful financial assistant. Answer the user's question directly in plain, conversational text.

IMPORTANT RULES:
- DO NOT output JSON. DO NOT use curly braces or square brackets.
- Write naturally as if speaking to a friend who asked for financial advice.
- Be concise but helpful.
- If you reference data, cite it naturally (e.g., ""Based on your portfolio..."").
- End with a brief disclaimer: ""Note: I'm not a licensed financial advisor; this is informational only.""

USER CONTEXT:
{healthSummary}

Portfolio:
{portfolioContext}

Recent News/Knowledge:
{ragContext}

User's Question: {userQuery}

Your helpful response (plain text only):";

                var plainBuilder = new System.Text.StringBuilder();

                await foreach (var token in _llmService.GenerateFinancialAdviceStreamAsync(
                    userQuery,
                    directAnswerPrompt,
                    sessionId,
                    cancellationToken,
                    enableReasoning: false))
                {
                    if (cancellationToken.IsCancellationRequested) yield break;

                    // Stream response tokens directly
                    yield return token;
                    
                    // Extract content for history
                    if (token.StartsWith("<response><![CDATA["))
                    {
                        var start = token.IndexOf("<![CDATA[", StringComparison.Ordinal) + 9;
                        var end = token.IndexOf("]]></response>", StringComparison.Ordinal);
                        if (start > 8 && end > start)
                        {
                            var inner = token.Substring(start, end - start);
                            inner = inner.Replace("]]]]><![CDATA[>", "]]>");
                            plainBuilder.Append(inner);
                        }
                    }
                }

                string answerText = plainBuilder.ToString();
                if (string.IsNullOrWhiteSpace(answerText))
                {
                    answerText = "(no content)";
                }

                // Persist chat history (user + assistant)
                await _contextService.AddChatMessageAsync(sessionId, new ChatMessage { Role = "user", Content = userQuery });
                await _contextService.AddChatMessageAsync(sessionId, new ChatMessage { Role = "assistant", Content = answerText });
                yield break;
            }

            yield return "<status>Planning...</status>";

            // 4. Call LLM (Plan) - Stream the planning phase for better UX
            var accumulatedPlanJson = "";
            var planJsonComplete = false;
            
            await foreach (var token in _llmService.GenerateFinancialAdviceStreamAsync(userQuery, fullPrompt, sessionId, cancellationToken, enableReasoning))
            {
                // Forward thinking tokens to frontend during planning
                if (token.StartsWith("<thinking>"))
                {
                    yield return token;
                }
                // Accumulate response tokens to build the JSON plan
                else if (token.StartsWith("<response>"))
                {
                    // Extract content from CDATA or regular response
                    string content;
                    if (token.Contains("<![CDATA["))
                    {
                        var cdataStart = token.IndexOf("<![CDATA[") + 9;
                        var cdataEnd = token.IndexOf("]]>", cdataStart);
                        if (cdataEnd > cdataStart)
                        {
                            content = token.Substring(cdataStart, cdataEnd - cdataStart);
                            content = content.Replace("]]]]><![CDATA[>", "]]>");
                        }
                        else
                        {
                            continue;
                        }
                    }
                    else
                    {
                        var endTag = token.IndexOf("</response>");
                        if (endTag > 9)
                        {
                            content = token.Substring(9, endTag - 9);
                            content = content.Replace("&amp;", "&").Replace("&lt;", "<").Replace("&gt;", ">");
                        }
                        else
                        {
                            continue;
                        }
                    }
                    
                    accumulatedPlanJson += content;
                    
                    // Try to detect if JSON is complete (has closing brace and looks valid)
                    // This is a heuristic - we'll validate properly after streaming
                    if (accumulatedPlanJson.Trim().EndsWith("}") && accumulatedPlanJson.Contains("\"type\""))
                    {
                        planJsonComplete = true;
                        // Don't break yet - let the stream finish to ensure we have everything
                    }
                }
                else if (!token.StartsWith("<status>"))
                {
                    // Fallback: accumulate any other content
                    accumulatedPlanJson += token;
                }
            }
            
            // Process the accumulated JSON plan
            var processedResponse = _promptService.PostProcessModelOutput(accumulatedPlanJson);

            JsonDocument doc = null;
            JsonElement root = default;
            bool isPlan = false;

            // Attempt to parse the plan JSON; be lenient so we don't leak the plan to the client
            try 
            {
                doc = JsonDocument.Parse(processedResponse);
                root = doc.RootElement;
                if (root.TryGetProperty("type", out var typeProp))
                {
                    var typeStr = typeProp.GetString()?.ToLower();
                    if (typeStr == "plan") isPlan = true;
                }
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to parse Plan JSON. Falling back to raw response.");
                // Cannot yield in catch block
            }

            // Heuristic: if the model returned a plan JSON string that failed strict parsing,
            // treat it as a plan to avoid sending raw plan JSON to the client.
            if (!isPlan && processedResponse.Contains("\"type\"", StringComparison.OrdinalIgnoreCase) &&
                processedResponse.Contains("plan", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    doc = JsonDocument.Parse(processedResponse);
                    root = doc.RootElement;
                    if (root.TryGetProperty("type", out var typeProp))
                    {
                        var typeStr = typeProp.GetString()?.ToLower();
                        if (typeStr == "plan") isPlan = true;
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Heuristic plan parse failed; still treating as plan to avoid leaking raw JSON.");
                    isPlan = true;
                }
            }

            if (doc == null && !isPlan)
            {
                // Safeguard: if we can't parse the response and it's not a plan,
                // treat it as plain text and wrap it properly
                _logger.LogWarning("[{SessionId}] Unparseable response, treating as plain text.", sessionId);
                yield return "<status>Finalizing answer...</status>";
                
                // Stream in chunks with proper wrapping
                const int chunkSize = 50;
                for (int i = 0; i < processedResponse.Length; i += chunkSize)
                {
                    if (cancellationToken.IsCancellationRequested) yield break;
                    int length = Math.Min(chunkSize, processedResponse.Length - i);
                    var chunk = processedResponse.Substring(i, length);
                    var safeChunk = chunk.Replace("]]>", "]]]]><![CDATA[>");
                    yield return $"<response><![CDATA[{safeChunk}]]></response>";
                    await Task.Delay(5, cancellationToken);
                }
                
                await _contextService.AddChatMessageAsync(sessionId, new ChatMessage { Role = "user", Content = userQuery });
                await _contextService.AddChatMessageAsync(sessionId, new ChatMessage { Role = "assistant", Content = processedResponse });
                yield break;
            }

            if (isPlan)
            {
                // Validate that we can actually execute the plan (root must be valid)
                if (root.ValueKind == JsonValueKind.Undefined || !root.TryGetProperty("steps", out var stepsElement))
                {
                    // Plan detected but malformed - fall back to direct answer
                    _logger.LogWarning("[{SessionId}] Plan JSON detected but malformed or missing 'steps'. Falling back to direct answer.", sessionId);
                    yield return "<status>Generating answer...</status>";
                    
                    // Re-prompt the LLM for a direct, plain-text answer
                    var fallbackPrompt = $@"The user asked: ""{userQuery}""

Based on the available context, provide a helpful, conversational response in plain text.
Do NOT use JSON format. Write naturally as if speaking to a friend.
If you don't have enough information, say so politely.

Context:
{fullPrompt}

Your response:";

                    await foreach (var token in _llmService.GenerateFinancialAdviceStreamAsync(userQuery, fallbackPrompt, sessionId, cancellationToken, false))
                    {
                        if (cancellationToken.IsCancellationRequested) yield break;
                        yield return token;
                    }
                    
                    await _contextService.AddChatMessageAsync(sessionId, new ChatMessage { Role = "user", Content = userQuery });
                    await _contextService.AddChatMessageAsync(sessionId, new ChatMessage { Role = "assistant", Content = "(fallback response)" });
                    yield break;
                }

                // EXECUTE PLAN
                yield return "<status>Executing plan...</status>";
                
                var steps = stepsElement;
                var toolOutputs = new List<string>();

                foreach (var step in steps.EnumerateArray())
                {
                    if (cancellationToken.IsCancellationRequested) yield break;

                    var toolName = step.GetProperty("tool").GetString();
                    var args = step.GetProperty("args");
                    var argsJson = args.ToString();
                    var why = step.TryGetProperty("why", out var whyProp) ? whyProp.GetString() : "";

                    yield return $"<status>Calling {toolName}...</status>";
                    _logger.LogInformation($"[{sessionId}] Tool Call: {toolName} | Reason: {why}");

                    var tool = _tools.FirstOrDefault(t => t.Name == toolName);
                    if (tool != null)
                    {
                        var output = await tool.ExecuteAsync(argsJson);
                        toolOutputs.Add($"Tool '{toolName}' output: {output}");

                        // Update metadata for context persistence
                        string symbol = null;
                        try 
                        {
                            using var argsDoc = JsonDocument.Parse(argsJson);
                            if (argsDoc.RootElement.TryGetProperty("symbol", out var sym))
                                symbol = sym.GetString();
                        } catch {} // Best effort extraction

                        await _contextService.UpdateMetadataAsync(sessionId, symbol, toolName);
                    }
                    else
                    {
                        toolOutputs.Add($"Tool '{toolName}' not found.");
                    }
                }

                // 6. Final Generation
                yield return "<status>Finalizing answer...</status>";
                
                string finalPromptInstruction = "Provide a helpful response based on the tool results.";
                if (root.TryGetProperty("final_prompt", out var fpProp))
                {
                    finalPromptInstruction = fpProp.GetString() ?? finalPromptInstruction;
                }
                
                var toolContext = string.Join("\n\n", toolOutputs);
                
                // Build a clear prompt that emphasizes plain text output
                // The LLMService will format this as: context + "\n\nUser Query: " + userQuery + "\n\nResponse:"
                var systemReminder = @"CRITICAL INSTRUCTION: You are generating the FINAL message that the user will see. 
- DO NOT use JSON format. 
- DO NOT use curly braces { } or quotes around your answer.
- Write in plain, natural conversational text.
- Start writing immediately without any formatting markers.
- Write as if you are speaking directly to the user in a friendly, professional manner.
- Include citations naturally in the text (e.g., 'According to recent news from TechCrunch...').
- Include the disclaimer naturally at the end (e.g., 'Please note: I am not a licensed financial advisor; this is informational only.').";
                
                var combinedContext = $@"{systemReminder}

Tool Results:
{toolContext}";
                
                var userQueryForFinal = $@"{finalPromptInstruction}

Begin your response now in plain text:";
                
                // Stream the final answer token by token
                string finalResponseClean = "";
                string streamBuffer = ""; // Buffer to detect JSON in stream
                
                await foreach (var token in _llmService.GenerateFinancialAdviceStreamAsync(userQueryForFinal, combinedContext, sessionId, cancellationToken, enableReasoning))
                {
                    // Forward all tokens (thinking, response) as-is - they're already in XML format
                    yield return token;
                    
                    // Extract response content for history (skip thinking and XML tags)
                    if (token.StartsWith("<response>"))
                    {
                        // Handle both CDATA and regular content
                        string content;
                        if (token.Contains("<![CDATA["))
                        {
                            // Extract from CDATA section
                            var cdataStart = token.IndexOf("<![CDATA[") + 9;
                            var cdataEnd = token.IndexOf("]]>", cdataStart);
                            if (cdataEnd > cdataStart)
                            {
                                content = token.Substring(cdataStart, cdataEnd - cdataStart);
                                // Restore any ]]> that were escaped
                                content = content.Replace("]]]]><![CDATA[>", "]]>");
                            }
                            else
                            {
                                // Incomplete CDATA, skip for now
                                continue;
                            }
                        }
                        else
                        {
                            // Regular content (no CDATA)
                            var endTag = token.IndexOf("</response>");
                            if (endTag > 9)
                            {
                                content = token.Substring(9, endTag - 9);
                                // Unescape XML entities if any
                                content = content.Replace("&amp;", "&").Replace("&lt;", "<").Replace("&gt;", ">");
                            }
                            else
                            {
                                continue;
                            }
                        }
                        
                        finalResponseClean += content;
                        streamBuffer += content;
                    }
                    else if (!token.StartsWith("<thinking>") && !token.StartsWith("<status>"))
                    {
                        // Fallback: if it's not a tag, treat as response content (for backward compatibility)
                        finalResponseClean += token;
                        streamBuffer += token;
                    }
                }
                
                // Post-process: If the response is JSON, extract the text for history
                // (We already streamed it to the user, but for history we want clean text)
                await _contextService.AddChatMessageAsync(sessionId, new ChatMessage { Role = "user", Content = userQuery });
                
                string historyContent = finalResponseClean;
                try 
                {
                    // Try to parse as JSON and extract text content
                    using var finalDoc = JsonDocument.Parse(finalResponseClean.Trim());
                    if (finalDoc.RootElement.TryGetProperty("answer_verbose", out var v) && !string.IsNullOrWhiteSpace(v.GetString()))
                    {
                        historyContent = v.GetString();
                    }
                    else if (finalDoc.RootElement.TryGetProperty("answer_plain", out var p) && !string.IsNullOrWhiteSpace(p.GetString()))
                    {
                        historyContent = p.GetString();
                    }
                }
                catch 
                {
                    // Not JSON, use as-is
                }
                
                await _contextService.AddChatMessageAsync(sessionId, new ChatMessage { Role = "assistant", Content = historyContent });
            }
            else 
            {
                // Direct Answer (FinalAnswer JSON or plain text)
                // Extract text from JSON if it's a FinalAnswer, otherwise stream as-is
                string answerToStream = processedResponse;
                
                if (root.ValueKind != JsonValueKind.Undefined)
                {
                    // Try to extract answer_verbose or answer_plain from JSON
                    if (root.TryGetProperty("answer_verbose", out var v) && !string.IsNullOrWhiteSpace(v.GetString()))
                    {
                        answerToStream = v.GetString();
                    }
                    else if (root.TryGetProperty("answer_plain", out var p) && !string.IsNullOrWhiteSpace(p.GetString()))
                    {
                        answerToStream = p.GetString();
                    }
                }
                
                // Stream the answer character by character to simulate token streaming
                // This provides a better UX than dumping it all at once
                yield return "<status>Finalizing answer...</status>";
                
                // Stream in chunks to simulate natural token-by-token streaming
                // Use CDATA to preserve markdown syntax
                const int chunkSize = 10; // Characters per chunk for smooth streaming effect
                for (int i = 0; i < answerToStream.Length; i += chunkSize)
                {
                    if (cancellationToken.IsCancellationRequested) yield break;
                    int length = Math.Min(chunkSize, answerToStream.Length - i);
                    var chunk = answerToStream.Substring(i, length);
                    // Use CDATA to preserve markdown - replace ]]> if it appears
                    var safeChunk = chunk.Replace("]]>", "]]]]><![CDATA[>");
                    yield return $"<response><![CDATA[{safeChunk}]]></response>";
                    await Task.Delay(10, cancellationToken); // Small delay for smooth streaming effect
                }

                // Save to history
                await _contextService.AddChatMessageAsync(sessionId, new ChatMessage { Role = "user", Content = userQuery });
                await _contextService.AddChatMessageAsync(sessionId, new ChatMessage { Role = "assistant", Content = answerToStream });
            }

            if (doc != null) doc.Dispose();
        }
    }
}
