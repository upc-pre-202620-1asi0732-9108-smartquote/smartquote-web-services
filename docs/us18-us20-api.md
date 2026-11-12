# Backend — US18 y US20

## Alcance y puesta en marcha

Implementación en el monolito modular, sin nuevos bounded contexts ni cambios en web/móvil:

- `PurchaseOrdering`: habilitación interna de proveedores, presupuestos y validación final de aprobación.
- `QuotationIntake`: vinculación de una identidad verificada con el catálogo de proveedores mediante un puerto.
- `EvaluationSimulation`: elegibilidad versionada, invalidación de resultados y publicación de la tasa e importes realmente utilizados.
- `SmartQuote.API/Integration`: adaptadores entre contratos, sin referencias circulares entre proyectos.

Aplicar las migraciones `AddSupplierEligibilityAndPurchaseBudgets` y `CaptureSupplierEligibility`. Con `Database:ApplyMigrations=true`, se aplican al reiniciar el backend recompilado. Alternativa local:

```powershell
dotnet ef database update --context PurchaseOrderingDbContext --project src/SmartQuote.Modules.PurchaseOrdering --startup-project src/SmartQuote.API
dotnet ef database update --context EvaluationSimulationDbContext --project src/SmartQuote.Modules.EvaluationSimulation --startup-project src/SmartQuote.API
```

La migración concilia proveedores históricos por RUC de once dígitos, conservando sus órdenes y snapshots. Los registra **Pending**, no los habilita automáticamente. Si se aplican migraciones por separado sobre una instalación nueva, crear primero el esquema de cotizaciones para que sus datos históricos puedan conciliarse; el arranque normal de API ya sigue ese orden.

Las vistas nuevas todavía no están implementadas en frontend/móvil: se pueden usar estos endpoints desde Swagger o Postman. Los clientes anteriores pueden omitir los campos nuevos de aprobación, pero el servidor **siempre** aplica las reglas vigentes. No se necesitan claves externas adicionales.

## US18 — Habilitación interna de proveedores

Al confirmar una cotización, el backend vincula el proveedor por RUC, preservando su nombre y evidencia de origen. Un proveedor nuevo comienza `Pending`; si ya existe, se conserva su estado y no se sobrescriben las decisiones del jefe. Un RUC sin identificación válida debe corregirse antes de confirmar.

### Consultas — analista o jefe

- `GET /api/v1/suppliers?status=Pending&page=1&pageSize=20`: catálogo paginado por parámetros; respuesta en lista.
- `GET /api/v1/suppliers/{supplierId}`: identidad, estado, versión e historial de decisiones.
- Se conserva `GET /api/v1/suppliers/{taxIdentifier}/performance` para las evaluaciones de entregas; es distinto del control de habilitación.

### Decisiones — solamente jefe

```http
POST /api/v1/suppliers/{supplierId}/enable
```

```json
{
  "reason": "Identidad y condiciones comerciales revisadas para compras internas.",
  "expectedVersion": 1
}
```

Las mismas propiedades se utilizan en `/suspend` y `/reactivate`, con el motivo y versión actuales. Transiciones permitidas:

- `Pending` → `Enabled`: `/enable`.
- `Enabled` → `Suspended`: `/suspend`.
- `Suspended` → `Enabled`: `/reactivate`.

Cada decisión conserva estado anterior/nuevo, jefe, fecha, motivo y versión. Es una autorización **interna**, no una certificación de SUNAT ni una decisión de la IA.

### Efecto sobre compras y simulaciones

- Solo proveedores habilitados participan como alternativas elegibles. Una simulación puede mostrar exclusiones `SUPPLIER_NOT_ENABLED`; `criterionId` es nulo porque la habilitación es una regla de negocio, no un criterio ponderado inventado.
- La simulación conserva cuenta, estado y versión de habilitación. Un cambio deja desactualizados los resultados pendientes afectados (`isCurrent=false`). Reactivar incrementa la versión y **no** revive una recomendación anterior: ejecutar una simulación nueva.
- La aprobación comprueba nuevamente el estado. Las órdenes ya emitidas y sus evidencias no se modifican; reintentar su recuperación no emite una nueva compra.
- Si no hay alternativas elegibles, habilitar los proveedores que el jefe considere procedentes y volver a simular. La verificación del PDF por el analista no sustituye esa autorización.

## US20 — Presupuesto de una adquisición

### Consultar presupuesto — analista o jefe

```http
GET /api/v1/purchase-requests/{requestId}/budget
```

Sin presupuesto devuelve `status: NotConfigured`, `amountPen: null` y `version: null`, nunca cero. El control presupuestal aplica cuando existe autorización; la ausencia no se presenta como «dentro del presupuesto». Los borradores privados de producción no son consultables desde esta función.

### Autorizar o revisar — jefe

```http
PUT /api/v1/purchase-requests/{requestId}/budget
```

```json
{
  "amount": 5000,
  "currency": "PEN",
  "reason": "Límite autorizado para la adquisición de alimento.",
  "expectedVersion": 0
}
```

Para el primer registro se usa versión esperada **0**. Para cambiarlo se utiliza la versión actual devuelta por el servidor. El importe debe ser positivo, en PEN, con hasta cuatro decimales y una justificación. Solo se autorizan solicitudes activas sin adjudicación previa. El historial conserva todas las versiones, autores y fechas.

### Contrastar una alternativa — analista o jefe

```http
GET /api/v1/simulations/{runId}/budget-check?quotationId={quotationId}
```

Devuelve la simulación y cotización, huella de datos, `assessment` y, cuando se aplica, `exchangeRate`:

- `status`: `NotConfigured`, `WithinBudget` o `Exceeded`.
- `budgetVersion` y `budgetAmountPen`: autorización contrastada.
- `totalPen`: importe de **todas** las partidas que se emitirían.
- `marginPen`, `excessPen` y `canApprove`: resultado del contraste presupuestal. No sustituyen los demás controles de aprobación.

USD usa exclusivamente la tasa conservada por la simulación; no consulta otra tasa al aprobar ni acepta una enviada por el cliente. Conserva importe y moneda originales, fuente, tipo, publicación y consulta de la tasa. Si falta esa evidencia o el total emitible no coincide con el evaluado, se rechaza la operación. Una caída actual de SUNAT no impide reproducir una tasa ya preservada correctamente.

### Aprobar — ruta existente, jefe

```http
POST /api/v1/simulations/{runId}/quotations/{quotationId}/purchase-orders
```

```json
{
  "deliveryConditions": "Recepción en horario de almacén.",
  "deliveryDestination": "Almacén principal",
  "expectedBudgetVersion": 1
}
```

- Si la comprobación pertenece a una versión anterior: `409`, código `budget_check_outdated` y `currentBudgetVersion`. Consultar de nuevo; no es necesario extraer los PDF ni calcular los puntajes otra vez si sus insumos no cambiaron.
- Si excede el presupuesto o falta conversión: `422`, sin orden emitida.
- Si se omite `expectedBudgetVersion`, el servidor calcula y valida contra el presupuesto **actual**. Omitirlo nunca desactiva el control.
- La orden incorpora `policyEvidence`: proveedor/versión autorizados, presupuesto/versión aplicados, total PEN, margen y conversión utilizada. La moneda original de una orden en USD no se reemplaza por PEN.

El cálculo de presupuesto es común y acepta el conjunto de importes de una adquisición, evitando evaluar cada proveedor aisladamente. La emisión de varias órdenes por solicitud pertenece a **US21**, que no se implementa en esta entrega; no se habilita una vía parcial para eludir el límite.

## Concurrencia y persistencia

Las decisiones de proveedor, cambios de presupuesto y aprobaciones se coordinan con bloqueos PostgreSQL. La aprobación y su evidencia se guardan en la misma transacción; los reintentos recuperan la misma orden. La serialización de solicitud se mantiene durante la recuperación del estado posterior al commit, evitando que dos peticiones compitan por cerrar el mismo proceso.

| Esquema/tabla | Datos incorporados |
| --- | --- |
| `purchase_ordering.supplier_accounts` | UUID, RUC único, razón social, estado, versión y creación. |
| `purchase_ordering.supplier_eligibility_history` | Proveedor, estados anterior/nuevo, actor, fecha, motivo y versión única por proveedor. |
| `purchase_ordering.purchase_budgets` | Solicitud como clave, importe PEN y versión actual. |
| `purchase_ordering.purchase_budget_revisions` | Presupuesto, versión, importe, actor, fecha y motivo; versión única por solicitud. |
| `purchase_ordering.purchase_orders` | Columnas `policy_*` para evidencia inmutable de la aprobación. Las órdenes anteriores permanecen con evidencia nula, sin inventar autorizaciones históricas. |
| `evaluation_simulation.simulation_quotation_snapshots` | Cuenta del proveedor, estado y versión usados al calcular el resultado. |
| `evaluation_simulation.exclusion_reasons` | Criterio opcional para distinguir exclusiones de negocio de criterios configurados. |

## Pruebas

- `tests/SmartQuote.Domain.Tests/OrderingPolicyTests.cs`: reglas de transición, límites, suma completa, conversión e historial.
- `tests/SmartQuote.Integration.Tests/OrderingPolicyAcceptanceTests.cs`: escenarios US18/US20 por API y PostgreSQL, permisos, reactivación, reintentos y carreras de suspensión/reducción presupuestal/aprobación.
- Las pruebas previas preparan una habilitación real mediante el usuario jefe cuando necesitan ese estado. No existe habilitación automática en producción ni una excepción para la cuenta de demostración.

```powershell
pwsh -File tests/run-tests.ps1 -Story US18 -Level All
pwsh -File tests/run-tests.ps1 -Story US20 -Level All
pwsh -File tests/run-tests.ps1 -Level All
```

El ejecutor requiere Docker Desktop y utiliza una base temporal aislada; no aplicar las pruebas sobre la base de trabajo ni sobre Azure.
