# Booking DB Compatibility Report (2026-04-09)

## Environment
- Database: `MyGarage` (Azure SQL)
- Checked on: `2026-04-09` (Asia/Saigon)

## 1. Schema Compatibility (Plan vs Actual)
### 1.1 New core tables
- `CapacityConfigs`: `PASS` (table, FK, constraints, indexes exist)
- `ServiceBOMs`: `PASS` (table, FK, constraints, indexes exist)
- `PartInventoryReservations`: `PASS` (table, FK, constraints, indexes exist)
- `CapacityConfigs.BookingThresholdPercent`: `PASS` (added, `NOT NULL`, default `0.85`, check `0.70..0.95`)

### 1.2 WorkSlots extension
- New columns present: `PASS`
  - `AppointmentId`
  - `ServiceOrderId`
  - `IsAdditional`
  - `SequenceOrder`
- Status/SlotType check constraints: `PASS`
- Important caveat:
  - `WorkSlots.BayId` is `NOT NULL` -> conflicts with strict requirement "set `BayId = NULL` at `DIAGNOSED_WAITING`".
  - Current state: `RISK`.

### 1.3 Bays extension
- `ReservationStatus`, `LastStatusChangedAt`: `PASS`

## 2. Data Compatibility (Operational Readiness)
### 2.1 Counts before/after supplementation
- `Appointments`: `101`
- `ServiceOrders`: `27`
- `WorkSlots`: `0` (`GAP`)
- `ServiceBOMs`: `0` (`GAP`)
- `PartInventoryReservations`: `0` (`GAP`)
- `CapacityConfigs`: was `0` -> now `126` (`FIXED`)

### 2.2 Capacity baseline supplementation executed
- Script executed:
  - [20260409_seed_capacityconfigs_shift_defaults.sql](D:/IT/OJT/repos/MyGarage/backend/Scripts/20260409_seed_capacityconfigs_shift_defaults.sql)
- Result:
  - 3 branches x 7 days x 6 windows = `126` rows.
  - Capacity windows follow plan:
    - `08:00-11:00 = 10`
    - `11:00-12:00 = 20`
    - `12:00-13:00 = 10`
    - `13:00-16:00 = 20`
    - `16:00-17:00 = 10`
    - `17:00-20:00 = 10`

## 3. Code Compatibility Supplementation Completed
### 3.1 Added model + EF context support
- Added model files:
  - [CapacityConfig.cs](D:/IT/OJT/repos/MyGarage/backend/Models/CapacityConfig.cs)
  - [ServiceBom.cs](D:/IT/OJT/repos/MyGarage/backend/Models/ServiceBom.cs)
  - [PartInventoryReservation.cs](D:/IT/OJT/repos/MyGarage/backend/Models/PartInventoryReservation.cs)
- Updated context/interface:
  - [IGarageContext.cs](D:/IT/OJT/repos/MyGarage/backend/Interfaces/Data/IGarageContext.cs)
  - [GarageContext.cs](D:/IT/OJT/repos/MyGarage/backend/Data/GarageContext.cs)

### 3.2 Normalized slot timing constants
- Updated check-in setup from `10` -> `15` minutes.
  - [AppointmentService.cs](D:/IT/OJT/repos/MyGarage/backend/Services/Application/AppointmentService.cs)
- Updated slot hold TTL from `5` -> `15` minutes.
  - [AppointmentService.SlotHolds.cs](D:/IT/OJT/repos/MyGarage/backend/Services/Application/AppointmentService.SlotHolds.cs)
  - [SlotHoldService.cs](D:/IT/OJT/repos/MyGarage/backend/Services/Infrastructure/SlotHoldService.cs)

### 3.3 Booking orchestration logic now implemented in backend
- Booking now validates mandatory BOM part availability before commit:
  - [AppointmentService.BookingCore.cs](D:/IT/OJT/repos/MyGarage/backend/Services/Application/AppointmentService.BookingCore.cs)
  - [AppointmentService.BookingOrchestration.cs](D:/IT/OJT/repos/MyGarage/backend/Services/Application/AppointmentService.BookingOrchestration.cs)
- Booking now persists actual `WorkSlots` (`RESERVED`) and auto-selects a free bay in branch.
- Booking now writes part reservations and increments inventory `ReservedQuantity`.
- App/Web booking now enforces threshold:
  - `BookingLimit = floor(MaxBaysAvailable * BookingThresholdPercent)`
  - default `0.85`, configurable per capacity window (`0.70..0.95`).
- Cancel/no-show now releases `WorkSlots` + `PartInventoryReservations` and decrements reserved inventory.
- Arrival now syncs slot status/link to `ServiceOrderId`.
- Background job now auto-releases expired `LOCKED` slots:
  - [AppointmentDispatchBackgroundService.cs](D:/IT/OJT/repos/MyGarage/backend/Services/Background/AppointmentDispatchBackgroundService.cs)
  - [AppointmentService.LockedSlotCleanup.cs](D:/IT/OJT/repos/MyGarage/backend/Services/Application/AppointmentService.LockedSlotCleanup.cs)

### 3.4 Diagnosis waiting handoff behavior aligned
- On diagnosis start: related diagnose slots move to `IN_PROGRESS`.
- When all diagnoses in a service order are completed: diagnose slot transitions to `DIAGNOSED_WAITING` and switches `SlotType` to `BUFFER` to release bay capacity for next dispatch.
  - [DiagnosisService.Workflow.cs](D:/IT/OJT/repos/MyGarage/backend/Services/Application/DiagnosisService.Workflow.cs)

## 4. Remaining Gaps to Close Next
1. `WorkSlots` historical backfill gap:
   - There are appointments/service orders but zero work slots.
   - Booking pipeline is now enforced for new data; still need one-time backfill for old records.
2. `ServiceBOMs` master data empty:
   - Need business onboarding seed/import per service + vehicle model.
3. `PartInventoryReservations` currently empty snapshot:
   - Expected at check time because no new booking created after code deployment yet.
   - Write path is already implemented in booking flow.
4. Day-of-week semantics mismatch:
   - `CapacityConfigs.DayOfWeek`: `1..7`
   - `BranchOperatingHours.DayOfWeek`: `0..6`
   - Service layer currently reads `BranchOperatingHours` with `0..6`.
   - When switching capacity engine to `CapacityConfigs`, add explicit mapping (`Sunday=7`).
5. Diagnose waiting strict-null policy:
   - Current implementation releases capacity by converting diagnose slot to `BUFFER` + `DIAGNOSED_WAITING`.
   - If strict requirement is `BayId = NULL`, schema + model migration is still required.
6. Slot hold store is still in-memory singleton:
   - Replace with DB/distributed store for multi-instance safety.

## 5. Build Verification Note
- Backend build in this workspace currently fails due pre-existing output/intermediate path issues unrelated to the changes above (missing nested `bin_temp_verify` artifacts and duplicate generated assembly attribute context when forcing custom output paths).
- Recommendation: run CI build on clean workspace or clean branch checkout before merge.
