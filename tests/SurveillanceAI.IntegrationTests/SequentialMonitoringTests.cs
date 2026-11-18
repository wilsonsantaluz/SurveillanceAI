using Microsoft.Extensions.Logging.Abstractions;
using OpenCvSharp;
using SurveillanceAI.Application;
using SurveillanceAI.Domain;
using SurveillanceAI.Infrastructure;
using SurveillanceAI.Monitoring;
using Xunit;

namespace SurveillanceAI.IntegrationTests;

public sealed class SequentialMonitoringTests
{
    [Fact] public async Task SequentialVideoProcessesAllFramesInOrderAndStopsAtEndWithoutDropsOrLooping()
    {
        await using var database = new TestDatabase(); await database.InitializeAsync();
        var store = new MonitoringStore(database, new EventWriter(database, new EvidenceStore(Path.Combine(database.Root, "evidence"))), "unused");
        using var detector = new FixtureDetector("person");
        var settings = new SmartMonitoringSettings { YoloEnabled = false };
        var camera = new Camera { Kind = SourceKind.VideoFile, InferenceFps = 30 };
        var florence = new FlorenceCameraWorker(camera.Id, settings, detector, store, NullLogger.Instance);
        var source = new ThreeFrames();
        var worker = new CameraWorker(camera, "fixture", () => source, "unused", store.RecordAsync, store.RulesAsync, NullLogger.Instance, settings,
            sequentialAnalysis: florence.ProcessFrameAsync);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await worker.RunOwnedAsync(timeout.Token);
        Assert.Equal(new[] { 20, 40, 60 }, detector.FrameValues);
        Assert.Equal(1, source.Opens); Assert.Equal(3, worker.Snapshot.VideoFrame);
        Assert.Equal(0, worker.Snapshot.DroppedFrames); Assert.Equal(0, florence.DroppedFrames);
        Assert.Equal(3, florence.Snapshot.Analyses); Assert.Equal("Video analysis complete", worker.Snapshot.State);
        Assert.NotNull(worker.Snapshot.Jpeg);
    }

    [Theory]
    [InlineData(true, "monitor", 0, 3)]
    [InlineData(true, "pistol", 1, 3)]
    [InlineData(false, "monitor", 1, 2)]
    public async Task GroundedRegionRequiresIndependentCropObjectDetectionWhenEnabled(bool confirm, string cropLabel, int events, int analyses)
    {
        await using var database = new TestDatabase(); await database.InitializeAsync();
        var session = new Session(); session.SignIn(Guid.NewGuid(), UserRole.Administrator);
        var product = new ProductStore(database, session, new DpapiProtector());
        var camera = new Camera { Name = "Confirmation fixture" }; await product.SaveCameraAsync(camera, "0");
        using var detector = new FixtureDetector(cropLabel, weapons: true);
        var settings = new SmartMonitoringSettings { FirearmsEnabled = true, ConfirmGrounding = confirm };
        await new SmartConfigStore(database, session, detector, _ => ["person"], "unused").SaveAsync(settings);
        var store = new MonitoringStore(database, new EventWriter(database, new EvidenceStore(Path.Combine(database.Root, "evidence"))), "unused");
        var worker = new FlorenceCameraWorker(camera.Id, settings, detector, store, NullLogger.Instance);
        using var frame = new Mat(480, 640, MatType.CV_8UC3, Scalar.White);
        await worker.ProcessFrameAsync(frame, default);
        Assert.Equal(events, (await product.EventsAsync(new EventFilter())).Count);
        Assert.Equal(analyses, worker.Snapshot.Analyses);
        if (confirm) Assert.Equal((50, 60), detector.LastInputSize);
        // Fixture outputs validate the gate and crop, not real-world firearm accuracy.
    }

    private sealed class ThreeFrames : IFrameSource
    {
        private int count;
        public int Opens { get; private set; }
        public double Fps => 30;
        public bool Open(Camera camera, string source) { Opens++; return true; }
        public bool Read(Mat frame)
        {
            if (++count > 3) return false;
            frame.Create(80, 100, MatType.CV_8UC3); frame.SetTo(Scalar.All(count * 20)); return true;
        }
        public void Dispose() { }
    }

    private sealed class FixtureDetector(string cropLabel, bool weapons = false) : IFlorenceDetector
    {
        private int calls;
        public List<int> FrameValues { get; } = [];
        public (int, int) LastInputSize { get; private set; }
        public DetectorDiagnostic Diagnostic => new("Ready", "fixture", "CPU", 1, 0);
        public Task LoadAsync(string directory, int maxTokens, CancellationToken token) => Task.CompletedTask;
        public async Task<VisualResult> DetectAsync(byte[] bytes, bool grounding, string? prompt, CancellationToken token)
        {
            await Task.Delay(30, token);
            using var input = Cv2.ImDecode(bytes, ImreadModes.Color);
            LastInputSize = (input.Width, input.Height); FrameValues.Add(input.At<Vec3b>(0, 0).Item0);
            calls++;
            IReadOnlyList<VisualRegion> regions = !weapons ? [new("person", 1, 1, 10, 10)] :
                grounding ? [new("pistol", 100, 100, 50, 60)] : calls == 1 ? [] : [new(cropLabel, 1, 1, 10, 10)];
            return new("florence2", "fixture", grounding ? "CAPTION_TO_PHRASE_GROUNDING" : "OD", "", regions, 30);
        }
        public Task UnloadAsync(CancellationToken token) => Task.CompletedTask;
        public void Dispose() { }
    }
}
