FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/FinancialAdvisor.Api/FinancialAdvisor.Api.csproj", "src/FinancialAdvisor.Api/"]
RUN dotnet restore "src/FinancialAdvisor.Api/FinancialAdvisor.Api.csproj"
COPY . .
WORKDIR "/src/src/FinancialAdvisor.Api"
RUN dotnet build "FinancialAdvisor.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "FinancialAdvisor.Api.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "FinancialAdvisor.Api.dll"]

