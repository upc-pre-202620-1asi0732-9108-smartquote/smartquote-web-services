[CmdletBinding()]
param(
    [switch]$SkipBrowser
)

$ErrorActionPreference = 'Stop'

Write-Host 'Iniciando SmartQuote con Docker Compose...'
docker compose up --build -d
if ($LASTEXITCODE -ne 0) {
    throw 'Docker Compose no pudo iniciar los servicios.'
}

$healthUrl = 'http://localhost:8080/health/live'
$swaggerUrl = 'http://localhost:8080/swagger/index.html'
$deadline = (Get-Date).AddSeconds(90)
$ready = $false

while ((Get-Date) -lt $deadline) {
    try {
        $response = Invoke-WebRequest -Uri $healthUrl -UseBasicParsing -TimeoutSec 5
        if ($response.StatusCode -eq 200) {
            $ready = $true
            break
        }
    }
    catch {
        Start-Sleep -Seconds 2
    }
}

if (-not $ready) {
    docker compose ps
    docker compose logs --tail=100 api
    throw 'La API no respondió dentro del tiempo esperado.'
}

if (-not $SkipBrowser) {
    Start-Process $swaggerUrl
}

Write-Host "Swagger disponible en $swaggerUrl"
