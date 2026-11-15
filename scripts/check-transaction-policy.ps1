Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
Set-Location $repoRoot

$mustTxFiles = @(
    "backend/src/MyGarage.Infrastructure/Queries/Accounting/OrderDataService.cs",
    "backend/src/MyGarage.Infrastructure/Queries/Workshop/BayAdminDataService.cs",
    "backend/src/MyGarage.Infrastructure/Queries/Vehicles/VehicleAdminDataService.cs",
    "backend/src/MyGarage.Infrastructure/Queries/Workshop/AppointmentDataService.cs",
    "backend/src/MyGarage.Infrastructure/Queries/Retail/RetailOrderDataService.cs",
    "backend/src/MyGarage.Infrastructure/Queries/Retail/RetailReturnDataService.cs",
    "backend/src/MyGarage.Infrastructure/Queries/Workshop/ServiceOrderDataService.cs",
    "backend/src/MyGarage.Infrastructure/Queries/Payroll/SalaryAdminDataService.cs",
    "backend/src/MyGarage.Infrastructure/Services/Application/Customers/CustomerAutomationJobProcessor.cs",
    "backend/src/MyGarage.Infrastructure/Persistence/Stores/Vehicles/EfVehicleConditionLogStore.cs",
    "backend/src/MyGarage.Infrastructure/Persistence/Stores/Workshop/EfBayManagementStore.cs",
    "backend/src/MyGarage.Infrastructure/Persistence/Stores/Workshop/EfConsumableReturnStore.cs",
    "backend/src/MyGarage.Infrastructure/Persistence/Stores/Workshop/EfTimesheetStore.cs",
    "backend/src/MyGarage.Infrastructure/Persistence/Stores/Auth/EfAuthStore.cs",
    "backend/src/MyGarage.Infrastructure/Persistence/Stores/Chatbot/EfChatbotStore.cs",
    "backend/src/MyGarage.Infrastructure/Persistence/Stores/Appointments/EfMaintenanceReminderStore.cs",
    "backend/src/MyGarage.Infrastructure/Persistence/Stores/Appointments/EfAppointmentStore.Transactions.cs",
    "backend/src/MyGarage.Infrastructure/Persistence/Stores/Workshop/EfQuotationWorkflowStore.cs",
    "backend/src/MyGarage.Infrastructure/Persistence/Stores/Workshop/EfDiagnosisStore.cs",
    "backend/src/MyGarage.Infrastructure/Persistence/Stores/Workshop/EfServiceOrderStore.cs",
    "backend/src/MyGarage.Infrastructure/Persistence/Stores/Workshop/EfQcInspectionStore.cs",
    "backend/src/MyGarage.Infrastructure/Support/RetailOrderStockCommitter.cs",
    "backend/src/MyGarage.Infrastructure/Persistence/Stores/Notifications/EfNotificationStore.cs",
    "backend/src/MyGarage.Infrastructure/Persistence/Stores/GatePasses/EfGatePassAndApprovalStore.cs",
    "backend/src/MyGarage.Infrastructure/Persistence/Stores/Ai/EfAiStore.cs",
    "backend/src/MyGarage.Infrastructure/Persistence/Stores/Vouchers/EfVoucherStore.cs",
    "backend/src/MyGarage.Infrastructure/Persistence/Stores/Catalog/EfCatalogStore.cs",
    "backend/src/MyGarage.Infrastructure/Persistence/Stores/Customers/EfCustomerStore.cs",
    "backend/src/MyGarage.Infrastructure/Persistence/Stores/Vehicles/EfVehicleStore.cs",
    "backend/src/MyGarage.Infrastructure/Persistence/Stores/CustomerPortal/EfCustomerPortalStore.cs",
    "backend/src/MyGarage.Infrastructure/Persistence/Stores/Payroll/EfPayrollStore.cs",
    "backend/src/MyGarage.Infrastructure/Persistence/Stores/Inventory/EfInventoryStore.cs",
    "backend/src/MyGarage.Infrastructure/Persistence/Stores/Retail/EfRetailOrderStore.cs",
    "backend/src/MyGarage.Infrastructure/Persistence/Stores/Purchasing/EfPurchaseOrderStore.cs",
    "backend/src/MyGarage.Infrastructure/Persistence/Stores/Invoices/EfInvoiceStore.cs",
    "backend/src/MyGarage.Infrastructure/Persistence/Stores/Payments/EfPaymentStore.cs",
    "backend/src/MyGarage.Infrastructure/Persistence/Stores/Bays/EfBayStore.cs"
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
