# System Architecture & Data Flow Explanation

## 🏗️ Overall Architecture

### Current System: Agent-Based RAG with Tool Calling

The system uses an **agent-based architecture** where the LLM acts as a planner that decides which tools to call, rather than having all data pre-fetched.

---

## 📊 Complete Data Flow

### Step-by-Step Flow (AgentOrchestrator)

```
1. User sends message: "What is Bitcoin price?"
   ↓
2. ChatController.StreamQuery() receives request
   ↓
3. AgentOrchestrator.ProcessQueryStreamAsync() starts
   ↓
4. Gather Context (Parallel):
   ├─► ContextService.GetChatHistoryAsync(sessionId, 6)
   │   └─► MongoDB: chat_history collection
   │   └─► Returns: Last 6 messages (rolling buffer)
   │
   ├─► ContextService.GetSessionAsync(sessionId)
   │   └─► MongoDB: sessions collection
   │   └─► Returns: User profile, risk profile, investment goal
   │
   └─► ContextService.GetPortfolioAsync(sessionId)
       └─► MongoDB: portfolio_snapshots collection
       └─► Returns: Current holdings, cash balance
   ↓
5. Proactive RAG Search:
   └─► SearchRagTool.ExecuteAsync(query, top_k=3)
       ├─► EmbeddingService.EmbedAsync(query)
       ├─► Vector search in MongoDB: financial_documents
       └─► Returns: Top 3 relevant news/articles (JSON)
   ↓
6. Build Initial Prompt:
   ├─► System Prompt (from PromptService)
   │   └─► Tool contracts, operational rules
   │
   ├─► User Profile Context
   │   └─► Risk profile, investment goal
   │
   ├─► Portfolio Context
   │   └─► Holdings, cash balance
   │
   ├─► Market Context ⚠️ **CURRENTLY EMPTY: "[]"**
   │   └─► AgentOrchestrator hardcodes: marketContext = "[]"
   │   └─► **NOT using market_cache at this stage**
   │
   ├─► RAG Context
   │   └─► Relevant news/articles from vector search
   │
   └─► Chat History
       └─► Last 6 messages
   ↓
7. LLM Planning Phase (Streaming):
   └─► LLMService.GenerateFinancialAdviceStreamAsync()
       ├─► Calls Ollama API with full prompt
       ├─► LLM analyzes query and context
       ├─► LLM generates JSON Plan:
       │   {
       │     "type": "plan",
       │     "steps": [
       │       {
       │         "tool": "get_stock_price",
       │         "args": { "symbol": "BTC-USD" },
       │         "why": "User asked for Bitcoin price"
       │       }
       │     ],
       │     "final_prompt": "Summarize the price..."
       │   }
       └─► Streams thinking tokens (if enableReasoning=true)
   ↓
8. Execute Plan:
   └─► For each step in plan:
       ├─► Find tool by name (e.g., "get_stock_price")
       ├─► GetStockPriceTool.ExecuteAsync(args)
       │   └─► MarketDataService.GetMarketDataAsync(["BTC-USD"])
       │       ├─► **CHECKS market_cache FIRST** (if < 5 min old)
       │       ├─► If not cached, fetches from Yahoo Finance API
       │       ├─► Updates market_cache (upsert)
       │       └─► Returns: { symbol, price, currency, timestamp, source }
       └─► Collect tool outputs
   ↓
9. Final Answer Generation (Streaming):
   └─► LLMService.GenerateFinancialAdviceStreamAsync()
       ├─► Builds final prompt with:
       │   ├─► Tool results (prices, data)
       │   ├─► System reminder (plain text output)
       │   └─► Final prompt instruction from plan
       ├─► LLM generates conversational response
       └─► Streams response tokens (markdown formatted)
   ↓
10. Response sent to frontend:
    └─► Chunks: <status>...</status>, <thinking>...</thinking>, <response>...</response>
```

---

## 🔍 Key Findings

### ❌ **Issue Found: AgentOrchestrator Doesn't Use market_cache Directly**

**Location**: `AgentOrchestrator.cs:93`

```csharp
string marketContext = "[]";  // ⚠️ HARDCODED - NOT USING market_cache!
```

**Why?**
- The agent-based architecture relies on **tool calling** instead of pre-fetching
- The LLM is expected to call `get_stock_price` tool when it needs price data
- This is different from `RagOrchestrator` (legacy) which pre-fetches market data

### ✅ **market_cache IS Used, But Indirectly**

**When market_cache is used:**
1. **By GetStockPriceTool** → Calls `MarketDataService.GetMarketDataAsync()`
   - Checks cache first (if < 5 minutes old)
   - Falls back to API if cache miss
   - Updates cache after fetching

2. **By MarketDataSyncService** (Background Service)
   - Runs every 15 minutes
   - Syncs ALL active assets from `assets` collection
   - Should populate BTC-USD and ETH-USD

3. **By RagOrchestrator** (Legacy - not currently used)
   - Pre-fetches market data before LLM call
   - Uses `FormatMarketContext()` to format prices

---

## 🐛 Why BTC-USD and ETH-USD Aren't in market_cache

### Possible Causes:

1. **MarketDataSyncService not running**
   - Check if service started: Look for log: `"Market Data Sync Service is starting."`
   - Service waits 30 seconds after startup before first sync

2. **API fetch failing for crypto**
   - Yahoo Finance API might return different format for crypto
   - Check logs for: `"API request failed for symbol BTC-USD"`
   - Check logs for: `"Price is zero for symbol: BTC-USD"`

3. **Validation rejecting data**
   - Price validation might be too strict
   - Check logs for: `"Invalid price for symbol: BTC-USD"`

4. **Cache update failing**
   - MongoDB write might be failing silently
   - Check logs for: `"Failed to update cache for symbol: BTC-USD"`

---

## 🔧 How to Fix & Verify

### Step 1: Check if MarketDataSyncService is Running

Look for these logs:
```
[Information] Market Data Sync Service is starting.
[Information] Starting market data sync for all active assets...
[Information] Found 10 active assets to sync.
[Information] Syncing market data for symbols: AAPL, MSFT, GOOGL, AMZN, TSLA, NVDA, META, NFLX, BTC-USD, ETH-USD
```

### Step 2: Manually Trigger Sync

```bash
curl -X POST http://localhost:5000/api/market/sync
```

This will:
- Fetch all 10 assets including crypto
- Show detailed results
- Update cache immediately

### Step 3: Test Crypto Symbols Directly

```bash
# Test BTC-USD
curl http://localhost:5000/api/market/BTC-USD

# Test ETH-USD  
curl http://localhost:5000/api/market/ETH-USD
```

### Step 4: Check Logs for Errors

Look for:
- `[Warning] API request failed for symbol BTC-USD`
- `[Warning] Price is zero for symbol: BTC-USD`
- `[Error] Exception while fetching market data for symbol: BTC-USD`
- `[Error] Failed to update cache for symbol: BTC-USD`

### Step 5: Verify MongoDB

```javascript
// Check if crypto symbols are in assets collection
db.assets.find({ symbol: { $in: ["BTC-USD", "ETH-USD"] } })

// Check if they're in market_cache
db.market_cache.find({ symbol: { $in: ["BTC-USD", "ETH-USD"] } })

// Check all market_cache entries
db.market_cache.find().sort({ last_updated: -1 })
```

---

## 📝 What Information is Fed to the LLM

### During Planning Phase:

```
System Prompt:
- Tool contracts (get_stock_price, search_rag, etc.)
- Operational rules
- Output format requirements

User Profile:
- Risk Profile: moderate
- Goal: long_term_growth

Portfolio:
- Current holdings
- Cash balance
- Portfolio value

Market Prices: ⚠️ **EMPTY: "[]"**
- AgentOrchestrator doesn't pre-fetch
- LLM must call tools to get prices

RAG Context:
- Top 3 relevant news articles
- Formatted as JSON array

Chat History:
- Last 6 messages (user + assistant)
```

### During Final Answer Phase:

```
System Reminder:
- Write in plain text
- No JSON format
- Include citations

Tool Results:
- get_stock_price output: {"symbol":"BTC-USD","price":45000,...}
- search_rag output: [{"title":"...","snippet":"..."}]
- Any other tool outputs

Final Prompt Instruction:
- From the LLM's plan
- Tells LLM what to focus on
```

---

## 🎯 How the Agent Decides What to Do

1. **LLM receives prompt** with:
   - User query: "What is Bitcoin price?"
   - Context: Profile, portfolio, RAG, history
   - **NO market prices** (empty array)

2. **LLM analyzes** and decides:
   - "User wants Bitcoin price"
   - "I need to call get_stock_price tool"
   - "Symbol should be BTC-USD"

3. **LLM generates Plan JSON**:
   ```json
   {
     "type": "plan",
     "steps": [
       {
         "tool": "get_stock_price",
         "args": { "symbol": "BTC-USD" },
         "why": "User asked for Bitcoin price"
       }
     ],
     "final_prompt": "Provide the current Bitcoin price..."
   }
   ```

4. **Orchestrator executes plan**:
   - Calls `get_stock_price("BTC-USD")`
   - Tool fetches from API (or cache)
   - Returns price data

5. **LLM generates final answer**:
   - Uses tool result: "BTC-USD is $45,000"
   - Formats as conversational text
   - Includes disclaimers

---

## 🔄 market_cache Usage Summary

| Component | Uses market_cache? | How? |
|-----------|-------------------|------|
| **AgentOrchestrator** | ❌ No (directly) | Hardcodes `marketContext = "[]"` |
| **GetStockPriceTool** | ✅ Yes | Checks cache before API call |
| **MarketDataService** | ✅ Yes | Reads/writes cache |
| **MarketDataSyncService** | ✅ Yes | Populates cache for all assets |
| **RagOrchestrator** | ✅ Yes | Pre-fetches market data (legacy) |

---

## 🚨 Current Problem

**BTC-USD and ETH-USD not in market_cache** means:

1. **When tool is called**: `get_stock_price("BTC-USD")`
   - Cache miss → Fetches from API
   - If API fails → Returns error
   - If API succeeds → Should update cache

2. **If API keeps failing**:
   - Tool returns error
   - LLM might hallucinate price
   - Cache never gets populated

3. **MarketDataSyncService should fix this**:
   - Runs every 15 minutes
   - Fetches ALL assets including crypto
   - Should populate cache even if tools fail

---

## ✅ Next Steps to Debug

1. **Check if MarketDataSyncService is running**
   - Look for startup logs
   - Check if it's registered in `Program.cs`

2. **Check logs for crypto fetch attempts**
   - Look for BTC-USD and ETH-USD in logs
   - Check for API errors

3. **Manually test crypto symbols**
   - Use `/api/market/sync` endpoint
   - Check response for BTC-USD and ETH-USD

4. **Verify API response format**
   - Check logs for "Crypto API response" (first 500 chars)
   - Verify price extraction is working

5. **Check MongoDB directly**
   - Verify assets collection has crypto
   - Check market_cache for crypto entries

---

## 💡 Architecture Decision

**Why AgentOrchestrator doesn't pre-fetch market data:**

✅ **Pros:**
- More flexible - LLM decides what data it needs
- Reduces unnecessary API calls
- Tool-based approach is more scalable

❌ **Cons:**
- If tool fails, LLM has no price data
- Relies on tools working correctly
- Cache must be populated separately

**Alternative Approach (RagOrchestrator style):**
- Pre-fetch market data for portfolio + query symbols
- Include in initial prompt
- LLM has prices upfront

**Current approach is better for:**
- Dynamic queries
- Reducing API calls
- Tool-based architecture

