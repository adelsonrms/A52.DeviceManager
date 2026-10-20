using System.Collections.ObjectModel;
using System.Windows.Input;
using A52.DeviceManager.Models;
using A52.DeviceManager.Services;

namespace A52.DeviceManager.ViewModels;

public class LogsViewModel
{
    private readonly DeviceLogService _logService;

    public ObservableCollection<DeviceLogEntry> Entries => _logService.Entries;
    public ICommand ClearCommand { get; }

    public LogsViewModel(DeviceLogService logService)
    {
        _logService = logService;
        ClearCommand = new Command(_logService.Clear);
    }
}
