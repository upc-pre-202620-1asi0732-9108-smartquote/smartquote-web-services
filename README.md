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
.\scripts\Start-SmartQuote.ps1
```

El script levanta los contenedores en segundo plano, espera a que la API esté
saludable y abre Swagger automáticamente. Si solo desea iniciar Docker sin
abrir el navegador, use `docker compose up --build -d`.

- Swagger: `http://localhost:8080/swagger`
- API: `http://localhost:8080`
- PostgreSQL: `localhost:5432`
- pgAdmin: `http://localhost:5050`

Docker Compose aplica las migraciones automáticamente. La API dentro de Docker se conecta con `Host=postgres`, no con `localhost`.

La carga de cotizaciones acepta archivos PDF sin datos previos del proveedor: el agente intenta extraer razón social y RUC por archivo, y el analista corrige los campos no acreditados antes de verificar. Si omite una especificación presente en el documento, `POST /api/v1/quotations/{quotationId}/lines/{lineId}/specifications` permite incorporarla con página, texto de origen, motivo y versión esperada. La simulación puede recuperarse con `GET /api/v1/purchase-requests/{requestId}/simulations` y la orden emitida con `GET /api/v1/purchase-requests/{requestId}/purchase-order`. La aprobación de una orden actualiza el estado de la solicitud a `Ordered`; una solicitud solo admite una orden. Al actualizar una instalación existente, aplique la nueva migración de `PurchaseOrdering` antes de probar la API.

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
dotnet ef database update --project src/SmartQuote.Modules.IdentityAccess --startup-project src/SmartQuote.API --context IdentityAccessDbContext
```

## Acceso local

No se crean cuentas de prueba al arrancar ni se requiere cargar usuarios con seeds. En una base de datos de identidad vacía, la primera persona se registra desde la pantalla pública y se convierte en `PurchaseManager` para completar la configuración inicial. El frontend puede consultar `GET /api/v1/iam/auth/registration-status` para saber si debe mostrar el flujo de configuración inicial.

Después del primer registro, cualquier persona puede solicitar una cuenta desde la misma pantalla, seleccionar `ProductionSpecialist`, `PurchaseAnalyst` o `PurchaseManager` y registrar sus credenciales. Esa cuenta queda `Pending` y no puede iniciar sesión hasta que un `PurchaseManager` la revise en `GET /api/v1/iam/registration-requests` y la apruebe mediante `POST /api/v1/iam/registration-requests/{userId}/approve`, asignándole el rol final. Así, las cuentas y roles se gestionan desde el propio sistema, sin seeds ni códigos de invitación, y nadie puede activarse permisos por sí mismo.

El frontend consume `POST /api/v1/iam/auth/login`, mantiene el access token únicamente en memoria y renueva la sesión mediante una cookie `HttpOnly`. No usa ni solicita claves JWT al usuario.

## Registro de cuentas (US09)

`POST /api/v1/iam/auth/register` recibe `email`, `displayName`, `password` y, después del primer registro, el rol solicitado (`ProductionSpecialist`, `PurchaseAnalyst` o `PurchaseManager`). La primera cuenta se activa automáticamente como `PurchaseManager`; las posteriores quedan pendientes de aprobación. Las solicitudes sólo las puede activar un `PurchaseManager`, que también confirma el rol efectivo. La unicidad del correo se compara sin distinguir mayúsculas y se protege con un índice único en la base de datos; los correos repetidos devuelven HTTP 409.

La contraseña de registro debe tener entre 12 y 128 caracteres, al menos una letra mayúscula, una minúscula, un número y un símbolo. No puede contener caracteres de control ni la parte anterior a `@` del correo cuando esta tenga al menos tres caracteres. Se valida en el backend sin modificar el texto ingresado y se almacena mediante el hasher de ASP.NET Core Identity. Las contraseñas inválidas devuelven HTTP 400 con la regla incumplida; el inicio de sesión conserva un mensaje genérico para credenciales incorrectas. El registro admite hasta cinco intentos por dirección IP cada quince minutos.

## OpenAI y secretos

El inicio de sesión admite 10 solicitudes por IP cada 15 segundos para facilitar las pruebas y demostraciones. Al superar el límite devuelve HTTP 429 con `Retry-After` y el tiempo de espera en el mensaje, que la aplicación móvil muestra directamente. El registro conserva 5 intentos por IP cada 15 minutos. Antes de una puesta en producción comercial, revisar estos límites y la identificación de IP detrás del proxy.

La configuración predeterminada usa `AI:Provider=Stub`, por lo que permite probar cotizaciones sin clave ni consumo externo. Para el agente real desde Rider:

```powershell
dotnet user-secrets set "AI:Provider" "OpenAI" --project src/SmartQuote.API
dotnet user-secrets set "OpenAI:ApiKey" "YOUR_OPENAI_API_KEY" --project src/SmartQuote.API
dotnet user-secrets set "OpenAI:Model" "gpt-4.1-mini" --project src/SmartQuote.API
```

Con Docker Compose, copie `.env.example` a `.env` y configure allí `AI__Provider=OpenAI` y `OpenAI__ApiKey`. Ese archivo está ignorado por Git y Docker no lo incluye en la imagen; Compose solo inyecta el valor en el contenedor durante la ejecución.

## Tipo de cambio SUNAT

Las simulaciones comparan cotizaciones PEN y USD en soles. Para USD, el backend consulta el tipo de cambio de venta (`V`) publicado por SUNAT para el día de ejecución en Lima, conserva los importes originales y registra la tasa, fuente, fecha de publicación y hora de consulta. Si SUNAT no publica la tasa exacta o no responde, la simulación devuelve HTTP 503 y no usa una tasa anterior.

Configure el token fuera del repositorio. Desde Rider/CLI:

```powershell
dotnet user-secrets set "SunatExchangeRate:Token" "YOUR_SUNAT_TOKEN" --project src/SmartQuote.API
```

Con Docker Compose, defina `SunatExchangeRate__Token` en el archivo `.env`. No copie cookies de Postman: son temporales y no forman parte de la configuración de la aplicación.

## Documentación de diagramas

US15: `GET /api/v1/suppliers/{taxIdentifier}/performance` devuelve los promedios, cantidad y período junto con `evaluations`, el historial individual ordenado desde la evaluación más reciente. Cada registro conserva orden, autor, fecha, calificaciones y observaciones. El analista o jefe registra la evaluación únicamente tras la entrega, con notas de 1 a 5 y hasta 500 caracteres de observaciones; una segunda evaluación de la misma orden devuelve 409. Este ajuste no requiere migraciones.

- Diagramas de clases PlantUML: `docs/1-supply-requests.puml`, `docs/2-quotation-intake.puml`, `docs/3-evaluation-simulation.puml`, `docs/4-purchase-ordering.puml` y `docs/5-identity-access.puml`.
- Modelo relacional: `docs/database-schema-documentation.md`.
