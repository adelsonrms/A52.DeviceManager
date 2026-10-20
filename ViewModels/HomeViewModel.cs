using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using A52.DeviceManager.Models;
using A52.DeviceManager.Services;

namespace A52.DeviceManager.ViewModels;

public class HomeViewModel : INotifyPropertyChanged
{
    private readonly IA52DeviceService _deviceService;
    private readonly DeviceLogService _logService;
    private readonly System.Timers.Timer _pollingTimer;
    private TspTelemetry _telemetry = TspTelemetry.CreateEmpty();
    private bool _isBusy;
    private string _statusMessage = "Iniciando monitoramento...";
    private string? _selectedDevice;

    // Rastreamento para gerar logs de transição
    private bool? _lastBiometricLockedState;
    private int _lastTnoiAlert;
    private bool _lastGhostTouchActive;

    public event PropertyChangedEventHandler? PropertyChanged;

    public bool IsWindows => DeviceInfo.Platform == DevicePlatform.WinUI;
    public bool IsMobile => !IsWindows;

    public ObservableCollection<string> AvailableDevices { get; } = new();

    public string? SelectedDevice
    {
        get => _selectedDevice;
        set
        {
            if (_selectedDevice != value)
            {
                _selectedDevice = value;
                OnPropertyChanged();
                if (!string.IsNullOrEmpty(value))
                {
                    _deviceService.SetTargetDevice(value);
                    _logService.Log("Dispositivo Selecionado", $"Alvo ADB alterado para: {value}", LogSeverity.Info);
                    _ = Task.Run(RefreshTelemetryAsync);
                }
            }
        }
    }

    public TspTelemetry Telemetry
    {
        get => _telemetry;
        private set
        {
            _telemetry = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Node0));
            OnPropertyChanged(nameof(Node1));
            OnPropertyChanged(nameof(Node2));
            OnPropertyChanged(nameof(Node3));
            OnPropertyChanged(nameof(Node4));
            OnPropertyChanged(nameof(Node5));
            OnPropertyChanged(nameof(Node6));
            OnPropertyChanged(nameof(Node7));
            OnPropertyChanged(nameof(Node8));
            OnPropertyChanged(nameof(Node4Color));
            OnPropertyChanged(nameof(Node5Color));
            OnPropertyChanged(nameof(Node5StatusText));
            OnPropertyChanged(nameof(Node5BadgeColor));
            OnPropertyChanged(nameof(BioBadgeColor));
            OnPropertyChanged(nameof(TouchBadgeColor));
            OnPropertyChanged(nameof(ConnectionDotColor));
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        set { _isBusy = value; OnPropertyChanged(); }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set { _statusMessage = value; OnPropertyChanged(); }
    }

    public Color ConnectionDotColor => Telemetry.DeviceConnected ? Colors.SeaGreen : Colors.Crimson;

    public int Node0 => Telemetry.FodNodes[0];
    public int Node1 => Telemetry.FodNodes[1];
    public int Node2 => Telemetry.FodNodes[2];
    public int Node3 => Telemetry.FodNodes[3];
    public int Node4 => Telemetry.FodNodes[4];
    public int Node5 => Telemetry.FodNodes[5];
    public int Node6 => Telemetry.FodNodes[6];
    public int Node7 => Telemetry.FodNodes[7];
    public int Node8 => Telemetry.FodNodes[8];

    public Color Node4Color => Node4 < -245 ? Colors.Crimson : Colors.DimGray;
    public Color Node5Color => Node5 < 5 ? Colors.Crimson : Colors.SeaGreen;

    public string Node5StatusText => Node5 >= 5 ? $"{Node5}  •  SAUDÁVEL" : $"{Node5}  •  ALERTA (TRAVADO)";
    public Color Node5BadgeColor => Node5 >= 5 ? Colors.SeaGreen : Colors.Crimson;

    public Color BioBadgeColor => Telemetry.IsBiometricLocked ? Colors.Crimson : Colors.SeaGreen;
    public Color TouchBadgeColor => Telemetry.IsGhostTouchActive ? Colors.DarkOrange : Colors.SeaGreen;

    public ICommand UnlockTouchCommand { get; }
    public ICommand CycleScreenCommand { get; }
    public ICommand ApplyHardeningCommand { get; }
    public ICommand RebootCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand BiometricCardTappedCommand { get; }

    public HomeViewModel(IA52DeviceService deviceService, DeviceLogService logService)
    {
        _deviceService = deviceService;
        _logService = logService;

        UnlockTouchCommand = new Command(async () => await ExecuteActionWithConfirmationAsync(
            "Destravar Touch",
            "Deseja resetar os registradores do sensor de toque (Touch/TSP)?",
            _deviceService.UnlockTouchAsync));

        CycleScreenCommand = new Command(async () => await ExecuteActionWithConfirmationAsync(
            "Ciclar Tela",
            "Deseja desligar e religar a alimentação do display OLED?",
            _deviceService.CycleDisplayPowerAsync));

        ApplyHardeningCommand = new Command(async () => await ExecuteActionWithConfirmationAsync(
            "Reaplicar Blindagem",
            "Deseja reaplicar as configurações de mitigação contra toques acidentais?",
            _deviceService.ApplyHardeningSettingsAsync));

        RebootCommand = new Command(async () => await ExecuteActionWithConfirmationAsync(
            "Reiniciar Celular",
            "Tem certeza que deseja reiniciar o aparelho agora?",
            _deviceService.RebootDeviceAsync));

        BiometricCardTappedCommand = new Command(async () => await OnBiometricCardTappedAsync());

        RefreshCommand = new Command(async () =>
        {
            await RefreshDevicesAsync();
            await RefreshTelemetryAsync();
        });

        _pollingTimer = new System.Timers.Timer(2000);
        _pollingTimer.Elapsed += async (s, e) => await RefreshTelemetryAsync();
        _pollingTimer.AutoReset = true;
        _pollingTimer.Start();

        _ = Task.Run(InitializeAsync);
    }

    private async Task OnBiometricCardTappedAsync()
    {
        if (Telemetry.IsBiometricLocked)
        {
            bool confirm = await Shell.Current.DisplayAlert(
                "Biometria Travada",
                $"O leitor biométrico óptico está inoperante devido à deriva no sensor (Nó 5 = {Node5} < 5).\n\nDeseja reiniciar o aparelho agora para restaurar?",
                "Sim, Reiniciar",
                "Cancelar");

            if (confirm)
            {
                _logService.Log("Reinício Solicitado", "Comando disparado via clique no card de Biometria travada.", LogSeverity.Warning);
                await ExecuteActionAsync("Reiniciando celular...", _deviceService.RebootDeviceAsync);
            }
        }
        else
        {
            await Shell.Current.DisplayAlert(
                "Biometria Operacional",
                $"O sensor biométrico óptico está saudável (Nó 5 = {Node5} ≥ 5).\nNenhum reinício necessário no momento.",
                "OK");
        }
    }

    private async Task InitializeAsync()
    {
        await AppPermissions.RequestNotificationPermissionAsync();
        StartForegroundService();
        await RefreshDevicesAsync();
        await RefreshTelemetryAsync();
    }

    private async Task RefreshDevicesAsync()
    {
        if (!IsWindows) return;
        try
        {
            var devs = await _deviceService.GetAvailableDevicesAsync();
            MainThread.BeginInvokeOnMainThread(() =>
            {
                var prev = SelectedDevice;
                AvailableDevices.Clear();
                foreach (var d in devs)
                {
                    AvailableDevices.Add(d);
                }

                if (AvailableDevices.Count == 1)
                {
                    SelectedDevice = AvailableDevices[0];
                }
                else if (!string.IsNullOrEmpty(prev) && AvailableDevices.Contains(prev))
                {
                    SelectedDevice = prev;
                }
                else if (AvailableDevices.Count > 0 && SelectedDevice == null)
                {
                    SelectedDevice = AvailableDevices[0];
                }
            });
        }
        catch { }
    }

    private static void StartForegroundService()
    {
#if ANDROID
        try
        {
            var context = Android.App.Application.Context;
            var intent = new Android.Content.Intent(context, typeof(Platforms.Android.A52ForegroundService));
            if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.O)
            {
                context.StartForegroundService(intent);
            }
            else
            {
                context.StartService(intent);
            }
        }
        catch { }
#endif
    }

    private async Task RefreshTelemetryAsync()
    {
        var data = await _deviceService.PollTelemetryAsync();
        MainThread.BeginInvokeOnMainThread(() =>
        {
            Telemetry = data;
            StatusMessage = $"Atualizado às {DateTime.Now:HH:mm:ss}";

            // Checagem e registro de eventos em log
            CheckAndLogTelemetryEvents(data);
        });
    }

    private void CheckAndLogTelemetryEvents(TspTelemetry data)
    {
        if (!data.DeviceConnected) return;

        // 1. Mudança no estado da biometria
        if (_lastBiometricLockedState != data.IsBiometricLocked)
        {
            if (data.IsBiometricLocked)
            {
                _logService.Log("⚠️ Biometria Inoperante", $"Deriva capacitiva detectada: Nó 5 = {data.Node5} (<5). Biometria travada, reinício necessário.", LogSeverity.Error);
            }
            else if (_lastBiometricLockedState == true)
            {
                _logService.Log("✅ Biometria Restaurada", $"Sensor normalizado com Nó 5 = {data.Node5} (≥5).", LogSeverity.Success);
            }
            _lastBiometricLockedState = data.IsBiometricLocked;
        }

        // 2. Ruído capacitivo TNOI
        if (data.Tnoi > 10 && _lastTnoiAlert != data.Tnoi)
        {
            _logService.Log("⚡ Ruído Eletrostático Elevado", $"TNOI atingiu {data.Tnoi} (>10). Sensor sujeito a instabilidade.", LogSeverity.Warning);
            _lastTnoiAlert = data.Tnoi;
        }

        // 3. Toques Fantasmas
        if (data.IsGhostTouchActive && !_lastGhostTouchActive)
        {
            _logService.Log("🚨 Toque Fantasma Detectado", $"{data.ActiveTouchSlots} slots de toque ativos em segundo plano. TMUL: {data.Tmul}.", LogSeverity.Warning);
            _lastGhostTouchActive = true;
        }
        else if (!data.IsGhostTouchActive && _lastGhostTouchActive)
        {
            _logService.Log("✅ Toques Fantasmas Cessados", "Painel touch sem slots fantasmas ativos.", LogSeverity.Success);
            _lastGhostTouchActive = false;
        }
    }

    private async Task ExecuteActionWithConfirmationAsync(string actionTitle, string prompt, Func<CancellationToken, Task<bool>> action)
    {
        if (IsBusy) return;

        bool confirm = await Shell.Current.DisplayAlert(
            "Confirmar Ação",
            prompt,
            "Sim, Executar",
            "Cancelar");

        if (!confirm) return;

        _logService.Log("Ação Solicitada", $"Executando: {actionTitle}...", LogSeverity.Info);
        await ExecuteActionAsync(actionTitle, action);
    }

    private async Task ExecuteActionAsync(string startMsg, Func<CancellationToken, Task<bool>> action)
    {
        if (IsBusy) return;
        try
        {
            IsBusy = true;
            StatusMessage = startMsg;
            var success = await action(CancellationToken.None);
            StatusMessage = success ? "Comando executado com sucesso!" : "Aviso: Comando retornou erro.";
            
            _logService.Log(
                success ? "Comando Concluído" : "Falha na Execução",
                $"{startMsg} -> {(success ? "Sucesso" : "Erro retornado")}",
                success ? LogSeverity.Success : LogSeverity.Error);

            await RefreshTelemetryAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Erro: {ex.Message}";
            _logService.Log("Erro de Comando", $"Falha ao executar '{startMsg}': {ex.Message}", LogSeverity.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    protected void OnPropertyChanged([CallerMemberName] string? propName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
}
