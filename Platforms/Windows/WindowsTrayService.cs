#if WINDOWS
using System.Runtime.InteropServices;
using A52.DeviceManager.Models;
using A52.DeviceManager.Services;

namespace A52.DeviceManager.Platforms.Windows;

public sealed class WindowsTrayService : IDisposable
{
    private const int NIF_MESSAGE = 0x01;
    private const int NIF_ICON = 0x02;
    private const int NIF_TIP = 0x04;
    private const int NIF_INFO = 0x10;
    private const int NIM_ADD = 0x00;
    private const int NIM_MODIFY = 0x01;
    private const int NIM_DELETE = 0x02;

    private const int WM_CLOSE = 0x0010;
    private const int WM_APP_TRAY = 0x8000 + 152;
    private const int WM_LBUTTONUP = 0x0202;
    private const int WM_LBUTTONDBLCLK = 0x0203;
    private const int WM_RBUTTONUP = 0x0205;
    private const int WM_CONTEXTMENU = 0x007B;

    private const int SW_HIDE = 0;
    private const int SW_SHOWNORMAL = 1;
    private const int SW_RESTORE = 9;

    private const uint MF_STRING = 0x0000;
    private const uint MF_SEPARATOR = 0x0800;
    private const uint TPM_RETURNCMD = 0x0100;
    private const uint TPM_RIGHTBUTTON = 0x0002;
    private const uint TPM_BOTTOMALIGN = 0x0020;

    private const int CMD_OPEN = 2001;
    private const int CMD_UNLOCK_TOUCH = 2002;
    private const int CMD_CYCLE_SCREEN = 2003;
    private const int CMD_REBOOT = 2004;
    private const int CMD_EXIT = 2099;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NOTIFYICONDATA
    {
        public int cbSize;
        public IntPtr hWnd;
        public int uID;
        public int uFlags;
        public int uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;
        public int dwState;
        public int dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szInfo;
        public int uTimeoutOrVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szInfoTitle;
        public int dwInfoFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int x;
        public int y;
    }

    private delegate IntPtr SubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, UIntPtr uIdSubclass, IntPtr dwRefData);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern bool Shell_NotifyIcon(int dwMessage, ref NOTIFYICONDATA lpData);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern IntPtr CreatePopupMenu();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool AppendMenu(IntPtr hMenu, uint uFlags, uint uIDNewItem, string lpNewItem);

    [DllImport("user32.dll")]
    private static extern int TrackPopupMenu(IntPtr hMenu, uint uFlags, int x, int y, int nReserved, IntPtr hWnd, IntPtr prcRect);

    [DllImport("user32.dll")]
    private static extern bool DestroyMenu(IntPtr hMenu);

    [DllImport("comctl32.dll")]
    private static extern bool SetWindowSubclass(IntPtr hWnd, SubclassProc pfnSubclass, UIntPtr uIdSubclass, IntPtr dwRefData);

    [DllImport("comctl32.dll")]
    private static extern bool RemoveWindowSubclass(IntPtr hWnd, SubclassProc pfnSubclass, UIntPtr uIdSubclass);

    [DllImport("comctl32.dll")]
    private static extern IntPtr DefSubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

    private readonly IA52DeviceService _deviceService;
    private IntPtr _hwnd;
    private bool _installed;
    private SubclassProc? _subclassDelegate;

    public WindowsTrayService(IA52DeviceService deviceService)
    {
        _deviceService = deviceService;
        _deviceService.TelemetryUpdated += OnTelemetryUpdated;
    }

    public void Attach(IntPtr hwnd)
    {
        _hwnd = hwnd;
        _subclassDelegate = WindowSubclass;
        SetWindowSubclass(_hwnd, _subclassDelegate, new UIntPtr(152), IntPtr.Zero);
        InstallTrayIcon("Dispositivo Monitor - Conectado");
    }

    private void InstallTrayIcon(string tip)
    {
        var nid = new NOTIFYICONDATA
        {
            cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
            hWnd = _hwnd,
            uID = 152,
            uFlags = NIF_MESSAGE | NIF_TIP,
            uCallbackMessage = WM_APP_TRAY,
            szTip = tip
        };

        Shell_NotifyIcon(NIM_ADD, ref nid);
        _installed = true;
    }

    public void ShowBalloon(string title, string message)
    {
        if (!_installed || _hwnd == IntPtr.Zero) return;

        var nid = new NOTIFYICONDATA
        {
            cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
            hWnd = _hwnd,
            uID = 152,
            uFlags = NIF_INFO,
            szInfoTitle = title,
            szInfo = message,
            dwInfoFlags = 0x01 // NIIF_INFO
        };

        Shell_NotifyIcon(NIM_MODIFY, ref nid);
    }

    private void OnTelemetryUpdated(object? sender, TspTelemetry e)
    {
        if (e.IsBiometricLocked)
        {
            ShowBalloon("A52 Alerta: Biometria Travada", "O nó central FOD caiu para negativo. Requer Reboot para restaurar.");
        }
        else if (e.IsGhostTouchActive)
        {
            ShowBalloon("A52 Alerta: Toque Fantasma Preso!", $"{e.ActiveTouchSlots} dedos presos. Clique na bandeja para destravar.");
        }
    }

    private IntPtr WindowSubclass(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, UIntPtr uIdSubclass, IntPtr dwRefData)
    {
        if (uMsg == WM_CLOSE)
        {
            ShowWindow(hWnd, SW_HIDE);
            return IntPtr.Zero;
        }

        if (uMsg == WM_APP_TRAY)
        {
            int eventId = (int)(lParam.ToInt64() & 0xFFFF);
            if (eventId == WM_LBUTTONUP || eventId == WM_LBUTTONDBLCLK)
            {
                ShowWindow(_hwnd, SW_RESTORE);
                SetForegroundWindow(_hwnd);
                return IntPtr.Zero;
            }

            if (eventId == WM_RBUTTONUP || eventId == WM_CONTEXTMENU)
            {
                ShowContextMenu();
                return IntPtr.Zero;
            }
        }

        return DefSubclassProc(hWnd, uMsg, wParam, lParam);
    }

    private void ShowContextMenu()
    {
        IntPtr hMenu = CreatePopupMenu();
        AppendMenu(hMenu, MF_STRING, CMD_OPEN, "Abrir Painel A52");
        AppendMenu(hMenu, MF_SEPARATOR, 0, string.Empty);
        AppendMenu(hMenu, MF_STRING, CMD_UNLOCK_TOUCH, "Destravar Touch (Sem Reboot)");
        AppendMenu(hMenu, MF_STRING, CMD_CYCLE_SCREEN, "Ciclar Tela (Off/On)");
        AppendMenu(hMenu, MF_STRING, CMD_REBOOT, "Reiniciar Celular");
        AppendMenu(hMenu, MF_SEPARATOR, 0, string.Empty);
        AppendMenu(hMenu, MF_STRING, CMD_EXIT, "Sair");

        GetCursorPos(out var pt);
        SetForegroundWindow(_hwnd);
        int cmd = TrackPopupMenu(hMenu, TPM_RETURNCMD | TPM_RIGHTBUTTON | TPM_BOTTOMALIGN, pt.x, pt.y, 0, _hwnd, IntPtr.Zero);
        DestroyMenu(hMenu);

        switch (cmd)
        {
            case CMD_OPEN:
                ShowWindow(_hwnd, SW_RESTORE);
                SetForegroundWindow(_hwnd);
                break;
            case CMD_UNLOCK_TOUCH:
                Task.Run(() => _deviceService.UnlockTouchAsync());
                break;
            case CMD_CYCLE_SCREEN:
                Task.Run(() => _deviceService.CycleDisplayPowerAsync());
                break;
            case CMD_REBOOT:
                Task.Run(() => _deviceService.RebootDeviceAsync());
                break;
            case CMD_EXIT:
                Dispose();
                Environment.Exit(0);
                break;
        }
    }

    public void Dispose()
    {
        if (_installed && _hwnd != IntPtr.Zero)
        {
            var nid = new NOTIFYICONDATA
            {
                cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
                hWnd = _hwnd,
                uID = 152
            };
            Shell_NotifyIcon(NIM_DELETE, ref nid);
            _installed = false;
        }

        if (_subclassDelegate != null && _hwnd != IntPtr.Zero)
        {
            RemoveWindowSubclass(_hwnd, _subclassDelegate, new UIntPtr(152));
            _subclassDelegate = null;
        }
    }
}
#endif
