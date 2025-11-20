#!/usr/bin/env pwsh

Write-Host "🧪 RabbitMQ STOMP Test Script" -ForegroundColor Cyan
Write-Host "================================" -ForegroundColor Cyan
Write-Host ""

# Najděte API endpoint v Aspire Dashboard
$apiUrl = Read-Host "Zadejte API URL z Aspire Dashboard (např. https://localhost:7xxx)"

if ([string]::IsNullOrWhiteSpace($apiUrl)) {
    Write-Host "❌ URL není zadána" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "📤 Posílám testovací zprávu..." -ForegroundColor Yellow

$body = @{
    user = "TestScript"
    text = "Hello from PowerShell test at $(Get-Date -Format 'HH:mm:ss')"
} | ConvertTo-Json

try {
    $response = Invoke-RestMethod -Uri "$apiUrl/messages" `
        -Method Post `
        -Body $body `
        -ContentType "application/json" `
        -SkipCertificateCheck

    Write-Host "✅ Zpráva odeslána úspěšně!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Response:" -ForegroundColor Cyan
    $response | ConvertTo-Json -Depth 5 | Write-Host
    Write-Host ""
    Write-Host "🔍 Kde zkontrolovat:" -ForegroundColor Yellow
    Write-Host "  1. Aspire Dashboard → Logs → 'consumer' - měli byste vidět přijatou zprávu" -ForegroundColor White
    Write-Host "  2. STOMP Chat v prohlížeči - zpráva se tam také zobrazí" -ForegroundColor White
} 
catch {
    Write-Host "❌ Chyba: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host ""
    Write-Host "💡 Tip: Zkontrolujte, že:" -ForegroundColor Yellow
    Write-Host "  - Aplikace běží (dotnet run v AppHost)" -ForegroundColor White
    Write-Host "  - URL je správná (zkontrolujte v Aspire Dashboard)" -ForegroundColor White
    Write-Host "  - Služba 'apiservice' je Running" -ForegroundColor White
}

Write-Host ""
Write-Host "Press any key to exit..." -ForegroundColor Gray
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
