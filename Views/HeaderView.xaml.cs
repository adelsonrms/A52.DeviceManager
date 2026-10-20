namespace A52.DeviceManager.Views;

public partial class HeaderView : ContentView
{
    public static readonly BindableProperty TitleProperty =
        BindableProperty.Create(nameof(Title), typeof(string), typeof(HeaderView), string.Empty);

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public HeaderView()
    {
        InitializeComponent();
        Loaded += (s, e) => UpdateThemeButton();
        if (Application.Current != null)
        {
            Application.Current.RequestedThemeChanged += (s, e) => UpdateThemeButton();
        }
    }

    private void UpdateThemeButton()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            var isDark = Application.Current?.RequestedTheme == AppTheme.Dark;
            ThemeToggleButton.Text = isDark ? "☀️" : "🌙";
        });
    }

    private void OnThemeToggleClicked(object? sender, EventArgs e)
    {
        var app = Application.Current;
        if (app == null) return;

        var isDark = app.RequestedTheme == AppTheme.Dark;
        app.UserAppTheme = isDark ? AppTheme.Light : AppTheme.Dark;
        UpdateThemeButton();
    }
}
