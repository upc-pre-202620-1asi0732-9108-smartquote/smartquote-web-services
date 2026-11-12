# Plan de implementación del backend — US15 a US22

Fecha de análisis: 20 de octubre de 2026.

## 1. Alcance y punto de partida

Este documento planifica las ocho historias nuevas de la sección 3.2 del reporte actual. No implementa cambios ni modifica las historias, el Product Backlog, los diagramas o los clientes. Los nombres de clases, tablas y rutas nuevas que aparecen aquí son propuestas de implementación, no elementos ya disponibles.

Se conserva el monolito modular con Clean Architecture y DDD: `SupplyRequests`, `QuotationIntake`, `EvaluationSimulation`, `PurchaseOrdering` e `IdentityAccess`. No se requieren nuevos bounded contexts, microservicios ni un API Gateway para estas historias.

### Hallazgos del código que condicionan el plan

| Código existente | Situación comprobada | Consecuencia para el plan |
| --- | --- | --- |
| `SupplyRequests/.../PurchaseRequest.cs` y `PurchaseRequestCommandService.cs` | La creación registra directamente una solicitud `Submitted`. Existe el valor `Draft`, pero no el ciclo de edición y envío del borrador recurrente. | US16 necesita ese ciclo; no basta con ejecutar periódicamente el registro actual. |
| `PurchaseRequestQueryService.cs` | El especialista consulta sus propias solicitudes; no existe responsable analista ni filtro de asignación. | US22 añade asignaciones sin reemplazar al solicitante ni ampliar permisos. |
| `TechnicalRequirement.cs` y `RequestedItem.cs` | No existe el proceso de aclaración ni la revisión histórica de una especificación. | US15 necesita cambios explícitos y versionados, no editar valores desde una respuesta de texto. |
| `PoultryQuote.cs` y `VerifiedQuotationSnapshot.cs` | La cotización conserva `ValidUntil`, pero el contrato publicado para simulación no la incluye. | Debe publicarse la vigencia y validarse tanto al simular como al aprobar. |
| `PoultryQuoteRepository.GetVerifiedForRequestAsync` | Filtra por `Verified`, no por fecha de vencimiento. | Una cotización verificada no debe confundirse con una cotización actualmente vigente. |
| `SimulationEngine.cs` | Evalúa y recomienda una cotización completa; no construye adjudicaciones por ítem ni análisis de sensibilidad. | US19 reutiliza el cálculo; US21 necesita un modo por ítem, no dividir el resultado actual artificialmente. |
| `EvaluationDataset.CalculateFingerprint` | Ya contempla el contenido técnico de la solicitud, versiones de cotizaciones y tipo de cambio. | Ampliar la comprobación con habilitación y vigencia; no invalidar cálculos por leer alertas o reasignar responsables. |
| `SimulationApplicationService.cs`, `SunatExchangeRateProvider.cs`, `ExchangeRateSnapshot.cs` | Ya existe conversión USD/PEN y evidencia persistida de la tasa oficial. | Reutilizarla. No implementar otro consumidor de SUNAT para estas historias. |
| `ApprovedSimulationSnapshot.cs` | Publica líneas e importe original, pero no la evidencia completa del tipo de cambio y total comparable. | Ampliar el contrato entre evaluación y órdenes para presupuesto y adjudicación distribuida. |
| `PurchaseOrderApplicationService.cs` y configuración EF de órdenes | Hay comprobaciones e índices únicos de una orden por solicitud y por simulación. | US21 exige una adjudicación que agrupe varias órdenes y sustituya esas restricciones sin permitir compras duplicadas. |
| `OrderRequestLifecycleAdapter.cs` | La orden se persiste antes de actualizar la solicitud a `Approved`/`Ordered`. | La aprobación distribuida necesita una frontera transaccional explícita; varios `SaveChangesAsync` independientes no son atomicidad. |
| `AuthController.cs` y `UserAccount.cs` | Existen cuentas activas, roles y aprobación de registros; no un directorio de analistas activos para asignar. | Reutilizar IAM y publicar un contrato mínimo de consulta. |
| `RequestNotificationsController.cs` | Las notificaciones actuales son para producción y contienen cambios de estado de solicitudes. | No reutilizar ese contrato incompatible para alertas comerciales de analistas. |

Las rutas de código de esta tabla se encuentran dentro de `src/SmartQuote.Modules.<Contexto>/`, salvo los adaptadores y analíticos que pertenecen a `src/SmartQuote.API/`.

## 2. Propiedad de las reglas y límites de arquitectura

| Bounded context | Responsabilidad que se incorpora | Historias |
| --- | --- | --- |
| **SupplyRequests** | Aclaraciones y revisiones técnicas; programaciones y borradores; responsable actual e historial de asignaciones. | US15, US16, US22; publica información para US17, US20 y US21. |
| **QuotationIntake** | Vigencia de cotizaciones y alertas preventivas. Conserva la evidencia del proveedor que figura en el documento. | US17; aporta ofertas para US18–US21. |
| **EvaluationSimulation** | Comparabilidad, sensibilidad, evaluación por ítem y evidencia reproducible de cada cálculo. | US19, US21; adapta vigencia para US15/17/18 y resultados monetarios para US20. |
| **PurchaseOrdering** | Habilitación interna de proveedores; presupuesto autorizado; aprobación y adjudicación con una o varias órdenes. | US18, US20, US21; publica si una solicitud ya está adjudicada. |
| **IdentityAccess** | Consulta de identidad, estado activo y rol para destinatarios y asignaciones. | Soporte de US15 y US22. |
| **Shared** | Únicamente primitivas genéricas que sean necesarias: tiempo, errores, concurrencia o infraestructura común. | No almacena presupuestos, proveedores, aclaraciones ni reglas de adjudicación. |

### Dependencias y contratos

- Dominio: reglas, entidades y objetos de valor propios; sin EF Core, HTTP, controladores ni entidades de otros módulos.
- Aplicación: casos de uso y puertos del consumidor; obtiene del servidor al actor y la fecha. El cliente no decide el rol, autor, habilitación, tasa ni total definitivo.
- Infraestructura: repositorios, EF, temporizadores, bloqueos y adaptadores de los puertos.
- Interfaces: recursos REST, autorización, validación de formato, traducción de respuestas y OpenAPI.
- La composición de adaptadores entre módulos corresponde a `SmartQuote.API/Integration`, siguiendo el patrón actual de `OrderRequestLifecycleAdapter`.
- No añadir referencias circulares. Actualmente `PurchaseOrdering` referencia `EvaluationSimulation`, y este referencia `QuotationIntake` y `SupplyRequests`. Por tanto, una consulta de habilitación desde evaluación se define como puerto en evaluación y se conecta en API con un contrato publicado por órdenes; **no** se añade una referencia inversa a su proyecto.
- Publicar DTO de lectura para usuarios, asignación, vigencia, habilitación y adjudicación; no compartir agregados, repositorios ni `DbContext` entre dominios.

### Decisiones transversales de implementación

1. **Tiempo:** usar `TimeProvider`, fechas UTC para eventos y zona de operación configurable, inicialmente `America/Lima`. Resolver correctamente la zona en Windows y Linux. Las fechas comerciales `DateOnly` se interpretan en esa zona, no en la zona del servidor.
2. **Concurrencia:** comandos de modificación reciben `expectedVersion`; conflictos devuelven `409`. Las restricciones únicas de PostgreSQL complementan las validaciones de aplicación.
3. **Historial:** registrar cambios de requisitos, responsable, proveedor y presupuesto con actor, fecha, motivo y versiones. Los resultados históricos son inmutables; su vigencia se calcula sin reescribir lo que se decidió entonces.
4. **Moneda:** reutilizar `ExchangeRateSnapshot` con importe/moneda originales, tasa, tipo, fuente, publicación y consulta. Usar `decimal` y la política existente de cuatro decimales y `AwayFromZero`; totalizar consistentemente los importes de líneas. La presentación a dos decimales no cambia la precisión de validación.
5. **Vigencia:** una oferta con `ValidUntil` válido vence al terminar esa fecha local. Si falta la fecha o no está verificada, no es elegible. Una lectura histórica puede mostrarla, pero no autorizar otra compra.
6. **Compatibilidad:** preservar el flujo normal de una cotización ganadora, sus rutas y contratos válidos. Agregar rutas específicas para nuevos recursos y campos documentados; no convertir silenciosamente un objeto en una lista.
7. **Errores:** `400` para formato; `401/403` para acceso; `404` para inexistencia; `409` para versiones e idempotencia incompatible; `422` para reglas de negocio; `503` para indisponibilidad de SUNAT cuando se necesita una tasa nueva. Incluir códigos estables y explicaciones entendibles.
8. **Notificaciones:** para cumplir estas historias basta una bandeja persistida consultable por REST. No introducir correo, notificaciones push, SignalR ni estimaciones ficticias de entrega.

## 3. US15 — Resolver aclaraciones técnicas

### Contextos y componentes

- **Principal: SupplyRequests.** Crear `TechnicalClarification` y su estado, respuesta y referencias a solicitud, ítem y requisito. Extender `PurchaseRequest` con una operación explícita de revisión técnica y conservar `RequirementRevision`.
- **IdentityAccess:** publicar consulta mínima de usuario activo/rol. El destinatario será el especialista solicitante o un especialista cuya autorización sobre esa solicitud esté registrada; ser especialista no habilita automáticamente a leer solicitudes ajenas.
- **EvaluationSimulation:** verificar la revisión técnica actual antes de reutilizar o aprobar una simulación. Si cambian los requisitos, una nueva versión de criterios debe reflejarlos; no copiar criterios obligatorios antiguos sin reconciliarlos.
- **PurchaseOrdering:** mantener el bloqueo de aprobación cuando la decisión se basa en una especificación anterior.

### Implementación por capas y persistencia

1. Crear `TechnicalClarificationService` con registrar, consultar, responder y confirmar cambio técnico como comandos separados. El ítem y requisito deben pertenecer a la solicitud indicada; validar que esté activa y no adjudicada.
2. Guardar pregunta, autor, destinatario, fecha, estado y revisión técnica de origen. Guardar respuesta, responsable y fecha sin sustituir la pregunta.
3. Persistir el aviso al destinatario en la misma transacción del registro. Añadir una bandeja de aclaraciones independiente del recurso actual de notificación de cambios de estado.
4. Al responder, marcar `Answered`, pero no modificar requisitos, no levantar restricciones y no confirmar automáticamente una cotización.
5. Para aplicar una especificación nueva, validar valor, operador, unidad y obligatoriedad mediante las reglas del dominio. Conservar identidad del requisito, valores anteriores y nuevos, motivo y referencia a la aclaración.
6. Incrementar revisión técnica y versión de concurrencia. Invalidar la vigencia de decisiones afectadas y exigir nueva evaluación con los requisitos actualizados, aunque la cotización siga verificada. Una modificación de requisitos no supone que la oferta ya los satisfaga.
7. Tablas propuestas en `supply_requests`: `technical_clarifications`, `requirement_revisions` y `clarification_notifications`; claves locales y referencias UUID a identidades externas.

### Contrato REST propuesto

- `POST /api/v1/purchase-requests/{requestId}/clarifications`: analista; `itemId`, `requirementId`, `specialistId`, `question`, `expectedVersion`; `201`.
- `GET /api/v1/purchase-requests/{requestId}/clarifications`: personal de compras autorizado o especialista destinatario, con el contexto permitido; `200`.
- `GET /api/v1/technical-clarifications?assignedToMe=true&status=Pending`: bandeja del especialista; `200`.
- `POST /api/v1/technical-clarifications/{id}/response`: destinatario autorizado; `answer`, `expectedVersion`; `200`.
- `POST /api/v1/technical-clarifications/{id}/requirement-revisions`: especialista autorizado; especificación completa, motivo y versiones de consulta/solicitud; `201`.

### Cobertura de aceptación

| Escenario | Prueba prevista y resultado verificable |
| --- | --- |
| E1 — Registro | `US15_E1_RegistersClarificationAndRecipientNotice`: persistir vínculos, actor, fecha y aviso; rechazar referencias de otra solicitud y destinatarios no autorizados. |
| E2 — Respuesta | `US15_E2_OnlyRecipientAnswersAndAnalystReadsContext`: conservar respuesta y estado; otro especialista recibe `403`; leer no altera requisitos. |
| E3 — Nueva especificación | `US15_E3_RevisionPreservesHistoryAndInvalidatesApproval`: mantener valores anteriores; rechazar orden desde simulación antigua; nueva evaluación aplica el requisito actualizado sin dispensar obligatoriedad. |

## 4. US16 — Programar solicitudes recurrentes

### Contextos y componentes

- **Principal: SupplyRequests.** `RecurringRequestSchedule`, `ScheduleOccurrence`, métodos de borrador/revisión/envío de `PurchaseRequest`, servicio de programación y trabajador periódico en infraestructura.
- **IdentityAccess:** validar al propietario cuando confirma el envío; la identidad de una ejecución automática no se hace pasar por el especialista.
- **QuotationIntake/EvaluationSimulation/PurchaseOrdering:** no consumen borradores como solicitudes enviadas. No se agregan automatismos de cotización ni de compra.

### Implementación por capas y persistencia

1. Programación con propietario, plantilla de ítems/requisitos, fecha de primera generación, periodicidad positiva, anticipación de fecha requerida, zona horaria, estado, próxima ocurrencia y versión. Propuesta inicial: periodicidad expresada en días y anticipación no negativa en días; se explicita la unidad en el contrato.
2. Validar la plantilla con las mismas reglas de registro. Guardarla como copia propia de la programación, no como referencias mutables a una solicitud anterior ni copiando adjuntos ajenos.
3. Añadir `CreateDraft`, edición del borrador y `Submit` a la solicitud. Mantener el `POST` actual de registro y su comportamiento `Submitted`. El borrador generado pertenece a su especialista, no aparece en la cola de adquisiciones y no genera `PurchaseRequestSubmitted` hasta confirmarse.
4. El trabajador procesa ocurrencias vencidas con exclusión entre instancias. Crear ocurrencia, borrador y avance de próxima fecha en una transacción. Índice único `(schedule_id, occurrence_key)` y una sola solicitud vinculada por ocurrencia.
5. La clave identifica la fecha programada, no la hora real del reintento. Ante carrera o reejecución se devuelve/identifica el borrador existente, sin reconstruirlo ni borrar ediciones humanas.
6. Suspender con bloqueo y comprobación de versión. La transacción de generación vuelve a comprobar el estado: no crea ocurrencias posteriores al instante efectivo de suspensión. Mantener historial y solicitudes anteriores.
7. Política propuesta ante caída del trabajador: recuperar de forma acotada ocurrencias cuya fecha requerida aún sea válida; registrar como omitidas las ya vencidas. Nunca enviar borradores automáticamente ni crear cientos de solicitudes en un reinicio. El número máximo por ciclo será configurable y documentado.
8. Tablas propuestas: `recurring_request_schedules`, sus ítems/requisitos de plantilla, `schedule_occurrences` e historial; relación opcional de la solicitud con la ocurrencia.

### Contrato REST propuesto

- `POST /api/v1/recurring-request-schedules`: especialista; plantilla, `startDate`, `intervalDays`, `requiredDateOffsetDays`, `timeZoneId`; `201`.
- `GET /api/v1/recurring-request-schedules` y `GET /{id}`: programaciones propias, próxima ocurrencia e historial; `200`.
- `POST /api/v1/recurring-request-schedules/{id}/pause`: propietario, `expectedVersion`; `200`.
- `PUT /api/v1/purchase-requests/{requestId}/draft`: propietario; datos y `expectedVersion`; `200`, solo `Draft`.
- `POST /api/v1/purchase-requests/{requestId}/submit`: propietario; `expectedVersion`; `200`, valida nuevamente fecha, cantidades e ítems/requisitos.
- Consultar borradores y procedencia con el listado/detalle de solicitudes, sin cambiar su visibilidad para otros roles. No exponer un endpoint público para disparar el trabajador.

### Cobertura de aceptación

| Escenario | Prueba prevista y resultado verificable |
| --- | --- |
| E1 — Programación | `US16_E1_StoresValidScheduleAndNextLocalOccurrence`: zona y próxima ocurrencia correctas; intervalo inválido rechazado; ninguna orden creada. |
| E2 — Revisión | `US16_E2_GeneratesDraftAndRequiresExplicitSubmit`: copia completa de plantilla; compras no lo ve; sin evento de envío hasta confirmación válida del propietario. |
| E3 — Reintento | `US16_E3_ConcurrentOccurrenceIsUniqueAndPreservesEdits`: dos trabajadores y un reintento generan una sola solicitud y conservan las ediciones del especialista. |
| E4 — Suspensión | `US16_E4_PauseStopsFutureOccurrencesWithoutDeletingHistory`: avanzar el reloj después de suspender no crea nuevas solicitudes; comprobar carrera pausa/generación. |

## 5. US17 — Alertas anticipadas de vencimiento

### Contextos y componentes

- **Principal: QuotationIntake.** Política de vigencia, `QuotationExpiryAlert`, servicio de consulta/lectura y trabajador periódico.
- **SupplyRequests:** publica estado y asignación vigente de US22.
- **PurchaseOrdering:** publica la existencia de una adjudicación, incluso si una proyección de estado aún no se actualizó.
- **EvaluationSimulation y PurchaseOrdering:** verifican vigencia en cada intento de uso; no dependen de que el trabajador haya ejecutado la alerta.

### Implementación por capas y persistencia

1. Configurar zona operativa y anticipación en días. Usar la misma regla temporal para notificaciones, simulación y aprobación.
2. Seleccionar cotizaciones `Verified` vigentes dentro de la ventana, con solicitud activa no adjudicada y analista asignado. Sin analista no inventar destinatario ni avisar a todos.
3. Persistir solicitud, cotización, proveedor, vencimiento, anticipación aplicada, responsable, creación y `ReadAt`. Restricción única `(quotation_id, valid_until, lead_days)`; las lecturas nunca crean alertas.
4. Repeticiones mantienen el registro y su lectura. Propuesta para una reasignación: transferir la responsabilidad de la misma alerta y auditar el cambio, conservando su estado de lectura; no crear una segunda alerta por el mismo vencimiento. Los avisos cerrados conservan el destinatario histórico.
5. Al finalizar la condición preventiva, cerrar el seguimiento sin eliminar evidencia. El detalle de la cotización informa `IsCurrent`/motivo de vencimiento, calculado con el reloj actual.
6. Ampliar `VerifiedQuotationSnapshot` y sus adaptadores con `ValidUntil`. Un resultado histórico conserva sus ofertas, pero su aprobación revalida la fecha actual aunque no haya cambiado `Version`.
7. Tabla propuesta `quotation_intake.quotation_expiry_alerts`, con índices para destinatario, seguimiento activo y consultas no leídas.

### Contrato REST propuesto

- `GET /api/v1/quotation-expiry-alerts?unreadOnly=true`: analista, solo alertas bajo su responsabilidad; `200`.
- `PUT /api/v1/quotation-expiry-alerts/{alertId}/read`: responsable; idempotente, `204`.
- Ampliar el detalle de cotización con su condición comercial de vigencia. Conservar intacto el recurso actual `/api/v1/notifications` de producción.

### Cobertura de aceptación

| Escenario | Prueba prevista y resultado verificable |
| --- | --- |
| E1 — Detección | `US17_E1_AlertsAssignedAnalystInsideLocalWindow`: contenido completo y destinatario correcto; probar medianoche local y ausencia de asignación. |
| E2 — Duplicados | `US17_E2_RepeatedChecksPreserveSingleAlertAndReadState`: dos ejecuciones concurrentes y consultas dejan una alerta con el mismo `ReadAt`. |
| E3 — Finalización | `US17_E3_StopsAlertsAndRejectsExpiredQuotationForApproval`: cierre por inactividad, adjudicación o vencimiento; historial intacto; oferta vencida no se aprueba ni se prorroga. |

## 6. US18 — Habilitar o suspender proveedores

### Contextos y componentes

- **Principal: PurchaseOrdering.** Agregado `SupplierAccount` con estado interno `Pending`, `Enabled` o `Suspended`, versión monotónica e historial de decisiones.
- **QuotationIntake:** conserva el proveedor extraído como evidencia; registra o vincula su identidad comercial mediante un puerto, sin asignarle habilitación por IA.
- **EvaluationSimulation:** consulta estado/versión de habilitación para elegibilidad y vigencia de recomendaciones.
- **IdentityAccess:** se reutiliza la autorización de jefe de adquisiciones. No se añade un rol nuevo.

### Implementación por capas y persistencia

1. Separar identidad comercial del proveedor de los datos que aparecen en cada PDF. Vincular por identificador fiscal normalizado y validado; no deduplicar por nombre ni por textos de demostración.
2. Registrar proveedores identificados inicialmente `Pending`. Si no existe identidad fiscal válida, conservar la cotización para corrección y no inventar una identidad habilitada.
3. Operaciones `Enable`, `Suspend` y `Reactivate` validan transición, motivo, actor y versión. La respuesta identifica que se trata de habilitación interna de SmartQuote, no de certificación de SUNAT.
4. Publicar un snapshot de elegibilidad `{ supplierId, status, eligibilityVersion }`. Incluir su versión en la evidencia de evaluación; suspender y reactivar incrementa la versión dos veces, por lo que no revive una simulación antigua.
5. Revalidar en aprobación normal y distribuida, dentro de la transacción de adjudicación. Un resultado anterior o una modificación del cliente no evita este control.
6. Los estados históricos de órdenes no se modifican. Las recomendaciones pendientes se presentan como no vigentes cuando cambia la habilitación que utilizaron y requieren repetir la evaluación.
7. Tablas propuestas en `purchase_ordering`: `supplier_accounts`, `supplier_eligibility_history` y vínculos con referencias existentes. Índice único por identificador fiscal válido.
8. Migración: inventariar y conciliar proveedores históricos por identificador fiable; los que necesiten habilitación quedan pendientes de decisión humana para nuevas compras. Preservar los snapshots de órdenes anteriores y resolver duplicados antes de activar el índice; no habilitar masivamente por conveniencia de la demostración.

### Contrato REST propuesto

- `GET /api/v1/suppliers?status=Pending` y `GET /api/v1/suppliers/{supplierId:guid}`: compras; catálogo paginado, condición e historial autorizado.
- `POST /api/v1/suppliers/{supplierId:guid}/enable`, `/suspend` y `/reactivate`: jefe; `reason`, `expectedVersion`; `200`.
- Mantener `GET /api/v1/suppliers/{taxIdentifier}/performance`. Evitar colisiones usando restricciones de ruta y no cambiar su cálculo de evaluaciones de entrega.

### Cobertura de aceptación

| Escenario | Prueba prevista y resultado verificable |
| --- | --- |
| E1 — Habilitación | `US18_E1_ManagerEnablesPendingSupplierWithHistory`: anterior/nuevo estado, motivo, actor y fecha; analista no habilita. |
| E2 — Suspensión | `US18_E2_SuspensionInvalidatesPendingDecisionNotApprovedOrders`: recomendación obsoleta; historial de simulación y órdenes previas intactos. |
| E3 — Bloqueo | `US18_E3_PendingOrSuspendedSupplierCannotReceiveNewAward`: probar ambos estados, decisión antigua y carrera suspensión/aprobación; cero órdenes indebidas. |
| E4 — Reactivación | `US18_E4_ReactivationKeepsHistoryWithoutRevivingOldRun`: vuelve a habilitado con versión nueva; exige nueva evaluación, sin aprobación automática. |

## 7. US19 — Sensibilidad de la recomendación

### Contextos y componentes

- **Principal: EvaluationSimulation.** `SensitivityAnalysis`, configuraciones evaluadas y `SensitivityAnalysisService`; extraer únicamente el cálculo reutilizable del motor actual a un servicio de dominio puro.
- **QuotationIntake/SupplyRequests:** snapshots de ofertas y requisitos ya publicados, con las extensiones de vigencia.
- **PurchaseOrdering:** rechazar identificadores de análisis como fuentes de aprobación; solo consume decisiones oficiales vigentes.

### Implementación por capas y persistencia

1. Recibir el escenario base y una lista explícita de configuraciones comerciales. Validar antes de ejecutar: criterios existentes, pesos entre 0 y 100, total 100 y ausencia de cambios en restricciones obligatorias. Aplicar un límite configurable de configuraciones para acotar trabajo; no generar un barrido infinito.
2. Capturar una sola vez dataset, revisión técnica, versiones de proveedores, criterios y tipo de cambio aplicable. Si hay USD, reutilizar la tasa de la simulación base vigente o adquirir una única tasa oficial para la ejecución base; todas las variaciones usan exactamente esa evidencia.
3. Evaluar variaciones con las mismas funciones de elegibilidad, normalización y desempate del motor oficial. No llamar a OpenAI ni a SUNAT por variación. Conservar pesos, puntajes, alternativas recomendadas y empates, no solamente un resumen de estabilidad.
4. Comparar contra la configuración base y entre configuraciones. Explicar dónde cambia el proveedor y limitar las conclusiones a los casos realmente evaluados; no afirmar un umbral continuo a partir de dos muestras.
5. Persistir un análisis separado de `SimulationRun`: tablas `sensitivity_analyses`, `sensitivity_configurations` y sus resultados. No activar versiones de `EvaluationScenario` durante el análisis.
6. Para usar una variante, crear explícitamente una versión oficial de criterios mediante `ScenarioApplicationService` y ejecutar la simulación oficial. Si los insumos cambiaron, informar que el análisis ya no es vigente y recalcular; ninguna orden se genera desde el análisis. Preparar y validar el cálculo antes de confirmar juntos la nueva versión y su resultado, para no dejar al escenario anterior sustituido si falla la promoción.

### Contrato REST propuesto

- `POST /api/v1/evaluation-scenarios/{scenarioId}/sensitivity-analyses`: jefe; configuraciones con identificadores de criterio y pesos; `201`.
- `GET /api/v1/sensitivity-analyses/{analysisId}` y listado por solicitud: jefe; base, variantes, comparación y vigencia; `200`.
- `POST /api/v1/sensitivity-analyses/{analysisId}/configurations/{configurationId}/official-simulation`: jefe; versiones esperadas; crea versión oficial y simulación, devuelve ambos identificadores, **no** una orden.

### Cobertura de aceptación

| Escenario | Prueba prevista y resultado verificable |
| --- | --- |
| E1 — Comparabilidad | `US19_E1_AllVariantsUseSameInputsAndRateWithoutEditingOfficialCriteria`: mismos snapshots/tasa, resultados persistidos y escenario original sin modificaciones. |
| E2 — Cambios | `US19_E2_ReportsWinnerChangesOnlyForEvaluatedConfigurations`: datos con cambio de ganador y con estabilidad; pesos visibles y tratamiento explícito de empate. |
| E3 — Rechazo | `US19_E3_InvalidWeightsOrInsufficientCandidatesProduceNoAnalysis`: errores identificables y sin resultado/conclusión parcial. |
| E4 — Decisión | `US19_E4_AnalysisCannotIssueOrderAndPromotionRequiresCurrentOfficialRun`: intentar aprobar con `analysisId` falla; promoción crea criterios y simulación oficiales sujetos a todos los controles. |

## 8. US20 — Presupuesto de adquisición

### Contextos y componentes

- **Principal: PurchaseOrdering.** `PurchaseBudget`, revisiones y política de aprobación presupuestal. Se ubica aquí porque limita el compromiso monetario de la adjudicación, no la descripción técnica del insumo.
- **SupplyRequests:** publica si la solicitud está activa y su identidad.
- **EvaluationSimulation:** publica importes originales/comparables y la evidencia de la tasa; permite mostrar el contraste presupuestal en el resultado sin alterar los puntajes por introducir un criterio nuevo.

### Implementación por capas y persistencia

1. Registrar presupuesto positivo en PEN, solicitud, motivo, actor, fecha y versión. Una modificación crea revisión; no sobrescribe la evidencia anterior.
2. Ausencia de presupuesto se representa como `NotConfigured`, nunca como cero. Propuesta compatible con las historias anteriores: el control aplica cuando existe presupuesto autorizado; se informa su ausencia explícitamente y no se declara que la alternativa está «dentro del presupuesto». No se impone un presupuesto universal obligatorio que la US no establece.
3. Ampliar `ApprovedSimulationSnapshot` con la tasa y los importes comparables necesarios para comprobar la totalidad real de la compra. El importe a comprometer debe corresponder a las líneas y cantidades que efectivamente se emitirán; una diferencia respecto de lo simulado exige corregir/repetir la decisión, no aprobar con un total menor ficticio.
4. Centralizar el contraste antes de generar órdenes: presupuesto vigente, total PEN, margen y exceso. Para compra distribuida, sumar **todo el conjunto**, no aprobar cada proveedor aisladamente.
5. USD requiere la evidencia válida conservada por la simulación. No aceptar una tasa del navegador, una tasa de reserva o una consulta posterior que cambie el cálculo aprobado. La disponibilidad actual de SUNAT no impide reproducir una tasa ya conservada y válida para esa decisión.
6. Vincular la comprobación a `budgetVersion` y huella de la decisión. Si la versión cambia, devolver `409 budget_check_outdated`, exponer la versión vigente y exigir nuevo contraste. No es necesario repetir la extracción IA ni el cálculo de puntajes si sus insumos no cambiaron.
7. Bloquear presupuesto y adjudicación durante aprobación, impedir carreras entre dos aprobaciones o una modificación del límite. Guardar con la adjudicación la versión, el total y el margen autorizados.
8. Tablas propuestas `purchase_budgets` y `purchase_budget_revisions`; una cabecera por solicitud y revisiones inmutables. Registrar también el contraste final en la adjudicación.

### Contrato REST propuesto

- `PUT /api/v1/purchase-requests/{requestId}/budget`: jefe; `amount`, `currency: PEN`, `reason`, `expectedVersion`; crea/revisa y devuelve la versión vigente.
- `GET /api/v1/purchase-requests/{requestId}/budget`: compras; estado configurado/no configurado e historial autorizado.
- `GET /api/v1/simulations/{runId}/budget-check`: compras; incluye alternativa/modo para identificar inequívocamente lo que se aprobaría; total, margen/exceso, versión y tasa.
- La aprobación consume o confirma esa comprobación vigente. Para clientes anteriores sin comprobación previa, la ruta normal realiza el mismo contraste en el servidor; nunca omite el control por faltar un campo nuevo.

### Cobertura de aceptación

| Escenario | Prueba prevista y resultado verificable |
| --- | --- |
| E1 — Registro | `US20_E1_StoresPositivePenBudgetAndPreservesUnsetState`: positivo, motivo y autor; rechazar cero/negativo/otra moneda; ausencia no equivale a cero. |
| E2 — Dentro del límite | `US20_E2_AllowsAtOrBelowBudgetUsingSimulationRateAndWholeAward`: igualdad y menor importe, PEN/USD y suma de todas las órdenes. |
| E3 — Bloqueo | `US20_E3_ExcessOrMissingConversionCreatesNoOrders`: exceso correcto, ausencia de tasa, partidas completas y rechazo atómico del conjunto. |
| E4 — Revisión | `US20_E4_BudgetRevisionRejectsStaleCheckAndKeepsApprovedHistory`: no reutilizar comprobación anterior; nuevo contraste; conservar órdenes previas y ambas versiones. |

## 9. US21 — Compra distribuida entre proveedores

### Contextos y componentes

- **Principal del cálculo: EvaluationSimulation.** Modo `DistributedByItem`, candidatos por ítem, asignaciones y cobertura. Reutiliza restricciones y normalización del motor, sin convertirlo en un optimizador global que la historia no pide.
- **Principal de la adjudicación: PurchaseOrdering.** Agregado `PurchaseAward`, asignaciones aprobadas y conjunto de `PurchaseOrder` por proveedor; controla habilitación, presupuesto, atomicidad e idempotencia.
- **QuotationIntake:** líneas verificadas, correspondencia con ítems, cantidad, unidad, vigencia, identidad y versiones de proveedores.
- **SupplyRequests:** cantidades y requisitos actuales; transición a adjudicada/ordenada una sola vez para el conjunto.

### Evaluación de dominio

1. Mantener el modo actual de ganador único. Añadir un modo explícito para solicitudes con varios ítems y al menos dos cotizaciones verificadas y vigentes en el conjunto de entrada.
2. Agrupar candidatas por `requestedItemId`. Para cada línea comprobar correspondencia, unidad compatible, cantidad ofertada suficiente, requisitos obligatorios, proveedor habilitado y vigencia. No inventar conversiones de unidades ni asociaciones de líneas ambiguas.
3. El requisito obligatorio se aplica al ítem al que pertenece; no descartar una oferta para todos los ítems porque falla en otro. Cada ítem necesita al menos una oferta elegible que cubra íntegramente su cantidad; no se exige artificialmente que haya dos ofertas elegibles para cada ítem.
4. Comparar precio para la **cantidad solicitada**, no comprar automáticamente todo lo ofertado. Normalizar precio/plazo entre candidatos del mismo ítem, aplicar ponderaciones válidas y documentar desempate determinista; conservar puntajes y razones de exclusión.
5. Las restricciones técnicas ya existentes deben resolverse con los UUID y versiones correctos de cada requisito. Para criterios comerciales usar el precio de la asignación del ítem; no reutilizar el total de toda la cotización como precio de cada línea.
6. Construir una asignación por ítem, sin fraccionar cantidades entre proveedores. Conservar ítem, línea de cotización, proveedor, cantidades, monedas/importes originales y PEN, tasa y versiones. Si falta cobertura, devolver el diagnóstico con `CanApprove=false` y sin recomendación aprobable.
7. Congelar la misma evidencia de tipo de cambio para toda la combinación. Preservar el resultado histórico y recalcular vigencia con requisitos, criterios, ofertas, proveedor y fecha actuales.

### Adjudicación y persistencia

1. `PurchaseAward` identifica solicitud, simulación, modo, aprobación y conjunto completo de asignaciones. Una solicitud solo puede tener una adjudicación vigente, sea normal o distribuida.
2. Reemplazar la unicidad actual de orden por solicitud/simulación por unicidad en la adjudicación. Cada orden referencia `awardId`; índice único `(award_id, supplier_id)` y asignación única `(award_id, requested_item_id)`. Mantener números de orden únicos.
3. La operación valida cobertura exacta, sin duplicados ni cantidades faltantes, todos los controles de vigencia/habilitación y el presupuesto global antes de emitir el conjunto.
4. Para agrupar líneas de un mismo proveedor procedentes de distintas cotizaciones/monedas, las órdenes distribuidas se emitirán en PEN, conservando en cada línea origen, moneda e importe originales y conversión trazable. Es una decisión de diseño para cumplir «una orden por proveedor» sin perder evidencia. No cambiar retrospectivamente la moneda de las órdenes normales existentes.
5. Ampliar la referencia de origen: una orden distribuida puede tener varias cotizaciones. Conservar referencias por línea y por adjudicación; no introducir un `quotationId` ficticio ni perder el identificador de simulación común.
6. Implementar una transacción PostgreSQL coordinada en infraestructura de API para la aprobación: enlistar los contextos EF participantes en la misma conexión/transacción cuando sea necesario; ordenar bloqueos de solicitud, decisión, proveedores y presupuesto; revalidar y guardar adjudicación, todas las órdenes y transición de solicitud. No usar `Task.WhenAll` con el mismo `DbContext` ni asumir que cada `CompleteAsync` constituye una única transacción compartida.
7. Si falla una validación, inserción o transición antes del commit, revertir **todo**. No despachar notificaciones ni auditorías externas antes de confirmar. Guardar eventos pendientes/outbox junto al cambio y entregarlos después con reintentos idempotentes cuando involucren otro contexto de persistencia, como auditoría.
8. Idempotencia en la adjudicación: clave estable y única, ligada a la simulación y contenido autorizado. Repetición idéntica devuelve el mismo `awardId` y todas sus órdenes (`200`); misma clave con otro contenido devuelve `409`. Dos aprobaciones simultáneas no pueden emitir conjuntos distintos para la misma solicitud.
9. Migrar órdenes históricas a adjudicaciones de modo normal, conservando IDs, números, fuentes e idempotencia anterior. Solo después sustituir índices. Actualizar repositorios que hoy devuelven una única orden por solicitud/simulación.
10. Mantener consultas individuales y documentos por orden. Agregar consultas del conjunto; las rutas singulares antiguas siguen funcionando para adjudicación normal y devuelven un conflicto explícito con referencia al conjunto cuando hay varias órdenes, nunca una orden elegida arbitrariamente.
11. Revisar métricas y auditoría: una adquisición distribuida es una solicitud/adjudicación, no varias solicitudes por cada proveedor. Evitar duplicar tiempos/ahorros al unir con múltiples órdenes y preservar las evaluaciones de entrega por orden.

### Contrato REST propuesto

- `POST /api/v1/evaluation-scenarios/{scenarioId}/distributed-simulations`: analista o jefe; `201` para resultado nuevo, recuperación del mismo resultado cuando corresponda.
- `GET /api/v1/simulations/{runId}`: resultado discriminado por `mode`, con asignaciones, exclusiones, cobertura, total y vigencia; conservar el formato normal existente mediante extensión compatible.
- `POST /api/v1/simulations/{runId}/purchase-awards`: jefe; `Idempotency-Key`, condiciones/destino y versiones de decisión/presupuesto; `201` con adjudicación y órdenes, `200` en reintento idéntico.
- `GET /api/v1/purchase-awards/{awardId}` y `GET /api/v1/purchase-requests/{requestId}/purchase-award`: compras, historial completo del conjunto; `200`.
- La ruta normal `POST /api/v1/simulations/{runId}/quotations/{quotationId}/purchase-orders` sigue siendo para decisión de ganador único y utiliza los mismos controles de adjudicación.

### Cobertura de aceptación

| Escenario | Prueba prevista y resultado verificable |
| --- | --- |
| E1 — Asignación | `US21_E1_RanksPerItemAndBuildsTraceableWholeQuantityAllocation`: varios ítems con ganadores distintos, precio por cantidad solicitada, total y fuentes correctos; PEN/USD con una tasa común. |
| E2 — Cobertura | `US21_E2_RejectsMandatoryFailuresAndInsufficientQuantityWithoutSplitting`: exclusión por ítem, unidades incompatibles, cobertura incompleta no aprobable; ninguna suma artificial de proveedores para completar un ítem. |
| E3 — Atomicidad | `US21_E3_ApprovesOneOrderPerSupplierOrRollsBackEntireAward`: proveedor con varias líneas, referencias comunes y presupuesto global; provocar fallo en la segunda orden y en cambio de estado, comprobar cero órdenes/adjudicaciones nuevas. |
| E4 — Reintento | `US21_E4_ConcurrentRetriesReturnExactExistingOrderSet`: mismas órdenes, cantidades y aprobación; carrera de dos conexiones y reintento posterior al commit sin respuesta HTTP. |

## 10. US22 — Asignar solicitudes a analistas

### Contextos y componentes

- **Principal: SupplyRequests.** Analista actual e historial de asignaciones asociados a `PurchaseRequest`; `RequestAssignmentService` y filtros de consulta.
- **IdentityAccess:** directorio de cuentas activas con rol `PurchaseAnalyst`, publicado mediante contrato mínimo y adaptado en API. No exponer contraseñas, sesiones ni datos de autenticación.
- **QuotationIntake:** consume el responsable vigente para US17. La reasignación no transfiere autoría de cotizaciones ni cambia evidencia anterior.

### Implementación por capas y persistencia

1. Representar un único `AssignedAnalystId` actual, inicialmente opcional, con versión de concurrencia. La asignación histórica registra anterior, nuevo, actor, fecha y motivo.
2. El jefe asigna únicamente solicitudes activas y cuentas actualmente activas con rol de analista. Para esta función, no son asignables `Draft`, `Ordered`, `Rejected` o `Cancelled`; conservar los demás estados operativos sin cambiarlos.
3. Primera asignación no exige inventar un motivo histórico; reasignar exige justificación y destinatario diferente. Conservar al solicitante y toda actuación de ambos analistas.
4. Validar versión e identidad activa en servidor; transacción para actualizar responsable e historial. Una carrera no mantiene dos responsables actuales.
5. Añadir `assignedToMe=true` al listado, utilizando el actor autenticado, paginación y filtros de estado. Devolver prioridad, fecha requerida y estado. No aceptar un `analystId` arbitrario como sustituto de autorización.
6. Mantener las reglas de acceso actuales: el filtro de trabajo propio no vuelve al analista dueño de solicitudes de producción ni amplía permisos de aprobación. Reasignar no modifica por sí mismo los insumos ni invalida una simulación técnica.
7. Campos de asignación en `supply_requests.purchase_requests` y tabla `request_assignment_history`; índice por responsable y estado. Las referencias a usuarios son IDs de IAM, no copias de sus entidades.

### Contrato REST propuesto

- `GET /api/v1/iam/users?role=PurchaseAnalyst&status=Active`: jefe; directorio paginado mínimo de destinatarios válidos.
- `PUT /api/v1/purchase-requests/{requestId}/assignment`: jefe; `analystId`, `reason` para reasignación y `expectedVersion`; `200`.
- `GET /api/v1/purchase-requests/{requestId}/assignment-history`: personal de compras autorizado; `200`.
- `GET /api/v1/purchase-requests?assignedToMe=true`: analista; asignación vigente y filtros existentes; `200`.

### Cobertura de aceptación

| Escenario | Prueba prevista y resultado verificable |
| --- | --- |
| E1 — Asignación | `US22_E1_ManagerAssignsOneActiveAnalystWithoutChangingRequesterOrStatus`: identidad y fecha correctas; un solo responsable. |
| E2 — Reasignación | `US22_E2_ReassignmentRequiresReasonAndPreservesPriorWork`: anterior/nuevo y motivo; carrera de dos jefes produce conflicto sin dos asignaciones actuales. |
| E3 — Rechazo | `US22_E3_InvalidAccountRoleOrInactiveRequestLeavesAssignmentUnchanged`: cuenta pendiente/deshabilitada, otro rol y estados no asignables; sin cambios parciales. |
| E4 — Trabajo propio | `US22_E4_MyQueueExcludesReassignedRequestsAndRespectsPermissions`: filtros y paginación, prioridad/fecha/estado; un analista no modifica la asignación ni aprueba compras. |

## 11. Orden recomendado de implementación

El orden técnico no sustituye el orden por valor del Product Backlog.

| Etapa | Trabajo | Dependencia y criterio de salida |
| --- | --- | --- |
| 0 | Fijar contratos y base de regresión | Verificar pruebas actuales, datos de muestra, políticas temporales, transacción de aprobación y estrategia de migración sin borrar datos. |
| 1 | US22 y directorio IAM mínimo | Asignación única y cola propia comprobadas; desbloquea el destinatario de US17. |
| 2 | US15 | Aclaración y revisión técnica completas; aprobación antigua bloqueada. Puede avanzarse en paralelo conceptual con US16, sin mezclar cambios sobre el agregado. |
| 3 | US16 | Borrador, confirmación y trabajador idempotente; conservar registro normal existente. |
| 4 | US18 y vigencia comercial | Catálogo conciliado, habilitación y fechas aplicadas en simulación/aprobación; contratos publicados sin ciclos. |
| 5 | US17 | Trabajador temporal y bandeja del analista con asignación y adjudicación reales. |
| 6 | US20 | Presupuesto, contraste final, evidencia monetaria y protección ante carreras en aprobación normal. |
| 7 | US19 | Motor reutilizable, análisis independiente y promoción a simulación oficial. |
| 8 | US21 | Evaluación por ítem, adjudicación, sustitución de índices, emisión atómica, compatibilidad y métricas. Depende de habilitación y presupuesto. |
| 9 | Verificación conjunta | Ejecutar los 30 escenarios nuevos y regresión del flujo anterior; revisar OpenAPI y ensayo con clientes antes de desplegar. |

## 12. Plan de pruebas, migración y entrega

### Pruebas por nivel

- **Dominio:** transiciones, especificaciones, periodicidad, fechas, pesos, cobertura, habilitación, presupuesto y cálculos monetarios. Nuevas clases en `tests/SmartQuote.Domain.Tests`, con nombres vinculados a cada US; no limitarse a comprobar constructores.
- **Aplicación:** puertos simulados, selección de destinatarios, autorización contextual, vigencia y orquestación. Añadir proyecto específico si resulta necesario, siguiendo las referencias actuales sin incorporar EF al dominio.
- **Integración HTTP/PostgreSQL:** ampliar `tests/SmartQuote.Integration.Tests` con archivos por historia, pruebas de migración y transacciones reales. Consultar filas persistidas, no solo códigos HTTP. Ejecutar explícitamente este proyecto y comprobar su inclusión efectiva en CI, pues no debe asumirse que `dotnet test SmartQuote.sln` cubre todos los proyectos.
- **Concurrencia:** conexiones/scopes independientes para ocurrencias, alertas, asignación, presupuesto, suspensión y adjudicación. Verificar índices y rollback en PostgreSQL; no sustituir estas pruebas por un repositorio en memoria.
- **Tiempo y servicios externos:** reloj controlado para vencimiento/periodicidad; proveedor de SUNAT de prueba con tasa conocida y casos de indisponibilidad. No consumir créditos OpenAI ni depender de SUNAT real en cada ejecución de CI.
- **BDD:** ampliar `tests/SmartQuote.Bdd.Tests` con recorridos representativos: aclaración → nueva evaluación, recurrencia → confirmación, y distribución → aprobación/reintento. No duplicar obligatoriamente cada prueba en todos los niveles.
- Identificar pruebas con `Trait("Story", "US15")` y `Trait("Scenario", "E1")`, adaptando números. Los 30 renglones de cobertura de este documento son el mínimo funcional; cada renglón puede necesitar varios casos parametrizados.

Ejemplo de ejecución futura por historia, una vez implementadas sus pruebas y configurada la base de pruebas aislada:

```powershell
dotnet test tests/SmartQuote.Domain.Tests/SmartQuote.Domain.Tests.csproj --filter "Story=US21"
dotnet test tests/SmartQuote.Integration.Tests/SmartQuote.Integration.Tests.csproj --filter "Story=US21"
```

### Migraciones y operación

1. Crear migraciones nuevas por contexto; no editar ni eliminar las migraciones existentes para «hacerlas coincidir».
2. Respaldar y ensayar sobre una copia de datos. Comprobar órdenes, proveedores duplicados, asignaciones inexistentes y versiones antes del cambio.
3. Aplicar primero columnas/tablas aditivas, conciliar proveedores y crear adjudicaciones normales para órdenes anteriores; después reemplazar índices incompatibles con US21. No dejar una ventana donde se permitan dos adjudicaciones por solicitud.
4. Actualizar lectura/escritura de órdenes y contratos de simulación de forma coordinada. Antes de habilitar el modo distribuido, los clientes deben poder mostrar un conjunto y no asumir una sola orden.
5. Registrar trabajadores de recurrencias y alertas en la composición de API. Tienen que funcionar con múltiples instancias, apagarse correctamente y recuperarse de reinicios sin duplicar; no depender de que alguien visite una pantalla.
6. Configurar zona, anticipación, intervalo de sondeo, límite por lote y opciones de trabajadores en configuración de servidor. No requieren nuevas claves de IA; conservar secretos de SUNAT/OpenAI fuera del repositorio e imagen.
7. Registrar métricas/logs de ejecuciones, errores y reintentos, sin contenidos sensibles. La salud de PostgreSQL no demuestra por sí sola que el trabajador está procesando; exponer evidencia operativa de su última ejecución/fracaso sin crear funcionalidades ajenas a las US.
8. Actualizar documentación de clases y base de datos **al implementar**, únicamente donde cambie el modelo. Este plan no altera los diagramas existentes ni afirma que las nuevas funciones estén disponibles.

### Criterio de finalización

Una historia queda terminada cuando todos sus escenarios tienen pruebas ejecutables y satisfactorias, permisos y errores documentados en OpenAPI, persistencia/migración verificada y ausencia de regresiones en las historias anteriores. Para US21, además, debe demostrarse con fallo inducido y concurrencia que nunca queda emitido un subconjunto de órdenes ni una compra duplicada.

La entrega posterior incluirá backend y contratos listos para web/móvil; este documento no supone que los clientes ya incorporen las nuevas pantallas o que un flujo móvil se haya probado por ejecutar tests del servidor.
