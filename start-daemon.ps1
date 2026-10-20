<#
.SYNOPSIS
    Inicia o daemon de segundo plano do A52.DeviceManager no celular via ADB (UID 2000 shell).
#>
Write-Host "========================================================" -ForegroundColor Cyan
Write-Host "  Iniciando A52 Device Manager Background Daemon (Shell)" -ForegroundColor Cyan
Write-Host "========================================================" -ForegroundColor Cyan

$scriptPath = Join-Path $PSScriptRoot "a52-daemon.sh"
adb push $scriptPath /data/local/tmp/a52-daemon.sh
adb shell "dos2unix /data/local/tmp/a52-daemon.sh 2>/dev/null || sed -i 's/\r$//' /data/local/tmp/a52-daemon.sh; chmod 755 /data/local/tmp/a52-daemon.sh; nohup sh /data/local/tmp/a52-daemon.sh >/dev/null 2>&1 &"

Write-Host "`nDaemon iniciado com sucesso em segundo plano no celular!" -ForegroundColor Green
Write-Host "Telemetria e comandos ativos com privilégios UID 2000." -ForegroundColor Green
