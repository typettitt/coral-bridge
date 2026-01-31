using System.Text.Json.Serialization;

namespace CoralBridge.Cli;

/// <summary>
/// Stats response from the service
/// </summary>
public record StatsResponse
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
    public LatencyStats? LatencyMs { get; init; }

    [JsonPropertyName("device")]
    public DeviceInfo? Device { get; init; }
}

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
}

/// <summary>
/// Health response from the service
/// </summary>
public record HealthResponse
{
    [JsonPropertyName("status")]
    public string Status { get; init; } = "";

    [JsonPropertyName("version")]
    public string Version { get; init; } = "";

    [JsonPropertyName("model")]
    public string Model { get; init; } = "";

    [JsonPropertyName("using_edgetpu")]
    public bool UsingEdgeTpu { get; init; }

    [JsonPropertyName("input_width")]
    public int InputWidth { get; init; }

    [JsonPropertyName("input_height")]
    public int InputHeight { get; init; }
}

/// <summary>
/// Service info response from root endpoint
/// </summary>
public record ServiceInfoResponse
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = "";

    [JsonPropertyName("version")]
    public string Version { get; init; } = "";

    [JsonPropertyName("status")]
    public string Status { get; init; } = "";
}
