using FinancialAdvisor.Application.Interfaces;
using FinancialAdvisor.Api.Services;
using FinancialAdvisor.Infrastructure.ExternalServices;
using FinancialAdvisor.Infrastructure.Services;
using FinancialAdvisor.Infrastructure.Services.RAG;
using FinancialAdvisor.Infrastructure.Tools;
using FinancialAdvisor.Infrastructure.Data;
using FinancialAdvisor.RAG.Services; // Added this
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// CORS Configuration
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:3000", "http://localhost:3001")
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

// 1. Core Infrastructure
builder.Services.AddSingleton<MongoDbContext>();
builder.Services.AddSingleton<OpenAiLlmClient>();
builder.Services.AddSingleton<OpenAiEmbeddingsClient>();

// 2. Application Services
builder.Services.AddScoped<IContextService, ContextService>();
builder.Services.AddScoped<IPromptService, PromptService>();
builder.Services.AddScoped<IMarketDataService, MarketDataService>();
builder.Services.AddHttpClient<IMarketDataService, MarketDataService>();
builder.Services.AddScoped<ILLMService, LLMService>();
builder.Services.AddHttpClient<ILLMService, LLMService>();
builder.Services.AddHttpClient<IEmbeddingService, EmbeddingService>();
builder.Services.AddScoped<IActionService, ActionService>();

// Background Services
builder.Services.AddHostedService<NewsIngestionService>();
builder.Services.AddHostedService<MarketDataSyncService>();

// 3. Tools
builder.Services.AddScoped<ITool, GetStockPriceTool>();
builder.Services.AddScoped<ITool, GetProfileTool>();
builder.Services.AddScoped<ITool, GetOwnedSharesTool>();
builder.Services.AddScoped<ITool, SearchRagTool>();
builder.Services.AddScoped<ITool, BuyStockTool>();
builder.Services.AddScoped<ITool, SellStockTool>();

// 4. RAG Orchestrator (The Agent)
builder.Services.AddScoped<IRagService, AgentOrchestrator>();

// 5. Legacy/Helpers (Removed in-memory VectorDbManager)
// builder.Services.AddSingleton<VectorDbManager>();
// builder.Services.AddScoped<RetrievalOrchestrator>();

var app = builder.Build();

// Seed Vector DB (MongoDB)
_ = Task.Run(async () =>
{
    using var scope = app.Services.CreateScope();
    var mongoContext = scope.ServiceProvider.GetRequiredService<MongoDbContext>();
    var embedder = scope.ServiceProvider.GetRequiredService<IEmbeddingService>();
    
    // Check if we have any docs
    var count = await mongoContext.FinancialDocuments.CountDocumentsAsync(Builders<FinancialAdvisor.Application.Models.FinancialDocument>.Filter.Empty);
    if (count > 0) return;

    var docs = new[]
    {
        "Apple (AAPL) reported strong Q3 earnings driven by Services revenue. The upcoming Vision Pro headset is expected to add a new revenue stream.",
        "Microsoft (MSFT) Azure growth accelerated to 29% YoY. The Copilot integration across Microsoft 365 is seeing rapid enterprise adoption.",
        "Inflation has cooled to 3.2%, signaling the Fed may pause rate hikes. This is generally bullish for growth stocks like Tech.",
        "NVIDIA (NVDA) H100 GPU demand continues to outstrip supply, with lead times extending into 2025. Data center revenue tripled YoY."
    };

    foreach (var text in docs)
    {
        try
        {
            var vectorFloat = await embedder.EmbedAsync(text);
            var vectorDouble = Array.ConvertAll(vectorFloat, x => (double)x);

            if (vectorDouble.Length > 0)
            {
                await mongoContext.FinancialDocuments.InsertOneAsync(new FinancialAdvisor.Application.Models.FinancialDocument 
                { 
                    Id = MongoDB.Bson.ObjectId.GenerateNewId(),
                    Title = "Initial Seed News",
                    Content = text,
                    Source = "System Seed",
                    Category = "News",
                    CreatedAt = DateTime.UtcNow,
                    Embedding = vectorDouble,
                    Metadata = new MongoDB.Bson.BsonDocument { { "type", "seed" } }
                });
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Seeding error: {ex.Message}");
        }
    }
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
