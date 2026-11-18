using System.Text.RegularExpressions;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using OpenCvSharp;
using SurveillanceAI.Application;
using SurveillanceAI.Domain;

namespace SurveillanceAI.Monitoring;

public sealed record FlorenceCameraSnapshot(Guid CameraId, string State, long Analyses, DateTime? LastAnalysisUtc, double LatencyMs, string? Error = null, long DroppedFrames = 0);

// One latest frame per camera; all cameras share one loaded, serialized Florence model.
public sealed class FlorenceCameraWorker(Guid cameraId, SmartMonitoringSettings settings, IFlorenceDetector detector, IMonitoringStore store, ILogger log)
{
    public const string WeaponPrompt = "handgun . pistol . revolver . firearm . rifle . person holding a handgun";
    private readonly Channel<(Mat Frame, DateTime CapturedUtc)> frames = Channel.CreateBounded<(Mat, DateTime)>(new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true, SingleWriter = true }, packet => packet.Item1.Dispose());
    private FlorenceCameraSnapshot snapshot = new(cameraId, "Waiting for model", 0, null, 0);
    public FlorenceCameraSnapshot Snapshot => Volatile.Read(ref snapshot) with { DroppedFrames = DroppedFrames };
    private void State(string state, string? error = null) => Volatile.Write(ref snapshot, Snapshot with { State = state, Error = error });
    private void Completed(VisualResult result) => Volatile.Write(ref snapshot, Snapshot with { Analyses = Snapshot.Analyses + 1, LastAnalysisUtc = DateTime.UtcNow, LatencyMs = result.LatencyMs, Error = null });
    private long sequentialFrame;
    private long dropped;
    private DateTime nextFrame;
    private (VisualResult Result, int Width, int Height, DateTime Time)? latest;
    private readonly object sync = new();
    public long DroppedFrames => Interlocked.Read(ref dropped);
    public static bool IsWeaponLabel(string label) => Regex.IsMatch(label, @"\b(handguns?|pistols?|revolvers?|firearms?|rifles?|shotguns?|guns?)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public void Offer(Mat frame)
    {
        if (DateTime.UtcNow < nextFrame) { Interlocked.Increment(ref dropped); return; }
        nextFrame = DateTime.UtcNow.AddSeconds(settings.FlorenceIntervalSeconds);
        // Retain original pixels for evidence; only the inference input is reduced.
        var copy = frame.Clone();
        if (frames.Reader.Count > 0) Interlocked.Increment(ref dropped);
        if (!frames.Writer.TryWrite((copy, DateTime.UtcNow))) copy.Dispose();
    }
    public void Overlay(Mat frame)
    {
        (VisualResult Result, int Width, int Height, DateTime Time)? current;
        lock (sync) current = latest;
        if (current is not { } result || (DateTime.UtcNow - result.Time).TotalSeconds > 30) return;
        foreach (var region in result.Result.Regions)
        {
            var x = (int)Math.Clamp(region.X * frame.Width / result.Width, 0, frame.Width - 1);
            var y = (int)Math.Clamp(region.Y * frame.Height / result.Height, 0, frame.Height - 1);
            var width = (int)Math.Clamp(region.Width * frame.Width / result.Width, 1, frame.Width - x);
            var height = (int)Math.Clamp(region.Height * frame.Height / result.Height, 1, frame.Height - y);
            Cv2.Rectangle(frame, new Rect(x, y, width, height), Scalar.Magenta, 2);
            Cv2.PutText(frame, "Florence: " + region.Label, new Point(x, Math.Max(16, y)), HersheyFonts.HersheySimplex, .5, Scalar.Magenta);
        }
    }
    public async Task RunAsync(CancellationToken token)
    {
        try
        {
            await EnsureModelAsync(token, force: true);
            State("Waiting for frames");
            await foreach (var packet in frames.Reader.ReadAllAsync(token))
            {
                var frame = packet.Frame;
                using (frame)
                {
                    try
                    {
                        await AnalyzeFrameAsync(frame, packet.CapturedUtc, token);
                    }
                    catch (OperationCanceledException) when (token.IsCancellationRequested) { break; }
                    catch (Exception error) { State("Error", error.GetType().Name); log.LogError("Florence camera {CameraId} failed: {ErrorType}", cameraId, error.GetType().Name); await Task.Delay(TimeSpan.FromSeconds(5), token); }
                }
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception error) { log.LogError("Florence initialization for camera {CameraId} failed: {ErrorType}", cameraId, error.GetType().Name); }
        finally { State("Stopped"); frames.Writer.TryComplete(); while (frames.Reader.TryRead(out var pending)) pending.Frame.Dispose(); }
    }
    // Sequential file mode awaits this method and retains the same frame through retries.
    public async Task ProcessFrameAsync(Mat frame, CancellationToken token)
    {
        var capturedUtc = DateTime.UtcNow;
        Interlocked.Increment(ref sequentialFrame);
        while (true)
        {
            try { await AnalyzeFrameAsync(frame, capturedUtc, token); return; }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
            catch (Exception error)
            {
                State("Error", error.GetType().Name);
                log.LogWarning("Sequential Florence frame retry {CameraId}: {ErrorType}", cameraId, error.GetType().Name);
                await Task.Delay(TimeSpan.FromSeconds(5), token);
            }
        }
    }
    public Task ProcessRecordedFrameAsync(Mat frame, DateTime capturedUtc, CancellationToken token) => AnalyzeFrameAsync(frame, capturedUtc, token);
    private async Task AnalyzeFrameAsync(Mat frame, DateTime capturedUtc, CancellationToken token)
    {
        await EnsureModelAsync(token);
        State("Analyzing objects");
        var scale = Math.Min(1d, 1280d / Math.Max(frame.Width, frame.Height));
        using var analysis = scale < 1 ? frame.Resize(new Size(Math.Max(1, (int)(frame.Width * scale)), Math.Max(1, (int)(frame.Height * scale)))) : frame.Clone();
        Cv2.ImEncode(".jpg", analysis, out var input);
        Cv2.ImEncode(".jpg", frame, out var jpeg, [(int)ImwriteFlags.JpegQuality, 95]);
        var result = ToEvidenceCoordinates(await detector.DetectAsync(input, false, null, token), analysis.Size(), frame.Size());
        Completed(result);
        lock (sync) latest = (result, frame.Width, frame.Height, DateTime.UtcNow);
        // Preserve first OD evidence before the directed grounding fallback.
        var found = await RecordWeaponsAsync(result, jpeg, token, capturedUtc, observeAbsence: false);
        if (settings.FirearmsEnabled && !found)
        {
            State("Searching for weapons");
            result = ToEvidenceCoordinates(await detector.DetectAsync(input, true, WeaponPrompt, token), analysis.Size(), frame.Size());
            Completed(result);
            var confirmed = !settings.ConfirmGrounding || await ConfirmRegionAsync(result, frame, token);
            if (!confirmed) result = result with { Regions = [] };
            lock (sync) latest = (result, frame.Width, frame.Height, DateTime.UtcNow);
            // Rearm only when both analyses fail to return a weapon hypothesis.
            await RecordWeaponsAsync(result, jpeg, token, capturedUtc, groundingConfirmed: confirmed);
        }
        State("Waiting for frames");
    }
    private async Task<bool> ConfirmRegionAsync(VisualResult grounded, Mat frame, CancellationToken token)
    {
        var region = grounded.Regions.FirstOrDefault(r => IsWeaponLabel(r.Label));
        if (region is null) return false;
        State("Checking detected region");
        var x = (int)Math.Clamp(region.X, 0, frame.Width - 1);
        var y = (int)Math.Clamp(region.Y, 0, frame.Height - 1);
        var width = (int)Math.Clamp(region.Width, 1, frame.Width - x);
        var height = (int)Math.Clamp(region.Height, 1, frame.Height - y);
        using var crop = new Mat(frame, new Rect(x, y, width, height));
        var scale = Math.Min(1d, 1280d / Math.Max(width, height));
        using var input = scale < 1 ? crop.Resize(new Size(Math.Max(1, (int)(width * scale)), Math.Max(1, (int)(height * scale)))) : crop.Clone();
        Cv2.ImEncode(".jpg", input, out var jpeg);
        var confirmation = await detector.DetectAsync(jpeg, false, null, token);
        Completed(confirmation);
        return confirmation.Regions.Any(r => IsWeaponLabel(r.Label));
    }
    private async Task EnsureModelAsync(CancellationToken token, bool force = false)
    {
        while (true)
        {
            token.ThrowIfCancellationRequested();
            if (!force && detector.Diagnostic.State == "Ready") return;
            try { await detector.LoadAsync(settings.FlorenceDirectory, settings.MaxTokens, settings.CpuThreads, token); return; }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
            catch (Exception error)
            {
                State("Retrying model", error.GetType().Name);
                log.LogWarning("Florence camera {CameraId} model retry: {ErrorType}", cameraId, error.GetType().Name);
                await Task.Delay(TimeSpan.FromSeconds(5), token);
            }
        }
    }
    private static VisualResult ToEvidenceCoordinates(VisualResult result, Size input, Size evidence)
    {
        var sx = (float)evidence.Width / input.Width; var sy = (float)evidence.Height / input.Height;
        return result with { Regions = result.Regions.Select(r => r with { X = r.X * sx, Y = r.Y * sy, Width = r.Width * sx, Height = r.Height * sy }).ToList() };
    }
    public async Task<bool> RecordWeaponsAsync(VisualResult result, byte[] jpeg, CancellationToken token, DateTime? capturedUtc = null, bool observeAbsence = true, bool groundingConfirmed = false)
    {
        if (!settings.FirearmsEnabled) return false;
        var region = result.Regions.FirstOrDefault(x => IsWeaponLabel(x.Label));
        if (result.Task is not ("OD" or "CAPTION_TO_PHRASE_GROUNDING")) return false;
        if (result.Task == "CAPTION_TO_PHRASE_GROUNDING" && settings.ConfirmGrounding && !groundingConfirmed) region = null;
        if (region is null)
        {
            if (observeAbsence) await store.ObserveFirearmAbsenceAsync(cameraId, capturedUtc ?? DateTime.UtcNow, token);
            return false;
        }
        var rule = (await store.RulesAsync(token)).FirstOrDefault(x => x.Enabled && x.TemplateId == TemplateIds.Firearms);
        if (rule is null) return false;
        token.ThrowIfCancellationRequested();
        await store.RecordAsync(new DetectionEvent
        {
            CameraId = cameraId, TimestampUtc = capturedUtc ?? DateTime.UtcNow, RuleId = rule.Id, TemplateId = TemplateIds.Firearms, DetectorId = result.DetectorId,
            Label = "possible_firearm", Confidence = null, X = region.X, Y = region.Y, Width = region.Width, Height = region.Height,
            ModelVersion = result.ModelVersion, Severity = Severity.Warning, EpisodeOpen = true, SaveEvidence = true,
            VisualAlert = settings.VisualAlert, SoundAlert = settings.SoundAlert, NotifyEmail = settings.NotifyEmail,
            Notes = "Florence hypothesis: " + region.Label + " / " + result.Task + (groundingConfirmed && settings.ConfirmGrounding ? " / confirmed by crop OD" : "") + (sequentialFrame > 0 ? $" / video frame {sequentialFrame}" : "")
        }, jpeg, token);
        return true;
    }
}
