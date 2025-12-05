# System Architecture

## Overview

The Financial Advisor system is an **agent-based RAG (Retrieval-Augmented Generation) system** that uses LLM agents with tool calling to provide intelligent financial advice. The system combines real-time market data, financial news retrieval, and user portfolio management into a conversational AI assistant.

## Architecture Pattern

### Agent-Based RAG with Tool Calling

The system uses an **intelligent agent pattern** where:
- The LLM acts as a **planner** that decides what tools to call
- Tools fetch data **on-demand** (not pre-fetched)
- This approach is more flexible and reduces unnecessary API calls
- The LLM generates a JSON plan with tool calls, which are then executed sequentially

## System Layers

```
┌─────────────────────────────────────────────────────────┐
│                    API Layer                             │
│  (Controllers, Middleware, Request/Response Handling)    │
└────────────────────┬────────────────────────────────────┘
                     │
┌────────────────────▼────────────────────────────────────┐
│              Application Layer                           │
│  (Interfaces, DTOs, Service Contracts, Validators)     │
└────────────────────┬────────────────────────────────────┘
                     │
┌────────────────────▼────────────────────────────────────┐
│                 Domain Layer                            │
│  (Entities, Value Objects, Enums)                      │
└────────────────────┬────────────────────────────────────┘
                     │
┌────────────────────▼────────────────────────────────────┐
│            Infrastructure Layer                         │
│  - Data Access (MongoDB)                                │
│  - External Services (Ollama LLM, Embeddings)            │
│  - Tools (get_stock_price, search_rag, etc.)            │
│  - Services (AgentOrchestrator, MarketDataService)      │
└─────────────────────────────────────────────────────────┘
```

## Key Components

### 1. AgentOrchestrator

**Location**: `FinancialAdvisor.Infrastructure/Services/RAG/AgentOrchestrator.cs`

**Responsibilities**:
- Coordinates the entire query processing flow
- Gathers context (chat history, session, portfolio) in parallel
- Performs proactive RAG search
- Builds augmented prompts for the LLM
- Executes LLM-generated plans (tool calls)
- Generates final streaming responses

**Key Design Decision**:
- Does NOT pre-fetch market data
- Relies on LLM to call tools when needed
- Market context is empty (`"[]"`) in initial prompt

### 2. LLMService

**Location**: `FinancialAdvisor.Infrastructure/Services/LLMService.cs`

**Responsibilities**:
- Communicates with Ollama LLM API
- Handles streaming responses
- Supports chain-of-thought reasoning (when enabled)
- Formats prompts and parses responses

**Model**: `deepseek-r1:14b` (configurable via `OLLAMA_MODEL`)

### 3. MarketDataService

**Location**: `FinancialAdvisor.Infrastructure/Services/MarketDataService.cs`

**Responsibilities**:
- Fetches market data from Yahoo Finance API
- Manages `market_cache` MongoDB collection
- Implements cache-first strategy (15-minute TTL)
- Handles both stocks and cryptocurrencies
- Normalizes symbols (e.g., "BTC" → "BTC-USD")

**Cache Strategy**:
- Checks cache first (if < 15 minutes old)
- Falls back to API if cache miss or expired
- Updates cache after successful API fetch
- Validates data before caching (price > 0, reasonable values)

### 4. MarketDataSyncService

**Location**: `FinancialAdvisor.Infrastructure/Services/MarketDataSyncService.cs`

**Responsibilities**:
- Background service that runs every 15 minutes
- Syncs ALL active assets from `assets` collection to `market_cache`
- Ensures cache is populated even if tools aren't called
- Logs sync results for monitoring

**Schedule**:
- Initial delay: 30 seconds (allows other services to initialize)
- Interval: 15 minutes

### 5. PromptService

**Location**: `FinancialAdvisor.Infrastructure/Services/PromptService.cs`

**Responsibilities**:
- Constructs system prompts with tool contracts
- Assembles augmented user prompts with context
- Post-processes LLM output (JSON extraction, cleanup)
- Formats market data, portfolio, and RAG context

**Key Features**:
- Optimized prompts (uses simple symbols: "BTC", "ETH")
- Clear tool descriptions
- Explicit output format requirements

### 6. Tools

**Location**: `FinancialAdvisor.Infrastructure/Tools/`

The system includes 6 tools that the LLM can call:

| Tool | Description | Input | Output |
|------|-------------|-------|--------|
| `get_stock_price` | Get current stock/crypto price | `{ symbol: "AAPL" }` or `{ symbol: "BTC" }` | `{ symbol, price, currency, timestamp, source }` |
| `get_profile` | Get user profile and portfolio | `{ user_id: "user-123" }` | `{ user_id, strategy, cash, holdings }` |
| `get_owned_shares` | Get owned shares for a symbol | `{ user_id: "user-123" }` | `{ user_id, holdings: [{symbol, qty}] }` |
| `search_rag` | Search financial news/documents | `{ query: "...", top_k: 3 }` | `[{ id, title, snippet, timestamp, source, score }]` |
| `buy_stock` | Place buy order | `{ symbol: "AAPL", qty: 10 }` | `{ status, order_id, executed_qty, avg_price }` |
| `sell_stock` | Place sell order | `{ symbol: "AAPL", qty: 10 }` | `{ status, order_id, executed_qty, avg_price }` |

**Symbol Normalization**:
- Stocks: Use ticker (e.g., "AAPL", "MSFT")
- Crypto: Use simple symbol (e.g., "BTC", "ETH")
- Backend automatically converts to API format ("BTC-USD", "ETH-USD")

## Data Flow

### Complete Query Flow

```
User: "What is Bitcoin price?"
  ↓
1. ChatController.StreamQuery()
   POST /api/chat/stream
   { message, sessionId, enableReasoning, documentCount }
  ↓
2. AgentOrchestrator.ProcessQueryStreamAsync()
  ↓
3. Gather Context (Parallel):
   ├─► ContextService.GetChatHistoryAsync(sessionId, 6)
   │   └─► MongoDB: chat_history
   ├─► ContextService.GetSessionAsync(sessionId)
   │   └─► MongoDB: sessions
   └─► ContextService.GetPortfolioAsync(sessionId)
       └─► MongoDB: portfolio_snapshots
  ↓
4. Proactive RAG Search:
   └─► SearchRagTool.ExecuteAsync(query, top_k)
       ├─► EmbeddingService.EmbedAsync(query)
       └─► Vector search in MongoDB: financial_documents
  ↓
5. Build Initial Prompt:
   ├─► System Prompt (tool contracts, rules)
   ├─► User Profile Context
   ├─► Portfolio Context
   ├─► Market Context: "[]" (empty - LLM must use tools)
   ├─► RAG Context (relevant news)
   └─► Chat History (last 6 messages)
  ↓
6. LLM Planning Phase (Streaming):
   └─► LLMService.GenerateFinancialAdviceStreamAsync()
       ├─► LLM analyzes query and context
       ├─► Generates Plan JSON:
       │   {
       │     "type": "plan",
       │     "steps": [
       │       {
       │         "tool": "get_stock_price",
       │         "args": { "symbol": "BTC" },
       │         "why": "User asked for Bitcoin price"
       │       }
       │     ],
       │     "final_prompt": "Provide current Bitcoin price..."
       │   }
       └─► Streams <thinking> tokens (if enabled)
  ↓
7. Execute Plan:
   └─► GetStockPriceTool.ExecuteAsync("BTC")
       └─► MarketDataService.GetMarketDataAsync(["BTC"])
           ├─► Check market_cache (if < 15 min old) ✅
           ├─► If miss: Fetch from Yahoo Finance API
           ├─► Normalize: "BTC" → "BTC-USD"
           ├─► Update market_cache ✅
           └─► Return: { symbol, price, currency, timestamp }
  ↓
8. Final Answer Generation (Streaming):
   └─► LLMService.GenerateFinancialAdviceStreamAsync()
       ├─► Builds final prompt with tool results
       └─► Streams <response> tokens (markdown formatted)
  ↓
9. Response sent to frontend:
   └─► SSE stream: <status>, <thinking>, <response>
```

## Data Storage

### MongoDB Collections

| Collection | Purpose | Key Fields |
|------------|---------|------------|
| `chat_history` | Chat messages per session | `session_id`, `role`, `message`, `timestamp` |
| `sessions` | User sessions and profiles | `session_id`, `risk_profile`, `investment_goal` |
| `portfolio_snapshots` | Portfolio holdings | `session_id`, `holdings[]`, `cash_balance` |
| `financial_documents` | News articles for RAG | `title`, `content`, `embedding`, `published_at` |
| `market_cache` | Cached market data | `symbol`, `price`, `volume`, `change_percent`, `last_updated` |
| `assets` | Available assets to invest | `symbol`, `name`, `sector`, `type`, `is_active` |

### Cache Strategy

**market_cache Collection**:
- **TTL**: 15 minutes
- **Update Strategy**: 
  - On-demand (when tool is called)
  - Background sync (every 15 minutes for all active assets)
- **Validation**: Prices must be > 0 and < $1B, change% capped at ±1000%

## Background Services

### NewsIngestionService

**Schedule**: Every 1 hour

**Responsibilities**:
- Fetches financial news articles
- Generates embeddings
- Stores in `financial_documents` collection
- Used by `SearchRagTool` for RAG retrieval

### MarketDataSyncService

**Schedule**: Every 15 minutes (30-second initial delay)

**Responsibilities**:
- Fetches all active assets from `assets` collection
- Syncs market data to `market_cache`
- Ensures cache is populated proactively
- Logs sync results

## API Endpoints

### Chat Endpoints

- `POST /api/chat/query` - Non-streaming chat query
- `POST /api/chat/stream` - Streaming chat query (primary)

### Market Data Endpoints

- `GET /api/market/{symbol}` - Get market data for a symbol
- `POST /api/market/batch` - Get market data for multiple symbols
- `POST /api/market/sync` - Manually trigger market data sync

### Dashboard Endpoints

- `GET /api/dashboard/news` - Latest financial news
- `GET /api/dashboard/assets` - All active assets with prices
- `GET /api/dashboard/portfolio?sessionId={id}` - Portfolio summary

## Streaming Architecture

### Server-Sent Events (SSE)

The system uses SSE for real-time streaming responses:

```
<status>Analyzing request...</status>
<status>Planning...</status>
<thinking>I need to check the user's profile first...</thinking>
<status>Executing plan...</status>
<status>Calling get_stock_price...</status>
<status>Finalizing answer...</status>
<response><![CDATA[Based on your portfolio...]]></response>
```

**XML Tags**:
- `<status>` - Processing status updates
- `<thinking>` - Chain-of-thought reasoning (when enabled)
- `<response>` - Final answer (wrapped in CDATA for markdown)

## Performance Optimizations

1. **Parallel Context Gathering**: Chat history, session, and portfolio fetched concurrently
2. **Cache-First Strategy**: Market data checked in cache before API calls
3. **Background Sync**: Proactive cache population for all assets
4. **Optimized Prompts**: Shorter prompts using simple symbols ("BTC" vs "BTC-USD")
5. **Streaming Responses**: Real-time token-by-token streaming for better UX

## Security Considerations

- Input validation on all endpoints
- Error handling middleware
- No sensitive data in logs
- Disclaimer in all financial advice responses

## Technology Stack

- **.NET 8.0** - Backend framework
- **MongoDB** - Document database
- **Ollama** - LLM inference (DeepSeek R1)
- **ASP.NET Core** - Web API framework
- **Docker** - Containerization

## Configuration

### Environment Variables

```bash
# MongoDB
ConnectionStrings__MongoDB=mongodb://localhost:27017/financial_advisor

# Ollama
OLLAMA_ENDPOINT=http://localhost:11434
OLLAMA_MODEL=deepseek-r1:14b

# Embeddings (if using external service)
EMBEDDING_SERVICE_URL=http://localhost:8000
```

## Future Enhancements

- [ ] Add more sophisticated caching strategies
- [ ] Implement rate limiting
- [ ] Add authentication and authorization
- [ ] Expand tool ecosystem
- [ ] Add more comprehensive error recovery
- [ ] Implement conversation memory optimization
