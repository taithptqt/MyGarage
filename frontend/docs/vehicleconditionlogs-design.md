# VehicleConditionLogs Design Note

## Data model (backend)
- Entity: `VehicleConditionLog`
- Core fields:
  - `serviceOrderId`
  - `logType` (`CHECK_IN`, `CHECK_OUT`)
  - `odoReading`
  - `fuelLevel`
  - `dashboardWarningLights`
  - `valuables`
  - `defectsAndImages` (JSON/text)
  - `customerSignatureUrl`
  - `inspectorId`
  - audit fields (`createdAt`, `createdBy`, `updatedAt`, `updatedBy`)

## Alignment with Excel spec
- Excel row: `VehicleConditionLogs` (CORE > 5. Dich vu)
- Business intent:
  - Create legal evidence for vehicle handover status.
  - Protect garage from post-service disputes about external defects and valuables.
  - Capture both receiving and returning moments (`CHECK_IN` / `CHECK_OUT`).

## Page behavior (v1 hi)
- Route context by service order: `.../orders/:orderId/vehicleconditionlogs`
- Fetch all logs and filter by `serviceOrderId` on frontend (current API shape).
- Group records by `logType`: `CHECK_IN`, `CHECK_OUT`, `OTHER`.
- Render details focused on dispute-proof data:
  - ODO, fuel, warning lights, valuables
  - defects/images payload
  - customer signature URL
  - inspector and created time

## Suggested next steps (future)
- Add backend endpoint: `GET /api/VehicleConditionLog?serviceOrderId=...` for efficient query.
- Add create/edit flow in reception check-in and handover check-out.
- Store `defectsAndImages` as structured JSON schema with image array + position map.
- Add signature capture widget and image upload pipeline.
