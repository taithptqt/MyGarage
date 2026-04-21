# Transaction Policy Matrix

Purpose: tracking file for standardizing `BeginTransaction` usage across backend services/controllers.

Last updated: 2026-04-20

## Sprint Completion Status

- Sprint 1: Completed on 2026-04-20.
- Sprint 2: Completed on 2026-04-20.
- Sprint 3: Completed on 2026-04-20 (re-evaluated `OptionalTx`, no promotion to `MustTx` required).
- Sprint 4: Completed on 2026-04-20 (PR checklist + automated policy check added).

## Policy Summary

- `MustTx`: multi-step mutation, multiple `SaveChangesAsync`, workflow state transitions, or DB + external side-effects.
- `OptionalTx`: currently single-step mutation, can stay implicit transaction unless scope grows.
- `NoTx`: infrastructure/interface/helper files that should not own business transactions.

## MustTx

| File | Sprint | Notes |
|---|---:|---|
| `backend/Services/Application/QuotationWorkflowService.cs` | 1 | High priority, many multi-step writes + email side-effects |
| `backend/Services/Application/DiagnosisService.Workflow.cs` | 1 | High write count, workflow-critical |
| `backend/Services/Application/ServiceOrderService.cs` | 1 | Existing tx mixed with non-tx paths |
| `backend/Services/Application/AppointmentService.BookingOrchestration.cs` | 1 | Multi-step orchestration |
| `backend/Services/Application/InvoiceService.cs` | 1 | Multiple writes in financial flow |
| `backend/Services/Application/PaymentConfirmationService.cs` | 1 | DB + email side-effects |
| `backend/Controllers/OrderController.cs` | 1 | Writes + external HTTP |
| `backend/Controllers/BayAdminController.cs` | 1 | Many mutation endpoints, no unified tx |
| `backend/Controllers/VehicleAdminController.cs` | 1 | Many mutation endpoints, no unified tx |
| `backend/Services/Application/InventoryAdminService.Approvals.cs` | 1 | Approval flow with multiple writes |
| `backend/Services/Application/AuthService.GoogleLogin.cs` | 1 | Concurrent login/account linking risks |
| `backend/Controllers/AppointmentsController.cs` | 1 | Already uses tx, needs standard template |
| `backend/Controllers/PurchaseOrderController.cs` | 1 | Already uses tx, needs standard template |
| `backend/Controllers/RetailOrderController.cs` | 1 | Already uses tx, needs standard template |
| `backend/Controllers/RetailReturnController.cs` | 1 | Already uses tx, needs standard template |
| `backend/Controllers/ServiceOrderController.cs` | 1 | Already uses tx, needs standard template |
| `backend/Controllers/SalaryAdminController.cs` | 1 | Already uses tx, needs standard template |
| `backend/Services/Application/AppointmentService.BookingVehicleOwnership.cs` | 2 | Multi-write path |
| `backend/Services/Application/AppointmentService.DynamicExtension.cs` | 2 | Multi-write path |
| `backend/Services/Application/AppointmentService.InsuranceAssets.cs` | 2 | Multi-write path |
| `backend/Services/Application/AppointmentService.ServiceOrders.cs` | 2 | Multi-write path |
| `backend/Services/Application/AppointmentService.StatusFlow.cs` | 2 | Transactional consistency normalization |
| `backend/Services/Application/AppointmentService.Reception.cs` | 2 | Transactional consistency normalization |
| `backend/Services/Application/AuthService.PasswordRecovery.cs` | 2 | Multi-write + email |
| `backend/Services/Application/AuthService.ChangePassword.cs` | 2 | Multi-write |
| `backend/Services/Application/AuthService.Registration.cs` | 2 | Multi-write |
| `backend/Services/Application/ChatbotService.cs` | 2 | Mixed tx patterns |
| `backend/Services/Application/ChatbotBookingWorkflowService.cs` | 2 | Mixed tx patterns |
| `backend/Services/Application/CustomerAutomationJobProcessor.cs` | 2 | Mixed tx patterns |
| `backend/Services/Application/CustomerPortalService.cs` | 2 | Mixed tx patterns |
| `backend/Services/Application/MaintenanceReminderService.cs` | 2 | Multi-write operations |
| `backend/Services/Application/PayrollBatchCalculationService.cs` | 2 | Transactional financial writes |
| `backend/Services/Application/QcInspectionService.cs` | 2 | Transactional consistency normalization |
| `backend/Services/Application/ServiceOrderService.InsuranceAssets.cs` | 2 | Multi-write path |
| `backend/Services/Application/VehicleConditionLogService.Commands.cs` | 2 | Multi-write path |
| `backend/Services/Application/VoucherApprovalRequestService.Decision.cs` | 2 | Approval write flow |
| `backend/Services/Execution/BayManagementService.cs` | 2 | Allocation/release consistency |
| `backend/Services/Execution/ConsumableReturnService.cs` | 2 | Multi-write path |
| `backend/Services/Execution/TimesheetService.cs` | 2 | Multi-write path |
| `backend/Services/Application/MaterialRequestService.cs` | 2 | Already tx, normalize pattern |
| `backend/Services/Application/MaterialReturnService.cs` | 2 | Already tx, normalize pattern |
| `backend/Services/Application/VoucherRedemptionService.cs` | 2 | Already tx, normalize pattern |
| `backend/Support/RetailOrderStockCommitter.cs` | 2 | Already tx, normalize pattern |

## OptionalTx

| File | Sprint | Notes |
|---|---:|---|
| `backend/Controllers/DiagnosisRecommendationController.cs` | 3 | Single-step mutation currently |
| `backend/Controllers/ServiceStaffAssignmentController.cs` | 3 | Single-step mutation currently |
| `backend/Controllers/ShiftClosingController.cs` | 3 | Single-step mutation currently |
| `backend/Controllers/StaffSpecialtyController.cs` | 3 | Single-step mutation currently |
| `backend/Controllers/WebhookController.cs` | 3 | Prefer idempotency first, tx optional |
| `backend/Services/Application/AppointmentService.AutoNoShow.cs` | 3 | Single-step mutation currently |
| `backend/Services/Application/AppointmentService.Dispatching.cs` | 3 | Single-step mutation currently |
| `backend/Services/Application/AppointmentService.LockedSlotCleanup.cs` | 3 | Single-step mutation currently |
| `backend/Services/Application/DiagnosisService.Queries.cs` | 3 | Light write path |
| `backend/Services/Application/GroupSessionService.Cancel.cs` | 3 | Single-step mutation currently |
| `backend/Services/Application/GrnSnapshotService.cs` | 3 | Single-step mutation currently |
| `backend/Services/Application/ServiceOrderDetailService.cs` | 3 | Single-step mutation currently |
| `backend/Services/Application/VoucherCampaignService.Assignment.cs` | 3 | Single-step mutation currently |
| `backend/Services/Application/VoucherCampaignService.Lifecycle.cs` | 3 | Single-step mutation currently |
| `backend/Services/Application/VoucherCampaignService.Mutation.cs` | 3 | Single-step mutation currently |
| `backend/Services/Application/VoucherRedemptionService.Execution.cs` | 3 | Covered by orchestration in service root |
| `backend/Services/Application/VoucherRedemptionService.Reverse.cs` | 3 | Covered by orchestration in service root |
| `backend/Services/Background/AppointmentInsuranceUploadCleanupBackgroundService.cs` | 3 | Background cleanup |
| `backend/Services/Background/ServiceOrderInsuranceUploadCleanupBackgroundService.cs` | 3 | Background cleanup |

## NoTx

| File | Sprint | Notes |
|---|---:|---|
| `backend/Interfaces/Data/IGarageContext.cs` | 4 | Interface contract only |
| `backend/Data/GarageContext.Audit.cs` | 4 | EF context infra; no business tx ownership |
| `backend/Repositories/GenericRepository.cs` | 4 | Repository layer should not orchestrate business tx |
| `backend/Interfaces/Services/IWriteRequestSpamGuard.cs` | 4 | Interface only |
| `backend/Services/Application/InMemoryWriteRequestSpamGuard.cs` | 4 | In-memory guard only |
| `backend/Middleware/WriteRequestSpamGuardMiddleware.cs` | 4 | Middleware only |

## Execution Plan by Sprint

1. Sprint 1: close all `MustTx` in core quotation/service-order/diagnosis/financial/admin mutation flows.
2. Sprint 2: close remaining `MustTx` in appointment/auth/chatbot/execution/voucher/material flows.
3. Sprint 3: re-evaluate `OptionalTx`; upgrade any file that becomes multi-step.
4. Sprint 4: enforce policy in PR checklist and add automated compliance checks.
