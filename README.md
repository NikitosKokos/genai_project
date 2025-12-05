# Financial Advisor System

A full-stack financial advisor application with RAG (Retrieval-Augmented Generation) capabilities, powered by LLM agents and real-time market data. The system provides intelligent financial advice through a conversational AI interface, combining real-time market data, financial news retrieval, and portfolio management.

## 🏗️ Project Structure

```
genai_project/
├── FinancialAdvisor.Backend/             # .NET 8.0 Backend API
│   ├── src/
│   │   ├── FinancialAdvisor.Api/          # Web API layer (Controllers, Middleware)
│   │   ├── FinancialAdvisor.Application/ # Application services, interfaces, DTOs
│   │   ├── FinancialAdvisor.Domain/      # Domain entities and value objects
│   │   ├── FinancialAdvisor.Infrastructure/ # Data access, external services, tools
│   │   ├── FinancialAdvisor.RAG/        # RAG pipeline services
│   │   ├── FinancialAdvisor.MarketData/  # Market data integration
│   │   └── FinancialAdvisor.SharedKernel/ # Shared utilities and constants
│   ├── docs/                             # Architecture and API documentation
│   └── tests/                            # Unit, integration, and E2E tests
├── FinancialAdvisor.Frontend/            # Next.js Frontend
│   ├── app/                              # Next.js app directory
│   │   ├── components/                  # React components
│   │   ├── api/                          # Next.js API routes (proxy)
│   │   └── context/                     # React context providers
│   └── lib/                              # Frontend utilities and API client
└── docker-compose.yml                    # Full stack orchestration
```

### Architecture Overview

The system uses an **agent-based RAG architecture** where:

-  The LLM acts as a **planner** that decides what tools to call
-  Tools fetch data **on-demand** (not pre-fetched)
-  Real-time streaming responses via Server-Sent Events (SSE)
-  Market data caching with 15-minute TTL
-  Background services for news ingestion and market data sync

See [Backend Architecture Documentation](FinancialAdvisor.Backend/docs/ARCHITECTURE.md) for detailed information.

---

## 🔌 API Endpoints

### Chat Endpoints

#### `POST /api/chat/query`

Non-streaming chat query endpoint.

**Request Body:**

```json
{
   "message": "What is the current price of AAPL?",
   "sessionId": "user-123",
   "enableReasoning": false,
   "documentCount": 3
}
```

**Response:**

```json
{
   "advice": "The current price of AAPL is $150.50...",
   "timestamp": "2025-01-15T10:30:00Z"
}
```

#### `POST /api/chat/stream` ⭐ **Primary Endpoint**

Streaming chat query endpoint with real-time token-by-token responses.

**Request Body:**

```json
{
   "message": "What is the price of Bitcoin?",
   "sessionId": "user-123",
   "enableReasoning": true, // Enable chain-of-thought reasoning (Supernova model)
   "documentCount": 6 // Number of RAG documents to retrieve (3 for Default, 6 for Supernova)
}
```

**Response:** Server-Sent Events (SSE) stream with XML-tagged chunks:

```
<status>Analyzing request...</status>
<status>Gathering context...</status>
<status>Planning...</status>
<thinking>I need to check the current Bitcoin price...</thinking>
<status>Executing plan...</status>
<status>Calling get_stock_price...</status>
<status>Finalizing answer...</status>
<response><![CDATA[The current price of Bitcoin (BTC) is $45,000...]]></response>
```

**Features:**

-  Real-time streaming with chunk-by-chunk updates
-  Chain-of-thought reasoning display (when `enableReasoning=true`)
-  Status updates during processing
-  Markdown-formatted responses
-  Parallel context gathering for improved performance

---

### Market Data Endpoints

#### `GET /api/market/{symbol}`

Get current market data for a specific symbol. Supports both stocks and cryptocurrencies.

**Examples:**

-  `GET /api/market/AAPL` (stock)
-  `GET /api/market/BTC` (crypto - automatically normalized to BTC-USD)

**Response:**

```json
[
   {
      "symbol": "AAPL",
      "price": 150.5,
      "changePercent": 2.5,
      "volume": 50000000,
      "lastUpdated": "2025-01-15T10:30:00Z"
   }
]
```

**Note:** The system uses a cache-first strategy with 15-minute TTL. Crypto symbols can be provided as simple symbols (e.g., "BTC", "ETH") and are automatically normalized to full format ("BTC-USD", "ETH-USD").

#### `POST /api/market/batch`

Get market data for multiple symbols.

**Request Body:**

```json
["AAPL", "MSFT", "BTC", "ETH"]
```

**Response:** Array of market data objects

#### `POST /api/market/sync`

Manually trigger market data sync for all active assets. Useful for testing and debugging.

**Response:**

```json
{
   "message": "Market data sync completed",
   "syncedCount": 10,
   "failedCount": 0
}
```

---

### Dashboard Endpoints

#### `GET /api/dashboard/news`

Get latest financial news articles (last 10).

**Response:**

```json
[
   {
      "id": "...",
      "title": "Apple Reports Strong Q3 Earnings",
      "summary": "Apple (AAPL) reported strong Q3 earnings...",
      "source": "TechCrunch",
      "publishedAt": "2025-01-15T09:00:00Z"
   }
]
```

#### `GET /api/dashboard/assets`

Get all active assets with real-time prices.

**Response:**

```json
[
   {
      "symbol": "AAPL",
      "name": "Apple Inc.",
      "sector": "Technology",
      "type": "Stock",
      "price": 150.5,
      "changePercent": 2.5,
      "volume": 50000000,
      "lastUpdated": "2025-01-15T10:30:00Z"
   }
]
```

#### `GET /api/dashboard/portfolio?sessionId=user-123`

Get portfolio summary with holdings and performance chart.

**Response:**

```json
{
   "totalValue": 125000.0,
   "cashBalance": 25000.0,
   "holdings": [
      {
         "symbol": "AAPL",
         "quantity": 100,
         "avgCost": 145.0,
         "currentPrice": 150.5,
         "value": 15050.0,
         "gainLoss": 550.0,
         "gainLossPercent": 3.79
      }
   ],
   "performance": [
      { "date": "Jan 01", "value": 120000 },
      { "date": "Jan 15", "value": 125000 }
   ]
}
```

---

### Other Endpoints

-  `GET /api/profile` - User profile (placeholder)
-  `GET /api/portfolio` - Portfolio operations (placeholder)
-  `GET /api/transaction` - Transaction history (placeholder)

---

## 🔄 Data Flow

### Chat Query Flow (Streaming)

```
User: "What is Bitcoin price?"
  ↓
Frontend (Next.js) → POST /api/chat/stream
  ↓
ChatController → AgentOrchestrator.ProcessQueryStreamAsync()
  ↓
1. Gather Context (Parallel):
   ├─► Chat History (last 6 messages)
   ├─► User Session (risk profile, goal)
   └─► Portfolio (holdings, cash balance)
  ↓
2. Proactive RAG Search:
   └─► SearchRagTool → Vector search in financial_documents
  ↓
3. Build Augmented Prompt:
   ├─► System prompt (tool contracts, rules)
   ├─► User profile + Portfolio + RAG context
   ├─► Market context: "[]" (empty - LLM uses tools)
   └─► Chat history
  ↓
4. LLM Planning Phase (Streaming):
   └─► LLM generates Plan JSON:
       {
         "type": "plan",
         "steps": [
           { "tool": "get_stock_price", "args": { "symbol": "BTC" } }
         ]
       }
   └─► Streams <thinking> tokens (if enabled)
  ↓
5. Execute Plan:
   └─► GetStockPriceTool.ExecuteAsync("BTC")
       └─► MarketDataService.GetMarketDataAsync(["BTC"])
           ├─► Check market_cache (if < 15 min old) ✅
           ├─► If miss: Fetch from Yahoo Finance API
           ├─► Normalize: "BTC" → "BTC-USD"
           └─► Update market_cache ✅
  ↓
6. Final Answer Generation (Streaming):
   └─► LLM generates response with tool results
       └─► Streams <response> tokens (markdown)
  ↓
Frontend displays: "The current price of Bitcoin (BTC) is $45,000..."
```

### Background Services

```
┌─────────────────────────────┐
│  NewsIngestionService       │
│  Runs every 1 hour          │
└──────┬──────────────────────┘
       │
       ├─► Fetch financial news
       ├─► Generate embeddings
       └─► Store in MongoDB (financial_documents)

┌─────────────────────────────┐
│  MarketDataSyncService      │
│  Runs every 15 minutes      │
└──────┬──────────────────────┘
       │
       ├─► Get all active assets
       ├─► Fetch market data
       └─► Update market_cache
           └─► Ensures cache is populated proactively
```

---

## 🛠️ Available Tools

The system includes 6 tools that the LLM can call during planning:

| Tool               | Description                     | Input                                       | Output                                               |
| ------------------ | ------------------------------- | ------------------------------------------- | ---------------------------------------------------- |
| `get_stock_price`  | Get current stock/crypto price  | `{ symbol: "AAPL" }` or `{ symbol: "BTC" }` | `{ symbol, price, currency, timestamp, source }`     |
| `get_profile`      | Get user profile and portfolio  | `{ user_id: "user-123" }`                   | `{ user_id, strategy, cash, holdings }`              |
| `get_owned_shares` | Get owned shares for a symbol   | `{ user_id: "user-123" }`                   | `{ user_id, holdings: [{symbol, qty}] }`             |
| `search_rag`       | Search financial news/documents | `{ query: "...", top_k: 3 }`                | `[{ id, title, snippet, timestamp, source, score }]` |
| `buy_stock`        | Place buy order                 | `{ symbol: "AAPL", qty: 10 }`               | `{ status, order_id, executed_qty, avg_price }`      |
| `sell_stock`       | Place sell order                | `{ symbol: "AAPL", qty: 10 }`               | `{ status, order_id, executed_qty, avg_price }`      |

**Symbol Format**:

-  **Stocks**: Use ticker symbol (e.g., `"AAPL"`, `"MSFT"`)
-  **Crypto**: Use simple symbol (e.g., `"BTC"`, `"ETH"`) - backend automatically normalizes to full format

---

## 🚀 Getting Started

### Prerequisites

-  .NET 8.0 SDK
-  MongoDB (running locally or via Docker)
-  Ollama (for LLM inference)
-  Docker & Docker Compose (optional, for full stack)

### Setup

1. **Restore dependencies:**

   ```bash
   dotnet restore
   ```

2. **Build the solution:**

   ```bash
   dotnet build
   ```

3. **Configure appsettings.json:**

   ```json
   {
      "ConnectionStrings": {
         "MongoDB": "mongodb://localhost:27017/financial_advisor"
      },
      "OLLAMA_ENDPOINT": "http://localhost:11434",
      "OLLAMA_MODEL": "deepseek-r1:14b"
   }
   ```

4. **Run the API:**

   ```bash
   dotnet run --project src/FinancialAdvisor.Api
   ```

   The API will be available at `http://localhost:5000`

5. **Access Swagger UI** (Development only):
   ```
   http://localhost:5000/swagger
   ```

### Using Docker Compose

```bash
docker-compose up -d
```

This starts:

-  Backend API (port 5000)
-  MongoDB (port 27017)
-  Ollama (port 11434)
-  Frontend (port 3000)

---

## 📊 Architecture Highlights

### Agent-Based RAG System

-  **LLM Planning**: The LLM generates a JSON plan with tool calls
-  **Tool Execution**: Tools are executed sequentially based on the plan
-  **Context Assembly**: Rich context from profile, portfolio, RAG, and chat history
-  **Streaming Responses**: Real-time token-by-token streaming with status updates
-  **Cache-First Strategy**: Market data cached with 15-minute TTL for performance

### Key Features

-  ✅ **Streaming Chat**: Real-time SSE streaming with chunk-by-chunk updates
-  ✅ **Chain-of-Thought**: Display LLM reasoning process (Supernova model)
-  ✅ **RAG Integration**: Hourly news ingestion with vector search
-  ✅ **Tool System**: Extensible tool-based architecture
-  ✅ **Context Management**: Automatic context gathering and assembly
-  ✅ **Parallel Processing**: Context gathering runs in parallel for performance
-  ✅ **Market Data Caching**: 15-minute cache TTL with background sync
-  ✅ **Symbol Normalization**: Automatic conversion (BTC → BTC-USD) for API calls
-  ✅ **Optimized Prompts**: Shorter prompts using simple symbols for faster processing

---

## 📝 Environment Variables

```bash
# MongoDB
ConnectionStrings__MongoDB=mongodb://localhost:27017/financial_advisor

# Ollama
OLLAMA_ENDPOINT=http://localhost:11434
OLLAMA_MODEL=deepseek-r1:14b

# Embeddings (if using external service)
EMBEDDING_SERVICE_URL=http://localhost:8000
```

---

## 🧪 Testing

```bash
# Run unit tests
dotnet test tests/FinancialAdvisor.UnitTests

# Run integration tests
dotnet test tests/FinancialAdvisor.IntegrationTests

# Run all tests
dotnet test
```

---

## 📚 Additional Documentation

-  [`FinancialAdvisor.Backend/docs/ARCHITECTURE.md`](FinancialAdvisor.Backend/docs/ARCHITECTURE.md) - Comprehensive architecture documentation
-  [`ARCHITECTURE_FLOW_DIAGRAM.md`](ARCHITECTURE_FLOW_DIAGRAM.md) - Detailed data flow diagrams
-  [`SYSTEM_ARCHITECTURE_EXPLANATION.md`](SYSTEM_ARCHITECTURE_EXPLANATION.md) - In-depth system explanation
-  `FinancialAdvisor.Backend/docs/API_SPECIFICATION.md` - API documentation
-  `FinancialAdvisor.Backend/docs/SETUP.md` - Detailed setup instructions
-  `FinancialAdvisor.Backend/docs/DEPLOYMENT.md` - Deployment guide

---

## 🔧 Technology Stack

-  **.NET 8.0** - Backend framework
-  **MongoDB** - Document database
-  **Ollama** - LLM inference (DeepSeek R1)
-  **ASP.NET Core** - Web API framework
-  **Docker** - Containerization

---

## 📄 License

[Your License Here]
