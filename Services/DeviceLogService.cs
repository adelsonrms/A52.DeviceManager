using System.Collections.ObjectModel;
using A52.DeviceManager.Models;

namespace A52.DeviceManager.Services;

public class DeviceLogService
{
    public ObservableCollection<DeviceLogEntry> Entries { get; } = new();

    public DeviceLogService()
    {
        Log("Serviço de Diagnóstico Inicializado", "Monitoramento de integridade de hardware ativo.", LogSeverity.Info);
    }

    public void Log(string title, string details, LogSeverity severity = LogSeverity.Info)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            Entries.Insert(0, new DeviceLogEntry
            {
                Timestamp = DateTime.Now,
                Title = title,
                Details = details,
                Severity = severity
            });

            while (Entries.Count > 200)
            {
                Entries.RemoveAt(Entries.Count - 1);
            }
        });
    }

    public void Clear()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            Entries.Clear();
            Log("Logs Limpos", "Histórico de eventos esvaziado pelo usuário.", LogSeverity.Info);
        });
    }
}
