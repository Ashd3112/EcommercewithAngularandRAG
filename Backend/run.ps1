# Save this script as run.ps1 in the Backend directory.
# You can run it with: .\run.ps1

Write-Host "Starting ProductService (http://localhost:5001)..." -ForegroundColor Green
$productProc = Start-Process dotnet -ArgumentList "run --project ProductService/ProductService.csproj" -PassThru -NoNewWindow

Write-Host "Starting ChatService (http://localhost:5002)..." -ForegroundColor Green
$chatProc = Start-Process dotnet -ArgumentList "run --project ChatService/ChatService.csproj" -PassThru -NoNewWindow

Write-Host "Starting Gateway (http://localhost:5194)..." -ForegroundColor Green
$gatewayProc = Start-Process dotnet -ArgumentList "run --project Gateway/Gateway.csproj" -PassThru -NoNewWindow

Write-Host "All services started! Gateway is routing traffic on http://localhost:5194" -ForegroundColor Cyan
Write-Host "Press Ctrl+C in this terminal to stop all microservices." -ForegroundColor Yellow

# Wait for Ctrl+C or process exit
try {
    while ($true) {
        Start-Sleep -Seconds 1
    }
}
finally {
    Write-Host "`nStopping microservices..." -ForegroundColor Red
    Stop-Process -Id $productProc.Id -Force -ErrorAction SilentlyContinue
    Stop-Process -Id $chatProc.Id -Force -ErrorAction SilentlyContinue
    Stop-Process -Id $gatewayProc.Id -Force -ErrorAction SilentlyContinue
    Write-Host "Stopped." -ForegroundColor Green
}
