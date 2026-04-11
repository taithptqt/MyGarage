# Chatbot Customer Self-Service Lookups

This module adds deterministic customer self-service lookups for chatbot orchestration.

## Supported lookups

- Service history lookup
- Invoice/payment status lookup
- Service order progress tracking
- Loyalty status lookup
- Warranty check lookup

## Security boundaries

- Every lookup uses authenticated `customerId` from server-side context.
- Service-order and vehicle-based data are restricted to the customer's current vehicles (`CustomerVehicles.IsCurrentOwner != false`).
- Invoice lookup is restricted to invoices linked to the customer's own service orders or retail orders.
- Loyalty lookup is restricted to the authenticated customer profile only.

## Orchestrator integration

`ChatbotOrchestrator` invokes `IChatbotCustomerSelfServiceLookupService` for inquiry/service-order intents before generic retrieval routing.

Lookup resolution priority:

1. Service order progress
2. Invoice/payment status
3. Warranty check
4. Service history
5. Loyalty status

## Progress stage model

The progress lookup always returns this staged pipeline:

- `received`
- `diagnosed`
- `repairing`
- `QC`
- `completed`

## Warranty logic

Warranty uses actual persisted values:

- Service warranty: `StandardService.WarrantyMonths`, `StandardService.WarrantyKm`
- Product warranty: `Product.WarrantyMonths`
- Current ODO: `Vehicle.Odo`
- Service ODO baseline: `VehicleConditionLogs.OdoReading` (latest per service order)
