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

        public async Task<object> QueryAsync(string query)
        {
            return await ProcessQueryAsync(query, "default-session");
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

            // 1. Gather Initial Context
            var history = await _contextService.GetChatHistoryAsync(sessionId, 6);
            var session = await _contextService.GetSessionAsync(sessionId);
            var portfolio = await _contextService.GetPortfolioAsync(sessionId);

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
                history);

            yield return "<status>Planning...</status>";

            // 4. Call LLM (Plan)
            var llmResponse = await _llmService.GenerateFinancialAdviceAsync(userQuery, fullPrompt, sessionId);
            var processedResponse = _promptService.PostProcessModelOutput(llmResponse);

            JsonDocument doc = null;
            JsonElement root = default;
            bool isPlan = false;

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

            if (doc == null && !isPlan)
            {
                 yield return processedResponse;
                 yield break;
            }

            if (isPlan)
            {
                // EXECUTE PLAN
                yield return "<status>Executing plan...</status>";
                
                var steps = root.GetProperty("steps");
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
                    }
                    else
                    {
                        toolOutputs.Add($"Tool '{toolName}' not found.");
                    }
                }

                // 6. Final Generation
                yield return "<status>Finalizing answer...</status>";
                var finalPromptInstruction = root.GetProperty("final_prompt").GetString();
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
