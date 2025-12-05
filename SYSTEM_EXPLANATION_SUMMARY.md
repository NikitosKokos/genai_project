# System Architecture & Data Flow - Quick Summary

## 🎯 How It Works

### Architecture: **Agent-Based RAG with Tool Calling**

The system uses an **intelligent agent pattern**:
- LLM acts as a **planner** that decides what tools to call
- Tools fetch data **on-demand** (not pre-fetched)
- More flexible and reduces unnecessary API calls

---

## 📊 Complete Flow

```
User: "What is Bitcoin price?"
  ↓
1. AgentOrchestrator gathers context:
   - Chat history (last 6 messages)
   - User session (risk profile, goal)
   - Portfolio (holdings, cash)
   - RAG search (relevant news)
   - Market context: "[]" ⚠️ EMPTY - LLM must use tools
  ↓
2. LLM Planning Phase:
   - Analyzes query: "User wants Bitcoin price"
   - Generates Plan JSON:
     {
       "tool": "get_stock_price",
       "args": { "symbol": "BTC-USD" }
     }
  ↓
3. Execute Plan:
   - GetStockPriceTool.ExecuteAsync("BTC-USD")
   - MarketDataService.GetMarketDataAsync(["BTC-USD"])
     ├─► Checks market_cache (MongoDB) first
     ├─► If cache miss: Fetches from Yahoo Finance API
     └─► Updates market_cache
  ↓
4. Final Answer:
   - LLM generates response with price data
   - Streams markdown to frontend
```

---

## 🔍 Key Finding: market_cache Usage

### ❌ **AgentOrchestrator Does NOT Pre-fetch Market Data**

**Location**: `AgentOrchestrator.cs:93`

```csharp
string marketContext = "[]";  // Hardcoded empty!
```

**Why?**
- Agent-based architecture relies on **tool calling**
- LLM decides what data it needs
- Tools fetch and cache data on-demand

### ✅ **market_cache IS Used Indirectly**

**When market_cache is used:**

1. **By GetStockPriceTool** (when LLM calls it)
   - Checks cache first (if < 5 min old)
   - Falls back to API if cache miss
   - Updates cache after fetching

2. **By MarketDataSyncService** (Background Service)
   - Runs every 15 minutes
   - Syncs ALL active assets from `assets` collection
   - Should populate BTC-USD and ETH-USD ✅

3. **By RagOrchestrator** (Legacy - not used)
   - Pre-fetches market data before LLM call

---

## 🐛 Why BTC-USD and ETH-USD Aren't in market_cache

### Possible Causes:

1. **MarketDataSyncService not running yet**
   - Waits 30 seconds after startup
   - Then runs every 15 minutes
   - Check logs: `"Market Data Sync Service is starting."`

2. **API fetch failing for crypto**
   - Yahoo Finance API might return different format
   - Check logs for: `"API request failed for symbol BTC-USD"`

3. **Price validation rejecting data**
   - Check logs for: `"Invalid price for symbol: BTC-USD"`

4. **Cache update failing**
   - Check logs for: `"Failed to update cache for symbol: BTC-USD"`

---

## ✅ How to Debug & Fix

### Step 1: Check Logs

Look for these log messages:

```
[Information] Market Data Sync Service is starting.
[Information] Starting market data sync for all active assets...
[Information] Found 10 active assets to sync.
[Information] Syncing market data for symbols: AAPL, MSFT, ..., BTC-USD, ETH-USD
[Information] Market data sync completed: X assets updated successfully.
```

### Step 2: Manually Trigger Sync

```bash
curl -X POST http://localhost:5000/api/market/sync
```

This will:
- Get all active assets from `assets` collection
- Fetch market data for all symbols (including crypto)
- Update `market_cache` immediately
- Return detailed results

### Step 3: Test Individual Symbols

```bash
# Test BTC-USD
curl http://localhost:5000/api/market/BTC-USD

# Test ETH-USD
curl http://localhost:5000/api/market/ETH-USD
```

### Step 4: Check MongoDB Directly

```javascript
// Check if crypto symbols are in assets
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

Market Prices: ⚠️ **EMPTY: "[]"**
- AgentOrchestrator doesn't pre-fetch
- LLM must call tools to get prices

RAG Context:
- Top 3 relevant news articles (JSON)

Chat History:
- Last 6 messages
```

### During Final Answer Phase:

```
System Reminder:
- Write in plain text (no JSON)

Tool Results:
- get_stock_price output: {"symbol":"BTC-USD","price":45000,...}

Final Prompt Instruction:
- From the LLM's plan
```

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

## 🎯 Next Steps

1. **Deploy updated code** (MarketController sync endpoint now uses all assets)
2. **Check application logs** for:
   - MarketDataSyncService startup
   - Crypto symbol fetch attempts
   - API responses
   - Cache update operations
3. **Manually trigger sync**: `POST /api/market/sync`
4. **Verify MongoDB**: Check if BTC-USD and ETH-USD appear in market_cache
5. **Test in chat**: Ask "What is Bitcoin price?" and verify it works

The logs will tell us exactly why crypto assets aren't being cached!

---

## 📚 Documentation Files

- `SYSTEM_ARCHITECTURE_EXPLANATION.md` - Detailed explanation
- `ARCHITECTURE_FLOW_DIAGRAM.md` - Visual flow diagram
- `README.md` - Project overview and API endpoints

