using Android.App;
using Android.Content;
using Android.OS;
using AndroidX.Core.App;
using A52.DeviceManager.Models;
using A52.DeviceManager.Services;

namespace A52.DeviceManager.Platforms.Android;

[Service(Name = "com.devonno.a52.A52ForegroundService", ForegroundServiceType = global::Android.Content.PM.ForegroundService.TypeDataSync)]
public class A52ForegroundService : Service
{
    private const int NotificationId = 2052;
    private const string ChannelId = "a52_monitor_channel";
    private const string ChannelName = "A52 Hardware Monitor";
    private IA52DeviceService? _deviceService;
    private NotificationCompat.Builder? _builder;

    public override IBinder? OnBind(Intent? intent) => null;

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        CreateNotificationChannel();
        StartForeground(NotificationId, BuildNotification("Iniciando monitoramento de hardware..."));

        if (_deviceService == null && IPlatformApplication.Current != null)
        {
            _deviceService = IPlatformApplication.Current.Services.GetRequiredService<IA52DeviceService>();
            _deviceService.TelemetryUpdated += OnTelemetryUpdated;
        }

        return StartCommandResult.Sticky;
    }

    private void OnTelemetryUpdated(object? sender, TspTelemetry e)
    {
        string title = "Dispositivo - Status Normal";
        string statusText = $"Ruído: {e.Tnoi} | Fantasma: {e.Tmul} | Slots: {e.ActiveTouchSlots} | Nó 5: {e.Node5}";
        bool isAlert = false;

        if (e.IsBiometricLocked)
        {
            title = "⚠️ ALERTA: BIOMETRIA TRAVADA";
            statusText = $"Nó 5={e.Node5} (<5) | Ruído={e.Tnoi} - Necessário Reiniciar!";
            isAlert = true;
        }
        else if (e.IsGhostTouchActive)
        {
            title = "🚨 ALERTA: TOQUE FANTASMA DETECTADO";
            statusText = $"Slots={e.ActiveTouchSlots} | TMUL={e.Tmul} - Ação: Destravar Touch";
            isAlert = true;
        }
        else if (e.Tnoi > 10)
        {
            title = "⚡ Ruído Eletrostático Elevado";
            statusText = $"TNOI={e.Tnoi} (>10) | Biometria pode instabilizar";
            isAlert = true;
        }

        UpdateNotification(title, statusText, isAlert);
    }

    private void CreateNotificationChannel()
    {
        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
        {
            var channel = new NotificationChannel(ChannelId, ChannelName, NotificationImportance.High)
            {
                Description = "Monitor de hardware da tela e biometria do Galaxy A52"
            };
            channel.EnableVibration(false);

            var notificationManager = (NotificationManager?)GetSystemService(NotificationService);
            notificationManager?.CreateNotificationChannel(channel);
        }
    }

    private global::Android.App.Notification BuildNotification(string message)
    {
        var launchIntent = PackageManager?.GetLaunchIntentForPackage(PackageName ?? "");
        var pendingIntent = PendingIntent.GetActivity(this, 0, launchIntent, PendingIntentFlags.Immutable);

        _builder = new NotificationCompat.Builder(this, ChannelId)
            .SetContentTitle("A52 Hardware Monitor")
            .SetContentText(message)
            .SetSmallIcon(global::Android.Resource.Drawable.StatNotifySync)
            .SetOngoing(true)
            .SetPriority(NotificationCompat.PriorityHigh)
            .SetContentIntent(pendingIntent);

        return _builder.Build();
    }

    private void UpdateNotification(string title, string message, bool isAlert)
    {
        if (_builder == null) return;

        _builder.SetContentTitle(title);
        _builder.SetContentText(message);

        if (isAlert)
        {
            _builder.SetPriority(NotificationCompat.PriorityHigh);
        }
        else
        {
            _builder.SetPriority(NotificationCompat.PriorityLow);
        }

        var notificationManager = (NotificationManager?)GetSystemService(NotificationService);
        notificationManager?.Notify(NotificationId, _builder.Build());
    }

    public override void OnDestroy()
    {
        if (_deviceService != null)
        {
            _deviceService.TelemetryUpdated -= OnTelemetryUpdated;
        }
        base.OnDestroy();
    }
}
