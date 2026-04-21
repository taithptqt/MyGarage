Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
Set-Location $repoRoot

$mustTxFiles = @(
    "backend/Services/Application/QuotationWorkflowService.cs",
    "backend/Services/Application/DiagnosisService.Workflow.cs",
    "backend/Services/Application/ServiceOrderService.cs",
    "backend/Services/Application/AppointmentService.BookingOrchestration.cs",
    "backend/Services/Application/InvoiceService.cs",
    "backend/Services/Application/PaymentConfirmationService.cs",
    "backend/Controllers/OrderController.cs",
    "backend/Controllers/BayAdminController.cs",
    "backend/Controllers/VehicleAdminController.cs",
    "backend/Services/Application/InventoryAdminService.Approvals.cs",
    "backend/Services/Application/AuthService.GoogleLogin.cs",
    "backend/Controllers/AppointmentsController.cs",
    "backend/Controllers/PurchaseOrderController.cs",
    "backend/Controllers/RetailOrderController.cs",
    "backend/Controllers/RetailReturnController.cs",
    "backend/Controllers/ServiceOrderController.cs",
    "backend/Controllers/SalaryAdminController.cs",
    "backend/Services/Application/AppointmentService.BookingVehicleOwnership.cs",
    "backend/Services/Application/AppointmentService.DynamicExtension.cs",
    "backend/Services/Application/AppointmentService.InsuranceAssets.cs",
    "backend/Services/Application/AppointmentService.ServiceOrders.cs",
    "backend/Services/Application/AppointmentService.StatusFlow.cs",
    "backend/Services/Application/AppointmentService.Reception.cs",
    "backend/Services/Application/AuthService.PasswordRecovery.cs",
    "backend/Services/Application/AuthService.ChangePassword.cs",
    "backend/Services/Application/AuthService.Registration.cs",
    "backend/Services/Application/ChatbotService.cs",
    "backend/Services/Application/ChatbotBookingWorkflowService.cs",
    "backend/Services/Application/CustomerAutomationJobProcessor.cs",
    "backend/Services/Application/CustomerPortalService.cs",
    "backend/Services/Application/MaintenanceReminderService.cs",
    "backend/Services/Application/PayrollBatchCalculationService.cs",
    "backend/Services/Application/QcInspectionService.cs",
    "backend/Services/Application/ServiceOrderService.InsuranceAssets.cs",
    "backend/Services/Application/VehicleConditionLogService.Commands.cs",
    "backend/Services/Application/VoucherApprovalRequestService.Decision.cs",
    "backend/Services/Execution/BayManagementService.cs",
    "backend/Services/Execution/ConsumableReturnService.cs",
    "backend/Services/Execution/TimesheetService.cs",
    "backend/Services/Application/MaterialRequestService.cs",
    "backend/Services/Application/MaterialReturnService.cs",
    "backend/Services/Application/VoucherRedemptionService.cs",
    "backend/Support/RetailOrderStockCommitter.cs"
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
