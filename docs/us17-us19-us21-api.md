# US17, US19 y US21 — Contratos del backend

Implementación en el monolito modular existente. No incluye pantallas nuevas en web o móvil. Las rutas usan JWT y los roles existentes; no requiere claves nuevas ni llamadas adicionales a OpenAI.

## US17 — Vencimiento de cotizaciones (QuotationIntake)

El trabajador comprueba ofertas verificadas cuya fecha de vigencia está entre hoy y los próximos `LeadDays`, inclusive. Solo crea alertas para solicitudes activas, no adjudicadas y con analista asignado. Una oferta sigue vigente durante su último día en Perú; desde el día siguiente queda excluida del cálculo y una decisión anterior deja de ser aprobable. Leer una alerta nunca extiende la vigencia.

| Operación | Acceso | Resultado |
| --- | --- | --- |
| `GET /api/v1/quotation-expiry-alerts?unreadOnly=true&page=1&pageSize=50` | Analista | `200`, su bandeja; máximo 100 por página. |
| `PUT /api/v1/quotation-expiry-alerts/{id}/read` | Analista destinatario | `204`, idempotente. Otro analista recibe `403`. |
| `GET /api/v1/quotations/{id}` | Compras | Añade `isCurrent` y `commercialStatus`: `Current`, `Expired`, `ValidityUnknown` o `NotVerified`. |

Cada alerta conserva solicitud, cotización, proveedor, destinatario, vencimiento, anticipación, creación y lectura. La combinación cotización/fecha/anticipación es única incluso con varios trabajadores. Una reasignación activa transfiere la misma alerta y conserva su lectura, registrando `reassignedAt`; una solicitud cerrada conserva su historial, sin generar alertas nuevas.

Configuración opcional de servidor (variables de entorno equivalentes con `__`):

```json
{
  "QuotationExpiry": {
    "Enabled": true,
    "LeadDays": 3,
    "PollSeconds": 60,
    "BatchSize": 200,
    "TimeZone": "America/Lima"
  }
}
```

El trabajador procesa por lotes y reintenta en el siguiente ciclo ante errores. Las lecturas de la bandeja no generan alertas; no es necesario abrir una pantalla para activar el procesamiento. Mantener `America/Lima` para alinearlo con la vigencia comercial y SUNAT.

## US19 — Sensibilidad (EvaluationSimulation)

Requiere una solicitud en `Evaluation`, escenario activo y al menos dos ofertas elegibles. Solo el jefe accede al análisis y la promoción.

```http
POST /api/v1/evaluation-scenarios/{scenarioId}/sensitivity-analyses
```

```json
{
  "configurations": [
    {
      "weights": [
        { "criterionId": "UUID_DEL_CRITERIO_PRECIO", "weight": 20 },
        { "criterionId": "UUID_DEL_CRITERIO_PLAZO", "weight": 80 }
      ]
    }
  ]
}
```

Obtener los UUID del escenario existente. Incluir cada criterio ponderado exactamente una vez, con pesos entre 0 y 100 que sumen 100. No incluir criterios obligatorios. Si existen ponderaciones técnicas, se conservan sin cambios; las variaciones son comerciales. El máximo predeterminado es 20 variantes (`Sensitivity:MaxConfigurations`, limitado a 100).

Respuesta `201`: `analysis`, `isCurrent`, `isStable` y `scope`. Se conserva también la configuración base, identificada por `analysis.inputs.baselineConfigurationId`: no asumir un orden de filas al consultar datos persistidos. Cada configuración incluye pesos, todos los puntajes/exclusiones, `tiedWinnerIds` y recomendación con desempate determinista por UUID. La estabilidad y las conclusiones corresponden únicamente a las configuraciones efectivamente probadas, no a un umbral continuo inferido.

Los datos técnicos/comerciales y sus versiones se capturan una vez; si hay USD se consulta una tasa oficial para todo el análisis. Consultarlo o promoverlo reutiliza la evidencia almacenada, sin consultar SUNAT por variante ni volver a extraer PDFs.

| Operación | Resultado |
| --- | --- |
| `GET /api/v1/sensitivity-analyses/{analysisId}` | `200`, evidencia y vigencia actual. |
| `GET /api/v1/purchase-requests/{requestId}/sensitivity-analyses` | `200`, hasta 50 análisis recientes. |
| `POST /api/v1/sensitivity-analyses/{analysisId}/configurations/{configurationId}/official-simulation` con `{"expectedCriteriaVersion":1}` | `201`, `scenarioId` y `simulationRunId` oficiales. |

El análisis no sobrescribe criterios ni es un identificador válido para emitir órdenes. La promoción verifica que los insumos no cambiaron, calcula primero y guarda en una transacción la nueva versión y el resultado; un rechazo no sustituye el escenario anterior. Pesos inválidos o candidatas insuficientes devuelven `422` sin análisis parcial; promoción desactualizada devuelve `409`. Después de promover se utiliza la aprobación normal, con sus controles de proveedor, presupuesto y vigencia.

## US21 — Distribución por ítem (EvaluationSimulation y PurchaseOrdering)

```http
POST /api/v1/evaluation-scenarios/{scenarioId}/distributed-simulations
```

Sin cuerpo. Analista o jefe; `201` nuevo o `200` si recupera la misma decisión. Requiere varios ítems, al menos dos cotizaciones verificadas y vigentes en el conjunto, reglas obligatorias de cada ítem y ponderaciones comerciales válidas.

`GET /api/v1/simulations/{runId}` y el historial existente añaden `mode` y `distribution`; el modo anterior se conserva como `SingleSupplier`. Para `DistributedByItem`:

- Se compara cada ítem por separado, usando su cantidad solicitada y sus requisitos, no el precio de toda la oferta ni toda la cantidad ofertada.
- Una sola línea debe cubrir la cantidad completa. No se suman cantidades entre proveedores, convierten unidades implícitamente ni asocian líneas ambiguas.
- La normalización y el cálculo reutilizan el motor oficial. Se excluyen proveedores no habilitados, ofertas vencidas, incumplimientos técnicos y cantidades insuficientes.
- `allocations` conserva ítem, línea/cotización, proveedor y versión de habilitación, cantidad, moneda/precio/importe originales, precio/importe PEN, puntaje y plazo. USD utiliza la misma tasa oficial conservada en la simulación.
- `candidates` informa resultados y exclusiones por ítem; `uncoveredItemIds` y `canApprove=false` permiten mostrar un resultado incompleto sin autorizarlo.
- Los importes distribuidos se redondean por asignación a cuatro decimales, como la conversión existente. El precio PEN unitario conserva su precisión; el total es la suma de asignaciones, no una tasa nueva obtenida al aprobar.

Antes de aprobar, consultar el presupuesto global:

```http
GET /api/v1/simulations/{runId}/purchase-award-budget-check
```

Devuelve `assessment` (incluye versión, total, margen/exceso y estado `NotConfigured` cuando no hay presupuesto), huella y tasa. La ruta anterior `/budget-check?quotationId=...` sigue siendo para la compra normal.

```http
POST /api/v1/simulations/{runId}/purchase-awards
Idempotency-Key: UNA_CLAVE_ESTABLE_PARA_ESTA_APROBACION
```

```json
{
  "deliveryConditions": "Recepción de 08:00 a 16:00",
  "deliveryDestination": "Almacén de la granja",
  "expectedBudgetVersion": 1,
  "expectedInputFingerprint": "HUELLA_DEVUELTA_POR_LA_SIMULACION"
}
```

Solo el jefe. Los campos de versión/huella son opcionales para clientes compatibles, pero los controles siempre se ejecutan en servidor. Usar `expectedBudgetVersion: 0` cuando no hay presupuesto. La clave admite de 1 a 100 caracteres: conservarla junto con el mismo cuerpo en reintentos.

Respuesta `201`: `award` y `orders`. Se genera exactamente una orden por proveedor, todas en PEN, con la misma adjudicación/aprobación y las referencias originales en cada línea. `quotationId` en la cabecera distribuida es `null`, porque no representa una única cotización. No se inventa un UUID. La orden conserva su documento y su evaluación de entrega individuales.

La aprobación bloquea solicitud y proveedores, revalida las condiciones con filas de solicitud, criterios y cotizaciones bloqueadas, contrasta el presupuesto de todo el conjunto y guarda adjudicación, órdenes y estado `Ordered` en la misma transacción PostgreSQL. Las notificaciones/auditorías se conservan en `ordering_outbox`: se entregan después del commit, con reintentos y confirmación transaccional para no duplicarlas. Un fallo previo al commit revierte el conjunto completo.

| Situación/ruta | Resultado |
| --- | --- |
| Reintento idéntico con la misma clave | `200`, misma adjudicación e IDs de todas las órdenes. |
| Misma clave con otro cuerpo, presupuesto cambiado o adquisición ya adjudicada | `409`. |
| Cobertura incompleta, exceso presupuestal u oferta/decisión no vigente | Rechazo sin órdenes nuevas (`422`; conflictos de versión se informan con `409`). |
| `GET /api/v1/purchase-awards/{awardId}` | `200`, conjunto completo. |
| `GET /api/v1/purchase-requests/{requestId}/purchase-award` | `200`, conjunto de la solicitud. |
| Ruta singular antigua por solicitud/simulación cuando existen varias órdenes | `409 multiple_purchase_orders`, incluye `awardId`. No devuelve una orden arbitraria. |

La aprobación normal conserva su ruta y su moneda original, pero también registra una adjudicación y usa la misma coordinación transaccional. Una solicitud solo puede tener una adjudicación, independientemente del modo. Las métricas cuentan una sola muestra temporal por adquisición distribuida; no atribuyen falsos ahorros de oferta completa a ganadores por ítem. `orderCount` continúa contando documentos emitidos.

## Persistencia y migraciones

Aplicar las migraciones de QuotationIntake, EvaluationSimulation y PurchaseOrdering usando los comandos del README, o arrancar la API con `Database:ApplyMigrations=true`. Respaldar primero cualquier base con datos reales. Las comprobaciones de desarrollo se realizan exclusivamente en PostgreSQL temporal.

| Esquema / tabla | Datos / restricciones nuevas |
| --- | --- |
| `quotation_intake.quotation_expiry_alerts` | UUID PK; cotización, solicitud y destinatario UUID; proveedor; vigencia `date`; anticipación; fechas de creación/lectura/reasignación. Único `(quotation_id, valid_until, lead_days)`; índice de destinatario/lectura. |
| `evaluation_simulation.simulation_quotation_snapshots` | `valid_until` nullable para no inventar fechas de snapshots históricos. |
| `evaluation_simulation.simulation_runs` | `mode`, predeterminado `SingleSupplier`; `distribution` JSONB inmutable con candidatos/asignaciones/cobertura. |
| `evaluation_simulation.sensitivity_analyses` | UUID PK; escenario, solicitud, versión, huella, actor/fecha; `inputs` JSONB con requisitos, líneas, versiones y configuración base; `exchange_rate` JSONB. |
| `evaluation_simulation.sensitivity_configurations` | UUID PK y FK al análisis; pesos, resultados y empates JSONB; recomendación. Separada de `simulation_runs`. |
| `purchase_ordering.purchase_awards` | UUID PK; solicitud y simulación únicas; modo, huella, clave idempotente única y hash de cuerpo; aprobación; total PEN; versión/límite/margen presupuestal; evidencia cambiaria. |
| `purchase_ordering.purchase_award_allocations` | PK `(award_id, requested_item_id)`; FK adjudicación; proveedor y fuentes UUID; cantidad/unidad; moneda/precio originales; precio/importe PEN. |
| `purchase_ordering.purchase_orders` | `award_id` FK y `award_supplier_id`; único `(award_id, award_supplier_id)`; fuente de cotización nullable para distribución. Índices de solicitud/simulación dejan de ser únicos: la unicidad está en la adjudicación. |
| `purchase_ordering.purchase_order_lines` | Referencia de cotización, moneda/precio originales; precio emitido admite `numeric(28,12)` para conservar la conversión. |
| `purchase_ordering.ordering_outbox` | UUID PK; tipo y contenido JSONB del evento, creación y confirmación de entrega; índice por pendiente. |

La migración convierte las órdenes históricas en adjudicaciones normales conservando sus IDs, números, origen y claves previas. Si una orden no-PEN antigua carece de evidencia cambiaria, la migración se detiene con un mensaje explícito en lugar de inventar un total; conciliar la evidencia antes de reintentar. La reversión de la migración se bloquea si ya existen adjudicaciones distribuidas, para no perder su trazabilidad.

## Pruebas para Rider y consola

- Dominio: `tests/SmartQuote.Domain.Tests/AdvancedPurchasingTests.cs`.
- HTTP/PostgreSQL: `tests/SmartQuote.Integration.Tests/AdvancedPurchasingAcceptanceTests.cs`.
- Cada prueba tiene `Trait("Story", "US17"/"US19"/"US21")`, escenario y comentario. Se pueden ejecutar desde el icono de prueba de Rider.

```powershell
pwsh -NoProfile -File tests/run-tests.ps1 -Story US17 -Level All
pwsh -NoProfile -File tests/run-tests.ps1 -Story US19 -Level All
pwsh -NoProfile -File tests/run-tests.ps1 -Story US21 -Level All
pwsh -NoProfile -File tests/run-tests.ps1 -Level All
```

El ejecutor crea y elimina su propio PostgreSQL temporal; no consume OpenAI ni consulta SUNAT real. Incluye pruebas de reintentos simultáneos, cobertura incompleta, presupuesto global, fallo después de insertar las órdenes y fallo durante la transición de solicitud. Los archivos TRX quedan en `artifacts/test-results`.
