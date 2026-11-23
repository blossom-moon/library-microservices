<#
 Перевірка матриці прав (Лаб. 09): для кожної ролі викликає ті самі ендпоінти через Gateway й друкує фактичні HTTP-коди.
 Allowed = будь-який код, крім 401/403 (авторизація проходить раніше за валідацію й пошук, тому 400/404 означає «доступ дозволено»).
 Запуск (PowerShell 7+), AppHost і Pulumi уже виконано:
   ./scripts/check-matrix.ps1 -KeycloakClientSecret '<PostmanClientSecret>' -AdminPassword 'Admin#12345'
#>
param(
    [string]$Gateway = 'http://localhost:5000',
    [string]$Keycloak = 'http://localhost:8080/realms/LibraryRealm',
    [string]$KeycloakClientSecret = '',
    [string]$KeycloakUserPassword = 'Pass#12345',
    [string]$AdminUser = 'admin',
    [string]$AdminPassword = 'Admin#12345'
)

$endpoints = @(
    @{ Name = 'GET    /api/books            (catalog:read)';    Method = 'GET';    Path = '/api/books' },
    @{ Name = 'POST   /api/books            (catalog:create)';  Method = 'POST';   Path = '/api/books';            Body = '{}' },
    @{ Name = 'PUT    /api/books/0/price    (catalog:update)';  Method = 'PUT';    Path = '/api/books/0/price';     Body = '{}' },
    @{ Name = 'DELETE /api/books/0          (catalog:delete)';  Method = 'DELETE'; Path = '/api/books/0' },
    @{ Name = 'GET    /api/loans/0          (loans:read)';      Method = 'GET';    Path = '/api/loans/0' },
    @{ Name = 'POST   /api/loans            (loans:create)';    Method = 'POST';   Path = '/api/loans';            Body = '{}' },
    @{ Name = 'POST   /api/loans/0/cancel   (loans:delete)';    Method = 'POST';   Path = '/api/loans/0/cancel' },
    @{ Name = 'GET    /api/payments         (payments:read)';   Method = 'GET';    Path = '/api/payments' },
    @{ Name = 'POST   /api/payments/process (payments:process)';Method = 'POST';   Path = '/api/payments/process' },
    @{ Name = 'POST   /api/payments/refund  (payments:refund)'; Method = 'POST';   Path = '/api/payments/refund' }
)

function Get-LocalToken([string]$user, [string]$password) {
    $r = Invoke-RestMethod -Method Post -Uri "$Gateway/auth/login" -ContentType 'application/json' `
        -Body (@{ userNameOrEmail = $user; password = $password } | ConvertTo-Json)
    $r.accessToken
}

function Get-KeycloakToken([string]$user) {
    $r = Invoke-RestMethod -Method Post -Uri "$Keycloak/protocol/openid-connect/token" -ContentType 'application/x-www-form-urlencoded' `
        -Body @{ grant_type = 'password'; client_id = 'postman'; client_secret = $KeycloakClientSecret; username = $user; password = $KeycloakUserPassword }
    $r.access_token
}

$subjects = [ordered]@{
    'guest (без токена)'          = $null
    'Identity: Admin'             = { Get-LocalToken $AdminUser $AdminPassword }
}
if ($KeycloakClientSecret) {
    $subjects['Keycloak: admin (alice)']   = { Get-KeycloakToken 'alice' }
    $subjects['Keycloak: manager (bob)']   = { Get-KeycloakToken 'bob' }
    $subjects['Keycloak: user (carol)']    = { Get-KeycloakToken 'carol' }
    $subjects['Keycloak: guest (dave)']    = { Get-KeycloakToken 'dave' }
}

$results = foreach ($endpoint in $endpoints) {
    $row = [ordered]@{ Endpoint = $endpoint.Name }
    foreach ($subject in $subjects.Keys) {
        $headers = @{}
        if ($subjects[$subject]) { $headers['Authorization'] = 'Bearer ' + (& $subjects[$subject]) }
        $params = @{ Method = $endpoint.Method; Uri = "$Gateway$($endpoint.Path)"; Headers = $headers; SkipHttpErrorCheck = $true }
        if ($endpoint.Body) { $params.Body = $endpoint.Body; $params.ContentType = 'application/json' }
        $code = (Invoke-WebRequest @params).StatusCode
        $row[$subject] = if ($code -in 401, 403) { "$code ✗" } else { "$code ✓" }
    }
    [pscustomobject]$row
}

$results | Format-Table -AutoSize
$results | ConvertTo-Csv -NoTypeInformation | Set-Content -Encoding utf8 matrix-result.csv
Write-Host "Збережено: matrix-result.csv (вставити в звіт як таблицю)"
