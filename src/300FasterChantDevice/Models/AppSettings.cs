using System.Text.Json.Serialization;

namespace _300FasterChantDevice.Models;

public class AppSettings
{
    // Trigger keys
    [JsonPropertyName("trigger_key")]
    public string TriggerKey { get; set; } = "F1";

    [JsonPropertyName("taunt_key")]
    public string TauntKey { get; set; } = "F2";

    // Burst mode
    [JsonPropertyName("burst_mode")]
    public bool BurstMode { get; set; } = true;

    [JsonPropertyName("burst_interval_ms")]
    public int BurstIntervalMs { get; set; } = 1000;

    // Taunt
    [JsonPropertyName("taunt_mode")]
    public string TauntMode { get; set; } = "both"; // "manual" | "timer" | "both"

    [JsonPropertyName("taunt_interval_s")]
    public int TauntIntervalS { get; set; } = 30;

    [JsonPropertyName("taunt_cooldown_s")]
    public int TauntCooldownS { get; set; } = 5;

    // Game window
    [JsonPropertyName("game_window_class")]
    public string GameWindowClass { get; set; } = "300Heroes";

    // OCR regions (ratio-based, 0.0-1.0)
    [JsonPropertyName("ocr_kda_region")]
    public OcrRegion KdaRegion { get; set; } = new()
    {
        XRatio = 0.80, YRatio = 0.02,
        WRatio = 0.18, HRatio = 0.10
    };

    [JsonPropertyName("ocr_broadcast_region")]
    public OcrRegion BroadcastRegion { get; set; } = new()
    {
        XRatio = 0.25, YRatio = 0.05,
        WRatio = 0.50, HRatio = 0.12
    };
}

public class OcrRegion
{
    [JsonPropertyName("x_ratio")]
    public double XRatio { get; set; }

    [JsonPropertyName("y_ratio")]
    public double YRatio { get; set; }

    [JsonPropertyName("w_ratio")]
    public double WRatio { get; set; }

    [JsonPropertyName("h_ratio")]
    public double HRatio { get; set; }
}
