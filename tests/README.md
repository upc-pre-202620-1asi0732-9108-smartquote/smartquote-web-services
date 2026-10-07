# API verification tests

The solution's existing xUnit projects run with `dotnet test SmartQuote.sln` and do not require PostgreSQL. The two additional projects below are run explicitly against a local PostgreSQL test database; they are not included in the solution's default test command.

## PostgreSQL integration tests

`SmartQuote.Integration.Tests` starts the actual ASP.NET Core API in memory with `WebApplicationFactory<Program>`, applies EF Core migrations, calls the HTTP endpoints, and checks the persisted rows directly in PostgreSQL. It covers request persistence, a mixed PDF/unsupported-file batch, duplicate PDFs in one batch, and the TS04 lookup of a persisted purchase order.

Set `SMARTQUOTE_TEST_CONNECTION` to a dedicated PostgreSQL database and `SMARTQUOTE_JWT_KEY_FILE` to a local text file containing a test signing key of at least 32 bytes. Do not use a production database or key. Then run:

```text
dotnet test tests/SmartQuote.Integration.Tests/SmartQuote.Integration.Tests.csproj --configuration Release
```

## TS04 HTTP contract tests

`PurchaseOrderContractTests` checks the purchase-order JSON response, allowed and denied roles, structured 401/403/404 errors, and the OpenAPI response codes. Its in-memory repository isolates the HTTP contract, so this filtered suite does not require PostgreSQL:

```text
dotnet test tests/SmartQuote.Integration.Tests/SmartQuote.Integration.Tests.csproj --filter FullyQualifiedName~PurchaseOrderContractTests
```

## US16 purchasing metrics

`PurchasingMetricsCalculatorTests` checks the comparative savings formula with synthetic PEN/USD quotations, an excluded offer, and missing comparable data. Four `ApiPersistenceTests.Metrics...` cases check the manager-only endpoint, date validation, empty periods, a persisted request-to-order time sample, and the calculation from a persisted simulation with two eligible quotations in PostgreSQL. The response reports UTC dates, the count used for each indicator, and `null` when data is insufficient. Filter these seven tests with:

```text
dotnet test tests/SmartQuote.Integration.Tests/SmartQuote.Integration.Tests.csproj --filter "FullyQualifiedName~PurchasingMetricsCalculatorTests|FullyQualifiedName~ApiPersistenceTests.Metrics"
```

The API tests require the dedicated PostgreSQL database and JWT test key described above. The fixtures are synthetic and do not establish actual savings in production.

## Executable Gherkin scenarios

`SmartQuote.Bdd.Tests` uses Reqnroll and xUnit to exercise the purchase request and quotation intake flows through a running API backed by PostgreSQL. It checks role authorization, request registration, and partial success in a mixed quotation batch. The scenarios mint local test JWTs with the same signing key as the local API.

Start the API with `Database__ApplyMigrations=true`, a dedicated PostgreSQL connection string, the JWT issuer `SmartQuote`, the audience `SmartQuote.Clients`, and the signing key from `SMARTQUOTE_JWT_KEY_FILE`. Set `SMARTQUOTE_API_URL` to that API's local HTTP address. Then run:

```text
dotnet test tests/SmartQuote.Bdd.Tests/SmartQuote.Bdd.Tests.csproj --configuration Release
```

These projects exercise separate levels of verification. Their passing result does not establish that the native Android application has been tested.
