using SurveillanceAI.Application.Localization;
using SurveillanceAI.Domain;

namespace SurveillanceAI.Application;

public static class TemplateIds
{
    public const string Firearms = "possible-firearm-v1";
    public const string Knives = "visible-knife-v1";
    public const string Objects = "selected-objects-v1";
    public const string Running = "running-v1";
    public const string Screams = "screams-v1";
    public const string AfterHours = "after-hours-motion-v1";
}
public sealed record AlertTemplate(string Id, string Name, string Description, string Detector, bool Available, string Reason);
public static class TemplateCatalog
{
    public static IReadOnlyList<AlertTemplate> Get(bool florenceReady, IReadOnlyCollection<string> yoloClasses) =>
    [
        new(TemplateIds.Firearms, "Firearms and replicas", "Record possible firearms immediately, including unattended objects. Experimental: operator review required.", "florence2", florenceReady, florenceReady ? "" : "Load the local Florence-2 base-ft bundle first."),
        new(TemplateIds.Knives, "Knives and sharp objects", "Identify visible knives independently of people or context.", "yolo", false, yoloClasses.Contains("knife") ? "The model has a knife class, but knife-image validation has not been performed." : "The active detector has no knife class."),
        new(TemplateIds.Objects, "Selected objects", "Choose objects supported by the active YOLO model.", "yolo", yoloClasses.Count > 0, yoloClasses.Count > 0 ? "" : "Load a valid YOLO model first."),
        new(TemplateIds.Running, "People running", "Requires verified movement analysis across frames.", "motion", false, "Persistent motion tracking and running validation are not implemented."),
        new(TemplateIds.Screams, "Screams and calls for help", "Requires a local acoustic event classifier and audio capture.", "audio", false, "Audio capture and a validated acoustic model are not implemented. Florence-2 does not process audio."),
        new(TemplateIds.AfterHours, "After-hours movement", "Requires actual motion detection and camera schedules.", "motion", false, "There is no motion detector or schedule engine in this installation.")
    ];
}
public sealed class SmartMonitoringSettings
{
    public bool YoloEnabled { get; set; } = true;
    // Retained for compatibility with saved profiles; the application now enforces true.
    public bool FlorenceEnabled { get; set; } = true;
    public string FlorenceDirectory { get; set; } = "";
    public int FlorenceIntervalSeconds { get; set; } = 5;
    public int CpuThreads { get; set; } = 4;
    public int MaxTokens { get; set; } = 128;
    public bool BufferLiveCameras { get; set; } = true;
    public int RecordedWorkers { get; set; } = 2;
    public int RecordedBufferGb { get; set; } = 10;
    public bool AnalyzeEveryVideoFrame { get; set; } = true;
    public bool ConfirmGrounding { get; set; } = true;
    public bool FirearmsEnabled { get; set; }
    public bool ObjectsEnabled { get; set; }
    public List<string> SelectedClasses { get; set; } = [];
    public List<Guid> CameraIds { get; set; } = [];
    public bool VisualAlert { get; set; } = true;
    public bool SoundAlert { get; set; }
    public bool SaveEvidence { get; set; } = true;
    public bool NotifyEmail { get; set; } = true;
    public bool Includes(Guid camera) => CameraIds.Count == 0 || CameraIds.Contains(camera);
    public void Validate(IReadOnlyCollection<string> yoloClasses, bool florenceReady)
    {
        if (!FlorenceEnabled) throw new ArgumentException("Florence is required for smart monitoring.");
        if (RecordedWorkers is < 1 or > 8 || RecordedBufferGb is < 1 or > 1000) throw new ArgumentException(Texts.T("Choose 1–8 recorded analysis workers and a buffer of 1–1000 GB."));
        if (CpuThreads is < 1 or > 64) throw new ArgumentException(Texts.T("Choose between 1 and 64 Florence CPU threads."));
        if (FlorenceIntervalSeconds is < 2 or > 300 || MaxTokens is < 32 or > 256) throw new ArgumentException(Texts.T("Invalid Florence interval or token limit."));
        if (FirearmsEnabled && (!FlorenceEnabled || !florenceReady)) throw new ArgumentException(Texts.T("Load and enable Florence before selecting the firearm template."));
        if (ObjectsEnabled && (!YoloEnabled || SelectedClasses.Count == 0 || SelectedClasses.Any(x => !yoloClasses.Contains(x)))) throw new ArgumentException(Texts.T("Select at least one object supported by the enabled YOLO detector."));
    }
}
public sealed record VisualRegion(string Label, float X, float Y, float Width, float Height, float? Confidence = null);
public sealed record VisualResult(string DetectorId, string ModelVersion, string Task, string Description, IReadOnlyList<VisualRegion> Regions, double LatencyMs);
public sealed record DetectorDiagnostic(string State, string Checkpoint, string Provider, double LatencyMs, long DroppedFrames, string? Error = null, int CpuThreads = 4);
public interface IFlorenceDetector : IDisposable
{
    DetectorDiagnostic Diagnostic { get; }
    Task LoadAsync(string directory, int maxTokens, CancellationToken token);
    Task LoadAsync(string directory, int maxTokens, int cpuThreads, CancellationToken token) => LoadAsync(directory, maxTokens, token);
    Task<VisualResult> DetectAsync(byte[] image, bool grounding, string? prompt, CancellationToken token);
    Task UnloadAsync(CancellationToken token);
}
