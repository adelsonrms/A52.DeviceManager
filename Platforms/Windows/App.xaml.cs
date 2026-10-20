using Microsoft.UI;
using Microsoft.UI.Windowing;

namespace A52.DeviceManager.WinUI;

public partial class App : MauiWinUIApplication
{
    public App()
    {
        this.InitializeComponent();
    }

    protected override MauiApp CreateMauiApp() => A52.DeviceManager.MauiProgram.CreateMauiApp();

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        base.OnLaunched(args);

        var window = Microsoft.Maui.Controls.Application.Current?.Windows.FirstOrDefault();
        if (window?.Handler?.PlatformView is Microsoft.UI.Xaml.Window winUIWindow)
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(winUIWindow);
            var trayService = IPlatformApplication.Current?.Services.GetService<A52.DeviceManager.Platforms.Windows.WindowsTrayService>();
            trayService?.Attach(hwnd);
        }
    }
}
