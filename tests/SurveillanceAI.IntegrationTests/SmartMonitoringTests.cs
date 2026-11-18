using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging.Abstractions;
using SurveillanceAI.Application;
using SurveillanceAI.Domain;
using SurveillanceAI.Infrastructure;
using SurveillanceAI.Monitoring;
using Xunit;

namespace SurveillanceAI.IntegrationTests;

public sealed class SmartMonitoringTests
{
    [Fact] public async Task FlorenceKeepsFullResolutionEvidenceAndMapsDetectionBoxToOriginalPixels()
    {
        await using var factory = new TestDatabase(); await factory.InitializeAsync();
        var session = new Session(); session.SignIn(Guid.NewGuid(), UserRole.Administrator);
        var product = new ProductStore(factory, session, new DpapiProtector());
        var camera = new Camera { Name = "Full resolution fixture" }; await product.SaveCameraAsync(camera, "0");
        using var detector = new RegionFlorence();
        var settings = new SmartMonitoringSettings { FirearmsEnabled = true, FlorenceDirectory = "test" };
        await new SmartConfigStore(factory, session, detector, _ => ["person"], "test").SaveAsync(settings);
        var evidence = new EvidenceStore(Path.Combine(factory.Root, "evidence"));
        var store = new MonitoringStore(factory, new EventWriter(factory, evidence), "test");
        var worker = new FlorenceCameraWorker(camera.Id, settings, detector, store, NullLogger.Instance);
        using var source = new OpenCvSharp.Mat(1440, 2560, OpenCvSharp.MatType.CV_8UC3, OpenCvSharp.Scalar.White);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        detector.FailNextLoad = true;
        worker.Offer(source);
        var running = Task.Run(() => worker.RunAsync(cancellation.Token));
        try
        {
            List<DetectionEvent> events;
            do { await Task.Delay(25, cancellation.Token); events = await product.EventsAsync(new EventFilter()); } while (events.Count == 0);
            var item = Assert.Single(events);
            Assert.True(detector.LoadFailures == 1 && worker.Snapshot.Analyses > 0);
            Assert.NotNull(worker.Snapshot.LastAnalysisUtc); Assert.Null(worker.Snapshot.Error);
            using var saved = OpenCvSharp.Cv2.ImRead(evidence.Resolve(item.EvidencePath!));
            Assert.Equal(2560, saved.Width); Assert.Equal(1440, saved.Height);
            Assert.Equal(1280, detector.InputWidth); Assert.Equal(720, detector.InputHeight);
            Assert.Equal(200, item.X); Assert.Equal(100, item.Y); Assert.Equal(160, item.Width); Assert.Equal(120, item.Height);
            Assert.Equal((byte)255, saved.At<OpenCvSharp.Vec3b>(100, 200).Item0); // Original evidence has no baked-in box.
        }
        finally { cancellation.Cancel(); await running.WaitAsync(TimeSpan.FromSeconds(5)); }
    }

    [Fact] public async Task LegacyDisabledFlorenceSettingsAreNormalizedAndEngineStartsAutomatically()
    {
        await using var factory = new TestDatabase(); await factory.InitializeAsync();
        var session = new Session(); session.SignIn(Guid.NewGuid(), UserRole.Administrator);
        var product = new ProductStore(factory, session, new DpapiProtector());
        await product.SaveSettingAsync("smartMonitoring", new SmartMonitoringSettings { FlorenceEnabled = false });
        using var detector = new StubFlorence();
        var configuration = new SmartConfigStore(factory, session, detector, _ => ["person"], "test");
        Assert.True((await configuration.GetAsync()).FlorenceEnabled);
        await configuration.SaveAsync(new SmartMonitoringSettings { FlorenceEnabled = false });
        var monitoring = new MonitoringStore(factory, new EventWriter(factory, new EvidenceStore(Path.Combine(factory.Root, "evidence"))), "test");
        Assert.True((await monitoring.SmartSettingsAsync(default)).FlorenceEnabled);
        using var startup = new FlorenceStartupService(monitoring, detector, NullLogger<FlorenceStartupService>.Instance);
        await startup.StartAsync(default);
        await startup.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(2, detector.LoadCalls);
        await startup.StopAsync(default);
    }

    [Fact] public async Task TemplatesPersistWithoutDeletingLegacyRulesAndRejectUnavailableCapabilities()
    {
        await using var factory = new TestDatabase(); await factory.InitializeAsync();
        var session = new Session(); session.SignIn(Guid.NewGuid(), UserRole.Administrator);
        var store = new ProductStore(factory, session, new DpapiProtector());
        var legacy = new AlertRule { Name = "Legacy", Enabled = true }; await store.SaveRuleAsync(legacy);
        using var florence = new StubFlorence();
        var smart = new SmartConfigStore(factory, session, florence, _ => ["person", "knife"], "test");
        await smart.SaveAsync(new SmartMonitoringSettings { ObjectsEnabled = true, CpuThreads = 8, SelectedClasses = ["person"] });
        Assert.True((await smart.GetAsync()).ObjectsEnabled);
        Assert.Equal(8, (await smart.GetAsync()).CpuThreads);
        Assert.Equal(8, florence.LastCpuThreads);
        Assert.Equal(4, System.Text.Json.JsonSerializer.Deserialize<SmartMonitoringSettings>("{}")!.CpuThreads);
        await Assert.ThrowsAsync<ArgumentException>(() => smart.SaveAsync(new SmartMonitoringSettings { CpuThreads = 0 }));
        await Assert.ThrowsAsync<ArgumentException>(() => smart.SaveAsync(new SmartMonitoringSettings { CpuThreads = 65 }));
        var rules = await store.RulesAsync(); Assert.Contains(rules, r => r.Id == legacy.Id && r.TemplateId == null && r.Enabled);
        Assert.Contains(rules, r => r.TemplateId == TemplateIds.Objects && r.Label == "person" && r.Enabled);
        await smart.SaveAsync(new SmartMonitoringSettings());
        Assert.False((await store.RulesAsync()).Single(r => r.TemplateId == TemplateIds.Objects).Enabled);
        await Assert.ThrowsAsync<ArgumentException>(() => smart.SaveAsync(new SmartMonitoringSettings { ObjectsEnabled = true, SelectedClasses = ["not-a-class"] }));
        session.SignIn(Guid.NewGuid(), UserRole.Viewer);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => smart.SaveAsync(new SmartMonitoringSettings()));
        Assert.All(TemplateCatalog.Get(true, ["person", "knife"]).Where(t => t.Id is TemplateIds.Running or TemplateIds.Screams or TemplateIds.AfterHours or TemplateIds.Knives), t => { Assert.False(t.Available); Assert.NotEmpty(t.Reason); });
    }

    [Fact] public async Task FirstWeaponRegionPersistsImmediatelyWithEvidenceAndDeduplicatesUntilManualClosure()
    {
        await using var factory = new TestDatabase(); await factory.InitializeAsync();
        var session = new Session(); session.SignIn(Guid.NewGuid(), UserRole.Administrator);
        var store = new ProductStore(factory, session, new DpapiProtector());
        var camera = new Camera { Name = "Counter" }; await store.SaveCameraAsync(camera, "0");
        using var detector = new StubFlorence();
        var profile = new SmartConfigStore(factory, session, detector, _ => ["person"], "test");
        var settings = new SmartMonitoringSettings { FlorenceEnabled = true, FirearmsEnabled = true, FlorenceDirectory = "test" };
        await profile.SaveAsync(settings);
        await store.SaveSettingAsync("smtp", new SmtpSettings { Enabled = true });
        await store.SaveRecipientAsync(new NotificationRecipient { Name = "Operator", Email = "operator@example.invalid", Enabled = true });
        var evidence = new EvidenceStore(Path.Combine(factory.Root, "evidence"));
        store = new ProductStore(factory, session, new DpapiProtector(), evidence);
        var writer = new EventWriter(factory, evidence); var monitoringStore = new MonitoringStore(factory, writer, "test");
        var worker = new FlorenceCameraWorker(camera.Id, settings, detector, monitoringStore, NullLogger.Instance);
        // Stub results test event semantics, NOT firearm recognition accuracy.
        var result = new VisualResult("florence2", "test-base-ft", "OD", "", [new VisualRegion("pistol", 10, 20, 30, 40)], 10);
        Assert.True(await worker.RecordWeaponsAsync(result with { Task = "CAPTION_TO_PHRASE_GROUNDING" }, [1, 2, 3], default, groundingConfirmed: true));
        var first = Assert.Single(await store.EventsAsync(new EventFilter()));
        Assert.Null(first.Confidence); Assert.True(first.EpisodeOpen); Assert.False(first.HumanConfirmed); Assert.Equal("florence2", first.DetectorId);
        Assert.NotNull(first.EvidencePath); Assert.Equal(new byte[] {1,2,3}, await File.ReadAllBytesAsync(evidence.Resolve(first.EvidencePath)));
        Assert.Single(await store.DeliveriesAsync());
        Assert.Single(await store.ActiveAlertsAsync());
        await worker.RecordWeaponsAsync(result, [9,9,9], default);
        Assert.Single(await store.EventsAsync(new EventFilter())); Assert.Single(await store.DeliveriesAsync());
        await store.ActOnEpisodeAsync(first.Id, "acknowledge", "Seen by operator");
        Assert.Empty(await store.ActiveAlertsAsync());
        await worker.RecordWeaponsAsync(result, [9], default); Assert.Single(await store.EventsAsync(new EventFilter()));
        await store.ActOnEpisodeAsync(first.Id, "confirm", "Human visual review");
        Assert.True((await store.EventsAsync(new EventFilter())).Single().HumanConfirmed);
        await store.ActOnEpisodeAsync(first.Id, "close", "Episode ended");
        await worker.RecordWeaponsAsync(result,[9],default,first.TimestampUtc); Assert.Single(await store.EventsAsync(new EventFilter()));
        await worker.RecordWeaponsAsync(result, [4,5,6], default);
        var closed = Assert.Single(await store.EventsAsync(new EventFilter())); Assert.False(closed.EpisodeOpen);
        var absent = result with { Regions = [] };
        await worker.RecordWeaponsAsync(absent, [0], default, first.TimestampUtc);
        await worker.RecordWeaponsAsync(result, [4], default); Assert.Single(await store.EventsAsync(new EventFilter()));
        await worker.RecordWeaponsAsync(absent, [0], default, observeAbsence: false);
        await worker.RecordWeaponsAsync(result with { Task = "CAPTION_TO_PHRASE_GROUNDING" }, [4], default, groundingConfirmed: true);
        Assert.Single(await store.EventsAsync(new EventFilter()));
        await worker.RecordWeaponsAsync(absent with { Task = "CAPTION_TO_PHRASE_GROUNDING" }, [0], default);
        await worker.RecordWeaponsAsync(result, [4,5,6], default); Assert.Equal(2, (await store.EventsAsync(new EventFilter())).Count);
        Assert.Equal(new byte[]{1,2,3},await File.ReadAllBytesAsync(evidence.Resolve(first.EvidencePath)));
        var second = (await store.EventsAsync(new EventFilter())).Single(x => x.Id != first.Id);
        session.SignIn(Guid.NewGuid(), UserRole.Viewer);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => store.DeleteEventAsync(second.Id));
        session.SignIn(Guid.NewGuid(), UserRole.Operator);
        await store.DeleteEventAsync(second.Id); await store.DeleteEventAsync(first.Id);
        Assert.Empty(await store.EventsAsync(new EventFilter()));
        session.SignIn(Guid.NewGuid(), UserRole.Administrator);
        Assert.Empty(await store.DeliveriesAsync());
        Assert.Empty(await store.ActiveAlertsAsync()); Assert.False(File.Exists(evidence.Resolve(first.EvidencePath)));
        Assert.False(File.Exists(evidence.Resolve(second.EvidencePath!)));
        // A fresh worker/store must respect suppression even after all incident rows are deleted.
        var restarted = new FlorenceCameraWorker(camera.Id, settings, detector,
            new MonitoringStore(factory, new EventWriter(factory, evidence), "test"), NullLogger.Instance);
        await restarted.RecordWeaponsAsync(result, [7], default); Assert.Empty(await store.EventsAsync(new EventFilter()));
        await restarted.RecordWeaponsAsync(absent, [0], default);
        await restarted.RecordWeaponsAsync(result, [8], default);
        var third = Assert.Single(await store.EventsAsync(new EventFilter()));
        await store.ReviewAsync(third.Id, ReviewStatus.FalsePositive, "Not a weapon");
        await restarted.RecordWeaponsAsync(result, [8], default); Assert.Single(await store.EventsAsync(new EventFilter()));
    }

    [Fact] public async Task UpgradePreservesExistingCameraRuleAndEvent()
    {
        await using var factory = new TestDatabase(); await using var db = factory.CreateDbContext();
        await db.GetService<IMigrator>().MigrateAsync("20261117143508_InitialDesktop");
        var camera = Guid.NewGuid(); var rule = Guid.NewGuid(); var incident = Guid.NewGuid();
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO Cameras (Id,Name,Description,Location,Kind,ProtectedSource,Enabled,InferenceFps) VALUES ({camera},'Existing','','',0,'',0,3)");
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO Rules (Id,Name,Label,CameraId,Enabled,Confidence,Confirmations,WindowSeconds,CooldownSeconds,AssociationIou,Severity) VALUES ({rule},'Existing','person',NULL,1,0.75,3,5,60,0.25,0)");
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO Events (Id,CameraId,TimestampUtc,Label,Confidence,X,Y,Width,Height,ModelVersion,RuleId,Severity,Status,Notes) VALUES ({incident},{camera},{DateTime.UtcNow},'person',0.9,1,2,3,4,'legacy',{rule},0,0,'existing note')");
        await db.Database.MigrateAsync();
        var existing = await db.Events.SingleAsync();
        Assert.Equal(incident, existing.Id); Assert.Equal("existing note", existing.Notes); Assert.Equal(.9f, existing.Confidence); Assert.Equal("yolo", existing.DetectorId); Assert.Null(existing.TemplateId);
        Assert.Single(await db.Cameras.ToListAsync()); Assert.True((await db.Rules.SingleAsync()).Enabled);
    }

    [Theory][InlineData("pistol",true)][InlineData("rifle",true)][InlineData("revolver on the floor",true)][InlineData("toy gun",true)][InlineData("cell phone",false)][InlineData("person",false)]
    public void WeaponHypothesisDoesNotRequirePeople(string label,bool expected) => Assert.Equal(expected,FlorenceCameraWorker.IsWeaponLabel(label));

    [Fact] public async Task MissingFlorenceFilesFailClearlyWithoutAffectingYolo()
    {
        using var detector=new FlorenceDetector();await using var folder=new TestDatabase();
        await Assert.ThrowsAsync<FileNotFoundException>(()=>detector.LoadAsync(folder.Root,128,default));Assert.Equal("Error",detector.Diagnostic.State);
        using var yolo=new OnnxDetector(Path.Combine(AppContext.BaseDirectory,"models","legacy"));
        using var blank=new OpenCvSharp.Mat(240,320,OpenCvSharp.MatType.CV_8UC3,OpenCvSharp.Scalar.Black);Assert.Empty(yolo.Detect(blank));
        await detector.UnloadAsync(default);Assert.Equal("Unloaded",detector.Diagnostic.State);
    }
    [Fact] public async Task IncorrectCheckpointOrOnnxHashIsRejectedBeforeInference()
    {
        await using var folder = new TestDatabase();
        var manifest = new { SchemaVersion=1, Checkpoint="microsoft/Florence-2-base", Revision=FlorenceBundle.Revision, ExportRepository="onnx-community/Florence-2-base-ft" };
        await File.WriteAllTextAsync(Path.Combine(folder.Root,"florence-manifest.json"),System.Text.Json.JsonSerializer.Serialize(manifest));
        Assert.Throws<InvalidDataException>(()=>new FlorenceBundle(folder.Root));
        var correct = manifest with { Checkpoint=FlorenceBundle.Checkpoint };
        await File.WriteAllTextAsync(Path.Combine(folder.Root,"florence-manifest.json"),System.Text.Json.JsonSerializer.Serialize(correct));
        await File.WriteAllBytesAsync(Path.Combine(folder.Root,"decoder_model_merged_quantized.onnx"),[1,2,3]);
        Assert.Throws<InvalidDataException>(()=>new FlorenceBundle(folder.Root));
    }

    [Fact] public async Task SlowFlorenceDoesNotStopYoloCaptureAndBothWorkersCancel()
    {
        await using var factory = new TestDatabase(); await factory.InitializeAsync();
        var store = new MonitoringStore(factory,new EventWriter(factory,new EvidenceStore(Path.Combine(factory.Root,"evidence"))),Path.Combine(AppContext.BaseDirectory,"models","legacy"));
        using var detector = new BlockingFlorence(); using var cancellation=new CancellationTokenSource();
        var settings=new SmartMonitoringSettings{FlorenceEnabled=true,FlorenceIntervalSeconds=2};var camera=new Camera{Name="Independent pipelines",InferenceFps=3};
        var additional=new FlorenceCameraWorker(camera.Id,settings,detector,store,NullLogger.Instance);
        var worker=new CameraWorker(camera,"0",()=>new BlackFrames(),Path.Combine(AppContext.BaseDirectory,"models","legacy"),store.RecordAsync,store.RulesAsync,NullLogger.Instance,settings,additional.Offer);
        var a=Task.Run(()=>additional.RunAsync(cancellation.Token));var b=Task.Run(()=>worker.RunOwnedAsync(cancellation.Token));
        try
        {
            await detector.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await Task.Delay(TimeSpan.FromSeconds(5));
            Assert.NotNull(worker.Snapshot.Jpeg);Assert.True(worker.Snapshot.InferenceFps>0);Assert.Equal("Online",worker.Snapshot.State);Assert.True(additional.DroppedFrames>0);
        }
        finally { cancellation.Cancel();await Task.WhenAll(a,b).WaitAsync(TimeSpan.FromSeconds(5)); }
        Assert.Equal("Stopped",worker.Snapshot.State);
    }
    private sealed class BlackFrames : IFrameSource
    {
        public double Fps=>30;
        public bool Open(Camera camera,string source)=>true;
        public bool Read(OpenCvSharp.Mat frame){Thread.Sleep(10);frame.Create(240,320,OpenCvSharp.MatType.CV_8UC3);frame.SetTo(OpenCvSharp.Scalar.Black);return true;}
        public void Dispose(){}
    }
    private sealed class BlockingFlorence : IFlorenceDetector
    {
        public TaskCompletionSource Started {get;}=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public DetectorDiagnostic Diagnostic=>new("Ready",FlorenceBundle.Checkpoint,"CPU",0,0);
        public Task LoadAsync(string directory,int maxTokens,CancellationToken token)=>Task.CompletedTask;
        public async Task<VisualResult> DetectAsync(byte[] image,bool grounding,string? prompt,CancellationToken token){Started.TrySetResult();await Task.Delay(Timeout.Infinite,token);throw new InvalidOperationException();}
        public Task UnloadAsync(CancellationToken token)=>Task.CompletedTask;
        public void Dispose(){}
    }
    private sealed class StubFlorence : IFlorenceDetector
    {
        public int LastCpuThreads { get; private set; } = 4;
        public Task LoadAsync(string directory, int maxTokens, int cpuThreads, CancellationToken token) { LastCpuThreads = cpuThreads; return LoadAsync(directory, maxTokens, token); }
        public int LoadCalls { get; private set; }
        public DetectorDiagnostic Diagnostic => new("Ready", FlorenceBundle.Checkpoint, "CPU", 0, 0);
        public Task LoadAsync(string directory,int maxTokens,CancellationToken token) { LoadCalls++; return Task.CompletedTask; }
        public Task<VisualResult> DetectAsync(byte[] image,bool grounding,string? prompt,CancellationToken token)=>throw new NotSupportedException();
        public Task UnloadAsync(CancellationToken token)=>Task.CompletedTask;
        public void Dispose() { }
    }
    private sealed class RegionFlorence : IFlorenceDetector
    {
        public bool FailNextLoad { get; set; }
        public int LoadFailures { get; private set; }
        public int InputWidth { get; private set; }
        public int InputHeight { get; private set; }
        public DetectorDiagnostic Diagnostic => new("Ready", "test", "CPU", 0, 0);
        public Task LoadAsync(string directory, int maxTokens, CancellationToken token)
        {
            if (FailNextLoad) { FailNextLoad = false; LoadFailures++; throw new IOException("Transient model fixture failure"); }
            return Task.CompletedTask;
        }
        public Task<VisualResult> DetectAsync(byte[] image, bool grounding, string? prompt, CancellationToken token)
        {
            using var decoded = OpenCvSharp.Cv2.ImDecode(image, OpenCvSharp.ImreadModes.Color);
            InputWidth = decoded.Width; InputHeight = decoded.Height;
            return Task.FromResult(new VisualResult("florence2", "test", "OD", "", [new VisualRegion("pistol", 100, 50, 80, 60)], 1));
        }
        public Task UnloadAsync(CancellationToken token) => Task.CompletedTask;
        public void Dispose() { }
    }
}

public sealed class FlorenceModelFactAttribute : FactAttribute
{
    public FlorenceModelFactAttribute() { if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SURVEILLANCEAI_FLORENCE_MODEL"))) Skip = "Install the local base-ft bundle and set SURVEILLANCEAI_FLORENCE_MODEL to run real CPU inference."; }
}
public sealed class FlorenceModelTests
{
    [FlorenceModelFact] public async Task RealBaseFtCpuLoadsDetectsGroundsCancelsAndUnloads()
    {
        using var detector = new FlorenceDetector(); using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        await detector.LoadAsync(Environment.GetEnvironmentVariable("SURVEILLANCEAI_FLORENCE_MODEL")!,128,6,timeout.Token);
        Assert.Equal(6, detector.Diagnostic.CpuThreads);
        Assert.Equal("CPU",detector.Diagnostic.Provider);Assert.Equal("Ready",detector.Diagnostic.State);
        var image=await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory,"Fixtures","bus.jpg"));
        var result=await detector.DetectAsync(image,false,null,timeout.Token);Assert.NotEmpty(result.Regions);Assert.All(result.Regions,r=>Assert.Null(r.Confidence));
        var grounding=await detector.DetectAsync(image,true,"bus",timeout.Token);Assert.Contains(grounding.Regions,r=>r.Label.Contains("bus",StringComparison.OrdinalIgnoreCase));
        await detector.LoadAsync(Environment.GetEnvironmentVariable("SURVEILLANCEAI_FLORENCE_MODEL")!, 128, 8, timeout.Token);
        Assert.Equal(8, detector.Diagnostic.CpuThreads);
        // Exercise the camera frame queue and worker with the actual local model, not only image-test inference.
        await using (var database = new TestDatabase())
        {
            await database.InitializeAsync();
            var store = new MonitoringStore(database, new EventWriter(database, new EvidenceStore(Path.Combine(database.Root, "evidence"))), "test");
            var settings = new SmartMonitoringSettings { CpuThreads = 8, FlorenceDirectory = Environment.GetEnvironmentVariable("SURVEILLANCEAI_FLORENCE_MODEL")! };
            var worker = new FlorenceCameraWorker(Guid.NewGuid(), settings, detector, store, NullLogger.Instance);
            using var cameraFrame = OpenCvSharp.Cv2.ImDecode(image, OpenCvSharp.ImreadModes.Color);
            using var stop = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token);
            worker.Offer(cameraFrame);
            var task = Task.Run(() => worker.RunAsync(stop.Token));
            try
            {
                while (worker.Snapshot.Analyses == 0) await Task.Delay(50, stop.Token);
                Assert.NotNull(worker.Snapshot.LastAnalysisUtc); Assert.True(worker.Snapshot.LatencyMs > 0); Assert.Null(worker.Snapshot.Error);
            }
            finally { stop.Cancel(); await task.WaitAsync(TimeSpan.FromSeconds(10)); }
        }
        using var cancelled=new CancellationTokenSource();cancelled.Cancel();await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>detector.DetectAsync(image,false,null,cancelled.Token));
        using var runningCancel=new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>detector.DetectAsync(image,false,null,runningCancel.Token).WaitAsync(TimeSpan.FromSeconds(10)));
        await detector.UnloadAsync(default);Assert.Equal("Unloaded",detector.Diagnostic.State);await Assert.ThrowsAsync<InvalidOperationException>(()=>detector.DetectAsync(image,false,null,default));
    }
}
