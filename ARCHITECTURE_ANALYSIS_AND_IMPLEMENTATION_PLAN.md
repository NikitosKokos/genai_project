# Architecture Analysis & Implementation Plan

## Executive Summary

This document analyzes the current codebase structure, identifies what's implemented, what's missing, and provides a comprehensive implementation plan following industry best practices.

---

## 1. Current Architecture Overview

### 1.1 PromptService.cs Analysis

**Location**: `FinancialAdvisor.Backend/src/FinancialAdvisor.Infrastructure/Services/PromptService.cs`

**Purpose**: 
- Constructs system prompts for the LLM
- Assembles augmented user prompts with context
- Post-processes LLM output (JSON extraction, cleanup)

**Key Components**:

1. **`ConstructSystemPrompt()`** (Lines 13-77)
   - Defines FinAssist persona and operational rules
   - Documents available tools and their schemas
   - Specifies Plan vs FinalAnswer JSON formats
   - ✅ **Well-structured and complete**

2. **`ConstructAugmentedUserPrompt()`** (Lines 79-127)
   - Overloads: one with history, one without
   - Combines: system prompt + user profile + portfolio + market data + RAG + chat history
   - ✅ **Properly implemented with context assembly**

3. **`PostProcessModelOutput()`** (Lines 129-153)
   - Removes DeepSeek artifacts (`<think>`)
   - Extracts JSON from markdown code blocks
   - Regex-based JSON extraction
   - ✅ **Functional but could be more robust**

**How It Works**:
```
User Query → PromptService.ConstructAugmentedUserPrompt()
  ↓
Assembles: System Prompt + Context (Profile, Portfolio, Market, RAG, History)
  ↓
LLM generates Plan/Answer
  ↓
PromptService.PostProcessModelOutput() → Clean JSON
```

**Strengths**:
- ✅ Clear separation of concerns
- ✅ Well-documented tool contracts
- ✅ Handles both planning and final answer formats
- ✅ Context assembly is comprehensive

**Weaknesses**:
- ⚠️ No prompt versioning
- ⚠️ No prompt optimization for different model sizes
- ⚠️ JSON extraction could fail on malformed output

---

## 2. Implementation Status

### ✅ **IMPLEMENTED**

#### 2.1 Conversation Manager + Buffer
**Status**: ✅ **Partially Implemented**

**Current Implementation**:
- `ContextService.GetChatHistoryAsync(sessionId, limit=6)` - Gets last 6 messages
- Messages stored in MongoDB `chat_history` collection
- Messages retrieved in chronological order

**What's Missing**:
- ❌ **No rolling deque** - Uses MongoDB query with limit, not in-memory deque
- ❌ **No automatic buffer management** - Manual limit parameter
- ❌ **No conversation windowing** - Always gets last 6, no sliding window logic

**Location**: `ContextService.cs:82-93`

#### 2.2 User Strategy/Profile Persistence
**Status**: ✅ **Implemented**

**Current Implementation**:
- `Session` model stores `PortfolioContext` (RiskProfile, InvestmentGoal, TotalPortfolioValue)
- Persisted in MongoDB `sessions` collection
- Auto-created if missing with defaults

**Location**: `ContextService.cs:22-48`, `MongoModels.cs:38-73`

#### 2.3 Tools Implementation
**Status**: ✅ **All 6 Tools Implemented**

| Tool | Status | Location | Notes |
|------|--------|----------|-------|
| `get_stock_price` | ✅ | `MarketTools.cs:11-52` | Returns normalized JSON |
| `get_profile` | ✅ | `ProfileTools.cs:10-63` | Returns normalized JSON |
| `get_owned_shares` | ✅ | `ProfileTools.cs:65-103` | Returns normalized JSON |
| `search_rag` | ✅ | `RagTools.cs:12-90` | Vector search with embeddings |
| `buy_stock` | ✅ | `TradeTools.cs:8-38` | Mock implementation |
| `sell_stock` | ✅ | `TradeTools.cs:40-70` | Mock implementation |

**Tool Interface**: `ITool.cs` - Clean, well-typed interface

#### 2.4 RAG Ingestion & Retriever
**Status**: ✅ **Implemented**

**Current Implementation**:
- `NewsIngestionService` - Background service running hourly
- Stores raw articles in `FinancialDocument` collection
- Generates embeddings via `EmbeddingService`
- Vector search with cosine similarity in `SearchRagTool`

**What's Working**:
- ✅ Hourly ingestion job (`NewsIngestionService.cs:20`)
- ✅ Raw article storage with source, timestamp
- ✅ Embedding pipeline
- ✅ Top-K retrieval with provenance

**Location**: 
- Ingestion: `NewsIngestionService.cs`
- Retrieval: `RagTools.cs:26-72`

#### 2.5 Orchestrator Skeleton
**Status**: ✅ **Fully Implemented**

**Current Implementation**:
- `AgentOrchestrator` - Main orchestrator
- Accepts: `userQuery`, `sessionId`, `enableReasoning`, `documentCount`
- Flow: Context gathering → RAG → Planning → Tool execution → Final answer
- Uses LLM for planning (not rule-based)

**Location**: `AgentOrchestrator.cs:67-314`

**Flow**:
```
1. Gather Context (parallel)
2. Proactive RAG search
3. LLM Planning (streaming)
4. Execute Plan (tool calls)
5. Final Answer Generation (streaming)
```

#### 2.6 System Prompts
**Status**: ✅ **Implemented**

- Well-defined system prompt in `PromptService.ConstructSystemPrompt()`
- Includes operational rules, tool contracts, output formats
- ✅ **Complete and production-ready**

#### 2.7 Unit Tests
**Status**: ⚠️ **Partially Implemented**

**Current Tests**:
- `AgentOrchestratorTests.cs` - Basic orchestrator test
- `PromptServiceTests.cs` - Prompt service tests
- `ApiControllerTests.cs` - API integration tests

**What's Missing**:
- ❌ No tests for individual tools
- ❌ No tests for common flows (price query, buy/sell, investment advice)
- ❌ No tests for RAG retrieval
- ❌ No tests for conversation buffer management

---

### ❌ **MISSING / INCOMPLETE**

#### 2.8 Lightweight Metadata Store
**Status**: ❌ **NOT IMPLEMENTED**

**Required**:
- `last_symbol` - Last symbol queried
- `last_tool` - Last tool called
- `last_action_timestamp` - When last action occurred

**Current State**: No metadata tracking in Session or separate collection

#### 2.9 Rolling Deque for Conversation Buffer
**Status**: ❌ **NOT IMPLEMENTED**

**Required**: 
- In-memory rolling deque with `maxlen=6` per conversation
- Automatic windowing (oldest messages drop when limit exceeded)

**Current State**: Uses MongoDB query with limit, not true deque

#### 2.10 Comprehensive Logging & Audit Trail
**Status**: ⚠️ **Partially Implemented**

**Current**:
- Basic logging: `_logger.LogInformation($"[{sessionId}] Tool Call: {toolName} | Reason: {why}")`
- Logs tool name and reason

**Missing**:
- ❌ No structured audit log collection
- ❌ No tool input/output logging
- ❌ No decision reason persistence
- ❌ No audit trail querying capability

#### 2.11 Complete Test Suite
**Status**: ❌ **INCOMPLETE**

**Missing Tests**:
- Price query flow
- Buy/sell flow
- Investment advice flow
- Profile query flow
- RAG retrieval accuracy
- Conversation buffer management

---

## 3. Detailed Implementation Plan

### Phase 1: Metadata Store & Conversation Buffer (High Priority)

#### 3.1 Implement Metadata Store

**File**: `FinancialAdvisor.Backend/src/FinancialAdvisor.Application/Models/MongoModels.cs`

**Changes**:
```csharp
// Add to Session model
[BsonElement("metadata")]
public ConversationMetadata Metadata { get; set; }

// New class
public class ConversationMetadata
{
    [BsonElement("last_symbol")]
    public string LastSymbol { get; set; }
    
    [BsonElement("last_tool")]
    public string LastTool { get; set; }
    
    [BsonElement("last_action_timestamp")]
    public DateTime? LastActionTimestamp { get; set; }
    
    [BsonElement("tool_call_count")]
    public int ToolCallCount { get; set; }
}
```

**File**: `FinancialAdvisor.Backend/src/FinancialAdvisor.Infrastructure/Services/ContextService.cs`

**Add Methods**:
```csharp
public async Task UpdateMetadataAsync(string sessionId, string? symbol = null, string? tool = null)
{
    var session = await GetSessionAsync(sessionId);
    if (session.Metadata == null)
        session.Metadata = new ConversationMetadata();
    
    if (symbol != null) session.Metadata.LastSymbol = symbol;
    if (tool != null) session.Metadata.LastTool = tool;
    session.Metadata.LastActionTimestamp = DateTime.UtcNow;
    session.Metadata.ToolCallCount++;
    
    await _mongoContext.Sessions.ReplaceOneAsync(
        s => s.SessionId == sessionId, 
        session);
}
```

**Update**: `AgentOrchestrator.cs` to call `UpdateMetadataAsync` after each tool call.

---

#### 3.2 Implement Rolling Deque for Conversation Buffer

**Approach**: Use in-memory cache with MongoDB as persistence layer

**File**: `FinancialAdvisor.Backend/src/FinancialAdvisor.Infrastructure/Services/ConversationBufferService.cs` (NEW)

```csharp
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FinancialAdvisor.Application.Interfaces;
using FinancialAdvisor.Application.Models;

namespace FinancialAdvisor.Infrastructure.Services
{
    public interface IConversationBufferService
    {
        Task AddMessageAsync(string sessionId, ChatMessage message);
        Task<List<ChatMessage>> GetRecentMessagesAsync(string sessionId, int maxLen = 6);
        Task ClearBufferAsync(string sessionId);
    }

    public class ConversationBufferService : IConversationBufferService
    {
        private readonly IContextService _contextService;
        private readonly Dictionary<string, Queue<ChatMessage>> _buffers = new();
        private readonly object _lock = new();

        public ConversationBufferService(IContextService contextService)
        {
            _contextService = contextService;
        }

        public async Task AddMessageAsync(string sessionId, ChatMessage message)
        {
            lock (_lock)
            {
                if (!_buffers.ContainsKey(sessionId))
                    _buffers[sessionId] = new Queue<ChatMessage>();

                var buffer = _buffers[sessionId];
                buffer.Enqueue(message);

                // Rolling deque: remove oldest if exceeds maxlen
                const int maxLen = 6;
                while (buffer.Count > maxLen)
                    buffer.Dequeue();
            }

            // Persist to DB
            await _contextService.AddChatMessageAsync(sessionId, message);
        }

        public async Task<List<ChatMessage>> GetRecentMessagesAsync(string sessionId, int maxLen = 6)
        {
            // Try in-memory first
            lock (_lock)
            {
                if (_buffers.ContainsKey(sessionId) && _buffers[sessionId].Count > 0)
                {
                    return _buffers[sessionId].TakeLast(maxLen).ToList();
                }
            }

            // Fallback to DB
            var messages = await _contextService.GetChatHistoryAsync(sessionId, maxLen);
            
            // Populate cache
            lock (_lock)
            {
                _buffers[sessionId] = new Queue<ChatMessage>(messages);
            }

            return messages;
        }

        public Task ClearBufferAsync(string sessionId)
        {
            lock (_lock)
            {
                if (_buffers.ContainsKey(sessionId))
                    _buffers[sessionId].Clear();
            }
            return Task.CompletedTask;
        }
    }
}
```

**Update**: `AgentOrchestrator.cs` to use `IConversationBufferService` instead of direct `GetChatHistoryAsync`.

---

### Phase 2: Comprehensive Audit Logging (High Priority)

#### 3.3 Implement Audit Trail

**File**: `FinancialAdvisor.Backend/src/FinancialAdvisor.Application/Models/MongoModels.cs`

**Add Model**:
```csharp
[BsonCollection("audit_logs")]
public class AuditLog
{
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("session_id")]
    public string SessionId { get; set; }

    [BsonElement("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    [BsonElement("action_type")]
    public string ActionType { get; set; } // "tool_call", "llm_call", "plan_execution"

    [BsonElement("tool_name")]
    public string ToolName { get; set; }

    [BsonElement("input")]
    public BsonDocument Input { get; set; }

    [BsonElement("output")]
    public BsonDocument Output { get; set; }

    [BsonElement("decision_reason")]
    public string DecisionReason { get; set; }

    [BsonElement("execution_time_ms")]
    public long ExecutionTimeMs { get; set; }

    [BsonElement("success")]
    public bool Success { get; set; }

    [BsonElement("error_message")]
    public string ErrorMessage { get; set; }
}
```

**File**: `FinancialAdvisor.Backend/src/FinancialAdvisor.Infrastructure/Services/AuditService.cs` (NEW)

```csharp
using FinancialAdvisor.Application.Models;
using FinancialAdvisor.Infrastructure.Data;
using MongoDB.Bson;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace FinancialAdvisor.Infrastructure.Services
{
    public interface IAuditService
    {
        Task LogToolCallAsync(string sessionId, string toolName, object input, object output, string reason, long executionTimeMs, bool success, string errorMessage = null);
        Task LogLlmCallAsync(string sessionId, string promptType, object input, object output, long executionTimeMs);
        Task<List<AuditLog>> GetAuditLogsAsync(string sessionId, int limit = 100);
    }

    public class AuditService : IAuditService
    {
        private readonly MongoDbContext _mongoContext;

        public AuditService(MongoDbContext mongoContext)
        {
            _mongoContext = mongoContext;
        }

        public async Task LogToolCallAsync(string sessionId, string toolName, object input, object output, string reason, long executionTimeMs, bool success, string errorMessage = null)
        {
            var log = new AuditLog
            {
                Id = ObjectId.GenerateNewId(),
                SessionId = sessionId,
                Timestamp = DateTime.UtcNow,
                ActionType = "tool_call",
                ToolName = toolName,
                Input = BsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(input)),
                Output = BsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(output)),
                DecisionReason = reason,
                ExecutionTimeMs = executionTimeMs,
                Success = success,
                ErrorMessage = errorMessage
            };

            await _mongoContext.AuditLogs.InsertOneAsync(log);
        }

        public async Task LogLlmCallAsync(string sessionId, string promptType, object input, object output, long executionTimeMs)
        {
            var log = new AuditLog
            {
                Id = ObjectId.GenerateNewId(),
                SessionId = sessionId,
                Timestamp = DateTime.UtcNow,
                ActionType = "llm_call",
                ToolName = promptType,
                Input = BsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(input)),
                Output = BsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(output)),
                ExecutionTimeMs = executionTimeMs,
                Success = true
            };

            await _mongoContext.AuditLogs.InsertOneAsync(log);
        }

        public async Task<List<AuditLog>> GetAuditLogsAsync(string sessionId, int limit = 100)
        {
            return await _mongoContext.AuditLogs
                .Find(log => log.SessionId == sessionId)
                .SortByDescending(log => log.Timestamp)
                .Limit(limit)
                .ToListAsync();
        }
    }
}
```

**Update**: `MongoDbContext.cs` to include `AuditLogs` collection.

**Update**: `AgentOrchestrator.cs` to call `IAuditService.LogToolCallAsync` after each tool execution.

---

### Phase 3: Complete Test Suite (Medium Priority)

#### 3.4 Implement Comprehensive Tests

**File**: `FinancialAdvisor.Backend/tests/FinancialAdvisor.UnitTests/Tools/GetStockPriceToolTests.cs` (NEW)

```csharp
using Xunit;
using Moq;
using FinancialAdvisor.Infrastructure.Tools;
using FinancialAdvisor.Application.Interfaces;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FinancialAdvisor.UnitTests.Tools
{
    public class GetStockPriceToolTests
    {
        [Fact]
        public async Task ExecuteAsync_ValidSymbol_ReturnsNormalizedJson()
        {
            // Arrange
            var mockMarketDataService = new Mock<IMarketDataService>();
            mockMarketDataService.Setup(s => s.GetMarketDataAsync(It.IsAny<List<string>>()))
                .ReturnsAsync(new List<MarketData> 
                { 
                    new MarketData { Symbol = "AAPL", Price = 150.50m } 
                });

            var tool = new GetStockPriceTool(mockMarketDataService.Object);
            var args = System.Text.Json.JsonSerializer.Serialize(new { symbol = "AAPL" });

            // Act
            var result = await tool.ExecuteAsync(args);
            var json = System.Text.Json.JsonDocument.Parse(result);

            // Assert
            Assert.Equal("AAPL", json.RootElement.GetProperty("symbol").GetString());
            Assert.Equal(150.50m, json.RootElement.GetProperty("price").GetDecimal());
            Assert.Equal("USD", json.RootElement.GetProperty("currency").GetString());
            Assert.Equal("market-api", json.RootElement.GetProperty("source").GetString());
        }

        [Fact]
        public async Task ExecuteAsync_InvalidSymbol_ReturnsError()
        {
            // Arrange
            var mockMarketDataService = new Mock<IMarketDataService>();
            mockMarketDataService.Setup(s => s.GetMarketDataAsync(It.IsAny<List<string>>()))
                .ReturnsAsync(new List<MarketData>());

            var tool = new GetStockPriceTool(mockMarketDataService.Object);
            var args = System.Text.Json.JsonSerializer.Serialize(new { symbol = "INVALID" });

            // Act
            var result = await tool.ExecuteAsync(args);
            var json = System.Text.Json.JsonDocument.Parse(result);

            // Assert
            Assert.True(json.RootElement.TryGetProperty("error", out _));
        }
    }
}
```

**Similar tests for**:
- `GetProfileToolTests.cs`
- `GetOwnedSharesToolTests.cs`
- `SearchRagToolTests.cs`
- `BuyStockToolTests.cs`
- `SellStockToolTests.cs`

**File**: `FinancialAdvisor.Backend/tests/FinancialAdvisor.UnitTests/Flows/CommonFlowTests.cs` (NEW)

```csharp
using Xunit;
using FinancialAdvisor.Infrastructure.Services.RAG;
// ... test price query, buy/sell, investment advice flows
```

---

### Phase 4: Enhancements (Low Priority)

#### 3.5 Improve PromptService Robustness

- Add JSON schema validation
- Add prompt versioning
- Add prompt optimization for different model sizes
- Improve error handling in `PostProcessModelOutput`

#### 3.6 Performance Optimizations

- Add caching for frequently accessed sessions
- Optimize RAG vector search (use proper vector DB)
- Add request batching for tool calls

---

## 4. Implementation Checklist

### High Priority (Must Have)
- [ ] **Metadata Store** - Track last_symbol, last_tool, last_action_timestamp
- [ ] **Rolling Deque** - Implement proper conversation buffer with maxlen=6
- [ ] **Audit Logging** - Comprehensive tool call logging with input/output/reason
- [ ] **Update AgentOrchestrator** - Integrate metadata and audit logging

### Medium Priority (Should Have)
- [ ] **Tool Tests** - Unit tests for all 6 tools
- [ ] **Flow Tests** - Tests for common user flows
- [ ] **RAG Tests** - Test retrieval accuracy
- [ ] **Buffer Tests** - Test conversation buffer management

### Low Priority (Nice to Have)
- [ ] **Prompt Optimization** - Versioning, schema validation
- [ ] **Performance** - Caching, vector DB optimization
- [ ] **Monitoring** - Metrics, dashboards

---

## 5. How to Use Current Implementation

### 5.1 Using PromptService

```csharp
// Inject IPromptService
var promptService = serviceProvider.GetService<IPromptService>();

// Get system prompt
var systemPrompt = promptService.ConstructSystemPrompt();

// Build augmented prompt with context
var fullPrompt = promptService.ConstructAugmentedUserPrompt(
    userQuery: "What is AAPL price?",
    portfolioContext: "Portfolio: Empty",
    marketContext: "[]",
    ragContext: "No relevant news",
    session: sessionObject,
    history: chatHistory
);

// Post-process LLM output
var cleanedJson = promptService.PostProcessModelOutput(llmRawOutput);
```

### 5.2 Using Tools

```csharp
// Tools are registered in DI container
var tools = serviceProvider.GetServices<ITool>();

// Find specific tool
var priceTool = tools.FirstOrDefault(t => t.Name == "get_stock_price");

// Execute tool
var args = JsonSerializer.Serialize(new { symbol = "AAPL" });
var result = await priceTool.ExecuteAsync(args);
// Returns: {"symbol":"AAPL","price":150.50,"currency":"USD",...}
```

### 5.3 Using Orchestrator

```csharp
// Inject IRagService (AgentOrchestrator)
var orchestrator = serviceProvider.GetService<IRagService>();

// Process query (streaming)
await foreach (var chunk in orchestrator.ProcessQueryStreamAsync(
    userQuery: "What is AAPL price?",
    sessionId: "user-123",
    cancellationToken: cancellationToken,
    enableReasoning: true,
    documentCount: 6
))
{
    // Handle chunks: <status>...</status>, <thinking>...</thinking>, <response>...</response>
    Console.WriteLine(chunk);
}
```

---

## 6. Best Practices Recommendations

### 6.1 Architecture
- ✅ **Separation of Concerns**: Well-structured (Services, Tools, Models)
- ✅ **Dependency Injection**: Properly used throughout
- ⚠️ **Error Handling**: Add try-catch with proper logging
- ⚠️ **Validation**: Add input validation for tool arguments

### 6.2 Data Management
- ✅ **MongoDB Persistence**: Properly implemented
- ⚠️ **Caching**: Add in-memory cache for frequently accessed data
- ⚠️ **Connection Pooling**: Ensure MongoDB connection pooling is configured

### 6.3 Testing
- ⚠️ **Test Coverage**: Currently low, needs improvement
- ⚠️ **Integration Tests**: Add end-to-end flow tests
- ⚠️ **Mocking**: Use mocks for external services (market data, embeddings)

### 6.4 Monitoring
- ⚠️ **Structured Logging**: Use structured logging (Serilog)
- ⚠️ **Metrics**: Add performance metrics (response time, tool call duration)
- ⚠️ **Health Checks**: Add health check endpoints

---

## 7. Summary

### What's Working Well ✅
1. **Tools**: All 6 tools implemented with normalized JSON
2. **RAG**: Hourly ingestion and vector search working
3. **Orchestrator**: Full planning and execution flow
4. **System Prompts**: Well-defined and comprehensive
5. **Profile Persistence**: User strategy/profile stored in DB

### What Needs Work ⚠️
1. **Metadata Store**: Not implemented
2. **Rolling Deque**: Using DB query, not true deque
3. **Audit Logging**: Basic logging, needs structured audit trail
4. **Test Coverage**: Incomplete, missing tool and flow tests

### Next Steps 🎯
1. Implement metadata store (Phase 1)
2. Implement rolling deque (Phase 1)
3. Implement comprehensive audit logging (Phase 2)
4. Add complete test suite (Phase 3)

---

## 8. Code Quality Assessment

| Aspect | Rating | Notes |
|--------|--------|-------|
| Architecture | ⭐⭐⭐⭐ | Well-structured, clean separation |
| Code Organization | ⭐⭐⭐⭐ | Clear naming, proper folders |
| Error Handling | ⭐⭐⭐ | Basic, needs improvement |
| Testing | ⭐⭐ | Partial, needs expansion |
| Documentation | ⭐⭐⭐ | Good, could use more inline docs |
| Performance | ⭐⭐⭐ | Good, some optimizations possible |
| Security | ⭐⭐⭐ | Basic, needs input validation |

**Overall**: ⭐⭐⭐ (Good foundation, needs completion)

