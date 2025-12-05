# Financial Advisor Backend

A .NET 8.0 backend application for a financial advisor system with RAG (Retrieval-Augmented Generation) capabilities, powered by LLM agents and real-time market data.

## 🏗️ Project Structure

```
FinancialAdvisor.Backend/
├── src/
│   ├── FinancialAdvisor.Api/              # Web API layer (Controllers, Middleware)
│   ├── FinancialAdvisor.Application/       # Application services, interfaces, DTOs
│   ├── FinancialAdvisor.Domain/           # Domain entities and value objects
│   ├── FinancialAdvisor.Infrastructure/    # Data access, external services, tools
│   ├── FinancialAdvisor.RAG/              # RAG pipeline services
│   ├── FinancialAdvisor.MarketData/       # Market data integration
│   └── FinancialAdvisor.SharedKernel/     # Shared utilities and constants
└── tests/
    ├── FinancialAdvisor.UnitTests/        # Unit tests
    ├── FinancialAdvisor.IntegrationTests/ # Integration tests
    └── FinancialAdvisor.E2ETests/        # End-to-end tests
```

### Layer Responsibilities

-  **Api**: HTTP endpoints, request/response handling, middleware
-  **Application**: Business logic interfaces, DTOs, service contracts
-  **Domain**: Core business entities (User, Portfolio, Transaction)
-  **Infrastructure**:
   -  Data persistence (MongoDB)
   -  External services (Ollama LLM, Embeddings)
   -  Tools (get_stock_price, search_rag, buy_stock, etc.)
   -  RAG orchestrator (AgentOrchestrator)
-  **RAG**: Embedding generation, vector search
-  **MarketData**: Real-time market data fetching

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
   "message": "Should I buy Apple stock?",
   "sessionId": "user-123",
   "enableReasoning": true, // Enable chain-of-thought reasoning
   "documentCount": 6 // Number of RAG documents to retrieve
}
```

**Response:** Server-Sent Events (SSE) stream with XML-tagged chunks:

```
<status>Analyzing request...</status>
<status>Checking knowledge base...</status>
<status>Planning...</status>
<thinking>I need to check the user's profile first...</thinking>
<status>Executing plan...</status>
<status>Calling get_stock_price...</status>
<status>Finalizing answer...</status>
<response><![CDATA[Based on your portfolio...]]></response>
```

**Features:**

-  Real-time streaming with chunk-by-chunk updates
-  Chain-of-thought reasoning display (when `enableReasoning=true`)
-  Status updates during processing
-  Markdown-formatted responses

---

### Market Data Endpoints

#### `GET /api/market/{symbol}`

Get current market data for a specific symbol.

**Example:** `GET /api/market/AAPL`

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

#### `POST /api/market/batch`

Get market data for multiple symbols.

**Request Body:**

```json
["AAPL", "MSFT", "GOOGL"]
```

**Response:** Array of market data objects

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
┌─────────────┐
│   Frontend  │
│  (Next.js)  │
└──────┬──────┘
       │ POST /api/chat/stream
       │ { message, sessionId, enableReasoning, documentCount }
       ▼
┌─────────────────────────────────────┐
│      ChatController                 │
│  - Validates request                │
│  - Sets up SSE stream               │
└──────┬──────────────────────────────┘
       │
       ▼
┌─────────────────────────────────────┐
│   AgentOrchestrator                 │
│  (IRagService implementation)       │
└──────┬──────────────────────────────┘
       │
       ├─► 1. Gather Context (Parallel)
       │   ├─► ContextService.GetChatHistoryAsync(sessionId, 6)
       │   ├─► ContextService.GetSessionAsync(sessionId)
       │   └─► ContextService.GetPortfolioAsync(sessionId)
       │
       ├─► 2. Proactive RAG Search
       │   └─► SearchRagTool.ExecuteAsync(query, top_k)
       │       ├─► EmbeddingService.EmbedAsync(query)
       │       └─► Vector search in MongoDB
       │
       ├─► 3. Build Augmented Prompt
       │   └─► PromptService.ConstructAugmentedUserPrompt()
       │       ├─► System prompt (tool contracts, rules)
       │       ├─► User profile context
       │       ├─► Portfolio context
       │       ├─► Market data context
       │       ├─► RAG context
       │       └─► Chat history (last 6 messages)
       │
       ├─► 4. LLM Planning (Streaming)
       │   └─► LLMService.GenerateFinancialAdviceStreamAsync()
       │       ├─► Calls Ollama API (streaming)
       │       ├─► Yields <thinking> tokens (if enableReasoning=true)
       │       └─► Yields <response> tokens (JSON plan)
       │
       ├─► 5. Parse Plan JSON
       │   └─► PromptService.PostProcessModelOutput()
       │       └─► Extract JSON from LLM output
       │
       ├─► 6. Execute Plan
       │   └─► For each step in plan:
       │       ├─► Find tool by name
       │       ├─► Execute tool with args
       │       └─► Collect tool outputs
       │
       └─► 7. Final Answer Generation (Streaming)
           └─► LLMService.GenerateFinancialAdviceStreamAsync()
               ├─► Build final prompt with tool results
               ├─► Stream <thinking> tokens (if enabled)
               └─► Stream <response> tokens (markdown text)
                   └─► Wrapped in <![CDATA[]]> to preserve markdown
```

### Background Services

```
┌─────────────────────────────┐
│  NewsIngestionService       │
│  (Background Service)       │
│  Runs every 1 hour          │
└──────┬──────────────────────┘
       │
       ├─► Fetch news articles
       ├─► Generate embeddings
       └─► Store in MongoDB (financial_documents)
           └─► Used by SearchRagTool for RAG retrieval
```

---

## 🛠️ Available Tools

The system includes 6 tools that the LLM can call during planning:

| Tool               | Description                     | Input                         | Output                                               |
| ------------------ | ------------------------------- | ----------------------------- | ---------------------------------------------------- |
| `get_stock_price`  | Get current stock price         | `{ symbol: "AAPL" }`          | `{ symbol, price, currency, timestamp, source }`     |
| `get_profile`      | Get user profile and portfolio  | `{ user_id: "user-123" }`     | `{ user_id, strategy, cash, holdings }`              |
| `get_owned_shares` | Get owned shares for a symbol   | `{ user_id: "user-123" }`     | `{ user_id, holdings: [{symbol, qty}] }`             |
| `search_rag`       | Search financial news/documents | `{ query: "...", top_k: 3 }`  | `[{ id, title, snippet, timestamp, source, score }]` |
| `buy_stock`        | Place buy order                 | `{ symbol: "AAPL", qty: 10 }` | `{ status, order_id, executed_qty, avg_price }`      |
| `sell_stock`       | Place sell order                | `{ symbol: "AAPL", qty: 10 }` | `{ status, order_id, executed_qty, avg_price }`      |

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
-  **Context Assembly**: Rich context from profile, portfolio, market data, and RAG
-  **Streaming Responses**: Real-time token-by-token streaming with status updates

### Key Features

-  ✅ **Streaming Chat**: Real-time SSE streaming with chunk-by-chunk updates
-  ✅ **Chain-of-Thought**: Display LLM reasoning process (when enabled)
-  ✅ **RAG Integration**: Hourly news ingestion with vector search
-  ✅ **Tool System**: Extensible tool-based architecture
-  ✅ **Context Management**: Automatic context gathering and assembly
-  ✅ **Parallel Processing**: Context gathering runs in parallel for performance

---

## 📝 Environment Variables

```bash
# MongoDB
ConnectionStrings__MongoDB=mongodb://localhost:27017/financial_advisor

# Ollama
OLLAMA_ENDPOINT=http://localhost:11434
OLLAMA_MODEL=deepseek-r1:8b # faster fr MVP

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

-  `ARCHITECTURE_ANALYSIS_AND_IMPLEMENTATION_PLAN.md` - Detailed architecture analysis
-  `docs/API_SPECIFICATION.md` - API documentation
-  `docs/SETUP.md` - Detailed setup instructions

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
