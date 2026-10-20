@echo off
echo ========================================================
echo   Iniciando A52 Device Manager Background Daemon (Shell)
echo ========================================================
adb push "%~dp0a52-daemon.sh" /data/local/tmp/a52-daemon.sh
adb shell "dos2unix /data/local/tmp/a52-daemon.sh 2>/dev/null || sed -i 's/\r$//' /data/local/tmp/a52-daemon.sh; chmod 755 /data/local/tmp/a52-daemon.sh; nohup sh /data/local/tmp/a52-daemon.sh >/dev/null 2>&1 &"
echo.
echo Daemon iniciado com sucesso em segundo plano no celular!
echo Telemetria e comandos ativos com privilegios UID 2000.
pause
