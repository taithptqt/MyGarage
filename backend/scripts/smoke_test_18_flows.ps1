param(
  [string]$BaseUrl = "http://localhost:5062",
  [string]$BearerToken = "",
  [long]$QuotationId = 0,
  [long]$ActorStaffId = 0,
  [string]$InsuranceEvidenceUrl = "https://example.com/evidence/zalo-approval.jpg",
  [long]$ServiceOrderId = 0,
  [long]$ShiftClosingId = 0,
  [long]$RetailReturnId = 0,
  [long]$SupplierInvoiceId = 0,
  [long]$PurchaseOrderItemId = 0,
  [switch]$DryRun
)

$ErrorActionPreference = "Stop"

function New-IdempotencyKey {
  return ([guid]::NewGuid().ToString("N"))
}

function Invoke-Api {
  param(
    [string]$Method,
    [string]$Path,
    [object]$Body = $null,
    [string]$IdempotencyKey = ""
  )

  $url = "$BaseUrl$Path"
  $headers = @{}

  if (-not [string]::IsNullOrWhiteSpace($BearerToken)) {
    $headers["Authorization"] = "Bearer $BearerToken"
  }

  if (-not [string]::IsNullOrWhiteSpace($IdempotencyKey)) {
    $headers["X-Idempotency-Key"] = $IdempotencyKey
  }

  if ($DryRun) {
    Write-Host "[DRY-RUN] $Method $url"
    if ($Body -ne $null) {
      Write-Host ("         body=" + ($Body | ConvertTo-Json -Depth 10 -Compress))
    }
    return @{ dryRun = $true; status = "SKIPPED" }
  }

  try {
    if ($Body -ne $null) {
      $json = $Body | ConvertTo-Json -Depth 10
      $response = Invoke-RestMethod -Method $Method -Uri $url -Headers $headers -ContentType "application/json; charset=utf-8" -Body $json
    }
    else {
      $response = Invoke-RestMethod -Method $Method -Uri $url -Headers $headers
    }

    return @{ status = "PASS"; response = $response }
  }
  catch {
    $err = $_.Exception.Message
    return @{ status = "FAIL"; error = $err }
  }
}

$results = New-Object System.Collections.Generic.List[object]

function Add-Result {
  param([string]$Name, [hashtable]$Result)
  $results.Add([pscustomobject]@{
    Name = $Name
    Status = $Result.status
    Error = if ($Result.ContainsKey("error")) { $Result.error } else { "" }
  }) | Out-Null
}

if ($QuotationId -gt 0 -and $ActorStaffId -gt 0) {
  $r = Invoke-Api -Method "POST" -Path "/api/quotations/$QuotationId/submit" -Body @{
    actorStaffId = $ActorStaffId
    note = "Smoke submit quotation"
    idempotencyKey = (New-IdempotencyKey)
  }
  Add-Result "Quotation Submit" $r
}

if ($QuotationId -gt 0 -and $ActorStaffId -gt 0) {
  $idem = New-IdempotencyKey
  $r = Invoke-Api -Method "POST" -Path "/api/quotations/$QuotationId/insurance-approval" -Body @{
    actorStaffId = $ActorStaffId
    insuranceApprovalEvidenceUrl = $InsuranceEvidenceUrl
    note = "Smoke insurance approved"
    idempotencyKey = $idem
  } -IdempotencyKey $idem
  Add-Result "Quotation Insurance Approval" $r
}

if ($QuotationId -gt 0 -and $ActorStaffId -gt 0) {
  $idem = New-IdempotencyKey
  $r1 = Invoke-Api -Method "POST" -Path "/api/quotations/$QuotationId/publish-customer-link" -Body @{
    actorStaffId = $ActorStaffId
    note = "Smoke publish link"
    sendEmail = $false
    idempotencyKey = $idem
  } -IdempotencyKey $idem
  Add-Result "Quotation Publish Link #1" $r1

  $r2 = Invoke-Api -Method "POST" -Path "/api/quotations/$QuotationId/publish-customer-link" -Body @{
    actorStaffId = $ActorStaffId
    note = "Smoke publish link duplicate"
    sendEmail = $false
    idempotencyKey = $idem
  } -IdempotencyKey $idem
  Add-Result "Quotation Publish Link #2 (Idempotent)" $r2
}

if ($ShiftClosingId -gt 0) {
  $r = Invoke-Api -Method "POST" -Path "/api/shift-closings/$ShiftClosingId/reconciliations" -Body @{
    actualBalance = 0
    totalCashOut = 0
    note = "Smoke reconcile"
  }
  Add-Result "Shift Reconcile" $r
}

if ($RetailReturnId -gt 0) {
  $r = Invoke-Api -Method "POST" -Path "/api/retail-returns/$RetailReturnId/finalizations"
  Add-Result "Retail Return Finalize" $r
}

if ($ServiceOrderId -gt 0) {
  $r = Invoke-Api -Method "POST" -Path "/api/service-orders/$ServiceOrderId/cancel-with-rollback" -Body @{
    reason = "Smoke rollback"
  }
  Add-Result "Service Order Cancel With Rollback" $r
}

if ($ServiceOrderId -gt 0) {
  $r = Invoke-Api -Method "POST" -Path "/api/service-orders/$ServiceOrderId/warranty-rework" -Body @{
    reason = "Smoke create rework from qc fail"
  }
  Add-Result "Service Order Warranty Rework" $r
}

if ($SupplierInvoiceId -gt 0 -and $PurchaseOrderItemId -gt 0) {
  $r = Invoke-Api -Method "POST" -Path "/api/accounting/supplier-invoice-details" -Body @{
    supplierInvoiceId = $SupplierInvoiceId
    purchaseOrderItemId = $PurchaseOrderItemId
    quantity = 1
    unitCost = 1000
    lineTotal = 1000
  }
  Add-Result "Supplier Invoice Detail 3-way" $r
}

Write-Host ""
Write-Host "===== Smoke Test Summary ====="
$results | Format-Table -AutoSize

$failCount = ($results | Where-Object { $_.Status -eq "FAIL" }).Count
if ($failCount -gt 0) {
  Write-Host "Smoke test có lỗi: $failCount case FAIL" -ForegroundColor Red
  exit 1
}

Write-Host "Smoke test hoàn tất." -ForegroundColor Green
exit 0
