# System Architecture & Data Flow - Complete Explanation

## 🎯 How the System Works

### Architecture Type: **Agent-Based RAG with Tool Calling**

The system uses an **intelligent agent pattern** where:
- The LLM acts as a **planner** that decides what tools to call
- Tools fetch data on-demand (not pre-fetched)
- This is more flexible and reduces unnecessary API calls

---

## 📊 Complete Data Flow Diagram

```
┌─────────────────────────────────────────────────────────────────┐
│                    USER QUERY FLOW                              │
└─────────────────────────────────────────────────────────────────┘

User: "What is Bitcoin price?"
  │
  ▼
┌─────────────────────────────────────────────────────────────────┐
│ 1. ChatController.StreamQuery()                                 │
│    POST /api/chat/stream                                         │
│    { message, sessionId, enableReasoning, documentCount }       │
└─────────────────────────────────────────────────────────────────┘
  │
  ▼
┌─────────────────────────────────────────────────────────────────┐
│ 2. AgentOrchestrator.ProcessQueryStreamAsync()                   │
│    Main orchestrator - coordinates everything                   │
└─────────────────────────────────────────────────────────────────┘
  │
  ├─► ┌─────────────────────────────────────────────────────────┐
  │   │ 2.1 Gather Context (Parallel)                             │
  │   │                                                           │
  │   ├─► ContextService.GetChatHistoryAsync(sessionId, 6)      │
  │   │   └─► MongoDB: chat_history                              │
  │   │   └─► Returns: Last 6 messages                            │
  │   │                                                           │
  │   ├─► ContextService.GetSessionAsync(sessionId)              │
  │   │   └─► MongoDB: sessions                                  │
  │   │   └─► Returns: User profile, risk profile, goal          │
  │   │                                                           │
  │   └─► ContextService.GetPortfolioAsync(sessionId)            │
  │       └─► MongoDB: portfolio_snapshots                       │
  │       └─► Returns: Holdings, cash balance                    │
  └───────────────────────────────────────────────────────────────┘
  │
  ├─► ┌─────────────────────────────────────────────────────────┐
  │   │ 2.2 Proactive RAG Search                                 │
  │   │                                                           │
  │   └─► SearchRagTool.ExecuteAsync(query, top_k=3)            │
  │       ├─► EmbeddingService.EmbedAsync(query)                 │
  │       │   └─► Generates vector embedding                     │
  │       │                                                       │
  │       ├─► Vector search in MongoDB: financial_documents      │
  │       │   └─► Cosine similarity search                       │
  │       │                                                       │
  │       └─► Returns: Top 3 relevant articles (JSON)           │
  │           [                                                    │
  │             { "id": "...", "title": "...", "snippet": "..." }│
  │           ]                                                   │
  └───────────────────────────────────────────────────────────────┘
  │
  ├─► ┌─────────────────────────────────────────────────────────┐
  │   │ 2.3 Build Initial Prompt                                 │
  │   │                                                           │
  │   └─► PromptService.ConstructAugmentedUserPrompt()           │
  │       │                                                       │
  │       ├─► System Prompt                                      │
  │       │   └─► Tool contracts, rules, output formats         │
  │       │                                                       │
  │       ├─► User Profile                                       │
  │       │   └─► Risk: moderate, Goal: long_term_growth        │
  │       │                                                       │
  │       ├─► Portfolio Context                                  │
  │       │   └─► Holdings: AAPL 100 shares, Cash: $10,000      │
  │       │                                                       │
  │       ├─► Market Context ⚠️ **EMPTY: "[]"**                 │
  │       │   └─► AgentOrchestrator hardcodes: "[]"              │
  │       │   └─► **NOT using market_cache here**                │
  │       │   └─► LLM must call tools to get prices              │
  │       │                                                       │
  │       ├─► RAG Context                                       │
  │       │   └─► Relevant news/articles (JSON)                 │
  │       │                                                       │
  │       └─► Chat History                                       │
  │           └─► Last 6 messages                                │
  └───────────────────────────────────────────────────────────────┘
  │
  ├─► ┌─────────────────────────────────────────────────────────┐
  │   │ 2.4 LLM Planning Phase (Streaming)                       │
  │   │                                                           │
  │   └─► LLMService.GenerateFinancialAdviceStreamAsync()       │
  │       │                                                       │
  │       ├─► Calls Ollama API                                   │
  │       │   └─► Model: deepseek-r1:14b                        │
  │       │   └─► Stream: true, Think: enableReasoning          │
  │       │                                                       │
  │       ├─► LLM analyzes:                                      │
  │       │   - User wants Bitcoin price                        │
  │       │   - No price data in context                         │
  │       │   - Need to call get_stock_price tool               │
  │       │                                                       │
  │       ├─► LLM generates Plan JSON:                          │
  │       │   {                                                   │
  │       │     "type": "plan",                                  │
  │       │     "steps": [                                       │
  │       │       {                                               │
  │       │         "tool": "get_stock_price",                    │
  │       │         "args": { "symbol": "BTC-USD" },             │
  │       │         "why": "User asked for Bitcoin price"        │
  │       │       }                                               │
  │       │     ],                                                │
  │       │     "final_prompt": "Provide current Bitcoin..."     │
  │       │   }                                                   │
  │       │                                                       │
  │       └─► Streams: <thinking>...</thinking>, <response>...  │
  └───────────────────────────────────────────────────────────────┘
  │
  ├─► ┌─────────────────────────────────────────────────────────┐
  │   │ 2.5 Execute Plan                                         │
  │   │                                                           │
  │   └─► For each step in plan:                                 │
  │       │                                                       │
  │       └─► GetStockPriceTool.ExecuteAsync("BTC-USD")         │
  │           │                                                   │
  │           └─► MarketDataService.GetMarketDataAsync(["BTC-USD"])
  │               │                                               │
  │               ├─► **CHECKS market_cache FIRST**              │
  │               │   └─► MongoDB: market_cache collection       │
  │               │   └─► If found AND < 5 min old → use cache   │
  │               │                                               │
  │               ├─► If cache miss → Fetch from API             │
  │               │   └─► Yahoo Finance API                      │
  │               │   └─► URL: .../chart/BTC-USD?interval=1d    │
  │               │                                               │
  │               ├─► Parse API response                        │
  │               │   └─► Extract price, volume, change%        │
  │               │   └─► Handle crypto-specific fields         │
  │               │                                               │
  │               ├─► **UPDATE market_cache**                    │
  │               │   └─► MongoDB upsert                         │
  │               │   └─► Store: symbol, price, volume, etc.    │
  │               │                                               │
  │               └─► Returns:                                   │
  │                   {                                            │
  │                     "symbol": "BTC-USD",                     │
  │                     "price": 45000.00,                        │
  │                     "currency": "USD",                        │
  │                     "timestamp": "2025-01-15T10:30:00Z",      │
  │                     "source": "market-api"                   │
  │                   }                                            │
  └───────────────────────────────────────────────────────────────┘
  │
  ├─► ┌─────────────────────────────────────────────────────────┐
  │   │ 2.6 Final Answer Generation (Streaming)                 │
  │   │                                                           │
  │   └─► LLMService.GenerateFinancialAdviceStreamAsync()       │
  │       │                                                       │
  │       ├─► Builds final prompt:                                │
  │       │   - System reminder (plain text output)              │
  │       │   - Tool results (price data)                         │
  │       │   - Final prompt instruction                         │
  │       │                                                       │
  │       ├─► LLM generates response:                           │
  │       │   "The current price of Bitcoin (BTC-USD) is        │
  │       │    $45,000.00..."                                    │
  │       │                                                       │
  │       └─► Streams: <response><![CDATA[markdown text]]>     │
  └───────────────────────────────────────────────────────────────┘
  │
  ▼
┌─────────────────────────────────────────────────────────────────┐
│ Response sent to frontend:                                      │
│ - <status>...</status>                                          │
│ - <thinking>...</thinking> (if enabled)                        │
│ - <response><![CDATA[markdown]]></response>                    │
└─────────────────────────────────────────────────────────────────┘
```

---

## 🔍 Key Components & Their Roles

### 1. **AgentOrchestrator** (Main Orchestrator)

**Location**: `AgentOrchestrator.cs`

**Responsibilities**:
- Coordinates the entire flow
- Gathers context (history, session, portfolio)
- Calls RAG search
- Builds initial prompt
- Executes LLM plan
- Manages tool execution
- Generates final answer

**Key Decision**: 
- ❌ **Does NOT pre-fetch market data**
- ✅ **Relies on LLM to call tools when needed**

**Code**:
```csharp
string marketContext = "[]";  // Hardcoded empty - LLM must use tools
```

---

### 2. **MarketDataService** (Market Data Fetcher)

**Location**: `MarketDataService.cs`

**Responsibilities**:
- Fetches market data from Yahoo Finance API
- **Manages market_cache collection**
- Checks cache before API calls
- Updates cache after fetching
- Formats market data for prompts

**How it uses market_cache**:
1. **Read**: Checks cache first (if < 5 min old)
2. **Write**: Updates cache after API fetch
3. **Format**: Formats cached data for display

**Code Flow**:
```csharp
GetMarketDataAsync(["BTC-USD"])
  → GetCachedDataAsync("BTC-USD")  // Check cache
  → If miss: Fetch from API
  → UpdateCacheAsync(data)          // Update cache
  → Return data
```

---

### 3. **GetStockPriceTool** (Price Tool)

**Location**: `MarketTools.cs`

**Responsibilities**:
- Called by LLM during plan execution
- Gets current stock/crypto price
- Returns normalized JSON

**How it uses market_cache**:
- Calls `MarketDataService.GetMarketDataAsync()`
- Service checks cache internally
- Tool doesn't directly access cache

---

### 4. **MarketDataSyncService** (Background Sync)

**Location**: `MarketDataSyncService.cs`

**Responsibilities**:
- Runs every 15 minutes
- Syncs ALL active assets to market_cache
- Ensures cache is populated even if tools aren't called

**How it works**:
1. Gets all active assets from `assets` collection
2. Extracts symbols (AAPL, MSFT, BTC-USD, ETH-USD, etc.)
3. Calls `MarketDataService.GetMarketDataAsync(allSymbols)`
4. Service fetches and caches all symbols
5. Logs success/failure for each symbol

**This should populate BTC-USD and ETH-USD!**

---

### 5. **PromptService** (Prompt Builder)

**Location**: `PromptService.cs`

**Responsibilities**:
- Constructs system prompts
- Assembles augmented user prompts
- Post-processes LLM output

**What it receives**:
- `marketContext`: Currently `"[]"` from AgentOrchestrator
- Would be formatted prices if using RagOrchestrator approach

---

## 🎯 What Information is Fed to the LLM?

### During Planning Phase:

```
┌─────────────────────────────────────────────────────────────┐
│ SYSTEM PROMPT                                                │
├─────────────────────────────────────────────────────────────┤
│ - You are "FinAssist"                                        │
│ - Tool contracts (get_stock_price, search_rag, etc.)        │
│ - Operational rules (never fabricate, cite sources, etc.)    │
│ - Output format requirements (Plan JSON or FinalAnswer)     │
└─────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────┐
│ USER PROFILE                                                 │
├─────────────────────────────────────────────────────────────┤
│ Risk Profile: moderate                                       │
│ Goal: long_term_growth                                       │
└─────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────┐
│ PORTFOLIO                                                    │
├─────────────────────────────────────────────────────────────┤
│ Current Portfolio:                                           │
│ - AAPL: 100 shares @ $150.00                                 │
│ Total Value: $15,000                                         │
│ Cash Balance: $10,000                                        │
└─────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────┐
│ MARKET PRICES (Real-Time) ⚠️                                 │
├─────────────────────────────────────────────────────────────┤
│ []  ← EMPTY! LLM must call tools to get prices             │
└─────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────┐
│ RELEVANT NEWS (RAG)                                          │
├─────────────────────────────────────────────────────────────┤
│ [                                                             │
│   {                                                           │
│     "id": "news-123",                                        │
│     "title": "Bitcoin Reaches New High",                     │
│     "snippet": "Bitcoin price surged to...",                  │
│     "timestamp": "2025-01-15T09:00:00Z",                     │
│     "source": "TechCrunch",                                  │
│     "score": 0.93                                            │
│   }                                                           │
│ ]                                                             │
└─────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────┐
│ CHAT HISTORY (Last 6)                                        │
├─────────────────────────────────────────────────────────────┤
│ USER: What stocks should I buy?                              │
│ ASSISTANT: Based on your profile...                          │
└─────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────┐
│ USER QUERY                                                   │
├─────────────────────────────────────────────────────────────┤
│ "What is Bitcoin price?"                                      │
└─────────────────────────────────────────────────────────────┘
```

### During Final Answer Phase:

```
┌─────────────────────────────────────────────────────────────┐
│ SYSTEM REMINDER                                              │
├─────────────────────────────────────────────────────────────┤
│ - Write in plain text (no JSON)                              │
│ - Include citations naturally                                │
│ - Include disclaimer                                         │
└─────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────┐
│ TOOL RESULTS                                                 │
├─────────────────────────────────────────────────────────────┤
│ Tool 'get_stock_price' output:                              │
│ {                                                             │
│   "symbol": "BTC-USD",                                       │
│   "price": 45000.00,                                         │
│   "currency": "USD",                                         │
│   "timestamp": "2025-01-15T10:30:00Z",                      │
│   "source": "market-api"                                     │
│ }                                                             │
└─────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────┐
│ FINAL PROMPT INSTRUCTION                                     │
├─────────────────────────────────────────────────────────────┤
│ "Provide the current Bitcoin price and explain..."          │
└─────────────────────────────────────────────────────────────┘
```

---

## 🔄 market_cache Usage Flow

### When Tool is Called:

```
User asks: "What is BTC-USD price?"
  ↓
LLM generates plan: { tool: "get_stock_price", args: { symbol: "BTC-USD" } }
  ↓
GetStockPriceTool.ExecuteAsync("BTC-USD")
  ↓
MarketDataService.GetMarketDataAsync(["BTC-USD"])
  ↓
├─► Check market_cache (MongoDB)
│   └─► Query: { symbol: "BTC-USD" }
│   └─► If found AND < 5 min old → Return cached data ✅
│
└─► If cache miss OR > 5 min old:
    ├─► Fetch from Yahoo Finance API
    ├─► Parse response
    ├─► Validate data (price > 0, reasonable values)
    ├─► Update market_cache (MongoDB upsert) ✅
    └─► Return fresh data
```

### When Background Sync Runs:

```
MarketDataSyncService (every 15 minutes)
  ↓
Get all active assets from assets collection
  ↓
Extract symbols: [AAPL, MSFT, GOOGL, AMZN, TSLA, NVDA, META, NFLX, BTC-USD, ETH-USD]
  ↓
MarketDataService.GetMarketDataAsync(allSymbols)
  ↓
For each symbol:
  ├─► Check cache
  ├─► If miss: Fetch from API
  └─► Update cache ✅
```

---

## 🐛 Why BTC-USD and ETH-USD Aren't in market_cache

### Investigation Steps:

1. **Check if MarketDataSyncService is running**
   ```bash
   # Look for these logs:
   "Market Data Sync Service is starting."
   "Starting market data sync for all active assets..."
   "Syncing market data for symbols: ..., BTC-USD, ETH-USD"
   ```

2. **Check if assets exist in assets collection**
   ```javascript
   db.assets.find({ symbol: { $in: ["BTC-USD", "ETH-USD"] } })
   ```

3. **Check API responses**
   ```bash
   # Look for these logs:
   "Crypto API response for BTC-USD (first 500 chars): ..."
   "Successfully fetched and cached market data for BTC-USD"
   "API request failed for symbol BTC-USD"
   ```

4. **Check validation failures**
   ```bash
   # Look for:
   "Invalid price for symbol: BTC-USD"
   "Price is zero for symbol: BTC-USD"
   ```

5. **Check cache update failures**
   ```bash
   # Look for:
   "Failed to update cache for symbol: BTC-USD"
   ```

---

## ✅ Solution: Enhanced Debugging

I've added comprehensive logging to help identify the issue:

1. **MarketDataService** now logs:
   - Every API request
   - Crypto API responses (first 500 chars)
   - Price extraction attempts
   - Validation failures
   - Cache update operations

2. **MarketDataSyncService** now logs:
   - Which symbols are being synced
   - Success/failure counts
   - Failed symbols list

3. **Test endpoint** added:
   - `POST /api/market/sync` - Manually trigger sync

---

## 🎯 Next Steps

1. **Deploy the updated code**
2. **Check application logs** for:
   - MarketDataSyncService startup
   - Crypto symbol fetch attempts
   - API responses
   - Cache update operations
3. **Manually trigger sync**: `POST /api/market/sync`
4. **Verify MongoDB**: Check if BTC-USD and ETH-USD appear in market_cache
5. **Test in chat**: Ask "What is Bitcoin price?" and verify it works

The logs will tell us exactly why crypto assets aren't being cached!

