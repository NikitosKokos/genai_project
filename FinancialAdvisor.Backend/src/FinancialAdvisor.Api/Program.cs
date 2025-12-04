using FinancialAdvisor.Application.Interfaces;
using FinancialAdvisor.Api.Services;
using FinancialAdvisor.Infrastructure.ExternalServices;
using FinancialAdvisor.RAG.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Register RAG Stack
builder.Services.AddSingleton<OpenAiLlmClient>();
builder.Services.AddSingleton<OpenAiEmbeddingsClient>();
builder.Services.AddSingleton<VectorDbManager>();
builder.Services.AddScoped<RetrievalOrchestrator>();
builder.Services.AddScoped<IRagService, RagService>();

var app = builder.Build();

// Seed Vector DB (Fire and forget to not block startup if no key)
_ = Task.Run(async () =>
{
    using var scope = app.Services.CreateScope();
    var vectorDb = scope.ServiceProvider.GetRequiredService<VectorDbManager>();
    var embedder = scope.ServiceProvider.GetRequiredService<OpenAiEmbeddingsClient>();
    
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
            var vector = await embedder.GenerateEmbeddingAsync(text);
            if (vector.Length > 0 && vector.Sum() != 0) // Only add if valid
            {
                await vectorDb.UpsertAsync(new FinancialAdvisor.RAG.Models.EmbeddingedDocument 
                { 
                    Id = Guid.NewGuid().ToString(),
                    Content = text,
                    Embedding = vector,
                    Metadata = "Market News"
                });
            }
        }
        catch 
        {
            // Ignore seeding errors (e.g. missing API key)
        }
    }
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();

