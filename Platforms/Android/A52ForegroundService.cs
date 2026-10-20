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
        StartForeground(NotificationId, BuildNotification("Iniciando monitoramento A52..."));

        if (_deviceService == null && IPlatformApplication.Current != null)
        {
            _deviceService = IPlatformApplication.Current.Services.GetRequiredService<IA52DeviceService>();
            _deviceService.TelemetryUpdated += OnTelemetryUpdated;
        }

        return StartCommandResult.Sticky;
    }

    private void OnTelemetryUpdated(object? sender, TspTelemetry e)
    {
        string statusText = e.IsBiometricLocked
            ? $"⚠️ BIOMETRIA TRAVADA (Nó5={e.Node5}) - Reiniciar"
            : (e.IsGhostTouchActive ? $"🚨 FANTASMA ATIVO ({e.ActiveTouchSlots} slots) - Destravar" : $"✅ Touch & Bio OK (TNOI={e.Tnoi})");

        UpdateNotification(statusText);
    }

    private void CreateNotificationChannel()
    {
        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
        {
            var channel = new NotificationChannel(ChannelId, ChannelName, NotificationImportance.Low)
            {
                Description = "Monitor de hardware da tela e biometria do Galaxy A52"
            };

            var notificationManager = (NotificationManager?)GetSystemService(NotificationService);
            notificationManager?.CreateNotificationChannel(channel);
        }
    }

    private global::Android.App.Notification BuildNotification(string message)
    {
        var launchIntent = PackageManager?.GetLaunchIntentForPackage(PackageName ?? "");
        var pendingIntent = PendingIntent.GetActivity(this, 0, launchIntent, PendingIntentFlags.Immutable);

        _builder = new NotificationCompat.Builder(this, ChannelId)
            .SetContentTitle("Samsung A52 Monitor")
            .SetContentText(message)
            .SetSmallIcon(global::Android.Resource.Drawable.StatNotifySync)
            .SetOngoing(true)
            .SetContentIntent(pendingIntent);

        return _builder.Build();
    }

    private void UpdateNotification(string message)
    {
        if (_builder == null) return;

        _builder.SetContentText(message);
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
