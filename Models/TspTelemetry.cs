namespace A52.DeviceManager.Models;

public record TspTelemetry
{
    public bool DeviceConnected { get; init; } = true;
    public string DeviceSerial { get; init; } = "RQ8T601E01X";
    public string Uptime { get; init; } = "";
    public int Tnoi { get; init; }
    public int Tmul { get; init; }
    public int Twet { get; init; }
    public int ActiveTouchSlots { get; init; }
    public string LastScrubPos { get; init; } = "";
    public int[] FodNodes { get; init; } = new int[9];

    public int Node4 => FodNodes.Length >= 9 ? FodNodes[3] : 0; // Meio-Esquerda
    public int Node5 => FodNodes.Length >= 9 ? FodNodes[4] : 0; // Centro FOD

    public bool IsBiometricLocked => Node5 < 5 || Node4 < -245;
    public bool IsGhostTouchActive => ActiveTouchSlots > 0;

    public string BiometricStatusText => IsBiometricLocked ? "TRAVADA (Requer Reboot)" : "OPERACIONAL (OK)";
    public string TouchStatusText => IsGhostTouchActive ? $"FANTASMA ATIVO ({ActiveTouchSlots} slots)!" : "LIMPO (0 Toques)";

    public static TspTelemetry CreateEmpty() => new();
}
