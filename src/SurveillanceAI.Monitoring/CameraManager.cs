using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SurveillanceAI.Application;
using SurveillanceAI.Domain;

namespace SurveillanceAI.Monitoring;

public sealed class CameraManager(IMonitoringStore store, ISecretProtector secrets, Session session, ILogger<CameraManager> log, IFlorenceDetector? florence = null, string? recordedBufferDirectory = null) : BackgroundService, IMonitoringControl
{
    private readonly SemaphoreSlim gate = new(1);
    private readonly List<(CameraWorker Worker, CancellationTokenSource Cancellation, Task Task)> workers = [];
    private readonly object sync = new();
    private CancellationToken lifetime;
    private RecordedFrameBuffer? recordedBuffer;
    private RecordedAnalysisPool? recordedPool;
    private CancellationTokenSource? recordedCancellation;
    private Task recordedTask = Task.CompletedTask;
    private readonly Dictionary<Guid, FlorenceCameraSnapshot> recordedSnapshots = [];
    private readonly Dictionary<Guid, FlorenceCameraWorker> recordedOverlays = [];
    public RecordedBufferSnapshot? RecordedSnapshot => recordedBuffer?.Snapshot;
    public int RecordedActiveWorkers => recordedPool?.ActiveWorkers ?? 0;
    public string? RecordedError => recordedPool?.LastError;

    private readonly List<FlorenceCameraWorker> florenceWorkers = [];
    public IReadOnlyList<FlorenceCameraSnapshot> FlorenceSnapshots { get { session.Require(); lock (sync) return florenceWorkers.Select(x => x.Snapshot).Concat(recordedSnapshots.Values).ToList(); } }
    public long FlorenceDroppedFrames { get { lock (sync) return florenceWorkers.Sum(x => x.DroppedFrames); } }
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
            var settings = await store.SmartSettingsAsync(token);
            var bufferedCameras = cameras.Where(c => c.Kind != SourceKind.VideoFile && settings.Includes(c.Id) && settings.BufferLiveCameras).Select(c => c.Id).ToHashSet();
            recordedBuffer = settings.BufferLiveCameras ? new RecordedFrameBuffer(recordedBufferDirectory ?? Path.Combine(Environment.GetEnvironmentVariable("SURVEILLANCEAI_DATA") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SurveillanceAI"), "frame-buffer"), settings.RecordedBufferGb * 1024L * 1024 * 1024) : null;
            if (recordedBuffer is not null && bufferedCameras.Count > 0)
            {
                foreach (var id in bufferedCameras) lock (sync) recordedSnapshots[id] = new(id, "Recorded analysis pending", 0, null, 0);
                recordedCancellation = CancellationTokenSource.CreateLinkedTokenSource(lifetime);
                recordedPool = new RecordedAnalysisPool(recordedBuffer, bufferedCameras, settings, store, () => new FlorenceDetector(), log,
                    (lease, processor) =>
                    {
                        lock (sync)
                        {
                            var previous = recordedSnapshots[lease.CameraId];
                            recordedSnapshots[lease.CameraId] = processor.Snapshot with { Analyses = previous.Analyses + processor.Snapshot.Analyses };
                            recordedOverlays[lease.CameraId] = processor;
                        }
                    });
                recordedTask = recordedPool.RunAsync(recordedCancellation.Token);
            }
            foreach (var camera in cameras)
            {
                try
                {
                    var source = secrets.Unprotect(camera.ProtectedSource);
                    var cancellation = CancellationTokenSource.CreateLinkedTokenSource(lifetime);
                    var buffered = bufferedCameras.Contains(camera.Id);
                    FlorenceCameraWorker? additional = !buffered && settings.FlorenceEnabled && settings.Includes(camera.Id) && florence is not null ? new(camera.Id, settings, florence, store, log) : null;
                    async Task<IReadOnlyList<AlertRule>> CameraRules(CancellationToken ct) => (await store.RulesAsync(ct)).Where(r => r.TemplateId == null || (r.TemplateId == TemplateIds.Objects && settings.Includes(camera.Id))).ToList();
                    var sequential = camera.Kind == SourceKind.VideoFile && settings.AnalyzeEveryVideoFrame && additional is not null;
                    Action<OpenCvSharp.Mat>? offer = buffered ? frame => recordedBuffer!.Append(camera.Id, frame, DateTime.UtcNow) : additional is null || sequential ? null : additional.Offer;
                    Action<OpenCvSharp.Mat>? overlay = buffered ? frame => { lock (sync) { if (recordedOverlays.TryGetValue(camera.Id, out var processor)) processor.Overlay(frame); } } : additional is null ? null : additional.Overlay;
                    var worker = new CameraWorker(camera, source, () => new OpenCvFrameSource(), model, store.RecordAsync, CameraRules, log, settings, offer, overlay, sequential ? additional!.ProcessFrameAsync : null);
                    var captureTask = Task.Run(() => worker.RunOwnedAsync(cancellation.Token), CancellationToken.None);
                    var additionalTask = additional is null || sequential ? Task.CompletedTask : Task.Run(() => additional.RunAsync(cancellation.Token), CancellationToken.None);
                    var task = Task.WhenAll(captureTask, additionalTask);
                    if (additional is not null) lock (sync) florenceWorkers.Add(additional);
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
        lock (sync) { previous = workers.ToList(); workers.Clear(); florenceWorkers.Clear(); }
        foreach (var item in previous) item.Cancellation.Cancel();
        recordedCancellation?.Cancel();
        await Task.WhenAll(previous.Select(x => x.Task).Append(recordedTask));
        lock (sync) { recordedSnapshots.Clear(); recordedOverlays.Clear(); }
        recordedCancellation?.Dispose(); recordedCancellation = null; recordedPool = null; recordedBuffer = null; recordedTask = Task.CompletedTask;
        foreach (var item in previous) item.Cancellation.Dispose();
    }
}
