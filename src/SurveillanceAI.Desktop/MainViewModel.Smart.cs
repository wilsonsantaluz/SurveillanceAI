using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using OpenCvSharp;
using SurveillanceAI.Application;
using SurveillanceAI.Application.Localization;
using SurveillanceAI.Domain;
using SurveillanceAI.Infrastructure;
using SurveillanceAI.Monitoring;

namespace SurveillanceAI.Desktop;

public sealed partial class TemplateOption(AlertTemplate template) : ObservableObject
{
    public AlertTemplate Template { get; } = template;
    public string Name => Texts.T(Template.Name);
    public string Description => Texts.T(Template.Description);
    public string Reason => Texts.T(Template.Reason);
    [ObservableProperty, NotifyPropertyChangedFor(nameof(CanToggle))] private bool enabled;
    public bool CanToggle => Template.Available || Enabled;
    public void Translate() => OnPropertyChanged("");
}
public sealed partial class SelectionOption(string code, string name, bool localized = false) : ObservableObject
{
    public string Code { get; } = code;
    public string Name => localized ? Texts.T(name) : name;
    [ObservableProperty] private bool selected;
    public void Translate() => OnPropertyChanged(nameof(Name));
}
public sealed record ImageDetectionRow(string Detector, string Description, float? Confidence, string Region);

public sealed partial class MainViewModel
{
    private readonly SmartConfigStore smartStore;
    private readonly IFlorenceDetector florence;
    private CancellationTokenSource? imageCancellation;
    private readonly HashSet<Guid> soundedAlerts = [];
    private string lastDetectorState = "";
    [ObservableProperty] private SmartMonitoringSettings smartSettings = new();
    [ObservableProperty] private bool allCameras = true;
    [ObservableProperty] private string detectorStatus = "";
    [ObservableProperty] private string detectorSummary = "";
    [ObservableProperty] private string imagePath = "";
    [ObservableProperty] private bool testYolo = true;
    [ObservableProperty] private bool testFlorence = true;
    [ObservableProperty] private bool testGrounding;
    [ObservableProperty] private string testPhrase = "handgun";
    [ObservableProperty] private System.Windows.Media.Imaging.BitmapImage? testImage;
    [ObservableProperty] private string imageTestSummary = "";
    [ObservableProperty] private bool detectorBusy;
    [ObservableProperty] private bool alertVisible;
    [ObservableProperty] private string alertSummary = "";
    [ObservableProperty] private bool mandatoryModelPending = true;
    [ObservableProperty] private string mandatoryModelMessage = Texts.T("Starting required Florence analysis...");
    public bool DetectorIdle => !DetectorBusy;
    public bool SpecificCameras => !AllCameras;
    public string EpisodeDescription => SelectedEvent is null ? "" : Texts.Format("Detector: {0} | {1} | {2}", SelectedEvent.DetectorId, Texts.T(SelectedEvent.EpisodeOpen ? "Open episode" : "Closed episode"), Texts.T(SelectedEvent.HumanConfirmed ? "Human confirmed" : "Not confirmed by an operator"));
    public ObservableCollection<TemplateOption> Templates { get; } = [];
    public ObservableCollection<SelectionOption> ObjectChoices { get; } = [];
    public ObservableCollection<SelectionOption> CameraChoices { get; } = [];
    public ObservableCollection<ImageDetectionRow> ImageDetections { get; } = [];
    partial void OnDetectorBusyChanged(bool value) => OnPropertyChanged(nameof(DetectorIdle));
    partial void OnAllCamerasChanged(bool value) => OnPropertyChanged(nameof(SpecificCameras));

    private async Task InitializeSmartAsync()
    {
        SmartSettings = await smartStore.GetAsync();
        if (string.IsNullOrEmpty(SmartSettings.FlorenceDirectory)) SmartSettings.FlorenceDirectory = Path.Combine(Environment.GetEnvironmentVariable("SURVEILLANCEAI_DATA") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SurveillanceAI"), "models", "Florence-2-base-ft");
        Replace(ObjectChoices, (await smartStore.LabelsAsync()).Select(label => new SelectionOption(label, "Object: " + label, true) { Selected = SmartSettings.SelectedClasses.Contains(label) }));
        Replace(CameraChoices, Cameras.Select(camera => new SelectionOption(camera.Id.ToString(), camera.Name) { Selected = SmartSettings.CameraIds.Contains(camera.Id) }));
        AllCameras = SmartSettings.CameraIds.Count == 0;
        BuildTemplates();
    }
    private void BuildTemplates()
    {
        var states = Templates.ToDictionary(x => x.Template.Id, x => x.Enabled);
        Replace(Templates, TemplateCatalog.Get(florence.Diagnostic.State == "Ready", ObjectChoices.Select(x => x.Code).ToList()).Select(template => new TemplateOption(template)
        {
            Enabled = states.TryGetValue(template.Id, out var selected) ? selected : template.Id == TemplateIds.Firearms ? SmartSettings.FirearmsEnabled : template.Id == TemplateIds.Objects && SmartSettings.ObjectsEnabled
        }));
    }
    private void RefreshSmartTranslations()
    {
        OnPropertyChanged(nameof(EpisodeDescription));
        foreach (var template in Templates) template.Translate();
        foreach (var choice in ObjectChoices) choice.Translate();
        RefreshDetectorDiagnostic();
    }
    private void RefreshDetectorDiagnostic()
    {
        var state = florence.Diagnostic;
        MandatoryModelPending = state.State != "Ready";
        MandatoryModelMessage = state.State == "Loading" ? Texts.T("Starting required Florence analysis...") : Texts.T("Florence is required and is not ready. Open Alerts to configure the local model.");
        DetectorSummary = Texts.Format("Additional vision: {0}", Texts.T(state.State));
        if (state.State != lastDetectorState)
        {
            lastDetectorState = state.State;
            if (Administrator && ObjectChoices.Count > 0) BuildTemplates();
        }
        DetectorStatus = Texts.Format("Florence: {0} | {1} | {2} | {3:F0} ms | dropped: {4} | {5}", Texts.T(state.State), state.Checkpoint, state.Provider, state.LatencyMs, monitoring.FlorenceDroppedFrames, state.Error ?? "") + "\n" + Texts.Format("Active CPU threads: {0}", state.CpuThreads);
    }
    [RelayCommand] private Task SaveSmartAsync() => Run(async () =>
    {
        SmartSettings.FirearmsEnabled = Templates.First(x => x.Template.Id == TemplateIds.Firearms).Enabled;
        SmartSettings.ObjectsEnabled = Templates.First(x => x.Template.Id == TemplateIds.Objects).Enabled;
        SmartSettings.SelectedClasses = ObjectChoices.Where(x => x.Selected).Select(x => x.Code).ToList();
        SmartSettings.CameraIds = AllCameras ? [] : CameraChoices.Where(x => x.Selected).Select(x => Guid.Parse(x.Code)).ToList();
        if (!AllCameras && SmartSettings.CameraIds.Count == 0) throw new ArgumentException(Texts.T("Select at least one camera or use all cameras."));
        await smartStore.SaveAsync(SmartSettings); await monitoring.ReloadAsync(); Replace(Rules, (await store.RulesAsync()).Where(r => r.TemplateId == null));
        Message = Texts.T("Monitoring templates saved."); BuildTemplates();
    });
    [RelayCommand] private void BrowseFlorence()
    {
        var dialog = new OpenFolderDialog();
        if (dialog.ShowDialog() == true) { SmartSettings.FlorenceDirectory = dialog.FolderName; OnPropertyChanged(nameof(SmartSettings)); }
    }
    [RelayCommand] private Task LoadFlorenceAsync() => Run(async () =>
    {
        session.Require(UserRole.Administrator);
        if (DetectorBusy) return;
        DetectorBusy = true; imageCancellation = new CancellationTokenSource();
        try { await florence.LoadAsync(SmartSettings.FlorenceDirectory, SmartSettings.MaxTokens, SmartSettings.CpuThreads, imageCancellation.Token); SmartSettings.FlorenceEnabled = true; OnPropertyChanged(nameof(SmartSettings)); BuildTemplates(); RefreshDetectorDiagnostic(); Message = Texts.T("Florence loaded locally on CPU."); }
        finally { imageCancellation.Dispose(); imageCancellation = null; DetectorBusy = false; }
    });
    [RelayCommand] private void BrowseTestImage()
    {
        var dialog = new OpenFileDialog { Filter = Texts.T("Choose image") + "|*.jpg;*.jpeg;*.png;*.bmp" };
        if (dialog.ShowDialog() == true) ImagePath = dialog.FileName;
    }
    [RelayCommand] private void CancelImageTest() => imageCancellation?.Cancel();
    [RelayCommand] private Task RunImageTestAsync() => Run(async () =>
    {
        session.Require(UserRole.Administrator);
        if (DetectorBusy) return;
        TestFlorence = true;
        if (!TestYolo && !TestFlorence) throw new ArgumentException(Texts.T("Choose at least one detector."));
        if (!File.Exists(ImagePath) || new FileInfo(ImagePath).Length > 16 * 1024 * 1024) throw new ArgumentException(Texts.T("Choose an image of at most 16 MB."));
        DetectorBusy = true; imageCancellation = new CancellationTokenSource(); ImageDetections.Clear();
        try
        {
            var token = imageCancellation.Token;
            var results = await Task.Run(async () =>
            {
                using (var headerStream = File.OpenRead(ImagePath))
                using (var codec = SkiaSharp.SKCodec.Create(headerStream))
                    if (codec is null || (long)codec.Info.Width * codec.Info.Height > 20_000_000) throw new ArgumentException(Texts.T("Invalid image or image dimensions too large."));
                using var frame = Cv2.ImRead(ImagePath);
                if (frame.Empty() || (long)frame.Width * frame.Height > 20_000_000) throw new ArgumentException(Texts.T("Invalid image or image dimensions too large."));
                var list = new List<VisualResult>();
                if (TestYolo)
                {
                    using var yolo = new OnnxDetector(ModelDirectory); token.ThrowIfCancellationRequested(); var clock = Stopwatch.StartNew();
                    var detections = yolo.Detect(frame);
                    list.Add(new VisualResult("yolo", yolo.Manifest.Version, "OD", "", detections.Select(d => new VisualRegion(d.Label,d.X,d.Y,d.Width,d.Height,d.Confidence)).ToList(), clock.Elapsed.TotalMilliseconds));
                }
                if (TestFlorence)
                {
                    await florence.LoadAsync(SmartSettings.FlorenceDirectory, SmartSettings.MaxTokens, SmartSettings.CpuThreads, token);
                    Cv2.ImEncode(".jpg", frame, out var image);
                    list.Add(await florence.DetectAsync(image, TestGrounding, TestGrounding ? TestPhrase : null, token));
                }
                token.ThrowIfCancellationRequested();
                foreach (var result in list) foreach (var region in result.Regions)
                {
                    var x = Math.Clamp((int)region.X, 0, frame.Width-1); var y = Math.Clamp((int)region.Y,0,frame.Height-1);
                    Cv2.Rectangle(frame,new Rect(x,y,Math.Clamp((int)region.Width,1,frame.Width-x),Math.Clamp((int)region.Height,1,frame.Height-y)),result.DetectorId == "yolo" ? Scalar.Cyan : Scalar.Magenta,2);
                    Cv2.PutText(frame,result.DetectorId+": "+region.Label,new Point(x,Math.Max(16,y)),HersheyFonts.HersheySimplex,.5,Scalar.Cyan);
                }
                Cv2.ImEncode(".jpg",frame,out var preview);
                return (Results:list,Image:preview);
            }, token);
            TestImage = Decode(results.Image);
            foreach (var result in results.Results) foreach(var region in result.Regions) ImageDetections.Add(new ImageDetectionRow(result.DetectorId+" / "+result.ModelVersion,region.Label,region.Confidence,$"{region.X:F0}, {region.Y:F0}, {region.Width:F0}, {region.Height:F0}"));
            ImageTestSummary = string.Join(" | ",results.Results.Select(result => $"{result.DetectorId} / {result.Task}: {result.LatencyMs:F0} ms ({result.Regions.Count})"));
            BuildTemplates();
        }
        finally { imageCancellation.Dispose(); imageCancellation = null; DetectorBusy = false; RefreshDetectorDiagnostic(); }
    });
    [RelayCommand] private Task EpisodeActionAsync(string action) => Run(async () =>
    {
        if (SelectedEvent is null) throw new ArgumentException(Texts.T("Select an event first."));
        await store.ActOnEpisodeAsync(SelectedEvent.Id, action, Notes); await LoadEventsAsync(); await RefreshActiveAlertsAsync();
        Message = Texts.T("Operator action recorded.");
    });
    [ObservableProperty] private System.Windows.Media.Imaging.BitmapSource? evidencePreview;
    [RelayCommand] private Task OpenEvidenceAsync() => Run(() =>
    {
        session.Require();
        if (SelectedEvent is null || EvidenceImage is null) throw new ArgumentException(Texts.T("Evidence is unavailable or has expired."));
        var viewer = new EvidenceWindow(EvidenceImage, SelectedEvent) { Owner = System.Windows.Application.Current.MainWindow };
        viewer.ShowDialog();
        return Task.CompletedTask;
    });
    [RelayCommand] private Task DeleteEventAsync() => Run(async () =>
    {
        if (SelectedEvent is null) throw new ArgumentException(Texts.T("Select an event first."));
        if (System.Windows.MessageBox.Show(Texts.T("Delete this event, its evidence and notification history? This cannot be undone."), Texts.T("Delete event"), System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning, System.Windows.MessageBoxResult.No) != System.Windows.MessageBoxResult.Yes) return;
        await store.DeleteEventAsync(SelectedEvent.Id);
        await LoadEventsAsync(); await RefreshActiveAlertsAsync(); await LoadDashboardAsync();
        Message = Texts.T("Event deleted.");
    });
    private async Task RefreshActiveAlertsAsync()
    {
        var active = await store.ActiveAlertsAsync();
        AlertVisible = active.Any(x => x.VisualAlert);
        AlertSummary = Texts.Format("{0} potential detection(s) awaiting acknowledgement. Review Events.", active.Count(x => x.VisualAlert));
        soundedAlerts.RemoveWhere(id => active.All(x => x.Id != id));
        foreach (var item in active.Where(x => x.SoundAlert)) if (soundedAlerts.Add(item.Id)) System.Media.SystemSounds.Exclamation.Play();
    }
    private void ClearSmartSession()
    {
        imageCancellation?.Cancel(); Templates.Clear(); ObjectChoices.Clear(); CameraChoices.Clear();
        ImageDetections.Clear(); TestImage = null; ImagePath = ""; ImageTestSummary = ""; AlertVisible = false; soundedAlerts.Clear();
    }
    public void PrepareForShutdown() { timer.Stop(); imageCancellation?.Cancel(); }
}
