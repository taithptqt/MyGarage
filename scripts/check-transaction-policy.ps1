Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
Set-Location $repoRoot

$mustTxFiles = @(
    "backend/src/MyGarage.Infrastructure/Services/Application/QuotationWorkflowService.cs",
    "backend/src/MyGarage.Infrastructure/Services/Application/DiagnosisService.Workflow.cs",
    "backend/src/MyGarage.Infrastructure/Services/Application/ServiceOrderService.cs",
    "backend/src/MyGarage.Infrastructure/Services/Application/AppointmentService.BookingOrchestration.cs",
    "backend/src/MyGarage.Infrastructure/Services/Application/InvoiceService.cs",
    "backend/src/MyGarage.Infrastructure/Services/Application/PaymentConfirmationService.cs",
    "backend/src/MyGarage.Api/Controllers/OrderController.cs",
    "backend/src/MyGarage.Api/Controllers/BayAdminController.cs",
    "backend/src/MyGarage.Api/Controllers/VehicleAdminController.cs",
    "backend/src/MyGarage.Infrastructure/Services/Application/InventoryAdminService.Approvals.cs",
    "backend/src/MyGarage.Infrastructure/Services/Application/AuthService.GoogleLogin.cs",
    "backend/src/MyGarage.Api/Controllers/AppointmentsController.cs",
    "backend/src/MyGarage.Api/Controllers/PurchaseOrderController.cs",
    "backend/src/MyGarage.Api/Controllers/RetailOrderController.cs",
    "backend/src/MyGarage.Api/Controllers/RetailReturnController.cs",
    "backend/src/MyGarage.Api/Controllers/ServiceOrderController.cs",
    "backend/src/MyGarage.Api/Controllers/SalaryAdminController.cs",
    "backend/src/MyGarage.Infrastructure/Services/Application/AppointmentService.BookingVehicleOwnership.cs",
    "backend/src/MyGarage.Infrastructure/Services/Application/AppointmentService.DynamicExtension.cs",
    "backend/src/MyGarage.Infrastructure/Services/Application/AppointmentService.InsuranceAssets.cs",
    "backend/src/MyGarage.Infrastructure/Services/Application/AppointmentService.ServiceOrders.cs",
    "backend/src/MyGarage.Infrastructure/Services/Application/AppointmentService.StatusFlow.cs",
    "backend/src/MyGarage.Infrastructure/Services/Application/AppointmentService.Reception.cs",
    "backend/src/MyGarage.Infrastructure/Services/Application/AuthService.PasswordRecovery.cs",
    "backend/src/MyGarage.Infrastructure/Services/Application/AuthService.ChangePassword.cs",
    "backend/src/MyGarage.Infrastructure/Services/Application/AuthService.Registration.cs",
    "backend/src/MyGarage.Infrastructure/Services/Application/ChatbotService.cs",
    "backend/src/MyGarage.Infrastructure/Services/Application/ChatbotBookingWorkflowService.cs",
    "backend/src/MyGarage.Infrastructure/Services/Application/CustomerAutomationJobProcessor.cs",
    "backend/src/MyGarage.Infrastructure/Services/Application/CustomerPortalService.cs",
    "backend/src/MyGarage.Infrastructure/Services/Application/MaintenanceReminderService.cs",
    "backend/src/MyGarage.Infrastructure/Services/Application/PayrollBatchCalculationService.cs",
    "backend/src/MyGarage.Infrastructure/Services/Application/QcInspectionService.cs",
    "backend/src/MyGarage.Infrastructure/Services/Application/ServiceOrderService.InsuranceAssets.cs",
    "backend/src/MyGarage.Infrastructure/Services/Application/VehicleConditionLogService.Commands.cs",
    "backend/src/MyGarage.Infrastructure/Services/Application/VoucherApprovalRequestService.Decision.cs",
    "backend/src/MyGarage.Infrastructure/Services/Execution/BayManagementService.cs",
    "backend/src/MyGarage.Infrastructure/Services/Execution/ConsumableReturnService.cs",
    "backend/src/MyGarage.Infrastructure/Services/Execution/TimesheetService.cs",
    "backend/src/MyGarage.Infrastructure/Services/Application/MaterialRequestService.cs",
    "backend/src/MyGarage.Infrastructure/Services/Application/MaterialReturnService.cs",
    "backend/src/MyGarage.Infrastructure/Services/Application/VoucherRedemptionService.cs",
    "backend/src/MyGarage.Infrastructure/Support/RetailOrderStockCommitter.cs"
)

$errors = New-Object System.Collections.Generic.List[string]

foreach ($relativePath in $mustTxFiles) {
    $absolutePath = Join-Path $repoRoot $relativePath
    if (-not (Test-Path -LiteralPath $absolutePath)) {
        $errors.Add("Missing MustTx file: $relativePath")
        continue
    }

    $content = Get-Content -LiteralPath $absolutePath
    $hasTryBeginMethod = $content | Select-String -Pattern 'private\s+async\s+Task.*TryBeginTransactionAsync\('
    $hasCurrentTxGuard = $content | Select-String -Pattern 'CurrentTransaction'

    if ($hasTryBeginMethod -and -not $hasCurrentTxGuard) {
        $errors.Add("${relativePath}: missing CurrentTransaction guard in TryBeginTransactionAsync policy.")
    }

    $directBeginLines = $content | Select-String -Pattern 'await using var .*Database\.BeginTransactionAsync\(|using var .*Database\.BeginTransactionAsync\('
    foreach ($line in $directBeginLines) {
        $errors.Add("${relativePath}:$($line.LineNumber): direct Database.BeginTransactionAsync allocation is forbidden in MustTx files.")
    }
}

if ($errors.Count -gt 0) {
    Write-Host "Transaction policy check failed:" -ForegroundColor Red
    foreach ($message in $errors) {
        Write-Host "- $message" -ForegroundColor Red
    }

    exit 1
}

Write-Host "Transaction policy check passed for MustTx files." -ForegroundColor Green
exit 0
