namespace A52.DeviceManager.Services;

/// <summary>
/// Ponto centralizado para checagem e solicitação de permissões em tempo de execução no Android.
/// </summary>
public static class AppPermissions
{
    public static async Task<bool> RequestNotificationPermissionAsync()
    {
        if (DeviceInfo.Platform != DevicePlatform.Android)
            return true;

        try
        {
            var status = await Permissions.CheckStatusAsync<Permissions.PostNotifications>();
            if (status != PermissionStatus.Granted)
            {
                status = await Permissions.RequestAsync<Permissions.PostNotifications>();
            }
            return status == PermissionStatus.Granted;
        }
        catch
        {
            return false;
        }
    }
}
