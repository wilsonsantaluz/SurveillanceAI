using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SurveillanceAI.Application;
using SurveillanceAI.Domain;

namespace SurveillanceAI.Monitoring;

public sealed class CameraManager(IMonitoringStore store, ISecretProtector secrets, Session session, ILogger<CameraManager> log) : BackgroundService, IMonitoringControl
{
    private readonly SemaphoreSlim gate = new(1);
    private readonly List<(CameraWorker Worker, CancellationTokenSource Cancellation, Task Task)> workers = [];
    private readonly object sync = new();
    private CancellationToken lifetime;
    public IReadOnlyList<CameraSnapshot> Snapshots { get { session.Require(); lock (sync) return workers.Select(x => x.Worker.Snapshot).ToList(); } }
    public void SetOverlays(bool enabled, bool proximity) { session.Require(); lock (sync) foreach (var item in workers) { item.Worker.Overlays = enabled; item.Worker.ProximityDiagnostic = proximity; } }
    public async Task ReloadAsync(CancellationToken token = default) { session.Require(UserRole.Administrator); await ReloadCoreAsync(token); }
    private async Task ReloadCoreAsync(CancellationToken token)
    {
        await gate.WaitAsync(token);
        try
        {
            await StopWorkersAsync();
            var cameras = await store.EnabledCamerasAsync(token); var model = await store.ModelDirectoryAsync(token);
            foreach (var camera in cameras)
            {
                try
                {
                    var source = secrets.Unprotect(camera.ProtectedSource);
                    var cancellation = CancellationTokenSource.CreateLinkedTokenSource(lifetime);
                    var worker = new CameraWorker(camera, source, () => new OpenCvFrameSource(), model, store.RecordAsync, store.RulesAsync, log);
                    var task = Task.Run(() => worker.RunOwnedAsync(cancellation.Token), CancellationToken.None);
                    lock (sync) workers.Add((worker, cancellation, task));
                }
                catch (Exception error) { log.LogError("Cannot start camera {CameraId}: {ErrorType}", camera.Id, error.GetType().Name); }
            }
        }
        finally { gate.Release(); }
    }
    public async Task<bool> TestAsync(Camera camera, CancellationToken token = default)
    {
        session.Require(UserRole.Administrator);
        return await Task.Run(() =>
        {
            token.ThrowIfCancellationRequested();
            using var input = new OpenCvFrameSource(); using var frame = new OpenCvSharp.Mat();
            try { return input.Open(camera, secrets.Unprotect(camera.ProtectedSource)) && input.Read(frame); }
            catch { return false; }
        }, token);
    }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        lifetime = stoppingToken;
        await ReloadCoreAsync(stoppingToken);
        try { await Task.Delay(Timeout.Infinite, stoppingToken); }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        finally { await gate.WaitAsync(CancellationToken.None); try { await StopWorkersAsync(); } finally { gate.Release(); } }
    }
    private async Task StopWorkersAsync()
    {
        List<(CameraWorker Worker, CancellationTokenSource Cancellation, Task Task)> previous;
        lock (sync) { previous = workers.ToList(); workers.Clear(); }
        foreach (var item in previous) item.Cancellation.Cancel();
        await Task.WhenAll(previous.Select(x => x.Task));
        foreach (var item in previous) item.Cancellation.Dispose();
    }
}
