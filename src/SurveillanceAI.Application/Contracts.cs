using SurveillanceAI.Application.Localization;
using SurveillanceAI.Domain;

namespace SurveillanceAI.Application;

public sealed class Session
{
    public Guid? UserId { get; private set; }
    public UserRole Role { get; private set; }
    public void SignIn(Guid id, UserRole role) { UserId = id; Role = role; }
    public void Logout() => UserId = null;
    public void Require(UserRole maximum = UserRole.Viewer)
    {
        if (UserId is null || Role > maximum) throw new UnauthorizedAccessException(Texts.T("Your role does not permit this operation."));
    }
}
public interface ISecretProtector { string Protect(string value); string Unprotect(string value); }
public sealed record EventFilter(string Search = "", Guid? CameraId = null, ReviewStatus? Status = null, Severity? Severity = null, DateTime? FromUtc = null, DateTime? ToUtc = null, int Page = 0);
public sealed record EventCounts(int Total, int Pending, int Today);
public sealed class SmtpSettings
{
    public bool Enabled { get; set; }
    public string Host { get; set; } = "";
    public int Port { get; set; } = 587;
    public string TlsMode { get; set; } = "StartTls";
    public string Username { get; set; } = "";
    public string ProtectedPassword { get; set; } = "";
    public string Sender { get; set; } = "";
    public string SenderName { get; set; } = "SurveillanceAI";
    public int MaxAttempts { get; set; } = 3;
}
public interface IMailTransport { Task SendAsync(SmtpSettings settings, NotificationDelivery delivery, CancellationToken token); }
public interface IEvidenceStore
{
    Task<string> SaveAsync(Guid eventId, byte[] jpeg, CancellationToken token);
    void Delete(string relativePath);
    string Resolve(string relativePath);
}
public sealed record CameraSnapshot(Guid Id, string Name, string State, byte[]? Jpeg, double LatencyMs, double CaptureFps, double InferenceFps, long DroppedFrames, int QueueDepth, int Reconnects, string ModelVersion, long VideoFrame = 0, double VideoSeconds = 0);
public interface IMonitoringControl
{
    IReadOnlyList<CameraSnapshot> Snapshots { get; }
    Task ReloadAsync(CancellationToken token = default);
    Task<bool> TestAsync(Camera camera, CancellationToken token = default);
}
public interface IMonitoringStore
{
    Task<IReadOnlyList<Camera>> EnabledCamerasAsync(CancellationToken token);
    Task<IReadOnlyList<AlertRule>> RulesAsync(CancellationToken token);
    Task<string> ModelDirectoryAsync(CancellationToken token);
    Task RecordAsync(DetectionEvent item, byte[]? jpeg, CancellationToken token);
    Task ObserveFirearmAbsenceAsync(Guid cameraId, DateTime capturedUtc, CancellationToken token) => Task.CompletedTask;
    Task<SmartMonitoringSettings> SmartSettingsAsync(CancellationToken token) => Task.FromResult(new SmartMonitoringSettings());
}
public sealed class TemporalRules
{
    private sealed class Track(Detection detection, DateTime time)
    {
        public Detection Last = detection;
        public DateTime LastSeen = time;
        public List<DateTime> Hits = [];
        public DateTime? Alerted;
    }
    private readonly Dictionary<(Guid Camera, Guid Rule), List<Track>> tracks = [];
    public IReadOnlyList<(AlertRule Rule, Detection Detection)> Evaluate(Guid camera, IReadOnlyList<Detection> detections, IEnumerable<AlertRule> rules, DateTime now)
    {
        var alerts = new List<(AlertRule, Detection)>();
        foreach (var rule in rules.Where(r => r.Enabled && (r.CameraId == null || r.CameraId == camera)))
        {
            var key = (camera, rule.Id);
            if (!tracks.TryGetValue(key, out var list)) tracks[key] = list = [];
            list.RemoveAll(t => (now - t.LastSeen).TotalSeconds > Math.Max(rule.WindowSeconds, rule.CooldownSeconds));
            var used = new HashSet<Track>();
            foreach (var detection in detections.Where(d => d.Label == rule.Label && d.Confidence >= rule.Confidence))
            {
                var track = list.Where(t => !used.Contains(t) && t.Last.Iou(detection) >= rule.AssociationIou).OrderByDescending(t => t.Last.Iou(detection)).FirstOrDefault();
                if (track is null) { track = new Track(detection, now); list.Add(track); }
                used.Add(track);
                track.Last = detection; track.LastSeen = now;
                track.Hits.RemoveAll(t => (now - t).TotalSeconds > rule.WindowSeconds);
                if (!track.Hits.Contains(now)) track.Hits.Add(now);
                if (track.Hits.Count >= rule.Confirmations && (track.Alerted is null || (now - track.Alerted.Value).TotalSeconds >= rule.CooldownSeconds))
                { track.Alerted = now; track.Hits.Clear(); alerts.Add((rule, detection)); }
            }
        }
        return alerts;
    }
    public void Reset(Guid camera) { foreach (var key in tracks.Keys.Where(k => k.Camera == camera).ToList()) tracks.Remove(key); }
}
