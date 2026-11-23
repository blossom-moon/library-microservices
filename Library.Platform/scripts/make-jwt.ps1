<#
 Лаб. 10: видає тестовий JWT (HS256), який приймає Gateway і catalog-api (схема Local, Лаб. 09), без Auth Service.
 Підписує тим самим ключем, що лежить у Secret (Jwt__SecretKey). Токен потрібен, бо /api/** вимагає автентифікації (Лаб. 09).

   $env:JWT_SECRET = '<той самий ключ, що в create-secrets.ps1>'
   $token = ./scripts/make-jwt.ps1                       # роль Admin
   curl.exe -H "Authorization: Bearer $token" http://localhost:8080/api/books
#>
param(
    [string]$Role = 'Admin',
    [string]$User = 'k8s-demo',
    [string]$Issuer = 'library-auth',
    [string]$Audience = 'library-api',
    [int]$Minutes = 60
)

$secret = $env:JWT_SECRET
if ([string]::IsNullOrWhiteSpace($secret)) { throw 'Задайте $env:JWT_SECRET' }

function ConvertTo-Base64Url([byte[]]$bytes) { [Convert]::ToBase64String($bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_') }
function ConvertTo-Json64([hashtable]$o) { ConvertTo-Base64Url ([Text.Encoding]::UTF8.GetBytes(($o | ConvertTo-Json -Compress))) }

$now = [DateTimeOffset]::UtcNow
$header  = ConvertTo-Json64 @{ alg = 'HS256'; typ = 'JWT' }
$payload = ConvertTo-Json64 @{
    sub = [guid]::NewGuid().ToString(); unique_name = $User; role = $Role
    jti = [guid]::NewGuid().ToString(); iss = $Issuer; aud = $Audience
    iat = $now.ToUnixTimeSeconds(); exp = $now.AddMinutes($Minutes).ToUnixTimeSeconds()
}

$hmac = [Security.Cryptography.HMACSHA256]::new([Text.Encoding]::UTF8.GetBytes($secret))
$signature = ConvertTo-Base64Url ($hmac.ComputeHash([Text.Encoding]::UTF8.GetBytes("$header.$payload")))

"$header.$payload.$signature"
