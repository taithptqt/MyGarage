# Booking Orchestration Migration Checklist (v1)

## 1. Goal
Move booking and bay orchestration to a consistent model:
- deterministic 15-minute slotting
- BOM-based part reservation
- capacity windows by shift overlap
- multi-stage flow (`DIAGNOSE -> WAITING -> REPAIR`)
- overnight protection

## 2. Pre-Migration Safety
- [ ] Backup database.
- [ ] Confirm deployment window and rollback owner.
- [ ] Freeze schema-changing releases in parallel streams.
- [ ] Confirm app can run in maintenance mode if rollback needed.

## 3. SQL Script Execution Order
Run in this exact order:
1. [20260408_add_bays_status_reservation_columns.sql](D:/IT/OJT/repos/MyGarage/backend/Scripts/20260408_add_bays_status_reservation_columns.sql)
2. [20260409_add_capacityconfigs_partreservation_servicebom_tables.sql](D:/IT/OJT/repos/MyGarage/backend/Scripts/20260409_add_capacityconfigs_partreservation_servicebom_tables.sql)
3. [20260409_rework_workslots_status_slottype_and_links.sql](D:/IT/OJT/repos/MyGarage/backend/Scripts/20260409_rework_workslots_status_slottype_and_links.sql)
4. [20260409_add_capacityconfigs_booking_threshold_percent.sql](D:/IT/OJT/repos/MyGarage/backend/Scripts/20260409_add_capacityconfigs_booking_threshold_percent.sql)
5. [20260409_seed_booking_orchestration_test_data.sql](D:/IT/OJT/repos/MyGarage/backend/Scripts/20260409_seed_booking_orchestration_test_data.sql) (non-prod only)

## 4. DB Verification Checklist
- [ ] `CapacityConfigs` exists with unique index by `(BranchId, DayOfWeek, StartTime, EndTime)`.
- [ ] `ServiceBOMs` exists with unique `(ServiceId, VehicleModelId, ProductId)`.
- [ ] `PartInventoryReservations` exists with unique `(WorkSlotId, ProductId)`.
- [ ] `WorkSlots` has new columns:
  - `AppointmentId`
  - `ServiceOrderId`
  - `IsAdditional`
  - `SequenceOrder`
- [ ] `WorkSlots` status check constraint includes:
  - `LOCKED, RESERVED, ARRIVED, IN_PROGRESS, DIAGNOSED_WAITING, COMPLETED, NOSHOW, CANCELLED, OVERNIGHT_HOLD`
- [ ] `WorkSlots` slot type check constraint includes:
  - `DIAGNOSE, REPAIR, BUFFER`

## 5. EF and Domain Mapping Tasks
### 5.1 Add missing entities
- [ ] Add model `CapacityConfig`.
- [ ] Add model `ServiceBOM`.
- [ ] Add model `PartInventoryReservation`.

### 5.2 Wire context
- [ ] Add `DbSet<CapacityConfig>`.
- [ ] Add `DbSet<ServiceBOM>`.
- [ ] Add `DbSet<PartInventoryReservation>`.
- [ ] Add fluent mappings in `GarageContext.OnModelCreating`.
- [ ] Add sets to `IGarageContext`.

### 5.3 DTO and controller layer
- [ ] Add request/response DTOs for new tables.
- [ ] Add controllers:
  - `CapacityConfigsController`
  - `ServiceBOMsController`
  - `PartInventoryReservationsController`

## 6. Service Logic Hardening (must-fix)
### 6.1 Slot math constants
Current code has mismatches:
- `StandardCheckInSetupMinutes` currently `10`.
- `SlotHoldMinutes` currently `5`.

Target:
- [ ] Check-in setup `15`.
- [ ] Execution buffer `15`.
- [ ] Slot hold TTL `15`.

### 6.2 Capacity model
Target:
- [ ] Capacity per 15-minute slice must read `CapacityConfigs`.
- [ ] App/Web booking cap: `floor(MaxBaysAvailable * BookingThresholdPercent)`.
- [ ] `BookingThresholdPercent` default `0.85`, clamp `0.70..0.95`.
- [ ] Internal dispatch must continue to show full `MaxBaysAvailable` for walk-in and overflow handling.
- [ ] Provide manager override workflow to adjust threshold (70%/85%/95%) without code deploy.

### 6.3 Part reservation source of truth
Current schema uses `Inventory.ReservedQuantity`.

Target:
- [ ] `PartInventoryReservations` drives reservation details.
- [ ] Reservation delta updates `Inventory.ReservedQuantity`.
- [ ] Add reconciliation job:
  - compare `Inventory.ReservedQuantity` vs sum(`PartInventoryReservations` with `Status='RESERVED'`).

### 6.4 Slot hold durability
Current implementation uses in-memory hold service.

Target:
- [ ] Persist hold token and expiry in DB or distributed cache.
- [ ] Ensure cluster-safe behavior in multi-instance deployment.

### 6.5 Diagnose waiting bay release policy
Current schema has non-null `WorkSlots.BayId`.

Decision required:
- [ ] Option A: make `BayId` nullable for `DIAGNOSED_WAITING`.
- [ ] Option B: map waiting vehicles to dedicated parking/queue bay.

No go-live until one option is selected and tested.

## 7. Background Jobs
- [ ] Job A: release expired `LOCKED` slots every 5 minutes.
- [ ] Job B: cancel stale part reservations tied to expired holds.
- [ ] Job C: overnight carry-forward lock generation.
- [ ] Job D: reservation reconciliation and alerting.

## 8. Audit and Observability
- [ ] Log every status transition with actor and timestamp.
- [ ] Capture booking denial reason:
  - stock fail
  - slot chain fail
  - capacity fail
- [ ] Emit metrics:
  - slot utilization
  - hold timeout rate
  - diagnosed waiting queue depth
  - overnight lock count

## 9. UAT Exit Criteria
- [ ] Pass all booking tests (`BK-01` to `BK-11`).
- [ ] Pass diagnose and dispatch tests (`DG-*`, `DP-*`).
- [ ] Pass overnight tests (`ON-*`).
- [ ] Inventory invariant holds after stress:
  - `Inventory.ReservedQuantity == sum(active part reservations)`
- [ ] No duplicate slot allocation in concurrent booking load tests.

## 10. Rollback Plan
- [ ] Keep pre-deploy backup and point-in-time recovery mark.
- [ ] Feature-flag new booking path for quick cutback.
- [ ] If rollback:
  - disable new controllers
  - disable new jobs
  - restore previous booking service behavior
  - run data reconciliation cleanup
