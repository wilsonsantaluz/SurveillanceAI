using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;
using SurveillanceAI.Application;
using SurveillanceAI.Domain;
using SurveillanceAI.Monitoring;
using Xunit;

namespace SurveillanceAI.IntegrationTests;
public sealed class MonitoringTests
{
    private static string Bundle => Path.Combine(AppContext.BaseDirectory,"models","legacy");
    [Fact] public async Task SampleImageProducesDetectionsAndCanBePlayedAsVideo()
    {
        using var detector = new OnnxDetector(Bundle);
        using var image = Cv2.ImRead(Path.Combine(AppContext.BaseDirectory,"Fixtures","bus.jpg"));
        var detections = detector.Detect(image);
        Assert.Contains(detections, x => x.Label == "person" && x.Confidence > .65f);
        Assert.Contains(detections, x => x.Label == "bus");
        var path=Path.Combine(Path.GetTempPath(),Guid.NewGuid()+".avi");
        try
        {
            using(var writer=new VideoWriter(path,FourCC.MJPG,10,new Size(image.Width,image.Height)))
            { Assert.True(writer.IsOpened()); for(var i=0;i<30;i++)writer.Write(image); }
            using var cancellation=new CancellationTokenSource(TimeSpan.FromSeconds(20));
            var confirmed = new TaskCompletionSource<DetectionEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
            var camera = new Camera { Name="Public sample playback", Kind=SourceKind.VideoFile, InferenceFps=5 };
            var rule=new AlertRule { Name="Test person", Enabled=true, Confirmations=2 };
            var worker=new CameraWorker(camera,path,()=>new OpenCvFrameSource(),Bundle,(item,_,_)=>{confirmed.TrySetResult(item);return Task.CompletedTask;},_=>Task.FromResult<IReadOnlyList<AlertRule>>([rule]),NullLogger.Instance);
            var task=Task.Run(()=>worker.RunOwnedAsync(cancellation.Token));
            try { var item=await confirmed.Task.WaitAsync(cancellation.Token);Assert.Equal("person",item.Label);Assert.Equal(rule.Id,item.RuleId);Assert.NotNull(worker.Snapshot.Jpeg); }
            finally { cancellation.Cancel();await task.WaitAsync(TimeSpan.FromSeconds(5)); }
        }
        finally { File.Delete(path); }
    }
    [Fact] public void RealBundledModelRunsAndProbabilityScoresAreNotSigmoidedTwice()
    {
        using var detector=new OnnxDetector(Bundle);
        using var blank=new Mat(480,640,MatType.CV_8UC3,Scalar.Black);
        Assert.Empty(detector.Detect(blank));
        var tensor=new DenseTensor<float>(new[]{1,84,1}); tensor[0,0,0]=320;tensor[0,1,0]=320;tensor[0,2,0]=100;tensor[0,3,0]=100;tensor[0,4,0]=.9f;
        var result=Assert.Single(detector.Parse(tensor,1,0,0,640,640));Assert.Equal("person",result.Label);Assert.Equal(.9f,result.Confidence);Assert.Equal(270,result.X);
        tensor[0,4,0]=.6f;Assert.Empty(detector.Parse(tensor,1,0,0,640,640));
        tensor[0,4,0]=1.5f;Assert.Throws<InvalidDataException>(()=>detector.Parse(tensor,1,0,0,640,640));
        Assert.Throws<InvalidDataException>(()=>detector.Parse(new DenseTensor<float>(new[]{1,85,1}),1,0,0,640,640));
    }
    [Fact] public void BundleChecksumTamperingAndPathTraversalAreRejected()
    {
        var root=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try
        {
            var manifest=ModelManifest.Load(Bundle).Manifest;manifest.Sha256=new string('0',64);
            File.Copy(Path.Combine(Bundle,"yolov8n.onnx"),Path.Combine(root,"yolov8n.onnx"));File.WriteAllText(Path.Combine(root,"manifest.json"),JsonSerializer.Serialize(manifest));
            Assert.Throws<InvalidDataException>(()=>ModelManifest.Load(root));manifest.ModelFile="../yolov8n.onnx";Assert.Throws<InvalidDataException>(manifest.Validate);
            manifest.ModelFile="yolov8n.onnx";manifest.Scores="guess";Assert.Throws<InvalidDataException>(manifest.Validate);
        }
        finally { Directory.Delete(root,true); }
    }
    [Fact] public async Task WorkerDropsStaleFramesAndDisposesSourceOnCancellation()
    {
        var fake=new FakeSource();var camera=new Camera{Name="Fake source",InferenceFps=2};using var cancellation=new CancellationTokenSource();
        var worker=new CameraWorker(camera,"0",()=>fake,Bundle,(_,_,_)=>Task.CompletedTask,_=>Task.FromResult<IReadOnlyList<AlertRule>>([]),NullLogger.Instance);
        var task=Task.Run(()=>worker.RunOwnedAsync(cancellation.Token));
        var deadline=DateTime.UtcNow.AddSeconds(15);
        while(worker.Snapshot.Jpeg is null && DateTime.UtcNow<deadline) await Task.Delay(50);
        Assert.NotNull(worker.Snapshot.Jpeg);Assert.True(worker.Snapshot.DroppedFrames>0);Assert.InRange(worker.Snapshot.QueueDepth,0,1);
        cancellation.Cancel();await task.WaitAsync(TimeSpan.FromSeconds(5));Assert.True(fake.Disposed);Assert.Equal("Stopped",worker.Snapshot.State);
    }
    [Fact] public async Task WorkerReconnectsAndStopsDuringBackoff()
    {
        var sources=new System.Collections.Concurrent.ConcurrentBag<FakeSource>();using var cancellation=new CancellationTokenSource();
        var worker=new CameraWorker(new Camera{Name="Disconnected"},"0",()=>{var source=new FakeSource{FailOpen=true};sources.Add(source);return source;},Bundle,(_,_,_)=>Task.CompletedTask,_=>Task.FromResult<IReadOnlyList<AlertRule>>([]),NullLogger.Instance);
        var task=Task.Run(()=>worker.RunOwnedAsync(cancellation.Token));var deadline=DateTime.UtcNow.AddSeconds(10);
        while(worker.Snapshot.Reconnects<2 && DateTime.UtcNow<deadline)await Task.Delay(50);
        Assert.True(worker.Snapshot.Reconnects>=2);cancellation.Cancel();await task.WaitAsync(TimeSpan.FromSeconds(5));Assert.All(sources,x=>Assert.True(x.Disposed));
    }
    [Fact] public async Task PrerecordedVideoPlaybackWorksWithoutCameraHardware()
    {
        var path=Path.Combine(Path.GetTempPath(),Guid.NewGuid()+".avi");
        try
        {
            using(var writer=new VideoWriter(path,FourCC.MJPG,10,new Size(320,240)))
            {
                Assert.True(writer.IsOpened());using var image=new Mat(240,320,MatType.CV_8UC3,new Scalar(32,48,64));for(var i=0;i<20;i++)writer.Write(image);
            }
            using var source=new OpenCvFrameSource();Assert.True(source.Open(new Camera{Kind=SourceKind.VideoFile},path));using var frame=new Mat();Assert.True(source.Read(frame));Assert.Equal(320,frame.Width);
        }
        finally { File.Delete(path); }
    }
    private sealed class FakeSource : IFrameSource
    {
        public bool FailOpen; public bool Disposed; public double Fps=>30;
        public bool Open(Camera camera,string source)=>!FailOpen;
        public bool Read(Mat frame){Thread.Sleep(5);frame.Create(240,320,MatType.CV_8UC3);frame.SetTo(Scalar.Black);return true;}
        public void Dispose()=>Disposed=true;
    }
}
