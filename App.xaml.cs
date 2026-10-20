namespace A52.DeviceManager;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		var window = new Window(new AppShell());
#if WINDOWS
		window.Width = 470;
		window.Height = 780;
		window.MinimumWidth = 420;
		window.MinimumHeight = 650;
#endif
		return window;
	}
}
