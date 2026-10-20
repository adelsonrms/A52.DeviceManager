using A52.DeviceManager.ViewModels;

namespace A52.DeviceManager.Views;

public partial class HomePage : ContentPage
{
    public HomePage(HomeViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
