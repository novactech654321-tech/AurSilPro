# Starts the AurSil API (SQL Server LocalDB database: aursil) on port 5168.
# Usage:  powershell -ExecutionPolicy Bypass -File .\start-api.ps1

$env:DOTNET_ROOT = 'E:\DevTools\dotnet'
$env:DOTNET_MULTILEVEL_LOOKUP = '0'

sqllocaldb start MSSQLLocalDB | Out-Null

$ip = (Get-NetIPAddress -AddressFamily IPv4 | Where-Object { $_.IPAddress -notmatch '^(127|169\.254)' } | Select-Object -First 1).IPAddress
Write-Host "AurSil API starting..." -ForegroundColor Yellow
Write-Host "  On this PC:     http://localhost:5168"
Write-Host "  From the phone: http://${ip}:5168   (set this via the gear icon on the app's login screen)"
Write-Host "  OTP codes are printed below until SMTP is configured in AurSil.Api\appsettings.json`n"

Set-Location (Join-Path $PSScriptRoot 'AurSil.Api')
& E:\DevTools\dotnet\dotnet.exe run --launch-profile http
