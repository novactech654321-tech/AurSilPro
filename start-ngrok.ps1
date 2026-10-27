# Makes the AurSil API (port 5168) reachable from any network through ngrok.
# First time only:  E:\DevTools\ngrok\ngrok.exe config add-authtoken YOUR_TOKEN
# Usage:  powershell -ExecutionPolicy Bypass -File .\start-ngrok.ps1
# Keep start-api.ps1 running in another window.

Write-Host "Starting ngrok tunnel to http://localhost:5168 ..." -ForegroundColor Yellow
Write-Host "Copy the https://....ngrok-free.app address from the 'Forwarding' line below"
Write-Host "and enter it in the app via the gear icon on the login screen.`n"

& E:\DevTools\ngrok\ngrok.exe http 5168
