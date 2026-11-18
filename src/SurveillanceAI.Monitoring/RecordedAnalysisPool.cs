using Microsoft.Extensions.Logging;
using OpenCvSharp;
using SurveillanceAI.Application;
using SurveillanceAI.Domain;

namespace SurveillanceAI.Monitoring;

// Separate model instances compute in parallel; persistence remains ordered per camera.
public sealed class RecordedAnalysisPool(RecordedFrameBuffer buffer, IReadOnlySet<Guid> cameras, SmartMonitoringSettings settings,
    IMonitoringStore store, Func<IFlorenceDetector> createDetector, ILogger log, Action<RecordedFrameLease, FlorenceCameraWorker>? completed = null)
{
    private int active;
    private string? lastError;
    public string? LastError => Volatile.Read(ref lastError);
    public int ActiveWorkers => Volatile.Read(ref active);
    public Task RunAsync(CancellationToken token) => Task.WhenAll(Enumerable.Range(0, settings.RecordedWorkers).Select(_ => Task.Run(() => RunWorkerAsync(token), CancellationToken.None)));

    private async Task RunWorkerAsync(CancellationToken token)
    {
        using var detector = createDetector();
        while (!token.IsCancellationRequested)
        {
            RecordedFrameLease? lease = null;
            var occupied = false;
            try
            {
                lease = buffer.Claim(cameras);
                if (lease is null) { await Task.Delay(100, token); continue; }
                Interlocked.Increment(ref active); occupied = true;
                await detector.LoadAsync(settings.FlorenceDirectory, settings.MaxTokens, settings.CpuThreads, token);
                using var frame = Cv2.ImRead(lease.Path, ImreadModes.Color);
                if (frame.Empty()) throw new InvalidDataException("Recorded frame cannot be decoded.");
                var writes = new DeferredFrameStore(store);
                var processor = new FlorenceCameraWorker(lease.CameraId, settings, detector, writes, log);
                await processor.ProcessRecordedFrameAsync(frame, lease.CapturedUtc, token);
                while (!buffer.IsFirstForCamera(lease)) await Task.Delay(50, token);
                await writes.CommitAsync(token);
                completed?.Invoke(lease, processor);
                buffer.Complete(lease); lease = null; Volatile.Write(ref lastError, null);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { break; }
            catch (Exception exception)
            {
                Volatile.Write(ref lastError, exception.GetType().Name);
                log.LogError("Recorded analysis failed: {ErrorType}", exception.GetType().Name);
                try { await Task.Delay(TimeSpan.FromSeconds(2), token); }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { break; }
            }
            finally
            {
                if (lease is not null) buffer.Return(lease);
                if (occupied) Interlocked.Decrement(ref active);
            }
        }
    }

    private sealed class DeferredFrameStore(IMonitoringStore store) : IMonitoringStore
    {
        private readonly List<Func<CancellationToken, Task>> writes = [];
        public Task<IReadOnlyList<Camera>> EnabledCamerasAsync(CancellationToken token) => store.EnabledCamerasAsync(token);
        public Task<IReadOnlyList<AlertRule>> RulesAsync(CancellationToken token) => store.RulesAsync(token);
        public Task<string> ModelDirectoryAsync(CancellationToken token) => store.ModelDirectoryAsync(token);
        public Task<SmartMonitoringSettings> SmartSettingsAsync(CancellationToken token) => store.SmartSettingsAsync(token);
        public Task RecordAsync(DetectionEvent item, byte[]? jpeg, CancellationToken token)
        { writes.Add(ct => store.RecordAsync(item, jpeg, ct)); return Task.CompletedTask; }
        public Task ObserveFirearmAbsenceAsync(Guid cameraId, DateTime capturedUtc, CancellationToken token)
        { writes.Add(ct => store.ObserveFirearmAbsenceAsync(cameraId, capturedUtc, ct)); return Task.CompletedTask; }
        public async Task CommitAsync(CancellationToken token)
        { foreach (var write in writes) await write(token); writes.Clear(); }
    }
}
