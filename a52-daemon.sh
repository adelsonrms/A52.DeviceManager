#!/system/bin/sh
APP_DIR="/storage/emulated/0/Android/data/com.devonno.a52/files"
mkdir -p "$APP_DIR"

while true; do
    # 1. Update telemetry
    hw=$(cat /sys/class/sec/tsp/hw_param 2>/dev/null)
    sens=$(cat /sys/class/sec/tsp/sensitivity_mode 2>/dev/null)
    slots=$(dumpsys input 2>/dev/null | grep -c 'inUse=true')
    scrub=$(cat /sys/class/sec/tsp/scrub_pos 2>/dev/null)
    uptime_str=$(uptime 2>/dev/null)

    # Extrair TNOI, TMUL, TWET como inteiros limpos
    tnoi=$(echo "$hw" | sed -n 's/.*"TNOI":"\([0-9]*\)".*/\1/p')
    tmul=$(echo "$hw" | sed -n 's/.*"TMUL":"\([0-9]*\)".*/\1/p')
    twet=$(echo "$hw" | sed -n 's/.*"TWET":"\([0-9]*\)".*/\1/p')
    [ -z "$tnoi" ] && tnoi=0
    [ -z "$tmul" ] && tmul=0
    [ -z "$twet" ] && twet=0
    [ -z "$slots" ] && slots=0

    # Gravar JSON valido sem aspas quebras
    cat <<EOF > "$APP_DIR/telemetry.json.tmp"
{"tnoi":$tnoi,"tmul":$tmul,"twet":$twet,"sens":"$sens","slots":$slots,"scrub":"$scrub","uptime":"$uptime_str","timestamp":$(date +%s)}
EOF
    mv "$APP_DIR/telemetry.json.tmp" "$APP_DIR/telemetry.json"
    
    # 2. Check for commands
    if [ -f "$APP_DIR/cmd" ]; then
        cmd=$(cat "$APP_DIR/cmd" 2>/dev/null | tr -d '\r\n ')
        rm -f "$APP_DIR/cmd"
        
        case "$cmd" in
            "unlock-touch")
                service call SemInputDeviceManagerService 29 i32 1 i32 1 s16 "module_off_master" >/dev/null 2>&1
                sleep 1
                service call SemInputDeviceManagerService 29 i32 1 i32 1 s16 "module_on_master" >/dev/null 2>&1
                sleep 0.5
                service call SemInputDeviceManagerService 29 i32 1 i32 1 s16 "set_fod_rect,421,2018,659,2256" >/dev/null 2>&1
                service call SemInputDeviceManagerService 29 i32 1 i32 1 s16 "fod_enable,1,1,0" >/dev/null 2>&1
                echo "OK" > "$APP_DIR/cmd_result"
                ;;
            "cycle-screen")
                input keyevent 223
                sleep 1
                input keyevent 224
                echo "OK" > "$APP_DIR/cmd_result"
                ;;
            "hardening")
                settings put system any_screen_enabled 0
                settings put system any_screen_running 0
                settings put system touch_sensitivity_mode 0
                settings put system double_tab_to_wake_up 0
                settings put secure double_tap_to_wake 0
                settings put system aod_tap_to_show_mode 0
                settings put system screen_off_pocket 0
                settings put system show_touches 0
                echo "OK" > "$APP_DIR/cmd_result"
                ;;
            "reboot")
                echo "OK" > "$APP_DIR/cmd_result"
                sleep 1
                reboot
                ;;
            *)
                echo "UNKNOWN" > "$APP_DIR/cmd_result"
                ;;
        esac
    fi
    
    sleep 1
done
