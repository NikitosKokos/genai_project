# Financial Advisor Backend

A .NET 8.0 backend application for a financial advisor system with RAG capabilities.

## Project Structure

-  `src/FinancialAdvisor.Api` - Web API layer
-  `src/FinancialAdvisor.Application` - Application services and business logic
-  `src/FinancialAdvisor.Domain` - Domain entities and value objects
-  `src/FinancialAdvisor.Infrastructure` - Data access and external services
-  `src/FinancialAdvisor.RAG` - RAG pipeline services
-  `src/FinancialAdvisor.MarketData` - Market data integration
-  `src/FinancialAdvisor.SharedKernel` - Shared utilities and constants

## Getting Started

See `docs/SETUP.md` for setup instructions.

## How to run:

-  `dotnet restore` - First run, restores NuGet packages
-  `dotnet build` - build the solution
-  `dotnet run --project src/FinancialAdvisor.Api` - run the API project
