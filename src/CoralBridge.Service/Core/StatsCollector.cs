using System.Collections.Concurrent;
using System.Diagnostics;
using CoralBridge.Native;

namespace CoralBridge.Core;

/// <summary>
/// Collects and aggregates inference statistics
/// </summary>
public sealed class StatsCollector : IDisposable
{
    private const string CoralPerfCounterCategory = "Coral PCIe Accelerator";
    private const string TemperatureCounterName = "Temperature";

    private readonly DateTime _startTime = DateTime.UtcNow;
    private readonly ConcurrentQueue<InferenceRecord> _recentInferences = new();
    private readonly TimeSpan _windowSize = TimeSpan.FromSeconds(60);
    private readonly PerformanceCounter? _temperatureCounter;
    private readonly string? _temperatureError;

    private long _totalInferences;
    private long _successfulInferences;
    private long _failedInferences;

    // Device info (set once during initialization)
    private string _deviceType = "unknown";
    private string _deviceStatus = "initializing";
    private bool _usingEdgeTpu;
    private string _modelName = "";
    private string? _runtimeVersion;

    public StatsCollector()
    {
        if (OperatingSystem.IsWindows())
        {
            (_temperatureCounter, _temperatureError) = TryInitializeTemperatureCounter();
        }
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static (PerformanceCounter?, string?) TryInitializeTemperatureCounter()
    {
        try
        {
            if (!OperatingSystem.IsWindows())
                return (null, "Not Windows");

            if (!PerformanceCounterCategory.Exists(CoralPerfCounterCategory))
                return (null, $"Category '{CoralPerfCounterCategory}' not found");

            var category = new PerformanceCounterCategory(CoralPerfCounterCategory);
            var instances = category.GetInstanceNames();
            if (instances.Length == 0)
                return (null, "No instances found");

            // Use the first available instance (e.g., "\\apexdevice0")
            var counter = new PerformanceCounter(
                CoralPerfCounterCategory,
                TemperatureCounterName,
                instances[0],
                readOnly: true);

            // Prime the counter (first read is often 0)
            _ = counter.NextValue();
            return (counter, null);
        }
        catch (Exception ex)
        {
            return (null, ex.Message);
        }
    }

    /// <summary>
    /// Records an inference result
    /// </summary>
    public void RecordInference(long latencyMs, bool success)
    {
        Interlocked.Increment(ref _totalInferences);

        if (success)
        {
            Interlocked.Increment(ref _successfulInferences);
        }
        else
        {
            Interlocked.Increment(ref _failedInferences);
        }

        // Store in recent buffer
        _recentInferences.Enqueue(new InferenceRecord(DateTime.UtcNow, latencyMs, success));

        // Prune old entries
        PruneOldEntries();
    }

    /// <summary>
    /// Sets device information (called once during initialization)
    /// </summary>
    public void SetDeviceInfo(string deviceType, bool usingEdgeTpu, string modelName, string? runtimeVersion)
    {
        _deviceType = deviceType;
        _usingEdgeTpu = usingEdgeTpu;
        _modelName = modelName;
        _runtimeVersion = runtimeVersion;
        _deviceStatus = "ready";
    }

    /// <summary>
    /// Gets a snapshot of current statistics
    /// </summary>
    public StatsSnapshot GetSnapshot()
    {
        PruneOldEntries();

        var recentList = _recentInferences.ToArray();
        var uptimeSeconds = (long)(DateTime.UtcNow - _startTime).TotalSeconds;
        var total = Interlocked.Read(ref _totalInferences);
        var successful = Interlocked.Read(ref _successfulInferences);
        var failed = Interlocked.Read(ref _failedInferences);

        // Calculate success rate
        var successRate = total > 0 ? (double)successful / total : 1.0;

        // Calculate inferences per second (rolling window)
        var windowSeconds = Math.Min(uptimeSeconds, (long)_windowSize.TotalSeconds);
        var recentCount = recentList.Length;
        var inferencesPerSecond = windowSeconds > 0 ? (double)recentCount / windowSeconds : 0;

        // Calculate latency stats
        var latencyStats = CalculateLatencyStats(recentList);

        return new StatsSnapshot
        {
            UptimeSeconds = uptimeSeconds,
            TotalInferences = total,
            SuccessfulInferences = successful,
            FailedInferences = failed,
            SuccessRate = Math.Round(successRate, 4),
            InferencesPerSecond = Math.Round(inferencesPerSecond, 2),
            LatencyMs = latencyStats,
            Device = new DeviceInfo
            {
                Status = _deviceStatus,
                Type = _deviceType,
                UsingEdgeTpu = _usingEdgeTpu,
                Model = _modelName,
                RuntimeVersion = _runtimeVersion,
                TemperatureCelsius = GetTemperatureCelsius(),
                TemperatureError = _temperatureError
            }
        };
    }

    /// <summary>
    /// Reads the current TPU temperature from the Windows performance counter
    /// </summary>
    /// <returns>Temperature in Celsius, or null if not available</returns>
    private double? GetTemperatureCelsius()
    {
        if (_temperatureCounter == null || !OperatingSystem.IsWindows())
            return null;

        try
        {
            // Counter returns millidegrees Celsius (e.g., 46300 = 46.3°C)
            var millidegrees = _temperatureCounter.NextValue();
            return Math.Round(millidegrees / 1000.0, 1);
        }
        catch
        {
            return null;
        }
    }

    public void Dispose()
    {
        _temperatureCounter?.Dispose();
    }

    private void PruneOldEntries()
    {
        var cutoff = DateTime.UtcNow - _windowSize;
        while (_recentInferences.TryPeek(out var record) && record.Timestamp < cutoff)
        {
            _recentInferences.TryDequeue(out _);
        }
    }

    private static LatencyStats CalculateLatencyStats(InferenceRecord[] records)
    {
        if (records.Length == 0)
        {
            return new LatencyStats
            {
                Average = 0,
                Min = 0,
                Max = 0,
                P95 = 0
            };
        }

        var latencies = records.Select(r => r.LatencyMs).OrderBy(l => l).ToArray();
        var average = latencies.Average();
        var min = latencies.Min();
        var max = latencies.Max();

        // Calculate P95
        var p95Index = (int)Math.Ceiling(latencies.Length * 0.95) - 1;
        p95Index = Math.Max(0, Math.Min(p95Index, latencies.Length - 1));
        var p95 = latencies[p95Index];

        return new LatencyStats
        {
            Average = Math.Round(average, 2),
            Min = min,
            Max = max,
            P95 = p95
        };
    }

    private readonly record struct InferenceRecord(DateTime Timestamp, long LatencyMs, bool Success);
}
