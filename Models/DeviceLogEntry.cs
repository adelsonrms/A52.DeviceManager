namespace A52.DeviceManager.Models;

public enum LogSeverity
{
    Info,
    Success,
    Warning,
    Error
}

public class DeviceLogEntry
{
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public string TimeFormatted => Timestamp.ToString("HH:mm:ss");
    public string DateFormatted => Timestamp.ToString("dd/MM");
    public string Title { get; set; } = "";
    public string Details { get; set; } = "";
    public LogSeverity Severity { get; set; } = LogSeverity.Info;

    public Color BadgeColor => Severity switch
    {
        LogSeverity.Success => Colors.SeaGreen,
        LogSeverity.Warning => Colors.DarkOrange,
        LogSeverity.Error => Colors.Crimson,
        _ => Colors.SteelBlue
    };

    public string SeverityIcon => Severity switch
    {
        LogSeverity.Success => "✓",
        LogSeverity.Warning => "⚠",
        LogSeverity.Error => "✕",
        _ => "ℹ"
    };
}
