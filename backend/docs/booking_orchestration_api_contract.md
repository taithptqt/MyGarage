# Booking Orchestration API Contract (v1)

## 1. Scope
This contract defines APIs for:
- booking
- reception and check-in
- bay dispatch
- diagnose waiting flow
- overnight hold
- parts reservation guardrails

This file distinguishes:
- `Current`: endpoint already present in backend code.
- `To-be`: endpoint should be added to close logic gaps.

## 2. Global Rules
- Slot duration is fixed at `15` minutes.
- Check-in setup is fixed at `15` minutes.
- Execution buffer is fixed at `15` minutes.
- Required slot count formula:
  - `N = ceil((CheckInMinutes + Sum(StandardMinutes) + BufferMinutes) / 15)`
- Capacity check must run per 15-minute slice using `CapacityConfigs.MaxBaysAvailable`.
- Booking threshold for App/Web:
  - `BookingLimit = floor(MaxBaysAvailable * BookingThresholdPercent)`
  - default `BookingThresholdPercent = 0.85`
  - allowed range `0.70..0.95`
- Internal dispatch screens keep full operational capacity (`100%`) for walk-in and post-diagnosis overflow handling.
- Server is source of truth for slot math, capacity, and stock checks.

## 3. Canonical Enums
### 3.1 WorkSlotType
- `DIAGNOSE`
- `REPAIR`
- `BUFFER`

### 3.2 WorkSlotStatus
- `LOCKED`
- `RESERVED`
- `ARRIVED`
- `IN_PROGRESS`
- `DIAGNOSED_WAITING`
- `OVERNIGHT_HOLD`
- `COMPLETED`
- `NOSHOW`
- `CANCELLED`

### 3.3 PartInventoryReservationStatus
- `RESERVED`
- `FULFILLED`
- `CANCELLED`

## 4. Standard Error Body
```json
{
  "message": "Human readable error"
}
```

## 5. Current Endpoints (already in codebase)

### 5.1 Availability and Slot Hold
#### `GET /api/appointments/slots` (Current)
Query params:
- `branchId` (long, required)
- `date` (DateOnly, required)
- `requestedServiceIds` (string, optional, comma list)
- `bookingFlow` (string, optional)
- `vehicleCount` (int, optional, default `1`)
- `isParallel` (bool, optional, default `true`)

Response item (`AvailableSlotResponseDto`):
```json
{
  "startTime": "2026-04-10T08:00:00",
  "endTime": "2026-04-10T09:30:00",
  "maxCapacity": 20,
  "bookedCount": 8,
  "remainingCapacity": 12,
  "isAvailable": true,
  "isRoundedHour": true,
  "isTopPick": true,
  "session": "MORNING",
  "compactionScore": 90,
  "bookingFlow": "FIXED",
  "estimatedDurationMinutes": 90,
  "serviceMinutes": 60,
  "setupMinutes": 15,
  "bufferMinutes": 15,
  "dynamicNotice": null
}
```

#### `POST /api/appointments/slot-holds` (Current)
Request (`SlotHoldRequestDto`):
```json
{
  "branchId": 1,
  "startTime": "2026-04-10T08:00:00",
  "endTime": "2026-04-10T09:30:00",
  "bookingFlow": "FIXED",
  "requestedServiceIds": "10,11",
  "vehicleCount": 1
}
```

Response (`SlotHoldResponseDto`):
```json
{
  "holdToken": "HLD_abc123",
  "expiresAt": "2026-04-09T11:15:00",
  "holdMinutes": 15
}
```

#### `DELETE /api/appointments/slot-holds/{holdToken}` (Current)
Response: `204 NoContent`

### 5.2 Booking
#### `POST /api/appointments` (Current, authenticated)
#### `POST /api/appointments/public-bookings` (Current, guest)
Request (`BookAppointmentRequestDto`):
```json
{
  "branchId": 1,
  "vehicleId": 1001,
  "vehicleModelId": 101,
  "licensePlate": "51K-12345",
  "vin": "1HGCM82633A004352",
  "color": "Black",
  "startTime": "2026-04-10T08:00:00",
  "endTime": "2026-04-10T09:30:00",
  "checkInWindowStart": "2026-04-10T07:45:00",
  "checkInWindowEnd": "2026-04-10T08:15:00",
  "bookingFlow": "FIXED",
  "customerId": 2001,
  "customerFullName": "Nguyen Van A",
  "customerPhone": "0912345678",
  "requestedServiceIds": "10,11",
  "customerNotes": "Road trip check",
  "slotHoldToken": "HLD_abc123"
}
```

### 5.3 Reception and Lifecycle
#### `POST /api/appointments/reception-bookings` (Current)
Use when reception desk creates or replaces booking plans.

#### `PATCH /api/appointments/{id}` (Current)
Request:
```json
{
  "status": "ARRIVED",
  "reason": null,
  "arrivedAt": "2026-04-10T08:03:00"
}
```

Accepted status actions in controller:
- `CONFIRMED`
- `CANCELED`
- `CHECKED_IN`
- `ARRIVED`

### 5.4 Dispatch and Overnight
#### `GET /api/appointments/branches/{branchId}/allocation-metrics` (Current)
Dispatch dashboard snapshot.

#### `POST /api/appointments/branches/{branchId}/allocation-cycles` (Current)
Trigger dispatch cycle.

#### `POST /api/appointments/branches/{branchId}/overnight-slot-constraints` (Current)
Request:
```json
{
  "businessDate": "2026-04-10"
}
```

Response (`OvernightLockResponseDto`):
```json
{
  "branchId": 1,
  "businessDate": "2026-04-10",
  "lockedSlotCount": 2,
  "lockedSlots": [
    {
      "sourceWorkSlotId": 5001,
      "lockedWorkSlotId": 5101,
      "bayId": 20,
      "lockedFrom": "2026-04-11T08:00:00",
      "lockedTo": "2026-04-11T10:00:00",
      "vehicleId": 1001
    }
  ]
}
```

### 5.5 WorkSlot Operational Endpoint
#### `POST /api/work-slots/{id}/dynamic-extensions` (Current)
Request:
```json
{
  "action": "EXTEND",
  "additionalMinutes": 30,
  "saveOvernight": false,
  "tryBorrowDvnBay": true,
  "performedAt": "2026-04-10T15:35:00"
}
```

Response (`WorkSlotDynamicExtensionResponseDto`) includes:
- extension success
- collision
- manual dispatch requirement
- overnight lock creation info

## 6. To-be Endpoints (must be added)

### 6.1 Capacity Configuration API
#### `GET /api/capacity-configs?branchId={id}&dayOfWeek={1-7}`
#### `PUT /api/capacity-configs/bulk`
Payload:
```json
{
  "branchId": 1,
  "windows": [
    { "dayOfWeek": 1, "startTime": "08:00:00", "endTime": "11:00:00", "maxBaysAvailable": 10, "bookingThresholdPercent": 0.85 },
    { "dayOfWeek": 1, "startTime": "11:00:00", "endTime": "12:00:00", "maxBaysAvailable": 20, "bookingThresholdPercent": 0.95 }
  ]
}
```

### 6.2 Service BOM API
#### `GET /api/service-boms?serviceId={id}&vehicleModelId={id}`
#### `POST /api/service-boms`
#### `PUT /api/service-boms/{id}`
#### `DELETE /api/service-boms/{id}`

### 6.3 Part Reservation API
#### `GET /api/part-inventory-reservations?workSlotId={id}`
#### `POST /api/part-inventory-reservations/reconcile`
Purpose: reconcile `Inventory.ReservedQuantity` with sum of active reservations.

### 6.4 Slot Lifecycle API (recommended)
#### `POST /api/work-slots/{id}/status-transitions`
Request:
```json
{
  "targetStatus": "DIAGNOSED_WAITING",
  "reason": "Diagnosis complete",
  "performedAt": "2026-04-10T10:20:00"
}
```

Rules:
- transition matrix enforced server-side
- updates audit trail
- handles `BayId` release policy for diagnose waiting

## 7. Booking Transaction Contract (server order)
Every booking confirmation must run these steps in one transaction:
1. Compute duration and `N`.
2. Validate mandatory BOM parts.
3. Validate slot chain and capacity per slice.
   - App/Web cap by `BookingLimit`.
   - Internal operations still see full `MaxBaysAvailable`.
4. Create or convert slot records to `RESERVED`.
5. Create `PartInventoryReservations`.
6. Update inventory reserved amount.
7. Commit.

If any step fails:
- rollback full transaction
- return `409` or `400` with deterministic `message`

## 8. Important Compatibility Note
Current DB model has `WorkSlots.BayId` as non-nullable.
If strict business rule says `DIAGNOSED_WAITING` must release bay immediately, choose one:
- Option A: make `BayId` nullable for waiting status.
- Option B: keep `BayId` non-null and move vehicle to dedicated parking/queue bay type.

This must be decided before hardening diagnose flow tests.
