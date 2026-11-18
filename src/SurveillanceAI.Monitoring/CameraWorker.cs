using SurveillanceAI.Application.Localization;
using System.Diagnostics;
using System.Threading.Channels;
using OpenCvSharp;
using SurveillanceAI.Application;
using SurveillanceAI.Domain;
using Microsoft.Extensions.Logging;

namespace SurveillanceAI.Monitoring;

public interface IFrameSource : IDisposable { bool Open(Camera camera, string source); bool Read(Mat frame); double Fps { get; } }
public sealed class OpenCvFrameSource : IFrameSource
{
    private VideoCapture? capture;
    public double Fps => capture?.Fps ?? 0;
    public bool Open(Camera camera, string source)
    {
        capture = new VideoCapture();
        if (camera.Kind == SourceKind.Webcam) return capture.Open(int.Parse(source), VideoCaptureAPIs.DSHOW);
        if (camera.Kind == SourceKind.VideoFile) return capture.Open(source, VideoCaptureAPIs.FFMPEG);
        if (camera.Kind is not (SourceKind.Rtsp or SourceKind.HttpStream)) throw new ArgumentException("Unsupported camera source type.", nameof(camera));
        // OpenCV CAP_PROP_OPEN_TIMEOUT_MSEC=53, CAP_PROP_READ_TIMEOUT_MSEC=54;
        // this OpenCvSharp release does not expose names for these native properties.
        capture.Dispose();
        capture = new VideoCapture(source, VideoCaptureAPIs.FFMPEG, [ 53, 5000, 54, 3000 ]);
        return capture.IsOpened();
    }
    public bool Read(Mat frame) => capture?.Read(frame) == true && !frame.Empty();
    public void Dispose() => capture?.Dispose();
}
public sealed class CameraWorker(Camera camera, string source, Func<IFrameSource> sourceFactory, string modelDirectory, Func<DetectionEvent, byte[]?, CancellationToken, Task> record, Func<CancellationToken, Task<IReadOnlyList<AlertRule>>> rules, ILogger log, SmartMonitoringSettings? smart = null, Action<Mat>? additionalFrame = null, Action<Mat>? additionalOverlay = null, Func<Mat, CancellationToken, Task>? sequentialAnalysis = null)
{
    private CameraSnapshot snapshot = new(camera.Id, camera.Name, "Stopped", null, 0, 0, 0, 0, 0, 0, "");
    private readonly TemporalRules temporal = new();
    private bool completedVideo;
    private long dropped;
    private int reconnects;
    public bool Overlays { get; set; } = true;
    public bool ProximityDiagnostic { get; set; }
    public CameraSnapshot Snapshot => Volatile.Read(ref snapshot);
    public static TimeSpan Backoff(int failures) => TimeSpan.FromSeconds(Math.Min(30, Math.Pow(2, Math.Clamp(failures - 1, 0, 5))));
    private async Task RunCoreAsync(CancellationToken token)
    {
        var frames = Channel.CreateBounded<Mat>(new BoundedChannelOptions(1) { SingleReader = true, SingleWriter = true, FullMode = sequentialAnalysis is null ? BoundedChannelFullMode.DropOldest : BoundedChannelFullMode.Wait }, stale => { stale.Dispose(); Interlocked.Increment(ref dropped); });
        // Both capture and inference execute on thread pool workers, independent of navigation.
        var capture = Task.Run(async () =>
        {
            var failures = 0;
            try
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        Update("Connecting"); using var input = sourceFactory();
                        if (!input.Open(camera, source)) throw new IOException(Texts.T("Source unavailable."));
                        Update("Online");
                        log.LogInformation("Camera {CameraId} online", camera.Id);
                        var clock = Stopwatch.StartNew(); long captured = 0;
                        while (!token.IsCancellationRequested)
                        {
                            var frame = new Mat();
                            try
                            {
                                if (!input.Read(frame))
                                {
                                    if (sequentialAnalysis is not null && camera.Kind == SourceKind.VideoFile) { completedVideo = true; return; }
                                    throw new IOException(Texts.T("Source disconnected or playback ended."));
                                }
                                failures = 0;
                                if (sequentialAnalysis is not null) await sequentialAnalysis(frame, token);
                                try { additionalFrame?.Invoke(frame); }
                                catch (Exception error) { log.LogWarning("Additional detector frame rejected for {CameraId}: {ErrorType}", camera.Id, error.GetType().Name); }
                                captured++;
                                if (sequentialAnalysis is not null)
                                {
                                    var position = Snapshot;
                                    Volatile.Write(ref snapshot, position with { VideoFrame = captured, VideoSeconds = (captured - 1) / (input.Fps > 0 ? input.Fps : 25) });
                                    await frames.Writer.WriteAsync(frame, token);
                                }
                                else if (!frames.Writer.TryWrite(frame)) frame.Dispose();
                                frame = null!;
                            }
                            finally { frame?.Dispose(); }
                            var current = Snapshot; Volatile.Write(ref snapshot, current with { CaptureFps = captured / Math.Max(.001, clock.Elapsed.TotalSeconds) });
                            if (camera.Kind == SourceKind.VideoFile && sequentialAnalysis is null) await Task.Delay(TimeSpan.FromSeconds(1 / (input.Fps is > 0 and < 240 ? input.Fps : 25)), token);
                        }
                    }
                    catch (OperationCanceledException) when (token.IsCancellationRequested) { break; }
                    catch (Exception error)
                    {
                        temporalReset = true;
                        while (frames.Reader.TryRead(out var stale)) stale.Dispose();
                        Interlocked.Increment(ref reconnects); Update("Offline");
                        log.LogWarning("Camera {CameraId} disconnected: {ErrorType}", camera.Id, error.GetType().Name);
                        await Task.Delay(Backoff(++failures), token);
                    }
                }
            }
            finally { frames.Writer.TryComplete(); }
        }, CancellationToken.None);
        try
        {
            using var detector = smart?.YoloEnabled == false ? null : new OnnxDetector(modelDirectory);
            var clock = Stopwatch.StartNew(); long processed = 0;
            var activeRules = await rules(token); var refresh = DateTime.UtcNow;
            await foreach (var frame in frames.Reader.ReadAllAsync(token))
            {
                using (frame)
                {
                    if (temporalReset) { temporal.Reset(camera.Id); temporalReset = false; }
                    if (DateTime.UtcNow >= refresh) { activeRules = await rules(token); refresh = DateTime.UtcNow.AddSeconds(5); }
                    var start = Stopwatch.StartNew(); var detections = detector?.Detect(frame) ?? []; var latency = detector is null ? 0 : start.Elapsed.TotalMilliseconds;
                    var confirmed = temporal.Evaluate(camera.Id, detections, activeRules, DateTime.UtcNow);
                    if (Overlays) foreach (var d in detections)
                    {
                        Cv2.Rectangle(frame, new Rect((int)d.X, (int)d.Y, (int)d.Width, (int)d.Height), Scalar.Cyan, 2);
                        Cv2.PutText(frame, $"{d.Label} {d.Confidence:P0}", new Point((int)d.X, Math.Max(16, (int)d.Y)), HersheyFonts.HersheySimplex, .5, Scalar.Cyan);
                    }
                    if (ProximityDiagnostic)
                    {
                        var people = detections.Where(x => x.Label == "person").ToList();
                        for (var i = 0; i < people.Count; i++) for (var j = i + 1; j < people.Count; j++)
                            Cv2.Line(frame, new Point(people[i].X + people[i].Width / 2, people[i].Y + people[i].Height / 2), new Point(people[j].X + people[j].Width / 2, people[j].Y + people[j].Height / 2), Scalar.Yellow);
                    }
                    Cv2.ImEncode(".jpg", frame, out var jpeg, [(int)ImwriteFlags.JpegQuality, 80]);
                    foreach (var (rule, d) in confirmed)
                        await record(new DetectionEvent { CameraId = camera.Id, RuleId = rule.Id, TemplateId = rule.TemplateId, Label = d.Label, Confidence = d.Confidence, X = d.X, Y = d.Y, Width = d.Width, Height = d.Height, ModelVersion = detector?.Manifest.Version ?? "", Severity = rule.Severity, VisualAlert = rule.TemplateId == null || smart?.VisualAlert == true, SoundAlert = rule.TemplateId != null && smart?.SoundAlert == true, SaveEvidence = rule.TemplateId != null && smart?.SaveEvidence == true, NotifyEmail = rule.TemplateId == null || smart?.NotifyEmail == true }, jpeg, token);
                    processed++;
                    var current = Snapshot;
                    if (Overlays && additionalOverlay is not null) { additionalOverlay(frame); Cv2.ImEncode(".jpg", frame, out jpeg, [(int)ImwriteFlags.JpegQuality, 80]); }
                    Volatile.Write(ref snapshot, current with { Jpeg = jpeg, LatencyMs = latency, InferenceFps = detector is null ? 0 : processed / Math.Max(.001, clock.Elapsed.TotalSeconds), DroppedFrames = Interlocked.Read(ref dropped), QueueDepth = frames.Reader.Count, Reconnects = reconnects, ModelVersion = detector?.Manifest.Version ?? "Preview" });
                }
                await Task.Delay(TimeSpan.FromSeconds(1d / Math.Clamp(camera.InferenceFps, 1, 30)), token);
            }
        }
        finally
        {
            // Owner cancels the linked token on processing failure before awaiting capture.
            stopCapture?.Invoke();
            try { await capture; } catch (OperationCanceledException) when (token.IsCancellationRequested) { }
            while (frames.Reader.TryRead(out var pending)) pending.Dispose();
            temporal.Reset(camera.Id); Update(completedVideo ? "Video analysis complete" : "Stopped");
        }
    }
    private volatile bool temporalReset;
    private Action? stopCapture;
    public async Task RunOwnedAsync(CancellationToken token)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(token);
        stopCapture = linked.Cancel;
        try { await RunCoreAsync(linked.Token); }
        catch (OperationCanceledException) when (linked.IsCancellationRequested) { }
        catch (Exception error) { Update("Error: check model/source"); log.LogError("Camera worker {CameraId} failed: {ErrorType}", camera.Id, error.GetType().Name); }
    }
    private void Update(string state) { var current = Snapshot; Volatile.Write(ref snapshot, current with { State = state, Jpeg = state is "Online" or "Video analysis complete" ? current.Jpeg : null, Reconnects = reconnects }); }
}
