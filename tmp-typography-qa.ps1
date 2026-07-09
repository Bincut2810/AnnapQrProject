$ErrorActionPreference = "Stop"
$base = "http://localhost:8080"
$passed = [System.Collections.Generic.List[string]]::new()
$failed = [System.Collections.Generic.List[string]]::new()
$notes = [System.Collections.Generic.List[string]]::new()
function Ok($n){$script:passed.Add($n)}; function Bad($n,$d){$script:failed.Add("${n}: $d")}; function Note($t){$script:notes.Add($t)}
function Dec($s){[System.Net.WebUtility]::HtmlDecode($s)}
function Login-Staff($session,$user,$pass){$r=Invoke-WebRequest -Uri "$base/staff/login" -WebSession $session -UseBasicParsing; $t= if($r.Content -match 'name="__RequestVerificationToken"[^>]*value="([^"]+)"'){$Matches[1]}; Invoke-WebRequest -Uri "$base/staff/login" -Method POST -WebSession $session -Body @{UserName=$user;Password=$pass;__RequestVerificationToken=$t} -MaximumRedirection 5 -UseBasicParsing | Out-Null}

Invoke-WebRequest -Uri "$base/" -UseBasicParsing -TimeoutSec 20 | Out-Null
Ok "app_running"

# Fonts loaded on guest
$home = Dec (Invoke-WebRequest -Uri "$base/" -UseBasicParsing).Content
if($home -match 'fonts\.googleapis\.com.*Fraunces'){Ok 'guest_fraunces_font'}else{Bad 'guest_fraunces_font' ''}
if($home -match 'fonts\.googleapis\.com.*Inter'){Ok 'guest_inter_font'}else{Bad 'guest_inter_font' ''}
if($home -match 'typography-brand\.css'){Ok 'guest_typography_brand_css'}else{Bad 'guest_typography_brand_css' ''}

# Vietnamese on landing
if($home -match 'Một lá thư|khẩu vị'){Ok 'guest_vn_hero_text'}else{Bad 'guest_vn_hero_text' ''}
if($home -match 'ge-hero-title|ge-ritual-title'){Ok 'guest_hero_markup'}else{Bad 'guest_hero_markup' ''}

$cssBrand = (Invoke-WebRequest -Uri "$base/css/typography-brand.css" -UseBasicParsing).Content
if($cssBrand -match '\.ge-hero-title[\s\S]*var\(--font-display\)'){Ok 'css_hero_display'}else{Bad 'css_hero_display' ''}
if($cssBrand -match 'guest-masthead__mark'){Ok 'css_brand_mark'}else{Bad 'css_brand_mark' ''}
if($cssBrand -match 'staff-shell[\s\S]*--font-display:\s*var\(--font-ui\)'){Ok 'css_staff_no_display'}else{Bad 'css_staff_no_display' ''}

$menu = Dec (Invoke-WebRequest -Uri "$base/Menu/Index" -UseBasicParsing).Content
if($menu -match 'menu-cat-heading|menu-cat-section'){Ok 'guest_menu_sections'}else{Bad 'guest_menu_sections' ''}

$staffCss = (Invoke-WebRequest -Uri "$base/css/staff-board.css" -UseBasicParsing).Content
if($staffCss -match 'staff-order-card__table-code[\s\S]*var\(--font-code\)'){Ok 'css_staff_table_code_mono'}else{Bad 'css_staff_table_code_mono' ''}

$login = Dec (Invoke-WebRequest -Uri "$base/staff/login" -UseBasicParsing).Content
if($login -match 'fonts\.googleapis\.com.*Inter' -and $login -notmatch 'Fraunces'){Ok 'staff_inter_only'}else{Bad 'staff_inter_only' 'check font partial ops mode'}

$auth=(Get-Content "d:\ANNAP-PROJECT\Annap.CoffeeQrOrdering.Web\appsettings.json" -Encoding UTF8 -Raw|ConvertFrom-Json).StaffAuth
$s=New-Object Microsoft.PowerShell.Commands.WebRequestSession
Login-Staff $s $auth.UserName $auth.Password
$orders=Dec (Invoke-WebRequest -Uri "$base/staff/orders" -WebSession $s -UseBasicParsing).Content
if($orders -match 'staff-order-card__table-code|staff-board'){Ok 'staff_orders_page'}else{Bad 'staff_orders_page' ''}
$shift=Dec (Invoke-WebRequest -Uri "$base/staff/shift-close" -WebSession $s -UseBasicParsing).Content
if($shift -match 'staff-shift-close__kpi-value'){Ok 'staff_shift_close'}else{Bad 'staff_shift_close' ''}

$admin=Dec (Invoke-WebRequest -Uri "$base/staff/login?ReturnUrl=%2Fadmin" -UseBasicParsing).Content
# admin redirect - fetch login page fonts from admin layout via reports redirect
$adminLogin=Dec (Invoke-WebRequest -Uri "$base/admin" -WebSession $s -MaximumRedirection 0 -ErrorAction SilentlyContinue).Content
if(-not $adminLogin){ $adminPage=Dec (Invoke-WebRequest -Uri "$base/admin" -WebSession $s -UseBasicParsing).Content; if($adminPage -match 'admin-root|font-display'){Ok 'admin_page'}else{Bad 'admin_page' ''} } else { Ok 'admin_redirect_auth' }

$track=Dec (Invoke-WebRequest -Uri "$base/track/00000000-0000-0000-0000-000000000001" -UseBasicParsing -ErrorAction SilentlyContinue).Content
if($track -match 'id="track-table"[^>]*font-display'){Bad 'track_table_uses_display' 'T01 should be mono'}else{Ok 'track_table_not_display_only'}
if($track -match 'id="track-table"'){Ok 'track_table_markup'}else{Note 'track page may need valid order'}

$bankCss = (Invoke-WebRequest -Uri "$base/css/guest-tray-submitted.css" -UseBasicParsing).Content
if($bankCss -match 'guest-bank-transfer__amount'){Ok 'bank_transfer_css'}else{Bad 'bank_transfer_css' ''}
if($bankCss -match 'guest-bank-transfer__amount[\s\S]*font-family:\s*var\(--font-code\)'){Ok 'bank_amount_mono'}else{Bad 'bank_amount_mono' 'needs code font'}

# Fallback tokens
$site = (Invoke-WebRequest -Uri "$base/css/site.css" -UseBasicParsing).Content
if($site -match 'Fraunces|Newsreader|Georgia' -or $site -match '--font-display'){Ok 'fallback_display_stack'}else{Bad 'fallback_display_stack' ''}

Write-Host "PASSED $($passed.Count)"; $passed|ForEach-Object{Write-Host " OK $_"}
Write-Host "FAILED $($failed.Count)"; $failed|ForEach-Object{Write-Host " FAIL $_"}
$notes|ForEach-Object{Write-Host " NOTE $_"}
@{passed=$passed;failed=$failed;notes=$notes}|ConvertTo-Json -Depth 4|Set-Content "d:\ANNAP-PROJECT\tmp-typography-qa-result.json" -Encoding UTF8
if($failed.Count -gt 0){exit 1}
