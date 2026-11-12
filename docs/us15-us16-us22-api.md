# Backend: US15, US16 y US22

Las tres historias están implementadas en el backend. No se han añadido pantallas a web o móvil. Todos los endpoints requieren el JWT habitual; para crear aclaraciones, cambiar asignaciones y gestionar recurrencias, la cuenta también debe existir activa en IAM con el rol correspondiente.

## Puesta en marcha

Se incorporan dos migraciones aditivas: una en `SupplyRequestsDbContext` para colaboración, asignación y recurrencias; otra en `EvaluationSimulationDbContext` para la revisión técnica de escenarios y resultados. Se conservan las solicitudes y órdenes anteriores.

Si el arranque utiliza `Database:ApplyMigrations=true`, el backend las aplica al reiniciar con la versión nueva. También se pueden aplicar explícitamente, usando la configuración local habitual:

```powershell
dotnet ef database update --context SupplyRequestsDbContext --project src/SmartQuote.Modules.SupplyRequests --startup-project src/SmartQuote.API
dotnet ef database update --context EvaluationSimulationDbContext --project src/SmartQuote.Modules.EvaluationSimulation --startup-project src/SmartQuote.API
```

No se necesitan nuevas claves de OpenAI ni de SUNAT. La implementación se verificó en PostgreSQL temporal, sin aplicar migraciones a la base de trabajo.

## US22 — Responsable de la solicitud

1. El jefe obtiene los destinatarios con `GET /api/v1/iam/users?role=PurchaseAnalyst&status=Active&page=1&pageSize=20`.
2. Asigna con `PUT /api/v1/purchase-requests/{requestId}/assignment`:

```json
{
  "analystId": "UUID_REAL_DEL_ANALISTA",
  "reason": null,
  "expectedVersion": 1
}
```

3. Para reasignar se utiliza la misma ruta, otro analista, un motivo obligatorio y la versión actual. La respuesta devuelve la solicitud con `assignedAnalystId` y su nueva `version`.
4. Historial: `GET /api/v1/purchase-requests/{requestId}/assignment-history`.
5. Cola propia del analista: `GET /api/v1/purchase-requests?assignedToMe=true&page=1&pageSize=20`; admite el filtro `status` existente.

La asignación no cambia solicitante, estado ni revisión técnica, y no invalida simulaciones. No habilita al analista para aprobar órdenes ni para reasignar solicitudes.

## US15 — Aclaraciones técnicas

1. El analista registra `POST /api/v1/purchase-requests/{requestId}/clarifications`:

```json
{
  "itemId": "UUID_REAL_DEL_ITEM",
  "requirementId": "UUID_REAL_DEL_REQUISITO",
  "specialistId": "UUID_DEL_ESPECIALISTA_SOLICITANTE",
  "question": "¿El mínimo corresponde a proteína cruda, expresada en porcentaje?",
  "expectedVersion": 1
}
```

2. El aviso persistido aparece en `GET /api/v1/technical-clarifications?assignedToMe=true&status=Pending`. Cada consulta devuelve `id`, datos de origen, responsable, estado, respuesta y `version`. Solo su destinatario puede responder. En esta entrega el destinatario autorizado es el especialista que creó la solicitud; no se introduce una función de delegación adicional.
3. Opcionalmente marca lectura con `PUT /api/v1/technical-clarifications/{id}/read`. Es idempotente; recargar la consulta para obtener su versión vigente antes de otro comando.
4. Responde con `POST /api/v1/technical-clarifications/{id}/response`:

```json
{
  "answer": "Sí, el mínimo confirmado es 21% de proteína cruda.",
  "expectedVersion": 1
}
```

5. Compras consulta las respuestas en `GET /api/v1/purchase-requests/{requestId}/clarifications`. Responder no modifica un requisito ni permite incumplirlo.
6. Si corresponde cambiar la especificación, el destinatario confirma `POST /api/v1/technical-clarifications/{id}/requirement-revisions`:

```json
{
  "specification": {
    "name": "Proteína mínima",
    "operator": "GreaterThanOrEqual",
    "expectedValue": "21",
    "unitOfMeasure": "%",
    "isMandatory": true
  },
  "reason": "Especificación confirmada por el especialista.",
  "expectedClarificationVersion": 2,
  "expectedRequestVersion": 1
}
```

Los números de versión son ejemplos: utilizar siempre los devueltos por la consulta actual. Si otra revisión técnica cambió la interpretación de origen, registrar una nueva aclaración en lugar de aplicar una respuesta obsoleta.

7. Historial: `GET /api/v1/purchase-requests/{requestId}/requirement-revisions`. Conserva especificación anterior/nueva, autor, fecha y motivo. La solicitud publica `technicalRevision`.
8. Una simulación antigua queda `isCurrent=false` y no autoriza órdenes. Se debe crear una versión de criterios con los requisitos obligatorios actualizados mediante la ruta existente `/api/v1/evaluation-scenarios/{scenarioId}/versions` y ejecutar una nueva simulación. No basta con volver a ejecutar el escenario técnico anterior.

## US16 — Solicitudes recurrentes

El especialista registra `POST /api/v1/recurring-request-schedules`:

```json
{
  "startDate": "2026-11-01",
  "intervalDays": 7,
  "requiredDateOffsetDays": 2,
  "timeZoneId": "America/Lima",
  "priority": "Normal",
  "items": [
    {
      "description": "Alimento balanceado de crecimiento para pollos de engorde",
      "quantity": 1000,
      "unitOfMeasure": "kg",
      "requirements": [
        {
          "name": "Proteína cruda",
          "operator": "GreaterThanOrEqual",
          "expectedValue": "20",
          "unitOfMeasure": "%",
          "isMandatory": true
        }
      ]
    }
  ]
}
```

- Elegir una fecha de inicio actual o futura en la zona indicada. `startDate` es la primera fecha de **generación**; `requiredDateOffsetDays` añade días para obtener la fecha requerida. La siguiente generación avanza `intervalDays` días locales.
- Listado propio: `GET /api/v1/recurring-request-schedules`; detalle e historial: `GET /api/v1/recurring-request-schedules/{id}`.
- El trabajador genera `Draft`, no `Submitted`. El detalle de la programación incluye `occurrences` con el `requestId` generado. Los borradores también aparecen en las solicitudes del propietario; compras no puede consultarlos ni procesarlos.
- Editar con `PUT /api/v1/purchase-requests/{requestId}/draft`: mismo formato del registro normal (`requiredDate`, `priority`, `items`), añadiendo `expectedVersion`.
- Confirmar con `POST /api/v1/purchase-requests/{requestId}/submit`, cuerpo `{ "expectedVersion": 2 }`. Valida fecha, ítems y requisitos y genera el evento normal de envío.
- Suspender con `POST /api/v1/recurring-request-schedules/{id}/pause`, cuerpo con `expectedVersion` de la programación, no de la solicitud. Conserva fecha de suspensión, ocurrencias y solicitudes anteriores.

La plantilla se conserva como documento JSONB en el esquema de SupplyRequests; cada solicitud recibe ítems/requisitos con identidades nuevas. No se comparten entidades mutables con borradores anteriores. PostgreSQL garantiza una ocurrencia por programación/fecha y una solicitud por ocurrencia, y el bloqueo de fila serializa generación y suspensión.

### Trabajador de recurrencias

Opciones de servidor, todas opcionales:

```json
{
  "SupplyRequests": {
    "Recurrence": {
      "Enabled": true,
      "PollSeconds": 30,
      "MaxSchedulesPerCycle": 20,
      "MaxOccurrencesPerSchedule": 10
    }
  }
}
```

El trabajador requiere que el proceso del backend permanezca en ejecución. En Azure App Service se debe considerar `Always On` si el plan lo permite; no es un servicio externo que despierte una aplicación detenida. Tras una interrupción recupera ocurrencias por lotes acotados: omite las cuya fecha requerida ya pasó y conserva ese resultado en el historial. No envía solicitudes ni emite órdenes automáticamente.

## Pruebas

- Dominio: `tests/SmartQuote.Domain.Tests/RequestWorkflowTests.cs`.
- API y persistencia: `tests/SmartQuote.Integration.Tests/RequestWorkflowAcceptanceTests.cs`.
- Las pruebas tienen `Trait("Story", "US15")`, `US16` o `US22`, y el número del escenario. Incluyen concurrencia, borradores privados, reintentos, trabajador real y bloqueo de decisiones desactualizadas.

Con Docker Desktop iniciado, ejecución completa en una base temporal aislada:

```powershell
pwsh -File tests/run-tests.ps1 -Level All
```

Por historia:

```powershell
pwsh -File tests/run-tests.ps1 -Story US15 -Level All
pwsh -File tests/run-tests.ps1 -Story US16 -Level All
pwsh -File tests/run-tests.ps1 -Story US22 -Level All
```

En Rider se puede ejecutar la clase o método correspondiente desde el icono de pruebas. Las pruebas de integración necesitan la configuración PostgreSQL/JWT de pruebas descrita en `tests/README.md`; no usar una base de producción.
