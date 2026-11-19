# ===============================================================================
# Business Logic Flow:
# 1. [Configuration & Discovery]: Read environment variables, .env, and parameters.
# 2. [Admin Authentication]: Authenticate as System Admin against Keycloak to obtain JWT.
# 3. [Domain Prerequisite Resolution]: Query Identity API for Roles and Branches.
# 4. [Terminal Registration]: Ensure an active POS Register exists for Cashier tests.
# 5. [Staff Provisioning]: Idempotently register Reader & Cashier accounts via Identity API.
# 6. [Cashier PIN Security]: Configure PBKDF2-hashed PIN for cashiers via Identity API.
# 7. [Token Generation]: Mint Keycloak JWT tokens for readers via OAuth2 password grant.
# 8. [Artifact Generation]: Export valid JSON files to tests_production/k6/data/.
# ===============================================================================

[CmdletBinding()]
param(
    [string]$BaseUrl = $env:BASE_URL,
    [string]$KeycloakUrl = '',
    [string]$Realm = 'supermarket',
    [string]$ClientId = 'pos-terminal',
    [string]$AdminUsername = '',
    [string]$AdminPassword = '',
    [string]$DefaultUserPassword = 'PerfTestPassword123!',
    [string]$DefaultCashierPin = '1234',
    [int]$ReaderCount = 3,
    [int]$CashierCount = 2,
    [string]$PostgresContainer = 'pos-postgres',
    [string]$PostgresUser = '',
    [string]$PostgresDb = 'identity_db'
)

$ErrorActionPreference = 'Stop'

# --- Helper: Parse .env if present ---
function Import-EnvFile {
    param([string]$Path)
    if (Test-Path $Path) {
        Get-Content $Path | ForEach-Object {
            $line = $_.Trim()
            if ($line -and -not $line.StartsWith('#') -and $line.Contains('=')) {
                $idx = $line.IndexOf('=')
                $key = $line.Substring(0, $idx).Trim()
                $val = $line.Substring($idx + 1).Trim().Trim('"').Trim("'")
                if (-not [System.Environment]::GetEnvironmentVariable($key)) {
                    [System.Environment]::SetEnvironmentVariable($key, $val, 'Process')
                }
            }
        }
    }
}

# --- 1. Configuration & Discovery ---
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host " SuperMarket POS - Performance Test User Provisioning     " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

$ScriptDir = $PSScriptRoot
$K6Root = Split-Path -Parent $ScriptDir
$RepoRoot = Split-Path -Parent (Split-Path -Parent $K6Root)
$DataDir = Join-Path $K6Root "data"
$EnvPath = Join-Path $RepoRoot ".env"

if (Test-Path $EnvPath) {
    Write-Host "[+] Loading environment from $EnvPath..." -ForegroundColor Gray
    Import-EnvFile -Path $EnvPath
}

if (-not $BaseUrl) {
    $BaseUrl = if ($env:BASE_URL) { $env:BASE_URL } else { 'http://127.0.0.1:5080' }
}
$BaseUrl = $BaseUrl.TrimEnd('/')

if (-not $KeycloakUrl) {
    $kcPort = if ($env:KEYCLOAK_HTTP_PORT) { $env:KEYCLOAK_HTTP_PORT } else { '8080' }
    $KeycloakUrl = "http://localhost:$kcPort" 
}
$KeycloakUrl = $KeycloakUrl.TrimEnd('/')

if (-not $AdminUsername) {
    $AdminUsername = if ($env:KEYCLOAK_ADMIN) { $env:KEYCLOAK_ADMIN } else { 'admin' }
}
if (-not $AdminPassword) {
    $AdminPassword = if ($env:KEYCLOAK_ADMIN_PASSWORD) { $env:KEYCLOAK_ADMIN_PASSWORD } else { 'admin123' }
}
if (-not $PostgresUser) {
    $PostgresUser = if ($env:POSTGRES_USER) { $env:POSTGRES_USER } else { 'postgres' }
}

Write-Host "Identity API Base URL : $BaseUrl" -ForegroundColor Gray
Write-Host "Keycloak Base URL     : $KeycloakUrl (Realm: $Realm)" -ForegroundColor Gray
Write-Host "Target Data Directory : $DataDir" -ForegroundColor Gray

# Ensure data directory exists
if (-not (Test-Path $DataDir)) {
    New-Item -ItemType Directory -Path $DataDir -Force | Out-Null
}

# --- 2. Admin Authentication (OAuth2 Password Grant) ---
Write-Host "`n[1/7] Authenticating as Administrator in Keycloak..." -ForegroundColor Yellow

$TokenEndpoint = "$KeycloakUrl/realms/$Realm/protocol/openid-connect/token"
$AdminToken = $null

$TokenBody = @{
    client_id  = $ClientId
    grant_type = 'password'
    username   = $AdminUsername
    password   = $AdminPassword
}

try {
    $TokenResponse = Invoke-RestMethod -Uri $TokenEndpoint -Method Post -Body $TokenBody -ContentType 'application/x-www-form-urlencoded'
    $AdminToken = $TokenResponse.access_token
    Write-Host "  -> Successfully acquired Admin Access Token (Expires in: $($TokenResponse.expires_in)s)" -ForegroundColor Green
}
catch {
    Write-Warning "Direct login with client '$ClientId' failed. Attempting master realm fallback..."
    try {
        $MasterTokenBody = @{
            client_id  = 'admin-cli'
            grant_type = 'password'
            username   = $AdminUsername
            password   = $AdminPassword
        }
        $MasterTokenEndpoint = "$KeycloakUrl/realms/master/protocol/openid-connect/token"
        $TokenResponse = Invoke-RestMethod -Uri $MasterTokenEndpoint -Method Post -Body $MasterTokenBody -ContentType 'application/x-www-form-urlencoded'
        $AdminToken = $TokenResponse.access_token
        Write-Host "  -> Successfully acquired Master Admin Access Token" -ForegroundColor Green
    }
    catch {
        throw "Failed to authenticate against Keycloak. Check credentials and server status. Details: $_"
    }
}

$AuthHeaders = @{
    Authorization = "Bearer $AdminToken"
    Accept        = "application/json"
}

 try {                                                                                                                                                                                             
        $ClientsList = Invoke-RestMethod -Uri "$KeycloakUrl/admin/realms/$Realm/clients" -Method Get -Headers $AuthHeaders                                                                            
        $TerminalClient = $ClientsList | Where-Object { $_.clientId -eq $ClientId } | Select-Object -First 1                                                                                          
        if ($TerminalClient) {                                                                                                                                                                        
            $Mappers = Invoke-RestMethod -Uri "$KeycloakUrl/admin/realms/$Realm/clients/$($TerminalClient.id)/protocol-mappers/models" -Method Get -Headers $AuthHeaders                              
            $HasAudMapper = $Mappers | Where-Object { $_.name -eq 'pos-api-audience' }                                                                                                                
            if (-not $HasAudMapper) {                                                                                                                                                                 
                $AudMapperBody = @{                                                                                                                                                                   
                    name           = "pos-api-audience"                                                                                                                                               
                    protocol       = "openid-connect"                                                                                                                                                 
                    protocolMapper = "oidc-audience-mapper"                                                                                                                                           
                    consentRequired = $false                                                                                                                                                          
                    config         = @{                                                                                                                                                               
                        "included.custom.audience" = "pos-api"                                                                                                                                        
                        "id.token.claim"           = "false"                                                                                                                                          
                        "access.token.claim"       = "true"                                                                                                                                           
                    }                                                                                                                                                                                 
                } | ConvertTo-Json                                                                                                                                                                    
                Invoke-RestMethod -Uri "$KeycloakUrl/admin/realms/$Realm/clients/$($TerminalClient.id)/protocol-mappers/models" -Method Post -Headers $AuthHeaders -Body $AudMapperBody -ContentType  
                'application/json' | Out-Null                                                                                                                                                         
                Write-Host "  -> Verified/Configured 'pos-api' audience mapper in Keycloak client '$ClientId'" -ForegroundColor Gray                                                                  
            }                                                                                                                                                                                         
        }                                                                                                                                                                                             
    } catch {                                                                                                                                                                                         
        Write-Warning "Could not verify/add audience mapper: $_"                                                                                                                                      
    }

# --- 3. Domain Prerequisite Resolution: Roles & Branches ---
Write-Host "`n[2/7] Fetching Roles and Branches from Identity API..." -ForegroundColor Yellow

$RolesResponse = $null
try {
    $RolesResponse = Invoke-RestMethod -Uri "$BaseUrl/api/v1/roles?page=1&pageSize=50" -Method Get -Headers $AuthHeaders
}
catch {
    throw "Failed to fetch roles from Identity API ($BaseUrl/api/v1/roles). Is the API running? Details: $_"
}

$Roles = $RolesResponse.items
$ReaderRole = $Roles | Where-Object { $_.roleName -eq 'Branch Manager' } | Select-Object -First 1
if (-not $ReaderRole) {
    $ReaderRole = $Roles | Where-Object { $_.roleName -eq 'System Admin' } | Select-Object -First 1
}
$CashierRole = $Roles | Where-Object { $_.roleName -eq 'Cashier' } | Select-Object -First 1

if (-not $ReaderRole) { throw "Could not find a Role with users:read permissions ('Branch Manager' or 'System Admin')." }
if (-not $CashierRole) { throw "Could not find 'Cashier' role in Identity API." }

Write-Host "  -> Reader Role  : $($ReaderRole.roleName) ($($ReaderRole.id))" -ForegroundColor Gray
Write-Host "  -> Cashier Role : $($CashierRole.roleName) ($($CashierRole.id))" -ForegroundColor Gray

# Fetch or Create Branch
$BranchesResponse = Invoke-RestMethod -Uri "$BaseUrl/api/v1/branches?page=1&pageSize=10" -Method Get -Headers $AuthHeaders
$BranchId = $null
if ($BranchesResponse.items -and $BranchesResponse.items.Count -gt 0) {
    $BranchId = $BranchesResponse.items[0].id
    Write-Host "  -> Using existing branch: $($BranchesResponse.items[0].name) ($BranchId)" -ForegroundColor Gray
}
else {
    Write-Host "  -> No branches found. Creating default performance test branch..." -ForegroundColor Yellow
    $NewBranchBody = @{
        code       = "HQ-01"
        name       = "Central Benchmark Supermarket"
        street     = "King Fahd Road"
        city       = "Riyadh"
        region     = "Central"
        postalCode = "12271"
        phone      = "+966110000000"
        currency   = "SAR"
    } | ConvertTo-Json

    $BranchResult = Invoke-RestMethod -Uri "$BaseUrl/api/v1/branches" -Method Post -Headers $AuthHeaders -Body $NewBranchBody -ContentType 'application/json'
    $BranchId = $BranchResult.id
    Write-Host "  -> Created new branch with ID: $BranchId" -ForegroundColor Green
}

# --- 4. Terminal Registration: Ensure POS Register exists ---
Write-Host "`n[3/7] Ensuring active POS Register exists for cashier terminals..." -ForegroundColor Yellow
$DefaultRegisterId = [System.Guid]::Parse("11111111-2222-3333-4444-555555555555")

# Check if docker is available to execute SQL check
$DockerAvailable = (Get-Command docker -ErrorAction SilentlyContinue) -ne $null
if ($DockerAvailable) {
    try {
        $Sql = "INSERT INTO identity.pos_registers (id, branch_id, register_code, name, is_active, is_deleted, created_at, updated_at) " +
               "VALUES ('$DefaultRegisterId', '$BranchId', 'REG-PERF-01', 'Main Benchmark Register', true, false, NOW(), NOW()) " +
               "ON CONFLICT (id) DO UPDATE SET is_active = true, is_deleted = false, branch_id = '$BranchId';"
        $DockerOutput = $Sql | docker exec -i $PostgresContainer psql -U $PostgresUser -d $PostgresDb 2>&1
        Write-Host "  -> Verified POS Register in PostgreSQL ($DefaultRegisterId)" -ForegroundColor Green
    }
    catch {
        Write-Warning "Could not interact with PostgreSQL via docker exec. Using fallback Register ID: $DefaultRegisterId"
    }
}
else {
    Write-Host "  -> Docker command not directly available. Assuming pre-existing Register ID: $DefaultRegisterId" -ForegroundColor Gray
}

# --- 5. Staff Provisioning: Create Readers and Cashiers via Identity API ---
Write-Host "`n[4/7] Provisioning Reader accounts (users:read:all)..." -ForegroundColor Yellow

$ProvisionedReaders = @()
for ($i = 1; $i -le $ReaderCount; $i++) {
    $Seq = "{0:D2}" -f $i
    $Username = "perf.reader.$Seq"
    $Phone = "+9677700010$Seq"
    $Email = "perf.reader.$Seq@supermarket.pos"
    $FullName = "Performance Reader $Seq"

    $CreateBody = @{
        username        = $Username
        fullName        = $FullName
        phoneNumber     = $Phone
        email           = $Email
        roleId          = $ReaderRole.id
        branchId        = $BranchId
        initialPassword = $DefaultUserPassword
    } | ConvertTo-Json

    try {
        $Res = Invoke-RestMethod -Uri "$BaseUrl/api/v1/staff" -Method Post -Headers $AuthHeaders -Body $CreateBody -ContentType 'application/json'
        Write-Host "  [+] Created reader: $Username (ID: $($Res.id))" -ForegroundColor Green
        $ProvisionedReaders += @{ username = $Username; id = $Res.id }
    }
    catch {
        if ($_.Exception.Response.StatusCode -eq [System.Net.HttpStatusCode]::Conflict) {
            Write-Host "  [*] Reader already exists: $Username (Skipping creation)" -ForegroundColor Cyan
            $ProvisionedReaders += @{ username = $Username; id = $null }
        }
        else {
            Write-Warning "Failed to create reader $Username : $_"
        }
    }
}

Write-Host "`n[5/7] Provisioning Cashier accounts and configuring PINs..." -ForegroundColor Yellow

$ProvisionedCashiers = @()
for ($i = 1; $i -le $CashierCount; $i++) {
    $Seq = "{0:D2}" -f $i
    $Username = "perf.cashier.$Seq"
    $Phone = "+9677700020$Seq"
    $Email = "perf.cashier.$Seq@supermarket.pos"
    $FullName = "Performance Cashier $Seq"

    $CreateBody = @{
        username        = $Username
        fullName        = $FullName
        phoneNumber     = $Phone
        email           = $Email
        roleId          = $CashierRole.id
        branchId        = $BranchId
        initialPassword = $DefaultUserPassword
    } | ConvertTo-Json

    $CashierId = $null
    try {
        $Res = Invoke-RestMethod -Uri "$BaseUrl/api/v1/staff" -Method Post -Headers $AuthHeaders -Body $CreateBody -ContentType 'application/json'
        $CashierId = $Res.id
        Write-Host "  [+] Created cashier: $Username (ID: $CashierId)" -ForegroundColor Green
    }
    catch {
        if ($_.Exception.Response.StatusCode -eq [System.Net.HttpStatusCode]::Conflict) {
            Write-Host "  [*] Cashier already exists: $Username" -ForegroundColor Cyan
            # Query existing user ID
            try {
                $SearchRes = Invoke-RestMethod -Uri "$BaseUrl/api/v1/staff?search=$Username&pageSize=1" -Method Get -Headers $AuthHeaders
                if ($SearchRes.items -and $SearchRes.items.Count -gt 0) {
                    $CashierId = $SearchRes.items[0].id
                }
            } catch { }
        }
        else {
            Write-Warning "Failed to create cashier $Username : $_"
        }
    }

    # Set Cashier Fast-Login PIN via API
    if ($CashierId) {
        try {
            $PinBody = @{ pin = $DefaultCashierPin } | ConvertTo-Json
            Invoke-RestMethod -Uri "$BaseUrl/api/v1/staff/$CashierId/pin" -Method Put -Headers $AuthHeaders -Body $PinBody -ContentType 'application/json' | Out-Null
            Write-Host "      -> Fast-login PIN set successfully for $Username" -ForegroundColor Gray
        }
        catch {
            Write-Warning "Failed to set PIN for ${Username}: $_"
        }
    }

    $ProvisionedCashiers += @{
        username   = $Username
        pin        = $DefaultCashierPin
        registerId = $DefaultRegisterId.ToString()
    }
}

# --- 6. Token Generation: Acquire Keycloak JWTs for Readers ---
Write-Host "`n[6/7] Minting active JWT tokens from Keycloak..." -ForegroundColor Yellow

$TokensList = @()
$FirstReaderToken = $null
$FirstCashierToken = $null

foreach ($reader in $ProvisionedReaders) {
    $UserTokenBody = @{
        client_id  = $ClientId
        grant_type = 'password'
        username   = $reader.username
        password   = $DefaultUserPassword
    }

    try {
        $UserTokenRes = Invoke-RestMethod -Uri $TokenEndpoint -Method Post -Body $UserTokenBody -ContentType 'application/x-www-form-urlencoded'
        $TokensList += @{
            label = $reader.username
            token = $UserTokenRes.access_token
        }
        if (-not $FirstReaderToken) { $FirstReaderToken = $UserTokenRes.access_token }
        Write-Host "  [✓] Minted JWT for $($reader.username) (Expires in: $($UserTokenRes.expires_in)s)" -ForegroundColor Green
    }
    catch {
        Write-Warning "Could not obtain token for $($reader.username): $_"
    }
}

# Acquire a Cashier token to use as FORBIDDEN_TOKEN in smoke test
if ($ProvisionedCashiers.Count -gt 0) {
    try {
        $CashierTokenBody = @{
            client_id  = $ClientId
            grant_type = 'password'
            username   = $ProvisionedCashiers[0].username
            password   = $DefaultUserPassword
        }
        $CashierTokenRes = Invoke-RestMethod -Uri $TokenEndpoint -Method Post -Body $CashierTokenBody -ContentType 'application/x-www-form-urlencoded'
        $FirstCashierToken = $CashierTokenRes.access_token
        Write-Host "  [✓] Minted Cashier JWT for smoke test (Forbidden Token)" -ForegroundColor Green
    }
    catch {
        Write-Warning "Could not obtain token for cashier: $_"
    }
}

# --- 7. Artifact Generation: Write to data/staff-tokens.json and cashiers.json ---
Write-Host "`n[7/7] Saving test data artifacts for k6..." -ForegroundColor Yellow

$StaffTokensPath = Join-Path $DataDir "staff-tokens.json"
$CashiersPath = Join-Path $DataDir "cashiers.json"

$TokensList | ConvertTo-Json -Depth 4 | Set-Content -Path $StaffTokensPath -Encoding utf8
Write-Host "  -> Saved $($TokensList.Count) tokens to $StaffTokensPath" -ForegroundColor Green

$ProvisionedCashiers | ConvertTo-Json -Depth 4 | Set-Content -Path $CashiersPath -Encoding utf8
Write-Host "  -> Saved $($ProvisionedCashiers.Count) cashier records to $CashiersPath" -ForegroundColor Green

Write-Host "`n==========================================================" -ForegroundColor Cyan
Write-Host " Provisioning Completed Successfully!                     " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "To execute the smoke test, run:" -ForegroundColor White
Write-Host "  `$env:BASE_URL = '$BaseUrl'" -ForegroundColor Gray
Write-Host "  `$env:READER_TOKEN = '$FirstReaderToken'" -ForegroundColor Gray
Write-Host "  `$env:FORBIDDEN_TOKEN = '$FirstCashierToken'" -ForegroundColor Gray
Write-Host "  .\tests_production\k6\scripts\run-k6.ps1 -Test authz-smoke -Build discovery" -ForegroundColor Yellow
Write-Host "==========================================================`n" -ForegroundColor Cyan
