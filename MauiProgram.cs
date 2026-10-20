using A52.DeviceManager.Services;
using A52.DeviceManager.ViewModels;
using A52.DeviceManager.Views;
using Microsoft.Extensions.Logging;

namespace A52.DeviceManager;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                fonts.AddFont("MaterialIcons-Regular.ttf", "MaterialIcons");
            });

#if ANDROID
        builder.Services.AddSingleton<IA52DeviceService, LocalKernelService>();
#else
        builder.Services.AddSingleton<IA52DeviceService, AdbDeviceService>();
        builder.Services.AddSingleton<A52.DeviceManager.Platforms.Windows.WindowsTrayService>();
#endif

        builder.Services.AddSingleton<DeviceLogService>();
        builder.Services.AddSingleton<HomeViewModel>();
        builder.Services.AddSingleton<HomePage>();
        builder.Services.AddSingleton<LogsViewModel>();
        builder.Services.AddSingleton<LogsPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
