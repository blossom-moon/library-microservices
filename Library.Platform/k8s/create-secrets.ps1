<#
 Лаб. 10: створює Kubernetes Secrets командою kubectl, щоб значення НЕ потрапили в YAML у репозиторії.
 Паролі беруться зі змінних оточення (або запитуються). Повторний запуск оновлює секрети (apply).

   $env:SQL_PASSWORD  = 'Str0ng#Passw0rd'      # політика SQL Server: ≥ 8 символів, великі/малі літери, цифри, символи
   $env:RABBIT_PASSWORD = '<пароль>'
   $env:JWT_SECRET    = '<ключ підпису JWT з Лаб. 09, ≥ 32 символи>'
   ./k8s/create-secrets.ps1
#>
param([string]$Namespace = 'default')

function Get-Value([string]$name, [string]$prompt) {
    $v = [Environment]::GetEnvironmentVariable($name)
    if ([string]::IsNullOrWhiteSpace($v)) { $v = Read-Host $prompt -MaskInput }
    if ([string]::IsNullOrWhiteSpace($v)) { throw "Не задано $name" }
    $v
}

$sql    = Get-Value 'SQL_PASSWORD'    'Пароль SA для SQL Server'
$rabbit = Get-Value 'RABBIT_PASSWORD' 'Пароль RabbitMQ (guest)'
$jwt    = Get-Value 'JWT_SECRET'      'Ключ підпису JWT (≥ 32 символи)'
if ($jwt.Length -lt 32) { throw 'JWT_SECRET має містити щонайменше 32 символи' }

# Рядок підключення можна задати окремо (CATALOG_DB), інакше формується з пароля SA
$catalogDb = $env:CATALOG_DB
if ([string]::IsNullOrWhiteSpace($catalogDb)) {
    $catalogDb = "Server=sql-server,1433;Database=CatalogDb;User Id=sa;Password=$sql;TrustServerCertificate=True;Encrypt=False"
}

function Apply-Secret([string]$name, [string[]]$literals) {
    kubectl create secret generic $name @literals -n $Namespace --dry-run=client -o yaml | kubectl apply -n $Namespace -f -
}

Apply-Secret 'sql-secrets'      @("--from-literal=MSSQL_SA_PASSWORD=$sql")
Apply-Secret 'rabbitmq-secrets' @("--from-literal=RABBITMQ_DEFAULT_PASS=$rabbit")
Apply-Secret 'catalog-secrets'  @(
    "--from-literal=ConnectionStrings__CatalogDb=$catalogDb",
    "--from-literal=ConnectionStrings__messaging=amqp://guest:$rabbit@rabbitmq:5672",
    "--from-literal=Jwt__SecretKey=$jwt")
Apply-Secret 'gateway-secrets'  @("--from-literal=Jwt__SecretKey=$jwt")

Write-Host "Secrets створено: sql-secrets, rabbitmq-secrets, catalog-secrets, gateway-secrets"
