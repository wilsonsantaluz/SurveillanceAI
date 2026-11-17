using SurveillanceAI.Application.Localization;
using SurveillanceAI.Desktop.Localization;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SurveillanceAI.Application;
using SurveillanceAI.Domain;
using SurveillanceAI.Infrastructure;
using SurveillanceAI.Monitoring;

namespace SurveillanceAI.Desktop;

public sealed record LanguageChoice(string Code, string Name);

public sealed partial class CameraTile : ObservableObject
{
    public Guid Id { get; init; }
    [ObservableProperty] private string name = "";
    [ObservableProperty] private string state = "";
    [ObservableProperty] private string metrics = "";
    [ObservableProperty] private BitmapImage? image;
}
public sealed partial class MainViewModel : ObservableObject
{
    private readonly Authentication auth;
    private readonly ProductStore store;
    private readonly Session session;
    private readonly CameraManager monitoring;
    private readonly ISecretProtector secrets;
    private readonly NotificationAdmin notifications;
    private readonly IEvidenceStore evidence;
    private readonly LogPreferences logPreferences;
    private readonly LanguagePreferences languagePreferences;
    private readonly DispatcherTimer timer;
    private DateTime refreshEventsAt = DateTime.MinValue;
    private EventCounts counts = new(0, 0, 0);
    private int eventPage;
    [ObservableProperty] private bool loggedIn;
    [ObservableProperty] private bool loginVisible = true;
    [ObservableProperty] private bool setup;
    [ObservableProperty] private string username = "";
    [ObservableProperty] private string message = "";
    [ObservableProperty] private string selectedLanguage = "pt-BR";
    [ObservableProperty] private string summary = Texts.T("No cameras configured");
    [ObservableProperty] private bool administrator;
    [ObservableProperty] private bool canReview;
    [ObservableProperty] private string roleDisplay = "";
    [ObservableProperty] private Camera? selectedCamera;
    [ObservableProperty] private Camera cameraDraft = new();
    [ObservableProperty] private DetectionEvent? selectedEvent;
    [ObservableProperty] private string notes = "";
    [ObservableProperty] private string search = "";
    [ObservableProperty] private Camera? filterCamera;
    [ObservableProperty] private string filterStatus = "All";
    [ObservableProperty] private string filterSeverity = "All";
    [ObservableProperty] private DateTime? fromDate;
    [ObservableProperty] private DateTime? toDate;
    [ObservableProperty] private string pageDisplay = Texts.T("Page 1");
    [ObservableProperty] private AlertRule? selectedRule;
    [ObservableProperty] private AlertRule ruleDraft = new();
    [ObservableProperty] private NotificationRecipient? selectedRecipient;
    [ObservableProperty] private NotificationRecipient recipientDraft = new();
    [ObservableProperty] private User? selectedUser;
    [ObservableProperty] private User userDraft = new();
    [ObservableProperty] private SmtpSettings smtp = new();
    [ObservableProperty] private string testAddress = "";
    [ObservableProperty] private string modelDirectory = Path.Combine(AppContext.BaseDirectory, "models", "legacy");
    [ObservableProperty] private string modelDetails = "";
    [ObservableProperty] private string loggingLevel = "Information";
    [ObservableProperty] private bool evidenceEnabled;
    [ObservableProperty] private int retentionDays = 30;
    [ObservableProperty] private bool previewPaused;
    [ObservableProperty] private bool overlays = true;
    [ObservableProperty] private bool proximityDiagnostic;
    [ObservableProperty] private BitmapImage? evidenceImage;
    [ObservableProperty] private CameraTile? fullscreenCamera;
    public ObservableCollection<Camera> Cameras { get; } = [];
    public ObservableCollection<CameraTile> Tiles { get; } = [];
    public ObservableCollection<DetectionEvent> Events { get; } = [];
    public ObservableCollection<DetectionEvent> RecentEvents { get; } = [];
    public ObservableCollection<AlertRule> Rules { get; } = [];
    public ObservableCollection<NotificationRecipient> Recipients { get; } = [];
    public ObservableCollection<NotificationDelivery> Deliveries { get; } = [];
    public ObservableCollection<User> Users { get; } = [];
    public ObservableCollection<AuditLog> Audit { get; } = [];
    public Array SourceKinds => Enum.GetValues<SourceKind>();
    public Array Roles => Enum.GetValues<UserRole>();
    public Array Severities => Enum.GetValues<Severity>();
    public string[] StatusFilters => ["All", "Pending", "Reviewed", "FalsePositive"];
    public string[] SeverityFilters => ["All", "Information", "Warning", "Critical"];
    public string[] TlsModes => ["StartTls", "SslOnConnect", "LocalTest"];
    public string[] LogLevels => ["Information", "Warning", "Error"];
    public string EvidenceDirectory => Path.GetDirectoryName(evidence.Resolve("location.marker"))!;
    public string EvidenceLocationText => Texts.Format("Evidence directory: {0}", EvidenceDirectory);
    public IReadOnlyList<LanguageChoice> Languages { get; } = [new("pt-BR", "Português (Brasil)"), new("en-US", "English")];
    public MainViewModel(Authentication auth, ProductStore store, Session session, CameraManager monitoring, ISecretProtector secrets, NotificationAdmin notifications, IEvidenceStore evidence, LogPreferences logPreferences, LanguagePreferences languagePreferences)
    {
        this.auth = auth; this.store = store; this.session = session; this.monitoring = monitoring; this.secrets = secrets; this.notifications = notifications; this.evidence = evidence;
        this.logPreferences = logPreferences;
        this.languagePreferences = languagePreferences;
        selectedLanguage = Texts.Instance.LanguageTag;
        timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) }; timer.Tick += async (_, _) =>
        {
            RefreshTiles();
            if (LoggedIn && DateTime.UtcNow >= refreshEventsAt)
            {
                refreshEventsAt = DateTime.UtcNow.AddSeconds(10);
                // Do not replace the selected event while an operator is writing review notes.
                await Run(async () => { await LoadDashboardAsync(); if (SelectedEvent is null) await LoadEventsAsync(); });
            }
        };
    }
    public async Task InitializeAsync() => Setup = await auth.NeedsSetupAsync();
    partial void OnSelectedLanguageChanged(string value)
    {
        try
        {
            languagePreferences.Save(value);
            Texts.Instance.SetLanguage(value);
            RoleDisplay = LoggedIn ? $"{Username} · {Texts.T(session.Role.ToString())}" : "";
            PageDisplay = Texts.Format("Page {0} · {1} results (100 per page)", eventPage + 1, Events.Count);
            ModelDetails = "";
            OnPropertyChanged(nameof(EvidenceLocationText));
            RefreshTiles();
            Message = Texts.T("Language saved.");
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
        {
            selectedLanguage = Texts.Instance.LanguageTag;
            OnPropertyChanged(nameof(SelectedLanguage));
            Message = Texts.Format("Operation failed ({0}). Check configuration and diagnostics.", error.GetType().Name);
        }
    }
    private async Task Run(Func<Task> action)
    {
        try { Message = ""; await action(); }
        catch (Exception error) { Message = error is ArgumentException or UnauthorizedAccessException or InvalidDataException or InvalidOperationException ? error.Message : Texts.Format("Operation failed ({0}). Check configuration and diagnostics.", error.GetType().Name); }
    }
    private static T Copy<T>(T item) => System.Text.Json.JsonSerializer.Deserialize<T>(System.Text.Json.JsonSerializer.Serialize(item))!;
    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> values) { target.Clear(); foreach (var value in values) target.Add(value); }
    [RelayCommand] private Task LoginAsync(PasswordBox box) => Run(async () =>
    {
        var password = box.Password; box.Clear();
        if (Setup) { await auth.SetupAsync(Username, password); Setup = false; }
        await auth.LoginAsync(Username, password); password = "";
        Administrator = session.Role == UserRole.Administrator; CanReview = session.Role <= UserRole.Operator;
        RoleDisplay = $"{Username} · {Texts.T(session.Role.ToString())}";
        LoggedIn = true; LoginVisible = false;
        await LoadAsync(); timer.Start();
    });
    [RelayCommand] private void Logout()
    {
        timer.Stop(); auth.Logout(); LoggedIn = false; LoginVisible = true; Administrator = false; CanReview = false;
        Tiles.Clear(); Events.Clear(); RecentEvents.Clear(); Cameras.Clear(); Rules.Clear(); Recipients.Clear(); Users.Clear(); Audit.Clear(); Deliveries.Clear();
        SelectedCamera = null; SelectedEvent = null; SelectedUser = null; SelectedRule = null; SelectedRecipient = null; FullscreenCamera = null;
        CameraDraft = new(); RuleDraft = new(); RecipientDraft = new(); UserDraft = new(); Smtp = new(); EvidenceImage = null; Message = "";
    }
    private async Task LoadAsync()
    {
        Replace(Cameras, await store.CamerasAsync()); Replace(Rules, await store.RulesAsync()); await LoadEventsAsync();
        await LoadDashboardAsync();
        if (Administrator)
        {
            Replace(Recipients, await store.RecipientsAsync()); Replace(Users, await auth.UsersAsync()); Replace(Deliveries, await store.DeliveriesAsync()); Replace(Audit, await store.AuditAsync());
            Smtp = await store.SettingAsync("smtp", new SmtpSettings());
            ModelDirectory = await store.SettingAsync("modelDirectory", ModelDirectory);
            EvidenceEnabled = await store.SettingAsync("evidenceEnabled", false); RetentionDays = await store.SettingAsync("retentionDays", 30);
            LoggingLevel = await store.SettingAsync("loggingLevel", "Information");
            if (LogLevels.Contains(LoggingLevel)) logPreferences.MinimumLevel = Enum.Parse<Microsoft.Extensions.Logging.LogLevel>(LoggingLevel);
        }
        RefreshTiles();
    }
    private async Task LoadDashboardAsync() { counts = await store.EventCountsAsync(); Replace(RecentEvents, (await store.EventsAsync(new EventFilter())).Take(5)); }
    [RelayCommand] private Task RefreshAsync() => Run(LoadAsync);
    partial void OnSelectedCameraChanged(Camera? value) { if (value is not null) CameraDraft = Copy(value); }
    partial void OnSelectedRuleChanged(AlertRule? value) { if (value is not null) RuleDraft = Copy(value); }
    partial void OnSelectedRecipientChanged(NotificationRecipient? value) { if (value is not null) RecipientDraft = Copy(value); }
    partial void OnSelectedUserChanged(User? value) { if (value is not null) UserDraft = Copy(value); }
    partial void OnSelectedEventChanged(DetectionEvent? value)
    {
        Notes = value?.Notes ?? ""; EvidenceImage = null;
        if (value?.EvidencePath is { } path) { try { EvidenceImage = Decode(File.ReadAllBytes(evidence.Resolve(path))); } catch { Message = Texts.T("Evidence is unavailable or has expired."); } }
    }
    [RelayCommand] private void NewCamera() { SelectedCamera = null; CameraDraft = new(); }
    [RelayCommand] private Task SaveCameraAsync(TextBox box) => Run(async () => { var source = box.Text; box.Clear(); await store.SaveCameraAsync(CameraDraft, source); await monitoring.ReloadAsync(); Replace(Cameras, await store.CamerasAsync()); Message = Texts.T("Camera saved and monitoring configuration applied."); });
    [RelayCommand] private Task TestCameraAsync() => Run(async () => { Message = await monitoring.TestAsync(CameraDraft) ? Texts.T("Connection succeeded; a frame was received.") : Texts.T("Connection failed. Save the source first, then check device availability."); });
    [RelayCommand] private Task RestartMonitoringAsync() => Run(async () => { await monitoring.ReloadAsync(); Message = Texts.T("Monitoring configuration applied."); });
    [RelayCommand] private void NewRule() { SelectedRule = null; RuleDraft = new(); }
    [RelayCommand] private Task SaveRuleAsync() => Run(async () => { await store.SaveRuleAsync(RuleDraft); Replace(Rules, await store.RulesAsync()); Message = Texts.T("Rule saved. Active workers refresh rules within five seconds."); });
    [RelayCommand] private void NewRecipient() { SelectedRecipient = null; RecipientDraft = new(); }
    [RelayCommand] private Task SaveRecipientAsync() => Run(async () => { await store.SaveRecipientAsync(RecipientDraft); Replace(Recipients, await store.RecipientsAsync()); Message = Texts.T("Recipient saved."); });
    [RelayCommand] private void NewUser() { SelectedUser = null; UserDraft = new(); }
    [RelayCommand] private Task SaveUserAsync(PasswordBox box) => Run(async () => { var password = box.Password; box.Clear(); await auth.SaveUserAsync(SelectedUser?.Id, UserDraft.Username, password, UserDraft.Role, UserDraft.Active); Replace(Users, await auth.UsersAsync()); Message = Texts.T("User saved. Role and activation changes take effect on the next login."); });
    [RelayCommand] private Task ChangePasswordAsync(object parameter) => Run(async () =>
    {
        if (parameter is not Panel panel) return;
        var boxes = panel.Children.OfType<PasswordBox>().ToArray(); if (boxes.Length != 2) return;
        var old = boxes[0].Password; var replacement = boxes[1].Password; foreach (var box in boxes) box.Clear();
        await auth.ChangePasswordAsync(old, replacement); Message = Texts.T("Password changed.");
    });
    private async Task LoadEventsAsync()
    {
        var status = Enum.TryParse<ReviewStatus>(FilterStatus, out var s) ? s : (ReviewStatus?)null;
        var severity = Enum.TryParse<Severity>(FilterSeverity, out var v) ? v : (Severity?)null;
        Replace(Events, await store.EventsAsync(new EventFilter(Search, FilterCamera?.Id, status, severity, FromDate?.ToUniversalTime(), ToDate?.Date.AddDays(1).AddTicks(-1).ToUniversalTime(), eventPage)));
        PageDisplay = Texts.Format("Page {0} · {1} results (100 per page)", eventPage + 1, Events.Count);
    }
    [RelayCommand] private Task SearchEventsAsync() => Run(async () => { eventPage = 0; await LoadEventsAsync(); });
    [RelayCommand] private Task NextPageAsync() => Run(async () => { eventPage++; await LoadEventsAsync(); });
    [RelayCommand] private Task PreviousPageAsync() => Run(async () => { eventPage = Math.Max(0, eventPage - 1); await LoadEventsAsync(); });
    [RelayCommand] private Task ClearFiltersAsync() => Run(async () => { Search = ""; FilterCamera = null; FilterStatus = "All"; FilterSeverity = "All"; FromDate = null; ToDate = null; eventPage = 0; await LoadEventsAsync(); });
    [RelayCommand] private Task ReviewAsync(string value) => Run(async () => { if (SelectedEvent is null) throw new ArgumentException(Texts.T("Select an event first.")); await store.ReviewAsync(SelectedEvent.Id, Enum.Parse<ReviewStatus>(value), Notes); await LoadEventsAsync(); Message = Texts.T("Event review saved."); });
    [RelayCommand] private Task SaveSmtpAsync(PasswordBox box) => Run(async () =>
    {
        session.Require(UserRole.Administrator);
        if (string.IsNullOrWhiteSpace(Smtp.Host) || Smtp.Port is < 1 or > 65535 || !System.Net.Mail.MailAddress.TryCreate(Smtp.Sender, out _) || Smtp.MaxAttempts is < 1 or > 10 || !TlsModes.Contains(Smtp.TlsMode)) throw new ArgumentException(Texts.T("Enter a valid SMTP host, port, sender, TLS mode and 1–10 attempts."));
        if (Smtp.TlsMode == "LocalTest" && Smtp.Host is not ("localhost" or "127.0.0.1" or "::1")) throw new ArgumentException(Texts.T("LocalTest permits only loopback SMTP."));
        var password = box.Password; box.Clear(); if (password.Length > 0) Smtp.ProtectedPassword = secrets.Protect(password);
        await store.SaveSettingAsync("smtp", Smtp); Message = Texts.T("SMTP settings saved.");
    });
    [RelayCommand] private Task TestEmailAsync() => Run(async () => { await notifications.QueueTestAsync(TestAddress); Message = Texts.T("Test queued using saved SMTP settings. Refresh delivery status after a few seconds."); });
    [RelayCommand] private Task TestSmtpConnectionAsync() => Run(async () =>
    {
        session.Require(UserRole.Administrator);
        var settings = await store.SettingAsync("smtp", new SmtpSettings());
        try { using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15)); await new MailKitTransport(secrets).TestConnectionAsync(settings, timeout.Token); Message = Texts.T("SMTP connection and authentication succeeded."); }
        catch (Exception error) { Message = Texts.Format("SMTP connection failed ({0}). Check saved configuration and connectivity.", error.GetType().Name); }
    });
    [RelayCommand] private Task ValidateModelAsync() => Run(() => Task.Run(() => { using var detector = new OnnxDetector(ModelDirectory); var text = Texts.Format("{0} · {1} classes · {2}×{3} · {4} · {5} · checksum verified", detector.Manifest.Version, detector.Manifest.Labels.Length, detector.Manifest.Width, detector.Manifest.Height, Texts.T(detector.Manifest.Layout), Texts.T(detector.Manifest.Scores)); System.Windows.Application.Current.Dispatcher.Invoke(() => ModelDetails = text); }));
    [RelayCommand] private Task SaveSystemAsync() => Run(async () =>
    {
        session.Require(UserRole.Administrator);
        if (RetentionDays is < 1 or > 3650) throw new ArgumentException(Texts.T("Retention must be 1–3650 days."));
        if (!LogLevels.Contains(LoggingLevel)) throw new ArgumentException(Texts.T("Select a supported logging level."));
        await Task.Run(() => { using var detector = new OnnxDetector(ModelDirectory); });
        await store.SaveSettingAsync("modelDirectory", Path.GetFullPath(ModelDirectory)); await store.SaveSettingAsync("evidenceEnabled", EvidenceEnabled); await store.SaveSettingAsync("retentionDays", RetentionDays);
        await store.SaveSettingAsync("loggingLevel", LoggingLevel); logPreferences.MinimumLevel = Enum.Parse<Microsoft.Extensions.Logging.LogLevel>(LoggingLevel);
        await monitoring.ReloadAsync(); Message = Texts.T("System settings saved and monitoring restarted.");
    });
    [RelayCommand] private void TogglePreview() => PreviewPaused = !PreviewPaused;
    [RelayCommand] private void OpenFullscreen(CameraTile tile) => FullscreenCamera = tile;
    [RelayCommand] private void CloseFullscreen() => FullscreenCamera = null;
    partial void OnOverlaysChanged(bool value) { if (LoggedIn) monitoring.SetOverlays(value, ProximityDiagnostic); }
    partial void OnProximityDiagnosticChanged(bool value) { if (LoggedIn) monitoring.SetOverlays(Overlays, value); }
    private void RefreshTiles()
    {
        if (!LoggedIn) return;
        var snapshots = monitoring.Snapshots;
        foreach (var old in Tiles.Where(x => snapshots.All(s => s.Id != x.Id)).ToList()) Tiles.Remove(old);
        foreach (var snapshot in snapshots)
        {
            var tile = Tiles.FirstOrDefault(x => x.Id == snapshot.Id);
            if (tile is null) { tile = new CameraTile { Id = snapshot.Id }; Tiles.Add(tile); }
            tile.Name = snapshot.Name; tile.State = Texts.T(snapshot.State);
            tile.Metrics = Texts.Format("{0}\nCapture {1:F1} fps · Inference {2:F1} fps · {3:F0} ms\nQueue {4}/1 · Dropped {5} · Reconnects {6}", snapshot.ModelVersion, snapshot.CaptureFps, snapshot.InferenceFps, snapshot.LatencyMs, snapshot.QueueDepth, snapshot.DroppedFrames, snapshot.Reconnects);
            if (!PreviewPaused) { try { tile.Image = snapshot.Jpeg is null ? null : Decode(snapshot.Jpeg); } catch { tile.Image = null; } }
        }
        Summary = Texts.Format("{0} cameras   /   {1} online   /   {2} offline or starting   /   {3} pending   /   {4} events today (UTC)   /   {5} retained   /   Memory {6} MB", Cameras.Count, snapshots.Count(x => x.State == "Online"), snapshots.Count(x => x.State != "Online"), counts.Pending, counts.Today, counts.Total, Environment.WorkingSet / 1048576);
    }
    private static BitmapImage Decode(byte[] data)
    {
        using var stream = new MemoryStream(data); var image = new BitmapImage(); image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad; image.StreamSource = stream; image.EndInit(); image.Freeze(); return image;
    }
}
