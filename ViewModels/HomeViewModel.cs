using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using A52.DeviceManager.Models;
using A52.DeviceManager.Services;

namespace A52.DeviceManager.ViewModels;

public class HomeViewModel : INotifyPropertyChanged
{
    private readonly IA52DeviceService _deviceService;
    private readonly System.Timers.Timer _pollingTimer;
    private TspTelemetry _telemetry = TspTelemetry.CreateEmpty();
    private bool _isBusy;
    private string _statusMessage = "Iniciando monitoramento...";

    public event PropertyChangedEventHandler? PropertyChanged;

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
            OnPropertyChanged(nameof(BioBadgeColor));
            OnPropertyChanged(nameof(TouchBadgeColor));
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

    public Color BioBadgeColor => Telemetry.IsBiometricLocked ? Colors.Crimson : Colors.SeaGreen;
    public Color TouchBadgeColor => Telemetry.IsGhostTouchActive ? Colors.DarkOrange : Colors.SeaGreen;

    public ICommand UnlockTouchCommand { get; }
    public ICommand CycleScreenCommand { get; }
    public ICommand ApplyHardeningCommand { get; }
    public ICommand RebootCommand { get; }
    public ICommand RefreshCommand { get; }

    public HomeViewModel(IA52DeviceService deviceService)
    {
        _deviceService = deviceService;

        UnlockTouchCommand = new Command(async () => await ExecuteActionAsync("Destravando touch...", _deviceService.UnlockTouchAsync));
        CycleScreenCommand = new Command(async () => await ExecuteActionAsync("Ciclando energia da tela...", _deviceService.CycleDisplayPowerAsync));
        ApplyHardeningCommand = new Command(async () => await ExecuteActionAsync("Reaplicando blindagem ADB...", _deviceService.ApplyHardeningSettingsAsync));
        RebootCommand = new Command(async () => await ExecuteActionAsync("Reiniciando aparelho...", _deviceService.RebootDeviceAsync));
        RefreshCommand = new Command(async () => await RefreshTelemetryAsync());

        _pollingTimer = new System.Timers.Timer(2000);
        _pollingTimer.Elapsed += async (s, e) => await RefreshTelemetryAsync();
        _pollingTimer.AutoReset = true;
        _pollingTimer.Start();

        _ = Task.Run(InitializeAsync);
    }

    private async Task InitializeAsync()
    {
        await AppPermissions.RequestNotificationPermissionAsync();
        StartForegroundService();
        await RefreshTelemetryAsync();
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
        });
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
            await RefreshTelemetryAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Erro: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    protected void OnPropertyChanged([CallerMemberName] string? propName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
}
