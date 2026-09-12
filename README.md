# SmartQuote Web Services

Backend RESTful de SmartQuote para solicitudes de insumos avícolas, cotizaciones, simulaciones y órdenes de compra. Es un monolito modular con Clean Architecture y DDD.

## Requisitos

- .NET SDK compatible con `global.json`.
- Docker Desktop (recomendado para PostgreSQL) o PostgreSQL 16 local.
- JetBrains Rider o la CLI de .NET.

```powershell
dotnet tool restore
dotnet restore SmartQuote.sln
dotnet build SmartQuote.sln
dotnet test SmartQuote.sln
```

## Ejecutar con Docker Compose

Es la forma más rápida de levantar API y PostgreSQL.

```powershell
Copy-Item .env.example .env
# Edite .env y reemplace los valores replace-me.
docker compose up --build
```

- Swagger: `http://localhost:8080/swagger`
- API: `http://localhost:8080`
- PostgreSQL: `localhost:5432`
- pgAdmin: `http://localhost:5050`

Docker Compose aplica las migraciones automáticamente. La API dentro de Docker se conecta con `Host=postgres`, no con `localhost`.

Para detenerlo, ejecute `docker compose down`. Agregar `-v` también elimina los datos locales de PostgreSQL.

## Ejecutar desde Rider con PostgreSQL en Docker

```powershell
docker compose up -d postgres
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=smartquote;Username=smartquote;Password=YOUR_PASSWORD" --project src/SmartQuote.API
dotnet user-secrets set "Jwt:Issuer" "SmartQuote" --project src/SmartQuote.API
dotnet user-secrets set "Jwt:Audience" "SmartQuote.Clients" --project src/SmartQuote.API
dotnet user-secrets set "Jwt:SigningKey" "REPLACE_WITH_AT_LEAST_32_RANDOM_CHARACTERS" --project src/SmartQuote.API
dotnet run --project src/SmartQuote.API
```

Abra el Swagger indicado por el perfil activo de Rider (normalmente `https://localhost:7168/swagger`). Para aplicar migraciones manualmente, ejecute:

```powershell
dotnet ef database update --project src/SmartQuote.Modules.SupplyRequests --startup-project src/SmartQuote.API --context SupplyRequestsDbContext
dotnet ef database update --project src/SmartQuote.Modules.QuotationIntake --startup-project src/SmartQuote.API --context QuotationIntakeDbContext
dotnet ef database update --project src/SmartQuote.Modules.EvaluationSimulation --startup-project src/SmartQuote.API --context EvaluationSimulationDbContext
dotnet ef database update --project src/SmartQuote.Modules.PurchaseOrdering --startup-project src/SmartQuote.API --context PurchaseOrderingDbContext
```

## OpenAI y secretos

La configuración predeterminada usa `AI:Provider=Stub`, por lo que permite probar cotizaciones sin clave ni consumo externo. Para el agente real desde Rider:

```powershell
dotnet user-secrets set "AI:Provider" "OpenAI" --project src/SmartQuote.API
dotnet user-secrets set "OpenAI:ApiKey" "YOUR_OPENAI_API_KEY" --project src/SmartQuote.API
dotnet user-secrets set "OpenAI:Model" "gpt-4.1-mini" --project src/SmartQuote.API
```

Con Docker Compose, configure `AI__Provider=OpenAI` y `OpenAI__ApiKey` exclusivamente en su `.env` local. Ese archivo está ignorado por Git y Docker no lo incluye en la imagen.

## Documentación de diagramas

- Diagramas de clases PlantUML: `docs/1-supply-requests.puml`, `docs/2-quotation-intake.puml`, `docs/3-evaluation-simulation.puml` y `docs/4-purchase-ordering.puml`.
- Modelo relacional: `docs/database-schema-documentation.md`.
