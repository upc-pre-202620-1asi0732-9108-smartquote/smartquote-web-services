param(
    [ValidatePattern('^(All|US\d{2}|TS\d{2})$')][string]$Story = 'All',
    [ValidateSet('All','Unit','Contract','Integration')][string]$Level = 'All',
    [string]$ClientCommand = '',
    [string]$ClientDirectory = '',
    [switch]$ClientOnly,
    [switch]$Coverage
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$runId = [Guid]::NewGuid().ToString('N')
$tempRoot = Join-Path ([IO.Path]::GetTempPath()) "smartquote-tests-$runId"
$results = Join-Path $root "artifacts/test-results/$runId"
$container = "smartquote-tests-$runId"
$apiProcess = $null
$startedContainer = $false
$variables = @('SMARTQUOTE_TEST_CONNECTION','SMARTQUOTE_JWT_KEY_FILE','SMARTQUOTE_KEY_FILE','SMARTQUOTE_API_URL',
    'ConnectionStrings__DefaultConnection','Jwt__SigningKey','Jwt__Issuer','Jwt__Audience','AI__Provider',
    'Database__ApplyMigrations','ASPNETCORE_ENVIRONMENT','Logging__LogLevel__Default','SMARTQUOTE_BOOTSTRAP_PASSWORD',
    'SMARTQUOTE_E2E_AUTH','SMARTQUOTE_E2E_REAL','SMARTQUOTE_E2E_STUB','VITE_API_BASE_URL','ASPNETCORE_CONTENTROOT',
    'SMARTQUOTE_WEB_PORT','Cors__AllowedOrigins__0','Cors__AllowedOrigins__1')
$previous = @{}
foreach ($name in $variables) { $previous[$name] = [Environment]::GetEnvironmentVariable($name) }
function Invoke-Checked([string]$program, [string[]]$arguments) {
    & $program @arguments
    if ($LASTEXITCODE -ne 0) { throw "$program failed with exit code $LASTEXITCODE" }
}
function Free-Port {
    $listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 0)
    $listener.Start(); $port = $listener.LocalEndpoint.Port; $listener.Stop(); return $port
}
try {
    Set-Location $root
    New-Item -ItemType Directory -Path $tempRoot,$results -Force | Out-Null
    $env:SMARTQUOTE_JWT_KEY_FILE = Join-Path $tempRoot 'jwt-key.txt'
    $key = [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(48))
    [IO.File]::WriteAllText($env:SMARTQUOTE_JWT_KEY_FILE, $key)
    $env:SMARTQUOTE_KEY_FILE = $env:SMARTQUOTE_JWT_KEY_FILE
    if ($Level -in @('All','Integration') -or $ClientCommand) {
        Invoke-Checked 'docker' @('info','--format','{{.ServerVersion}}')
        Invoke-Checked 'docker' @('run','--detach','--rm','--name',$container,'--publish','127.0.0.1::5432',
            '--env','POSTGRES_DB=smartquote_tests','--env','POSTGRES_USER=smartquote_tests',
            '--env','POSTGRES_PASSWORD=TestOnly_NotProduction','postgres:16-alpine')
        $startedContainer = $true
        $mapping = (& docker port $container 5432).Trim()
        $dbPort = ($mapping -split ':')[-1]
        $ready = $false
        for ($attempt=0; $attempt -lt 60; $attempt++) {
            & docker exec $container pg_isready -U smartquote_tests -d smartquote_tests *> $null
            if ($LASTEXITCODE -eq 0) { $ready=$true; break }; Start-Sleep -Milliseconds 500
        }
        if (-not $ready) { throw 'Test PostgreSQL did not become ready.' }
        $env:SMARTQUOTE_TEST_CONNECTION = "Host=127.0.0.1;Port=$dbPort;Database=smartquote_tests;Username=smartquote_tests;Password=TestOnly_NotProduction"
        $env:ConnectionStrings__DefaultConnection = $env:SMARTQUOTE_TEST_CONNECTION
        $env:Jwt__SigningKey=$key; $env:Jwt__Issuer='SmartQuote'; $env:Jwt__Audience='SmartQuote.Clients'
        $env:AI__Provider='Stub'; $env:Database__ApplyMigrations='true'; $env:ASPNETCORE_ENVIRONMENT='Development'
        $env:Logging__LogLevel__Default='Warning'
        # API y navegador comparten hostname: SameSite=Lax funciona en HTTP local.
        $apiPort=Free-Port; $env:SMARTQUOTE_API_URL="http://localhost:$apiPort"
        $env:SMARTQUOTE_WEB_PORT=Free-Port
        $env:Cors__AllowedOrigins__0="http://localhost:$env:SMARTQUOTE_WEB_PORT"
        $env:Cors__AllowedOrigins__1="http://127.0.0.1:$env:SMARTQUOTE_WEB_PORT"
    }
    Invoke-Checked 'dotnet' @('build','SmartQuote.sln','--configuration','Release','--verbosity','quiet')
    if ($Level -eq 'All' -or $ClientCommand) {
        # Contenido raíz temporal: los documentos de BDD/clientes no llegan al App_Data real.
        $env:ASPNETCORE_CONTENTROOT=$tempRoot
        $processOptions=@{ FilePath='dotnet'; ArgumentList=@("`"$root/src/SmartQuote.API/bin/Release/net10.0/SmartQuote.API.dll`"",'--urls',$env:SMARTQUOTE_API_URL); PassThru=$true; RedirectStandardOutput=(Join-Path $tempRoot 'api.log'); RedirectStandardError=(Join-Path $tempRoot 'api.err') }
        if ($IsWindows -or $env:OS -eq 'Windows_NT') { $processOptions.WindowStyle='Hidden' }
        $apiProcess=Start-Process @processOptions
        $ready=$false
        for ($attempt=0; $attempt -lt 90; $attempt++) {
            if ($apiProcess.HasExited) { throw "Test API exited. See $(Join-Path $tempRoot 'api.err')" }
            try { $health=Invoke-WebRequest "$env:SMARTQUOTE_API_URL/health" -TimeoutSec 2; if ($health.StatusCode -eq 200) { $ready=$true; break } } catch { }
            Start-Sleep -Milliseconds 500
        }
        if (-not $ready) { throw 'Test API did not become ready.' }
    }
    if (-not $ClientOnly) {
        $filterParts=@()
        if ($Story -ne 'All') { $filterParts += "Story=$Story" }
        if ($Level -ne 'All') { $filterParts += "Category=$Level" }
        $arguments=@('test','SmartQuote.sln','--configuration','Release','--no-build','--logger','trx','--results-directory',$results)
        if ($Coverage) { $arguments+=@('--collect','Code Coverage;Format=Cobertura','--settings','tests/coverage.runsettings') }
        if ($filterParts.Count) { $arguments+=@('--filter',($filterParts -join '&')) }
        Invoke-Checked 'dotnet' $arguments
        $executed=0; $failed=0; $skipped=0
        foreach ($file in Get-ChildItem -LiteralPath $results -Filter '*.trx') {
            [xml]$trx=Get-Content -Raw -LiteralPath $file.FullName
            $executed += [int]$trx.TestRun.ResultSummary.Counters.executed
            $failed += [int]$trx.TestRun.ResultSummary.Counters.failed
            $skipped += [int]$trx.TestRun.ResultSummary.Counters.notExecuted
        }
        if ($executed -eq 0 -or $failed -gt 0 -or $skipped -gt 0) { throw "Invalid result: executed=$executed failed=$failed skipped=$skipped" }
        Write-Host "Story=$Story Level=$Level Executed=$executed Failed=$failed Skipped=$skipped"
        Write-Host "Results: $results"
    }
    if ($ClientCommand) {
        if (-not $ClientDirectory) { throw 'ClientDirectory is required.' }
        # Cuentas exclusivas de la base temporal: registro, aprobación y login reales.
        $env:SMARTQUOTE_BOOTSTRAP_PASSWORD='ClientTests!Only2026_Strong'
        $managerToken=$null
        foreach ($account in @(@('manager','PurchaseManager'),@('production','ProductionSpecialist'),@('analyst','PurchaseAnalyst'))) {
            $body=@{ email="$($account[0])@smartquote.local"; displayName="Test $($account[0])"; password=$env:SMARTQUOTE_BOOTSTRAP_PASSWORD; role=$account[1] } | ConvertTo-Json
            $registered=Invoke-RestMethod "$env:SMARTQUOTE_API_URL/api/v1/iam/auth/register" -Method Post -ContentType 'application/json' -Body $body
            if ($registered.status -eq 'Pending') {
                Invoke-RestMethod "$env:SMARTQUOTE_API_URL/api/v1/iam/registration-requests/$($registered.userId)/approve" -Method Post -Headers @{ Authorization="Bearer $managerToken" } -ContentType 'application/json' -Body (@{role=$account[1]} | ConvertTo-Json) | Out-Null
            }
            if ($account[0] -eq 'manager') {
                $login=Invoke-RestMethod "$env:SMARTQUOTE_API_URL/api/v1/iam/auth/login" -Method Post -ContentType 'application/json' -Body (@{email='manager@smartquote.local';password=$env:SMARTQUOTE_BOOTSTRAP_PASSWORD} | ConvertTo-Json)
                $managerToken=$login.accessToken
            }
        }
        $env:SMARTQUOTE_E2E_AUTH='1'; $env:SMARTQUOTE_E2E_REAL='1'; $env:SMARTQUOTE_E2E_STUB='1'
        $env:VITE_API_BASE_URL=$env:SMARTQUOTE_API_URL
        Set-Location -LiteralPath $ClientDirectory
        # Comando de pruebas solicitado explícitamente por el usuario/CI; no contiene secretos.
        & ([scriptblock]::Create($ClientCommand))
        if ($LASTEXITCODE -ne 0) { throw "Client tests failed: $LASTEXITCODE" }
    }
} catch {
    # Conservar el diagnóstico antes de retirar únicamente el entorno temporal.
    foreach ($log in @('api.log','api.err')) {
        $source = Join-Path $tempRoot $log
        if (Test-Path -LiteralPath $source) { Copy-Item -LiteralPath $source -Destination (Join-Path $results $log) }
    }
    Write-Host "Failure diagnostics: $results"
    throw
} finally {
    if ($apiProcess -and -not $apiProcess.HasExited) { Stop-Process -Id $apiProcess.Id -ErrorAction SilentlyContinue }
    if ($startedContainer) { & docker stop $container | Out-Null }
    foreach ($name in $variables) { [Environment]::SetEnvironmentVariable($name,$previous[$name]) }
    # Solo directorio creado por esta ejecución, nunca la raíz del proyecto.
    if ($tempRoot.StartsWith([IO.Path]::GetTempPath(),[StringComparison]::OrdinalIgnoreCase) -and (Test-Path -LiteralPath $tempRoot)) { Remove-Item -LiteralPath $tempRoot -Recurse -Force }
    Set-Location $root
}
