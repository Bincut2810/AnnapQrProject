$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Net.Http
$base = "http://localhost:8080"
$results = [ordered]@{}

function Note($key, $pass, $detail) {
    $script:results[$key] = @{ pass = $pass; detail = $detail }
    Write-Host ("[{0}] {1} - {2}" -f ($(if ($pass) {"PASS"} else {"FAIL"})), $key, $detail)
}

function Login-Staff([string]$password) {
    $session = New-Object Microsoft.PowerShell.Commands.WebRequestSession
    $get = Invoke-WebRequest -Uri "$base/Staff/Login" -WebSession $session -UseBasicParsing
    $m = [regex]::Match($get.Content, 'name="__RequestVerificationToken"[^>]*value="([^"]+)"')
    if (-not $m.Success) { throw "No antiforgery token" }
    $body = @{ UserName = "host"; Password = $password; __RequestVerificationToken = $m.Groups[1].Value }
    Invoke-WebRequest -Uri "$base/Staff/Login" -Method Post -Body $body -WebSession $session -UseBasicParsing | Out-Null
    return $session
}

function Post-OrderJson($session, $payload, $idem) {
    $json = $payload | ConvertTo-Json -Depth 6 -Compress
    $headers = @{ "Idempotency-Key" = $idem; "Content-Type" = "application/json" }
    $r = Invoke-WebRequest -Uri "$base/api/orders" -Method Post -Body $json -Headers $headers -WebSession $session -UseBasicParsing
    return ($r.Content | ConvertFrom-Json)
}

function Post-Webhook($payload) {
    $json = $payload | ConvertTo-Json -Compress
    return Invoke-RestMethod -Uri "$base/api/webhooks/bank-transfer/dev" -Method Post -Body $json -ContentType "application/json"
}

function Find-BoardOrder($board, $orderId) {
    foreach ($col in @("submitted","paid","active","completed")) {
        if ($board.PSObject.Properties.Name -contains $col) {
            foreach ($o in $board.$col) { if ($o.id -eq $orderId) { return @{ column = $col; order = $o } } }
        }
    }
    return $null
}

# Part 1
$bt = Invoke-RestMethod "$base/api/guest/bank-transfer"
Note "1_bank_transfer_enabled" ($bt.enabled -eq $true) "enabled=$($bt.enabled)"

# Part 2
$guest = New-Object Microsoft.PowerShell.Commands.WebRequestSession
$table = Invoke-WebRequest -Uri "$base/table/T01" -WebSession $guest -UseBasicParsing
$html = $table.Content
$mVt = [regex]::Match($html, '"venueTableId"\s*:\s*"([0-9a-fA-F\-]{36})"')
if (-not $mVt.Success) { throw "Could not parse venueTableId from /table/T01" }
$venueTableId = [guid]$mVt.Groups[1].Value
$mCat = [regex]::Match($html, '<script type="application/json" id="menu-catalog-json">(.+?)</script>', [Text.RegularExpressions.RegexOptions]::Singleline)
$catalog = $mCat.Groups[1].Value | ConvertFrom-Json
$menuItems = @()
if ($catalog -is [System.Array]) {
    $menuItems = @($catalog | Select-Object -First 2)
} else {
    foreach ($cat in $catalog.categories) { foreach ($it in $cat.items) { $menuItems += $it; if ($menuItems.Count -ge 2) { break } }; if ($menuItems.Count -ge 2) { break } }
}
$items = @(@{ menuItemId = $menuItems[0].id; quantity = 2; notes = $null }, @{ menuItemId = $menuItems[1].id; quantity = 1; notes = $null })
$idem1 = "e2e-bank-" + [guid]::NewGuid().ToString("N")
$submitted = Post-OrderJson $guest @{ venueTableId = $venueTableId; idempotencyKey = $idem1; paymentMethod = "BankTransfer"; items = $items } $idem1
$orderId = [guid]$submitted.id
$token = $submitted.guestSessionToken
$qr = Invoke-RestMethod "$base/api/orders/$orderId/transfer-qr?token=$([Uri]::EscapeDataString($token))"
$trackBefore = Invoke-RestMethod "$base/api/track/orders/$orderId`?token=$([Uri]::EscapeDataString($token))"
$trackPage = (Invoke-WebRequest -Uri "$base/track/$orderId`?token=$([Uri]::EscapeDataString($token))" -WebSession $guest -UseBasicParsing).Content
$memo = [string]$qr.memo
$amount = [decimal]$qr.amount
$part2 = ($qr.qrImageUrl) -and ($qr.accountNumber) -and ($trackBefore.pendingPayment) -and ($trackBefore.checkBill) -and ($trackPage.Contains($memo))
Note "2_guest_qr_tracking" $part2 "order=$orderId amount=$amount memo=$memo"

# Part 3
$checkoutSess = Login-Staff "checkout-dev-secret16"
$boardBefore = (Invoke-RestMethod -Uri "$base/api/staff/orders" -WebSession $checkoutSess)
$found = Find-BoardOrder $boardBefore $orderId
$part3 = ($found.column -eq "submitted") -and ($found.order.transferMemo -eq $memo)
$baristaSess = Login-Staff "barista-dev-secret16"
try {
    Invoke-WebRequest -Uri "$base/api/staff/orders/$orderId/mark-paid" -Method Post -Body "{}" -ContentType "application/json" -WebSession $baristaSess -UseBasicParsing | Out-Null
    $part3 = $false
} catch {
    $code = $_.Exception.Response.StatusCode.value__
    $part3 = $part3 -and ($code -eq 403)
}
Note "3_staff_board_before" $part3 "column=$($found.column) transferMemo=$($found.order.transferMemo)"

# Part 4
$wh1 = Post-Webhook @{ provider="dev"; transactionId="dev-e2e-001"; amount=$amount; memo=$memo; receivedAtUtc=(Get-Date).ToUniversalTime().ToString("o") }
$boardAfter = Invoke-RestMethod -Uri "$base/api/staff/orders" -WebSession $checkoutSess
$foundPaid = Find-BoardOrder $boardAfter $orderId
$trackPaid = Invoke-RestMethod "$base/api/track/orders/$orderId`?token=$([Uri]::EscapeDataString($token))"
$part4 = ($wh1.status -eq "matched") -and ($foundPaid.column -eq "paid") -and (-not $trackPaid.pendingPayment) -and ($trackPaid.showBill)
Note "4_webhook_matched" $part4 "status=$($wh1.status) auto=$($foundPaid.order.paymentConfirmedBy)"

# Part 5
$whDup = Post-Webhook @{ provider="dev"; transactionId="dev-e2e-001"; amount=$amount; memo=$memo; receivedAtUtc=(Get-Date).ToUniversalTime().ToString("o") }
$part5 = ($whDup.status -eq "duplicate")
Note "5_webhook_duplicate" $part5 "status=$($whDup.status)"

# Part 6
$idem2 = "e2e-amt-" + [guid]::NewGuid().ToString("N")
$sub2 = Post-OrderJson $guest @{ venueTableId = $venueTableId; idempotencyKey = $idem2; paymentMethod = "BankTransfer"; items = @(@{ menuItemId = $menuItems[0].id; quantity = 1; notes = $null }) } $idem2
$order2 = [guid]$sub2.id
$qr2 = Invoke-RestMethod "$base/api/orders/$order2/transfer-qr?token=$([Uri]::EscapeDataString($sub2.guestSessionToken))"
$whAmt = Post-Webhook @{ provider="dev"; transactionId="dev-e2e-amt-mismatch"; amount=($qr2.amount + 1); memo=$qr2.memo; receivedAtUtc=(Get-Date).ToUniversalTime().ToString("o") }
$foundAmt = Find-BoardOrder (Invoke-RestMethod -Uri "$base/api/staff/orders" -WebSession $checkoutSess) $order2
$part6 = ($whAmt.status -eq "amount_mismatch") -and ($foundAmt.column -eq "submitted")
Note "6_webhook_wrong_amount" $part6 "status=$($whAmt.status)"

# Part 7
$whUnm = Post-Webhook @{ provider="dev"; transactionId="dev-e2e-unmatched"; amount=999999; memo="ANNAP NO-SUCH-ORDER-E2E"; receivedAtUtc=(Get-Date).ToUniversalTime().ToString("o") }
$part7 = ($whUnm.status -eq "unmatched")
Note "7_webhook_wrong_memo" $part7 "status=$($whUnm.status)"

# Part 8
$adminSess = Login-Staff "ChangeMe"
$today = (Get-Date).ToString("yyyy-MM-dd")
$adminUrl = "$base/admin/payments?preset=custom&from=$today&to=$today"
$adminHtml = (Invoke-WebRequest -Uri $adminUrl -WebSession $adminSess -UseBasicParsing).Content
$searchHtml = (Invoke-WebRequest -Uri ($adminUrl + "&q=dev-e2e-001") -WebSession $adminSess -UseBasicParsing).Content
$idM = [regex]::Match($searchHtml, 'id=([0-9a-fA-F\-]{36})')
$detailOk = $false
if ($idM.Success) {
    $detailHtml = (Invoke-WebRequest -Uri ($adminUrl + "&id=$($idM.Groups[1].Value)") -WebSession $adminSess -UseBasicParsing).Content
    $detailOk = $detailHtml.Contains("Raw payload") -and $detailHtml.Contains("<details")
}
$coDenied = $false; $baDenied = $false
try { Invoke-WebRequest -Uri "$base/admin/payments" -WebSession (Login-Staff "checkout-dev-secret16") -UseBasicParsing | Out-Null } catch { $coDenied = $true }
try { Invoke-WebRequest -Uri "$base/admin/payments" -WebSession (Login-Staff "barista-dev-secret16") -UseBasicParsing | Out-Null } catch { $baDenied = $true }
$part8 = $adminHtml.Contains("Đã khớp") -and $adminHtml.Contains("Trùng giao dịch") -and $adminHtml.Contains("Sai số tiền") -and $adminHtml.Contains("Chưa khớp") -and $searchHtml.Contains("dev-e2e-001") -and $detailOk -and $coDenied -and $baDenied
Note "8_admin_reconciliation" $part8 "detail=$detailOk denied checkout=$coDenied barista=$baDenied"

# Part 9
$idemCash = "e2e-cash-" + [guid]::NewGuid().ToString("N")
$cash = Post-OrderJson $guest @{ venueTableId = $venueTableId; idempotencyKey = $idemCash; paymentMethod = "CashOrCardAtCounter"; items = @(@{ menuItemId = $menuItems[0].id; quantity = 1; notes = $null }) } $idemCash
$cashQrFail = $true
try { Invoke-RestMethod "$base/api/orders/$($cash.id)/transfer-qr?token=$([Uri]::EscapeDataString($cash.guestSessionToken))" | Out-Null; $cashQrFail = $false } catch {}
Invoke-WebRequest -Uri "$base/api/staff/orders/$($cash.id)/mark-paid" -Method Post -Body "{}" -ContentType "application/json" -WebSession $checkoutSess -UseBasicParsing | Out-Null
$trackCash = Invoke-RestMethod "$base/api/track/orders/$($cash.id)?token=$([Uri]::EscapeDataString($cash.guestSessionToken))"
$cashSearch = (Invoke-WebRequest -Uri ($adminUrl + "&q=$($cash.id)") -WebSession $adminSess -UseBasicParsing).Content
$part9 = $cashQrFail -and ($trackCash.showBill) -and (-not $cashSearch.Contains($cash.id))
Note "9_cash_regression" $part9 "noQr=$cashQrFail showBill=$($trackCash.showBill)"

Write-Host ""
$fail = @($results.Values | Where-Object { -not $_.pass })
Write-Host "OVERALL: $(if ($fail.Count -eq 0) {'PASS'} else {"FAIL ($($fail.Count))"})"
if ($fail.Count -gt 0) { exit 1 }
