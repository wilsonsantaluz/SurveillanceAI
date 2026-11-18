using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SurveillanceAI.Application;

namespace SurveillanceAI.Monitoring;

public static class FlorenceModelPaths
{
    public static string Resolve(string configured)
    {
        // A configured bundle is validated by the engine; never silently replace an existing invalid bundle.
        if (!string.IsNullOrWhiteSpace(configured) && File.Exists(Path.Combine(configured, "florence-manifest.json"))) return Path.GetFullPath(configured);
        var candidates = new[]
        {
            Environment.GetEnvironmentVariable("SURVEILLANCEAI_FLORENCE_MODEL"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SurveillanceAI", "models", "Florence-2-base-ft"),
            Path.Combine(AppContext.BaseDirectory, "models", "Florence-2-base-ft"),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "models", "florence-base-ft"))
        };
        return candidates.FirstOrDefault(path => !string.IsNullOrWhiteSpace(path) && File.Exists(Path.Combine(path, "florence-manifest.json")))
            ?? (!string.IsNullOrWhiteSpace(configured) ? configured : candidates[1]!);
    }
}

public sealed class FlorenceStartupService(IMonitoringStore store, IFlorenceDetector detector, ILogger<FlorenceStartupService> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            var settings = await store.SmartSettingsAsync(stoppingToken);
            await detector.LoadAsync(FlorenceModelPaths.Resolve(settings.FlorenceDirectory), settings.MaxTokens, settings.CpuThreads, stoppingToken);
            log.LogInformation("Mandatory Florence engine ready: {Checkpoint}", detector.Diagnostic.Checkpoint);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        catch (Exception error)
        {
            // Keep the UI and existing capture available for recovery; UI prominently reports not-ready state.
            log.LogError("Mandatory Florence engine unavailable: {ErrorType}", error.GetType().Name);
        }
    }
}
