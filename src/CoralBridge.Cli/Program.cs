using System.CommandLine;
using System.Net.Http.Json;
using System.Reflection;
using Spectre.Console;

namespace CoralBridge.Cli;

public class Program
{
    private static readonly HttpClient HttpClient = new();

    private static readonly string CliVersion =
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";

    // Coral-themed color palette
    private static readonly Color CoralOrange = new(255, 127, 80);   // Coral
    private static readonly Color CoralPink = new(255, 182, 193);    // Light coral/pink
    private static readonly Color OceanBlue = new(70, 130, 180);     // Steel blue
    private static readonly Color SeaGreen = new(46, 139, 87);       // Sea green
    private static readonly Color SandBeige = new(245, 222, 179);    // Wheat/sand

    // Use hex color for coral (#FF7F50)
    private const string AsciiBanner = @"
 [#FF7F50] ██████╗ ██████╗ ██████╗  █████╗ ██╗         ██████╗ ██████╗ ██╗██████╗  ██████╗ ███████╗[/]
 [#FF7F50]██╔════╝██╔═══██╗██╔══██╗██╔══██╗██║         ██╔══██╗██╔══██╗██║██╔══██╗██╔════╝ ██╔════╝[/]
 [#FF7F50]██║     ██║   ██║██████╔╝███████║██║         ██████╔╝██████╔╝██║██║  ██║██║  ███╗█████╗[/]
 [#FF7F50]██║     ██║   ██║██╔══██╗██╔══██║██║         ██╔══██╗██╔══██╗██║██║  ██║██║   ██║██╔══╝[/]
 [#FF7F50]╚██████╗╚██████╔╝██║  ██║██║  ██║███████╗    ██████╔╝██║  ██║██║██████╔╝╚██████╔╝███████╗[/]
 [#FF7F50] ╚═════╝ ╚═════╝ ╚═╝  ╚═╝╚═╝  ╚═╝╚══════╝    ╚═════╝ ╚═╝  ╚═╝╚═╝╚═════╝  ╚═════╝ ╚══════╝[/]
";

    // Shared options
    private static readonly Option<string> UrlOption = new(
        ["--url", "-u"],
        () => "http://localhost:5555",
        "CoralBridge service URL");

    private static readonly Option<bool> FahrenheitOption = new(
        ["--fahrenheit", "-f"],
        () => false,
        "Display temperature in Fahrenheit instead of Celsius");

    public static async Task<int> Main(string[] args)
    {
        var watchOption = new Option<bool>(
            ["--watch", "-w"],
            () => false,
            "Continuously monitor stats (refresh every second)");

        var rootCommand = new RootCommand("CoralBridge CLI - Monitor and control the CoralBridge service");

        // stats command
        var statsCommand = new Command("stats", "Display inference statistics");
        statsCommand.AddOption(UrlOption);
        statsCommand.AddOption(watchOption);
        statsCommand.AddOption(FahrenheitOption);
        statsCommand.SetHandler(HandleStatsCommand, UrlOption, watchOption, FahrenheitOption);
        rootCommand.AddCommand(statsCommand);

        // health command
        var healthCommand = new Command("health", "Check service health");
        healthCommand.AddOption(UrlOption);
        healthCommand.SetHandler(HandleHealthCommand, UrlOption);
        rootCommand.AddCommand(healthCommand);

        // version command
        var versionCommand = new Command("version", "Show CLI and service versions");
        versionCommand.AddOption(UrlOption);
        versionCommand.SetHandler(HandleVersionCommand, UrlOption);
        rootCommand.AddCommand(versionCommand);

        return await rootCommand.InvokeAsync(args);
    }

    private static async Task HandleVersionCommand(string url)
    {
        AnsiConsole.MarkupLine($"[#FF7F50]coralctl[/] version [bold]{CliVersion}[/]");

        try
        {
            var response = await HttpClient.GetAsync($"{url.TrimEnd('/')}/");
            response.EnsureSuccessStatusCode();
            var info = await response.Content.ReadFromJsonAsync<ServiceInfoResponse>();

            if (info != null)
            {
                AnsiConsole.MarkupLine($"[#FF7F50]CoralBridge Service[/] version [bold]{info.Version}[/]");
            }
        }
        catch (HttpRequestException)
        {
            AnsiConsole.MarkupLine($"[dim]CoralBridge Service: not reachable at {url}[/]");
        }
    }

    private static async Task HandleStatsCommand(string url, bool watch, bool fahrenheit)
    {
        if (watch)
        {
            await WatchStats(url, fahrenheit);
        }
        else
        {
            await DisplayStatsOnce(url, fahrenheit);
        }
    }

    private static async Task DisplayStatsOnce(string url, bool fahrenheit)
    {
        try
        {
            var stats = await FetchStats(url);
            if (stats == null)
            {
                AnsiConsole.MarkupLine("[red]Failed to fetch stats[/]");
                return;
            }

            RenderStats(stats, showBanner: true, fahrenheit: fahrenheit);
        }
        catch (HttpRequestException ex)
        {
            AnsiConsole.MarkupLine($"[red]Connection failed:[/] {ex.Message}");
            AnsiConsole.MarkupLine($"[dim]Is the service running at {url}?[/]");
        }
    }

    private static async Task WatchStats(string url, bool fahrenheit)
    {
        var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        // Show banner once
        Console.Clear();
        AnsiConsole.Markup(AsciiBanner);
        AnsiConsole.WriteLine();

        // Use Live display for flicker-free updates
        await AnsiConsole.Live(new Text("Connecting..."))
            .AutoClear(false)
            .StartAsync(async ctx =>
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    try
                    {
                        var stats = await FetchStats(url);
                        if (stats != null)
                        {
                            var table = BuildStatsTable(stats, fahrenheit);
                            ctx.UpdateTarget(new Rows(
                                table,
                                new Markup("\n[dim]Press [#FF7F50]Ctrl+C[/] to stop | Refreshing every second...[/]")
                            ));
                        }
                    }
                    catch (HttpRequestException)
                    {
                        ctx.UpdateTarget(new Markup("[red]Connection lost - retrying...[/]"));
                    }

                    try
                    {
                        await Task.Delay(1000, cts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            });

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[dim]Stopped.[/]");
    }

    private static async Task<StatsResponse?> FetchStats(string url)
    {
        var response = await HttpClient.GetAsync($"{url.TrimEnd('/')}/stats");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<StatsResponse>();
    }

    private static Table BuildStatsTable(StatsResponse stats, bool fahrenheit)
    {
        var uptime = FormatUptime(stats.UptimeSeconds);

        // Create a styled panel for stats
        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(CoralOrange)
            .Title($"[bold #FF7F50]Stats[/] [dim](uptime: {uptime})[/]")
            .AddColumn(new TableColumn("[blue]Metric[/]").Width(14))
            .AddColumn(new TableColumn("[blue]Value[/]"));

        // Inferences row
        var rate = stats.InferencesPerSecond > 0 ? $"[green]{stats.InferencesPerSecond:F1}/sec[/]" : "[dim]idle[/]";
        table.AddRow(
            "[#FF7F50]Inferences[/]",
            $"[bold]{stats.TotalInferences:N0}[/] total ({rate})");

        // Success rate row
        var successPct = stats.SuccessRate * 100;
        var successColor = successPct >= 99 ? "green" : successPct >= 95 ? "yellow" : "red";
        table.AddRow(
            "[#FF7F50]Success[/]",
            $"[{successColor}]{successPct:F1}%[/] [dim]({stats.SuccessfulInferences:N0} ok, {stats.FailedInferences:N0} failed)[/]");

        // Latency row
        if (stats.LatencyMs != null && stats.TotalInferences > 0)
        {
            var latency = stats.LatencyMs;
            table.AddRow(
                "[#FF7F50]Latency[/]",
                $"[bold]{latency.Average:F1}ms[/] avg [dim]({latency.Min:F0}-{latency.Max:F0}ms, P95: {latency.P95:F0}ms)[/]");
        }
        else
        {
            table.AddRow("[#FF7F50]Latency[/]", "[dim]No data yet[/]");
        }

        // Device row
        var tpuStatus = stats.Device?.UsingEdgeTpu == true ? "[green]Edge TPU active[/]" : "[yellow]CPU only[/]";
        var deviceType = stats.Device?.Type ?? "unknown";
        table.AddRow(
            "[#FF7F50]Device[/]",
            $"[bold]{deviceType}[/] - {tpuStatus}");

        // Temperature row
        if (stats.Device?.TemperatureCelsius.HasValue == true)
        {
            var tempC = stats.Device.TemperatureCelsius.Value;
            // Color code: green < 50C, yellow 50-70C, red > 70C
            var tempColor = tempC < 50 ? "green" : tempC < 70 ? "yellow" : "red";

            string tempDisplay;
            if (fahrenheit)
            {
                var tempF = CelsiusToFahrenheit(tempC);
                tempDisplay = $"{tempF:F1}F";
            }
            else
            {
                tempDisplay = $"{tempC:F1}C";
            }

            table.AddRow(
                "[#FF7F50]Temperature[/]",
                $"[{tempColor}]{tempDisplay}[/]");
        }

        // Model row
        if (!string.IsNullOrEmpty(stats.Device?.Model))
        {
            table.AddRow("[#FF7F50]Model[/]", $"[dim]{stats.Device.Model}[/]");
        }

        // Runtime version row
        if (!string.IsNullOrEmpty(stats.Device?.RuntimeVersion))
        {
            table.AddRow("[#FF7F50]Runtime[/]", $"[dim]{stats.Device.RuntimeVersion}[/]");
        }

        return table;
    }

    private static void RenderStats(StatsResponse stats, bool showBanner, bool fahrenheit)
    {
        if (showBanner)
        {
            AnsiConsole.Markup(AsciiBanner);
            AnsiConsole.WriteLine();
        }

        var table = BuildStatsTable(stats, fahrenheit);
        AnsiConsole.Write(table);
    }

    private static double CelsiusToFahrenheit(double celsius)
    {
        return celsius * 9.0 / 5.0 + 32.0;
    }

    private static string FormatUptime(long seconds)
    {
        var ts = TimeSpan.FromSeconds(seconds);
        if (ts.TotalDays >= 1)
            return $"{(int)ts.TotalDays}d {ts.Hours}h {ts.Minutes}m";
        if (ts.TotalHours >= 1)
            return $"{(int)ts.TotalHours}h {ts.Minutes}m {ts.Seconds}s";
        if (ts.TotalMinutes >= 1)
            return $"{(int)ts.TotalMinutes}m {ts.Seconds}s";
        return $"{ts.Seconds}s";
    }

    private static async Task HandleHealthCommand(string url)
    {
        AnsiConsole.Markup(AsciiBanner);
        AnsiConsole.WriteLine();

        try
        {
            var response = await HttpClient.GetAsync($"{url.TrimEnd('/')}/health");
            response.EnsureSuccessStatusCode();
            var health = await response.Content.ReadFromJsonAsync<HealthResponse>();

            if (health == null)
            {
                AnsiConsole.MarkupLine("[red]Failed to parse health response[/]");
                return;
            }

            var table = new Table()
                .Border(TableBorder.Rounded)
                .BorderColor(CoralOrange)
                .Title("[bold #FF7F50]Health Check[/]")
                .AddColumn(new TableColumn("[blue]Property[/]").Width(12))
                .AddColumn(new TableColumn("[blue]Value[/]"));

            var statusColor = health.Status == "healthy" ? "green" : "red";
            table.AddRow("[#FF7F50]Status[/]", $"[{statusColor}]{health.Status}[/]");

            table.AddRow("[#FF7F50]Version[/]", $"[bold]{health.Version}[/] [dim](CLI: {CliVersion})[/]");

            table.AddRow("[#FF7F50]Model[/]", $"[dim]{health.Model}[/]");

            var tpuText = health.UsingEdgeTpu ? "[green]active[/]" : "[yellow]not active[/]";
            table.AddRow("[#FF7F50]Edge TPU[/]", tpuText);

            table.AddRow("[#FF7F50]Input Size[/]", $"[bold]{health.InputWidth}x{health.InputHeight}[/]");

            AnsiConsole.Write(table);
        }
        catch (HttpRequestException ex)
        {
            AnsiConsole.MarkupLine($"[red]Connection failed:[/] {ex.Message}");
            AnsiConsole.MarkupLine($"[dim]Is the service running at {url}?[/]");
        }
    }
}
