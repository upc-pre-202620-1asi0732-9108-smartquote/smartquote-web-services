# TS04 — Consulta de órdenes de compra

Este contrato describe el endpoint implementado en `PurchaseOrdersController` y la respuesta construida por `PurchaseOrderResourceFromViewAssembler`. Los nombres y tipos se pueden consultar también en el OpenAPI generado por el backend en entorno Development (`/swagger/v1/swagger.json`).

## Solicitud

| Elemento | Valor |
| --- | --- |
| Método y ruta | `GET /api/v1/purchase-orders/{purchaseOrderId}` |
| `purchaseOrderId` | UUID de una orden de compra existente. |
| Autenticación | `Authorization: Bearer <JWT>`. |
| Roles autorizados | `PurchaseAnalyst` o `PurchaseManager` (`SmartQuoteRoles.PurchasingStaff`). |
| Emisión de JWT de usuario | `POST /api/v1/iam/auth/login`, definido en `AuthController`. |

## Respuesta `200 OK`

La API devuelve `PurchaseOrderResource` en JSON. Los campos proceden de la orden consultada; el endpoint no genera ni modifica órdenes.

| Campo | Tipo | Procedencia o significado |
| --- | --- | --- |
| `purchaseOrderId` | UUID | Identificador de la orden. |
| `orderNumber` | texto | Número legible de la orden. |
| `simulationRunId` | texto | Simulación que originó la decisión. |
| `purchaseRequestId` | texto | Solicitud de compra de origen. |
| `quotationId` | texto | Cotización seleccionada. |
| `inputFingerprint` | texto | Huella de las entradas de la decisión. |
| `supplierId`, `supplierBusinessName`, `supplierTaxIdentifier` | texto | Identificación conservada del proveedor. |
| `approvedBy` | UUID | Usuario que aprobó la orden. |
| `approvedAt`, `createdAt` | fecha y hora con zona | Aprobación y creación de la orden. |
| `status` | texto | Estado de la orden. |
| `currency` | texto | Moneda de los importes. |
| `deliveryLeadTimeDays` | entero | Plazo de entrega en días. |
| `deliveryConditions`, `deliveryDestination` | texto | Condiciones y destino de entrega. |
| `total` | decimal | Total calculado con las partidas de la orden. |
| `lines` | lista | Partidas de la orden, en el orden conservado. |

Cada elemento de `lines` contiene `lineId` (UUID), `lineNumber` (entero), `sourceQuotationLineId` y `sourceRequestedItemId` (UUID de origen, si existen), `description` (texto), `quantity` (decimal), `unitOfMeasure` (texto) y `unitPrice` (decimal). La API conserva estos nombres en `PurchaseOrderResource` y `PurchaseOrderLineResource`.

## Errores

| HTTP | Código `code` | Situación |
| ---: | --- | --- |
| 401 | `authentication_required` | Falta un JWT válido. |
| 403 | `forbidden` | El JWT es válido, pero su rol no permite la consulta. |
| 404 | `resource_not_found` | No existe una orden con ese UUID. |

Estos errores se devuelven como `application/problem+json` con `status`, `title`, `detail`, `type`, `instance`, `code` y `traceId`. Los errores 401 y 403 no incluyen datos de la orden solicitada.

## Verificación local

`PurchaseOrderContractTests` comprueba mediante el host de prueba HTTP la respuesta válida con ambos roles, los errores y la publicación de los cuatro códigos en OpenAPI. Usa un repositorio de prueba en memoria para aislar el contrato HTTP. Además, `ApiPersistenceTests.AuthorizedPurchaseOrderLookupReadsPersistedOrder` guarda una orden en PostgreSQL y verifica que el endpoint la consulte con sus partidas y su total.
