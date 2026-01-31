using System.Text.Json.Serialization;

namespace CoralBridge.Core;

/// <summary>
/// Immutable snapshot of inference statistics
/// </summary>
public record StatsSnapshot
{
    [JsonPropertyName("uptime_seconds")]
    public long UptimeSeconds { get; init; }

    [JsonPropertyName("total_inferences")]
    public long TotalInferences { get; init; }

    [JsonPropertyName("successful_inferences")]
    public long SuccessfulInferences { get; init; }

    [JsonPropertyName("failed_inferences")]
    public long FailedInferences { get; init; }

    [JsonPropertyName("success_rate")]
    public double SuccessRate { get; init; }

    [JsonPropertyName("inferences_per_second")]
    public double InferencesPerSecond { get; init; }

    [JsonPropertyName("latency_ms")]
    public LatencyStats LatencyMs { get; init; } = new();

    [JsonPropertyName("device")]
    public DeviceInfo Device { get; init; } = new();
}

/// <summary>
/// Latency statistics
/// </summary>
public record LatencyStats
{
    [JsonPropertyName("average")]
    public double Average { get; init; }

    [JsonPropertyName("min")]
    public double Min { get; init; }

    [JsonPropertyName("max")]
    public double Max { get; init; }

    [JsonPropertyName("p95")]
    public double P95 { get; init; }
}

/// <summary>
/// Device information
/// </summary>
public record DeviceInfo
{
    [JsonPropertyName("status")]
    public string Status { get; init; } = "unknown";

    [JsonPropertyName("type")]
    public string Type { get; init; } = "unknown";

    [JsonPropertyName("using_edgetpu")]
    public bool UsingEdgeTpu { get; init; }

    [JsonPropertyName("model")]
    public string Model { get; init; } = "";

    [JsonPropertyName("runtime_version")]
    public string? RuntimeVersion { get; init; }

    [JsonPropertyName("temperature_celsius")]
    public double? TemperatureCelsius { get; init; }

    [JsonPropertyName("temperature_error")]
    public string? TemperatureError { get; init; }
}
