using CoralBridge.Api;
using CoralBridge.Core;
using Microsoft.Extensions.Hosting.WindowsServices;

namespace CoralBridge;

public class Program
{
    public static void Main(string[] args)
    {
        var options = new WebApplicationOptions
        {
            Args = args,
            ContentRootPath = WindowsServiceHelpers.IsWindowsService()
                ? AppContext.BaseDirectory
                : default
        };

        var builder = WebApplication.CreateBuilder(options);

        // Configure Windows Service
        builder.Host.UseWindowsService();

        // Configure Kestrel
        builder.WebHost.ConfigureKestrel(serverOptions =>
        {
            serverOptions.ListenAnyIP(5555);
        });

        // Get configuration
        var config = builder.Configuration;
        var modelPath = config["CoralBridge:ModelPath"]
            ?? "models/ssd_mobilenet_v1_coco_quant_postprocess_edgetpu.tflite";

        // Resolve model path relative to content root
        if (!Path.IsPathRooted(modelPath))
        {
            var resolvedPath = Path.Combine(builder.Environment.ContentRootPath, modelPath);

            // If path doesn't exist and looks like a dev path (../../), try the deployment path
            if (!File.Exists(resolvedPath) && modelPath.Contains(".."))
            {
                var modelFileName = Path.GetFileName(modelPath);
                var deploymentPath = Path.Combine(builder.Environment.ContentRootPath, "models", modelFileName);
                if (File.Exists(deploymentPath))
                {
                    resolvedPath = deploymentPath;
                }
            }

            modelPath = resolvedPath;
        }

        // Register the stats collector as a singleton
        builder.Services.AddSingleton<StatsCollector>();

        // Register the object detector as a singleton
        builder.Services.AddSingleton<IObjectDetector>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<EdgeTpuDetector>>();
            var statsCollector = sp.GetRequiredService<StatsCollector>();
            return new EdgeTpuDetector(modelPath, logger, statsCollector);
        });

        var app = builder.Build();

        // Map API endpoints
        app.MapEndpoints();

        // Log startup info
        var logger = app.Services.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("CoralBridge starting...");
        logger.LogInformation("Model path: {ModelPath}", modelPath);
        logger.LogInformation("Listening on: http://0.0.0.0:5555");

        app.Run();
    }
}
