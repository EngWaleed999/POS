#requires -Version 5.1
<#
.SYNOPSIS
  Creates/reuses disposable Identity API test users and exports k6 data files.

.DESCRIPTION
  - Authenticates as the seeded System Admin in the supermarket realm.
  - Uses that realm-issued JWT only against the Identity API (correct issuer/audience).
  - Ensures a dedicated benchmark branch and an active POS register.
  - Creates/reuses Store Manager readers and Cashier users through the Identity API.
  - Sets cashier PINs through the Identity API (PBKDF2 happens in the application).
  - Obtains user JWTs through the pos-terminal client and validates reader=200/cashier=403.
  - Writes local-only JSON data files without a UTF-8 BOM.

  Run only against a disposable local/test database and Keycloak realm.
#>
[CmdletBinding()]
param(
    [string]$BaseUrl = '',
    [string]$KeycloakUrl = '',
    [string]$Realm = 'supermarket',
    [string]$ClientId = 'pos-terminal',
    [string]$IdentityAdminUsername = '',
    [string]$IdentityAdminPassword = '',
    [string]$DefaultUserPassword = 'PerfTestPassword123!',
    [ValidatePattern('^\d{4,6}$')][string]$DefaultCashierPin = '5826',
    [ValidateRange(1, 60)][int]$ReaderCount = 10,
    [ValidateRange(1, 20)][int]$CashierCount = 5,
    [string]$PostgresContainer = 'pos-postgres',
    [string]$PostgresUser = '',
    [string]$PostgresDb = 'identity_db',
    [string]$PerformanceBranchCode = 'PERF-HQ-01'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Import-EnvFile {
    param([string]$Path)
    if (-not (Test-Path -LiteralPath $Path)) { return }

    foreach ($lineValue in Get-Content -LiteralPath $Path) {
        $line = $lineValue.Trim()
        if (-not $line -or $line.StartsWith('#') -or -not $line.Contains('=')) { continue }
        $separator = $line.IndexOf('=')
        $key = $line.Substring(0, $separator).Trim()
        $value = $line.Substring($separator + 1).Trim().Trim('"').Trim("'")
        if ($key -and -not [Environment]::GetEnvironmentVariable($key, 'Process')) {
            [Environment]::SetEnvironmentVariable($key, $value, 'Process')
        }
    }
}

function Get-HttpStatusCode {
    param($ErrorRecord)
    try {
        $status = $ErrorRecord.Exception.Response.StatusCode
        if ($null -ne $status) { return [int]$status }
    }
    catch { }
    return 0
}

function Write-Utf8NoBom {
    param([string]$Path, [string]$Content)
    $encoding = [System.Text.UTF8Encoding]::new($false)
    [System.IO.File]::WriteAllText($Path, $Content, $encoding)
}

function Invoke-IdentityApi {
    param(
        [ValidateSet('GET', 'POST', 'PUT')][string]$Method,
        [string]$Path,
        [object]$Body = $null
    )

    $request = @{
        Uri = ($script:BaseUrl + $Path)
        Method = $Method
        Headers = $script:ApiHeaders
        TimeoutSec = 30
        ErrorAction = 'Stop'
    }

    if ($null -ne $Body) {
        $request.ContentType = 'application/json'
        $request.Body = ConvertTo-Json -InputObject $Body -Depth 10 -Compress
    }

    Invoke-RestMethod @request
}

function New-RealmAccessToken {
    param([string]$Username, [string]$Password)

    $form = @{
        client_id = $script:ClientId
        grant_type = 'password'
        username = $Username
        password = $Password
    }

    $response = Invoke-RestMethod -Uri $script:TokenEndpoint -Method Post -Body $form -ContentType 'application/x-www-form-urlencoded' -TimeoutSec 30 -ErrorAction Stop
    if ([string]::IsNullOrWhiteSpace([string]$response.access_token)) {
        throw "Keycloak returned no access_token for account '$Username'."
    }
    return [string]$response.access_token
}

function Get-StaffByUsername {
    param([string]$Username)

    $escapedUsername = [Uri]::EscapeDataString($Username)
    $response = Invoke-IdentityApi -Method GET -Path ("/api/v1/staff?page=1&pageSize=100&search=" + $escapedUsername)
    $matches = @($response.items | Where-Object { [string]$_.username -ieq $Username })
    if ($matches.Count -gt 0) { return $matches[0] }
    return $null
}

function Ensure-StaffAccount {
    param(
        [string]$Username,
        [string]$FullName,
        [string]$PhoneNumber,
        [string]$Email,
        [string]$RoleId,
        [string]$BranchId
    )

    $existing = Get-StaffByUsername -Username $Username
    $staffId = $null

    if ($null -eq $existing) {
        $body = [ordered]@{
            username = $Username
            fullName = $FullName
            phoneNumber = $PhoneNumber
            email = $Email
            roleId = $RoleId
            branchId = $BranchId
            initialPassword = $script:DefaultUserPassword
        }

        try {
            $created = Invoke-IdentityApi -Method POST -Path '/api/v1/staff' -Body $body
            $staffId = [string]$created.id
        }
        catch {
            if ((Get-HttpStatusCode $_) -eq 409) {
                $existing = Get-StaffByUsername -Username $Username
            }
            else {
                throw "Could not create '$Username'. HTTP $(Get-HttpStatusCode $_): $($_.Exception.Message)"
            }
        }
    }

    if ($null -ne $existing) {
        $staffId = [string]$existing.id
        if ([string]::IsNullOrWhiteSpace($staffId)) {
            throw "Staff search found '$Username' without an id."
        }

        if ([string]$existing.roleId -ne $RoleId) {
            Invoke-IdentityApi -Method PUT -Path "/api/v1/staff/$staffId/role" -Body @{ newRoleId = $RoleId } | Out-Null
        }
        if ([string]$existing.branchId -ne $BranchId) {
            Invoke-IdentityApi -Method PUT -Path "/api/v1/staff/$staffId/transfer" -Body @{ newBranchId = $BranchId } | Out-Null
        }
        if ($existing.isActive -eq $false) {
            Invoke-IdentityApi -Method POST -Path "/api/v1/staff/$staffId/activate" | Out-Null
        }
    }

    if ([string]::IsNullOrWhiteSpace($staffId)) {
        throw "Provisioning '$Username' did not return a valid staff id."
    }
    return $staffId
}

function Ensure-PerformanceBranch {
    param([string]$BranchCode)

    $response = Invoke-IdentityApi -Method GET -Path '/api/v1/branches?page=1&pageSize=100'
    $matches = @($response.items | Where-Object { [string]$_.code -ieq $BranchCode })
    $branch = if ($matches.Count -gt 0) { $matches[0] } else { $null }

    if ($null -eq $branch) {
        $body = [ordered]@{
            code = $BranchCode
            name = 'Performance Test Branch'
            street = 'Test Street'
            city = 'Sanaa'
            region = 'Amanat Al Asimah'
            postalCode = '00000'
            phone = '+967770000001'
            taxNumber = 'PERF-TEST-TAX-001'
            email = 'performance-test@example.com'
            currency = 'YE'
        }

        try {
            $created = Invoke-IdentityApi -Method POST -Path '/api/v1/branches' -Body $body
            $branchId = [string]$created.id
        }
        catch {
            if ((Get-HttpStatusCode $_) -ne 409) { throw }
            $refresh = Invoke-IdentityApi -Method GET -Path '/api/v1/branches?page=1&pageSize=100'
            $matches = @($refresh.items | Where-Object { [string]$_.code -ieq $BranchCode })
            if ($matches.Count -eq 0) {
                throw "Branch '$BranchCode' returned HTTP 409 but could not be found."
            }
            $branch = $matches[0]
            $branchId = [string]$branch.id
        }
    }
    else {
        $branchId = [string]$branch.id
    }

    if ([string]::IsNullOrWhiteSpace($branchId)) { throw "Could not resolve branch '$BranchCode'." }
    if ($null -ne $branch -and $branch.isActive -eq $false) {
        Invoke-IdentityApi -Method POST -Path "/api/v1/branches/$branchId/activate" | Out-Null
    }
    return $branchId
}

function Ensure-PerformanceRegister {
    param([string]$BranchId, [string]$ContainerName, [string]$DatabaseUser, [string]$DatabaseName)

    if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
        throw 'Docker CLI was not found. Start Docker Desktop and ensure docker is in PATH.'
    }

    $newRegisterId = [Guid]::NewGuid().ToString()
    $sql = @"
INSERT INTO identity.pos_registers
    (id, branch_id, register_code, name, is_active, is_deleted, created_at, updated_at)
VALUES
    ('$newRegisterId', '$BranchId', 'REG-PERF-01', 'Performance Test Register', TRUE, FALSE, NOW(), NOW())
ON CONFLICT (branch_id, register_code) WHERE is_deleted = FALSE
DO UPDATE SET is_active = TRUE, updated_at = NOW()
RETURNING id;
"@

    $output = $sql | & docker exec -i $ContainerName psql -X -q -t -A -v ON_ERROR_STOP=1 -U $DatabaseUser -d $DatabaseName 2>&1
    $dockerExitCode = $LASTEXITCODE
    if ($dockerExitCode -ne 0) {
        $details = ($output | ForEach-Object { [string]$_ }) -join [Environment]::NewLine
        throw "POS register upsert failed (psql exit $dockerExitCode). $details"
    }

    $guidPattern = '^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$'
    $registerIdText = [string](@($output | Where-Object { ([string]$_).Trim() -match $guidPattern } | Select-Object -Last 1)[0]).Trim()
    $parsedRegisterId = [Guid]::Empty
    if (-not [Guid]::TryParse($registerIdText, [ref]$parsedRegisterId)) {
        throw "POS register upsert returned no valid GUID. Output: $($output -join '; ')"
    }
    return $parsedRegisterId.ToString()
}

# Resolve repo paths and load local .env values.
$scriptDir = $PSScriptRoot
$k6Root = Split-Path -Parent $scriptDir
$repoRoot = Split-Path -Parent (Split-Path -Parent $k6Root)
$dataDir = Join-Path $k6Root 'data'
Import-EnvFile -Path (Join-Path $repoRoot '.env')

if ([string]::IsNullOrWhiteSpace($BaseUrl)) {
    $BaseUrl = if ($env:BASE_URL) { $env:BASE_URL } else { 'http://127.0.0.1:5080' }
}
$BaseUrl = $BaseUrl.TrimEnd('/')

if ([string]::IsNullOrWhiteSpace($KeycloakUrl)) {
    if ($env:KEYCLOAK_BASE_URL) {
        $KeycloakUrl = $env:KEYCLOAK_BASE_URL
    }
    else {
        $keycloakPort = if ($env:KEYCLOAK_HTTP_PORT) { $env:KEYCLOAK_HTTP_PORT } else { '8080' }
        $KeycloakUrl = "http://127.0.0.1:$keycloakPort"
    }
}
$KeycloakUrl = $KeycloakUrl.TrimEnd('/')

if ([string]::IsNullOrWhiteSpace($IdentityAdminUsername)) {
    $IdentityAdminUsername = if ($env:IDENTITY_ADMIN_USERNAME) { $env:IDENTITY_ADMIN_USERNAME } else { 'admin' }
}
if ([string]::IsNullOrWhiteSpace($IdentityAdminPassword)) {
    $IdentityAdminPassword = if ($env:IDENTITY_ADMIN_PASSWORD) { $env:IDENTITY_ADMIN_PASSWORD } else { 'admin123' }
}
if ([string]::IsNullOrWhiteSpace($PostgresUser)) {
    $PostgresUser = if ($env:POSTGRES_USER) { $env:POSTGRES_USER } else { 'postgres' }
}
if ($env:IDENTITY_DB_NAME -and $PostgresDb -eq 'identity_db') {
    $PostgresDb = $env:IDENTITY_DB_NAME
}

$script:BaseUrl = $BaseUrl
$script:ClientId = $ClientId
$script:DefaultUserPassword = $DefaultUserPassword
$script:TokenEndpoint = "$KeycloakUrl/realms/$Realm/protocol/openid-connect/token"

if (-not (Test-Path -LiteralPath $dataDir)) {
    New-Item -ItemType Directory -Path $dataDir -Force | Out-Null
}

Write-Host ''
Write-Host '============================================================' -ForegroundColor Cyan
Write-Host ' SuperMarket POS - Performance Test Provisioning' -ForegroundColor Cyan
Write-Host '============================================================' -ForegroundColor Cyan
Write-Host "Identity API : $BaseUrl" -ForegroundColor Gray
Write-Host "Keycloak     : $KeycloakUrl (realm: $Realm, client: $ClientId)" -ForegroundColor Gray
Write-Host "Readers      : $ReaderCount (Store Manager)" -ForegroundColor Gray
Write-Host "Cashiers     : $CashierCount" -ForegroundColor Gray
Write-Warning 'Only run against a disposable test database and Keycloak realm.'

# 1. Obtain an API JWT from the supermarket realm, not the master realm.
Write-Host '[1/6] Authenticating the Identity API administrator...' -ForegroundColor Yellow
try {
    $identityAdminToken = New-RealmAccessToken -Username $IdentityAdminUsername -Password $IdentityAdminPassword
}
catch {
    throw "Could not get a supermarket-realm token for '$IdentityAdminUsername'. Ensure IdentityDataSeeder has created the admin account, the password is correct, and pos-terminal Direct Access Grants are enabled. Details: $($_.Exception.Message)"
}

$script:ApiHeaders = @{
    Authorization = "Bearer $identityAdminToken"
    Accept = 'application/json'
}

try {
    $rolesResponse = Invoke-IdentityApi -Method GET -Path '/api/v1/roles?page=1&pageSize=100'
}
catch {
    throw "Identity API rejected the supermarket-realm JWT. Verify issuer, audience=pos-api, and admin permissions. HTTP $(Get-HttpStatusCode $_): $($_.Exception.Message)"
}

$roles = @($rolesResponse.items)
$readerRole = $roles | Where-Object { [string]$_.roleName -eq 'Store Manager' } | Select-Object -First 1
$cashierRole = $roles | Where-Object { [string]$_.roleName -eq 'Cashier' } | Select-Object -First 1

if ($null -eq $readerRole -or [string]::IsNullOrWhiteSpace([string]$readerRole.id)) {
    throw "The 'Store Manager' role is missing. Run the Identity database seeder before provisioning."
}
if ($null -eq $cashierRole -or [string]::IsNullOrWhiteSpace([string]$cashierRole.id)) {
    throw "The 'Cashier' role is missing. Run the Identity database seeder before provisioning."
}

# 2. Ensure branch and POS register.
Write-Host '[2/6] Ensuring benchmark branch and POS register...' -ForegroundColor Yellow
$branchId = Ensure-PerformanceBranch -BranchCode $PerformanceBranchCode
$registerId = Ensure-PerformanceRegister -BranchId $branchId -ContainerName $PostgresContainer -DatabaseUser $PostgresUser -DatabaseName $PostgresDb
Write-Host "  [OK] Branch ID: $branchId" -ForegroundColor Green
Write-Host "  [OK] Register ID: $registerId" -ForegroundColor Green

# 3. Provision readers and mint their JWTs.
Write-Host '[3/6] Provisioning readers and obtaining JWTs...' -ForegroundColor Yellow
$readerTokens = [System.Collections.Generic.List[object]]::new()

for ($i = 1; $i -le $ReaderCount; $i++) {
    $sequence = '{0:D2}' -f $i
    $username = "perf.reader.$sequence"
    $phone = "+9677700010$sequence"
    $email = "perf.reader.$sequence@supermarket.pos"

    $staffId = Ensure-StaffAccount -Username $username -FullName "Performance Reader $sequence" -PhoneNumber $phone -Email $email -RoleId ([string]$readerRole.id) -BranchId $branchId
    $token = New-RealmAccessToken -Username $username -Password $DefaultUserPassword
    $readerTokens.Add([pscustomobject]@{ label = $username; token = $token })
    Write-Host "  [OK] $username provisioned; JWT obtained." -ForegroundColor Green
}

# 4. Provision cashiers and set their PINs using the Identity API.
Write-Host '[4/6] Provisioning cashiers and setting PINs...' -ForegroundColor Yellow
$cashierAccounts = [System.Collections.Generic.List[object]]::new()

for ($i = 1; $i -le $CashierCount; $i++) {
    $sequence = '{0:D2}' -f $i
    $username = "perf.cashier.$sequence"
    $phone = "+9677700020$sequence"
    $email = "perf.cashier.$sequence@supermarket.pos"

    $staffId = Ensure-StaffAccount -Username $username -FullName "Performance Cashier $sequence" -PhoneNumber $phone -Email $email -RoleId ([string]$cashierRole.id) -BranchId $branchId
    Invoke-IdentityApi -Method PUT -Path "/api/v1/staff/$staffId/pin" -Body @{ pin = $DefaultCashierPin } | Out-Null
    $cashierAccounts.Add([pscustomobject]@{
        username = $username
        pin = $DefaultCashierPin
        registerId = $registerId
    })
    Write-Host "  [OK] $username provisioned; PIN configured." -ForegroundColor Green
}

# 5. Authorization preflight: Reader must be 200; Cashier must be 403.
Write-Host '[5/6] Validating Reader and Cashier authorization...' -ForegroundColor Yellow
$readerToken = [string]$readerTokens[0].token
$cashierToken = New-RealmAccessToken -Username $cashierAccounts[0].username -Password $DefaultUserPassword
$staffUrl = "$BaseUrl/api/v1/staff?page=1&pageSize=5"

try {
    Invoke-RestMethod -Uri $staffUrl -Method GET -Headers @{ Authorization = "Bearer $readerToken"; Accept = 'application/json' } -TimeoutSec 20 -ErrorAction Stop | Out-Null
}
catch {
    throw "Reader preflight failed: expected HTTP 200; received HTTP $(Get-HttpStatusCode $_). Verify Keycloak sub/User.Id mapping and RedisPermissionService."
}

$cashierStatus = 0
try {
    Invoke-RestMethod -Uri $staffUrl -Method GET -Headers @{ Authorization = "Bearer $cashierToken"; Accept = 'application/json' } -TimeoutSec 20 -ErrorAction Stop | Out-Null
    $cashierStatus = 200
}
catch {
    $cashierStatus = Get-HttpStatusCode $_
}
if ($cashierStatus -ne 403) {
    throw "Cashier preflight expected HTTP 403 but received HTTP $cashierStatus. Verify Cashier role permissions."
}
Write-Host '  [OK] Reader JWT -> HTTP 200' -ForegroundColor Green
Write-Host '  [OK] Cashier JWT -> HTTP 403' -ForegroundColor Green

# 6. Write local JSON files without BOM and configure this PowerShell session.
Write-Host '[6/6] Writing local k6 data files...' -ForegroundColor Yellow
$staffTokensPath = Join-Path $dataDir 'staff-tokens.json'
$cashiersPath = Join-Path $dataDir 'cashiers.json'
$staffTokensJson = ConvertTo-Json -InputObject ($readerTokens.ToArray()) -Depth 5
$cashiersJson = ConvertTo-Json -InputObject ($cashierAccounts.ToArray()) -Depth 5

Write-Utf8NoBom -Path $staffTokensPath -Content $staffTokensJson
Write-Utf8NoBom -Path $cashiersPath -Content $cashiersJson

$env:BASE_URL = $BaseUrl
$env:READER_TOKEN = $readerToken
$env:FORBIDDEN_TOKEN = $cashierToken
$env:CREATE_STAFF_TOKEN = $identityAdminToken
$env:ROLE_ID = [string]$cashierRole.id
$env:BRANCH_ID = $branchId
$env:TEST_INITIAL_PASSWORD = $DefaultUserPassword
$env:RUN_ID = (Get-Random -Minimum 100000 -Maximum 999999).ToString()

Write-Host ''
Write-Host '============================================================' -ForegroundColor Cyan
Write-Host 'Provisioning completed; authorization preflight passed.' -ForegroundColor Green
Write-Host "Reader tokens : $($readerTokens.Count)" -ForegroundColor Gray
Write-Host "Cashier users : $($cashierAccounts.Count)" -ForegroundColor Gray
Write-Host "Data files    : $dataDir" -ForegroundColor Gray
Write-Host 'JWTs and PINs were not printed.' -ForegroundColor Gray
Write-Host ''
Write-Host 'Run next in THIS PowerShell window:' -ForegroundColor White
Write-Host '  .\tests_production\k6\scripts\run-k6.ps1 -Test authz-smoke -Build discovery' -ForegroundColor Yellow
Write-Host '  .\tests_production\k6\scripts\run-k6.ps1 -Test staff-read -Build discovery -RunId read-01 -CaptureTimeSeries' -ForegroundColor Yellow
Write-Host '============================================================' -ForegroundColor Cyan
