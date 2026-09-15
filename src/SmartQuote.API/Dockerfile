FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY global.json ./
COPY src/SmartQuote.Shared/SmartQuote.Shared.csproj src/SmartQuote.Shared/
COPY src/SmartQuote.Modules.SupplyRequests/SmartQuote.Modules.SupplyRequests.csproj src/SmartQuote.Modules.SupplyRequests/
COPY src/SmartQuote.Modules.QuotationIntake/SmartQuote.Modules.QuotationIntake.csproj src/SmartQuote.Modules.QuotationIntake/
COPY src/SmartQuote.Modules.EvaluationSimulation/SmartQuote.Modules.EvaluationSimulation.csproj src/SmartQuote.Modules.EvaluationSimulation/
COPY src/SmartQuote.Modules.PurchaseOrdering/SmartQuote.Modules.PurchaseOrdering.csproj src/SmartQuote.Modules.PurchaseOrdering/
COPY src/SmartQuote.Modules.IdentityAccess/SmartQuote.Modules.IdentityAccess.csproj src/SmartQuote.Modules.IdentityAccess/
COPY src/SmartQuote.API/SmartQuote.API.csproj src/SmartQuote.API/
RUN dotnet restore src/SmartQuote.API/SmartQuote.API.csproj

COPY . .
RUN test ! -e .env && dotnet publish src/SmartQuote.API/SmartQuote.API.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "SmartQuote.API.dll"]
