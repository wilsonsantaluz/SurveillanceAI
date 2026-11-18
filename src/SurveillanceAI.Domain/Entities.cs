namespace SurveillanceAI.Domain;

public enum UserRole { Administrator, Operator, Viewer }
public enum SourceKind { Webcam, Rtsp, VideoFile, HttpStream }
public enum ReviewStatus { Pending, Reviewed, FalsePositive }
public enum Severity { Information, Warning, Critical }
public sealed class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Username { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public UserRole Role { get; set; } = UserRole.Viewer;
    public bool Active { get; set; } = true;
    public int FailedAttempts { get; set; }
    public DateTime? LockedUntilUtc { get; set; }
}
public sealed class Camera
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Location { get; set; } = "";
    public SourceKind Kind { get; set; }
    public string ProtectedSource { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public int InferenceFps { get; set; } = 3;
}
public sealed class DetectionEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CameraId { get; set; }
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    public string Label { get; set; } = "";
    public float? Confidence { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public float Width { get; set; }
    public float Height { get; set; }
    public string ModelVersion { get; set; } = "";
    public Guid RuleId { get; set; }
    public Severity Severity { get; set; }
    public ReviewStatus Status { get; set; }
    public Guid? ReviewerId { get; set; }
    public DateTime? ReviewedUtc { get; set; }
    public string Notes { get; set; } = "";
    public string? EvidencePath { get; set; }
    public string DetectorId { get; set; } = "yolo";
    public string? TemplateId { get; set; }
    public bool EpisodeOpen { get; set; }
    public DateTime? AcknowledgedUtc { get; set; }
    public DateTime? ClosedUtc { get; set; }
    public bool HumanConfirmed { get; set; }
    public bool VisualAlert { get; set; } = true;
    public bool SoundAlert { get; set; }
    public bool SaveEvidence { get; set; }
    public bool NotifyEmail { get; set; } = true;
}
public sealed class AlertRule
{
    public string? TemplateId { get; set; }
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string Label { get; set; } = "person";
    public Guid? CameraId { get; set; }
    public bool Enabled { get; set; }
    public float Confidence { get; set; } = .75f;
    public int Confirmations { get; set; } = 3;
    public int WindowSeconds { get; set; } = 5;
    public int CooldownSeconds { get; set; } = 60;
    public float AssociationIou { get; set; } = .25f;
    public Severity Severity { get; set; } = Severity.Information;
}
public sealed class NotificationRecipient
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public bool Enabled { get; set; }
    public string ClassFilter { get; set; } = "";
    public Guid? CameraId { get; set; }
    public Severity MinimumSeverity { get; set; }
}
public sealed class NotificationDelivery
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EventId { get; set; }
    public Guid RecipientId { get; set; }
    public string Address { get; set; } = "";
    public string Subject { get; set; } = "";
    public string Body { get; set; } = "";
    public int Attempts { get; set; }
    public DateTime NextAttemptUtc { get; set; } = DateTime.UtcNow;
    public DateTime? DeliveredUtc { get; set; }
    public string Status { get; set; } = "Pending";
    public string? Error { get; set; }
}
public sealed class SystemSetting { public string Key { get; set; } = ""; public string Value { get; set; } = ""; }
public sealed class AuditLog
{
    public long Id { get; set; }
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    public Guid? UserId { get; set; }
    public string Action { get; set; } = "";
}
public sealed record Detection(string Label, float Confidence, float X, float Y, float Width, float Height)
{
    public float Iou(Detection other)
    {
        var intersection = Math.Max(0, Math.Min(X + Width, other.X + other.Width) - Math.Max(X, other.X)) * Math.Max(0, Math.Min(Y + Height, other.Y + other.Height) - Math.Max(Y, other.Y));
        var union = Width * Height + other.Width * other.Height - intersection;
        return union <= 0 ? 0 : intersection / union;
    }
}
