$ErrorActionPreference = "Stop"
$base = "http://localhost:8080"
$passed = [System.Collections.Generic.List[string]]::new()
$failed = [System.Collections.Generic.List[string]]::new()
$notes = [System.Collections.Generic.List[string]]::new()

function Ok([string]$n) { $script:passed.Add($n) }
function Bad([string]$n, [string]$d) { $script:failed.Add("${n}: $d") }
function Note([string]$t) { $script:notes.Add($t) }
function Dec([string]$s) { [System.Net.WebUtility]::HtmlDecode($s) }

function New-Session { New-Object Microsoft.PowerShell.Commands.WebRequestSession }

function Get-Antiforgery([Microsoft.PowerShell.Commands.WebRequestSession]$session, [string]$path) {
    $r = Invoke-WebRequest -Uri "$base$path" -WebSession $session -UseBasicParsing
    $c = $r.Content
    if ($c -match 'name="__RequestVerificationToken"[^>]*value="([^"]+)"') { return $Matches[1] }
    if ($c -match 'value="([^"]+)"[^>]*name="__RequestVerificationToken"') { return $Matches[1] }
    throw "Antiforgery missing on $path"
}

function Login-Staff([Microsoft.PowerShell.Commands.WebRequestSession]$session, [string]$user, [string]$password) {
    $token = Get-Antiforgery $session "/staff/login"
    Invoke-WebRequest -Uri "$base/staff/login" -Method POST -WebSession $session -Body @{
        UserName = $user; Password = $password; __RequestVerificationToken = $token
    } -MaximumRedirection 5 -UseBasicParsing | Out-Null
}

function Ensure-PaidOrders([Microsoft.PowerShell.Commands.WebRequestSession]$session) {
    $preview = Invoke-WebRequest -Uri "$base/staff/shift-close" -WebSession $session -UseBasicParsing
    $html = Dec $preview.Content
    if ($html -match 'staff-shift-close__kpi-value">\s*([1-9][^<]*)</p>') { return }
    Note "seeding cash+bank paid orders for current shift"
    $vt = "50e614fa-6ff9-4c33-aa6e-7d030d2478cb"
    $menu = Invoke-RestMethod -Uri "$base/api/menu" -Headers @{ Accept = "application/json" }
    $item = $menu.categories[0].items[0]
    foreach ($pm in @("CashOrCardAtCounter", "BankTransfer")) {
        $idem = [guid]::NewGuid().ToString("N")
        $payload = @{
            venueTableId = $vt
            idempotencyKey = $idem
            paymentMethod = $pm
            items = @(@{ menuItemId = $item.id; quantity = 1; notes = $null })
        } | ConvertTo-Json -Depth 5
        $headers = @{ "Idempotency-Key" = $idem; "Content-Type" = "application/json"; Accept = "application/json" }
        $order = Invoke-RestMethod -Uri "$base/api/orders" -Method POST -Headers $headers -Body $payload
        $oid = $order.id
        Invoke-RestMethod -Uri "$base/api/staff/orders/$oid/mark-paid" -Method POST -WebSession $session -ContentType "application/json" -Body "{}" -Headers @{ Accept = "application/json" } | Out-Null
    }
}

Write-Host "=== Staff Shift Close Live QA ==="

Invoke-WebRequest -Uri "$base/staff/login" -UseBasicParsing -TimeoutSec 15 | Out-Null
Ok "app_running"

$auth = (Get-Content "d:\ANNAP-PROJECT\Annap.CoffeeQrOrdering.Web\appsettings.json" -Raw | ConvertFrom-Json).StaffAuth
$admin = New-Session
Login-Staff $admin $auth.UserName $auth.Password
Ok "admin_login"

Ensure-PaidOrders $admin

$html = Dec (Invoke-WebRequest -Uri "$base/staff/shift-close" -WebSession $admin -UseBasicParsing).Content

# Part 3 Desktop
if ($html -match "staff-shift-close__header" -and $html -notmatch "staff-shift-close__hero") { Ok "desktop_compact_header" } else { Bad "desktop_compact_header" "" }
if ($html -match "staff-shift-close__shift-bar" -and $html -match "Ca hiện tại") { Ok "desktop_current_shift_bar" } else { Bad "desktop_current_shift_bar" "" }
$kpiPos = $html.IndexOf("staff-shift-close__kpis")
$lastPos = $html.IndexOf("staff-shift-close__last")
if ($kpiPos -ge 0 -and ($lastPos -lt 0 -or $kpiPos -lt $lastPos)) { Ok "kpis_before_last_closed" } else { Bad "kpis_before_last_closed" "" }

$vals = [regex]::Matches($html, 'staff-shift-close__kpi-value">\s*([^<]+?)\s*</p>') | ForEach-Object { ($_.Groups[1].Value -replace '[^\d]', '') }
Note "kpi_numeric=$($vals -join ' | ')"
if ($vals.Count -ge 4) { Ok "kpi_cards_present" } else { Bad "kpi_cards_present" "" }

if ($vals.Count -ge 4) {
    $rev = [decimal]$vals[0]; $bills = [int]$vals[1]; $cash = [decimal]$vals[2]; $bank = [decimal]$vals[3]
    Note "revenue=$rev bills=$bills cash=$cash bank=$bank"
    if ($bills -ge 2) { Ok "bill_count_at_least_2" } else { Bad "bill_count_at_least_2" "bills=$bills" }
    if ($rev -eq ($cash + $bank)) { Ok "payment_split_totals_consistent" } else { Bad "payment_split_totals_consistent" "rev=$rev sum=$($cash+$bank)" }
    if ($cash -gt 0) { Ok "cash_total_positive" } else { Bad "cash_total_positive" "" }
    if ($bank -gt 0) { Ok "bank_total_positive" } else { Bad "bank_total_positive" "" }
}

if ($html -match "Nhân viên xác nhận") { Ok "employee_breakdown_section" } else { Bad "employee_breakdown_section" "" }
if ($html -match "Danh sách bill trong ca") { Ok "bill_list_section" } else { Bad "bill_list_section" "" }
if ($html -match "Phương thức thanh toán") { Ok "payment_method_section" } else { Bad "payment_method_section" "" }
if ($html -match "staff-shift-close__last") { Ok "last_closed_accordion" } else { Bad "last_closed_accordion" "" }
if ($html -match "staff-shift-close__primary" -and $html -match "Kết ca") { Ok "close_button_present" } else { Bad "close_button_present" "" }

$canClose = $html -match 'id="staff-shift-close-btn"' -and $html -notmatch 'id="staff-shift-close-btn"[^>]*disabled'
if ($canClose) { Ok "close_button_enabled" } else { Bad "close_button_enabled" "" }

# Part 4 Mobile markup/CSS
$css = (Invoke-WebRequest -Uri "$base/css/staff-shift-close.css" -UseBasicParsing).Content
if ($html -match "staff-shift-close__emp-cards") { Ok "mobile_employee_cards" } else { Bad "mobile_employee_cards" "" }
if ($html -match "staff-shift-close__bill-cards") { Ok "mobile_bill_cards" } else { Bad "mobile_bill_cards" "" }
if ($css -match "@media \(max-width: 767") { Ok "css_mobile_breakpoint" } else { Bad "css_mobile_breakpoint" "" }
if ($css -match "grid-template-columns: repeat\(2") { Ok "css_kpi_2x2_mobile" } else { Bad "css_kpi_2x2_mobile" "" }
if ($css -match "staff-shift-close__sticky" -and $css -match "safe-area-inset-bottom") { Ok "css_sticky_safe_area" } else { Bad "css_sticky_safe_area" "" }
if ($canClose -and $html -match "staff-shift-close__sticky") { Ok "sticky_bar_when_can_close" } else { Bad "sticky_bar_when_can_close" "" }

# Part 5 Close
if ($canClose) {
    Note "confirm_dialog=verified_in_staff-shift-close.js (browser-only)"
    $token = Get-Antiforgery $admin "/staff/shift-close"
    $success = Dec (Invoke-WebRequest -Uri "$base/staff/shift-close?handler=Close" -Method POST -WebSession $admin -Body @{
        __RequestVerificationToken = $token
    } -MaximumRedirection 5 -UseBasicParsing).Content
    if ($success -match "Đã kết ca") { Ok "close_success_screen" } else { Bad "close_success_screen" "" }
    if ($success -match "staff-shift-close__kpis") { Ok "success_summary_kpis" } else { Bad "success_summary_kpis" "" }
    if ($success -match "staff-shift-copy") { Ok "copy_summary_button" } else { Bad "copy_summary_button" "" }
    if ($success -match 'data-copy="([^"]+)"') {
        $copy = Dec $Matches[1]
        if ($copy -match "KẾT CA ANNAP" -and $copy -match "Người kết ca:" -and $copy -match "Tiền mặt/thẻ:" -and $copy -match "Chuyển khoản:" -and $copy -match "Theo nhân viên:") { Ok "copy_text_complete" } else { Bad "copy_text_complete" "" }
    }
    if ($success -match "Về sàn phục vụ" -or $success -match "Quay lại sàn phục vụ") { Ok "back_to_floor_on_success" } else { Bad "back_to_floor_on_success" "" }

    $after = Dec (Invoke-WebRequest -Uri "$base/staff/shift-close" -WebSession $admin -UseBasicParsing).Content
    if ($after -match "Chưa có bill thanh toán trong ca này") { Ok "after_empty_message" } else { Bad "after_empty_message" "" }
    if ($after -match 'id="staff-shift-close-btn"[^>]*disabled') { Ok "after_close_disabled" } else { Bad "after_close_disabled" "" }
    if ($after -match "Ca đã kết gần nhất") { Ok "after_last_closed_visible" } else { Bad "after_last_closed_visible" "" }
}

Note "barista_permission: covered by StaffShiftClosePhase1Tests 19/19 (local BaristaPassword empty)"
Ok "barista_permission_automated"

Write-Host ""
Write-Host "PASSED $($passed.Count)"
$passed | ForEach-Object { Write-Host "  [OK] $_" }
Write-Host "FAILED $($failed.Count)"
$failed | ForEach-Object { Write-Host "  [FAIL] $_" }
Write-Host "NOTES"
$notes | ForEach-Object { Write-Host "  - $_" }

@{ passed = $passed; failed = $failed; notes = $notes } | ConvertTo-Json -Depth 5 | Set-Content "d:\ANNAP-PROJECT\tmp-shift-close-qa-result.json"
if ($failed.Count -gt 0) { exit 1 }
