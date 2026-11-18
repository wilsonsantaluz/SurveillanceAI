using SurveillanceAI.Application.Localization;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Florence2;
using Microsoft.ML.OnnxRuntime;
using SurveillanceAI.Application;

namespace SurveillanceAI.Monitoring;

public sealed class FlorenceBundle : IModelSource
{
    public const string Checkpoint = "microsoft/Florence-2-base-ft";
    public const string Revision = "e88a44eaf3791a35eae0c5a47b3dbcd36e67eb6f";
    private static readonly Dictionary<IModelSource.Model, (string Name, string Hash)> Models = new()
    {
        [IModelSource.Model.DecoderModelMerged] = ("decoder_model_merged_quantized.onnx", "f22f52f980c33df0efa15932c2f3db6d9d3595ce6387eca938b8cfe23dc4c641"),
        [IModelSource.Model.EmbedTokens] = ("embed_tokens_quantized.onnx", "6b2258db1c8ee9b160576ccde3cd3814d83a2edaed0dd1c6ca9ff3c38fa62214"),
        [IModelSource.Model.EncoderModel] = ("encoder_model_quantized.onnx", "f4ad7a68f1fb875d3bcf735ea14a7021b7ba7e83baf7cf10289881b4ed6d9b85"),
        [IModelSource.Model.VisionEncoder] = ("vision_encoder_quantized.onnx", "3b79d54f23f666f731549db23cb070c35a979ce19cbd9720e90e67a78dc9768c")
    };
    private readonly string directory;
    public FlorenceBundle(string directory, CancellationToken token = default)
    {
        this.directory = Path.GetFullPath(directory);
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "florence-manifest.json")));
        var root = manifest.RootElement;
        if (root.GetProperty("Checkpoint").GetString() != Checkpoint || root.GetProperty("Revision").GetString() != Revision || root.GetProperty("ExportRepository").GetString() != "onnx-community/Florence-2-base-ft" || root.GetProperty("SchemaVersion").GetInt32() != 1)
            throw new InvalidDataException(Texts.T("Use the verified Florence-2-base-ft export installed by scripts/Install-FlorenceModel.ps1; base is not base-ft."));
        foreach (var (_, file) in Models)
        {
            token.ThrowIfCancellationRequested();
            using var stream = File.OpenRead(Path.Combine(directory, file.Name));
            if (!Convert.ToHexString(SHA256.HashData(stream)).Equals(file.Hash, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException(Texts.Format("Florence ONNX checksum mismatch: {0}", file.Name));
        }
        foreach (var name in new[] { "vocab.json", "tokenizer_config.json", "config.json", "preprocessor_config.json" })
        {
            using var stream = File.OpenRead(Path.Combine(directory, name));
            if (!Convert.ToHexString(SHA256.HashData(stream)).Equals(root.GetProperty("Files").GetProperty(name).GetString(), StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException(Texts.Format("Florence configuration checksum mismatch: {0}", name));
        }
    }
    public bool TryGetModelPath(IModelSource.Model model, out string modelPath) { modelPath = Path.Combine(directory, Models[model].Name); return true; }
    public byte[] GetModelBytes(IModelSource.Model model) => throw new NotSupportedException("Models are loaded from verified local files only.");
}

public sealed class FlorenceDetector(string executionProvider = "CPU") : IFlorenceDetector
{
    private readonly SemaphoreSlim gate = new(1);
    private Florence2Model? model;
    private SessionOptions? options;
    private string? loadedDirectory;
    private int loadedCpuThreads;
    private DetectorDiagnostic diagnostic = new("Unloaded", FlorenceBundle.Checkpoint, executionProvider, 0, 0);
    public DetectorDiagnostic Diagnostic => Volatile.Read(ref diagnostic);

    public Task LoadAsync(string directory, int maxTokens, CancellationToken token) => LoadAsync(directory, maxTokens, 4, token);
    public async Task LoadAsync(string directory, int maxTokens, int cpuThreads, CancellationToken token)
    {
        if (cpuThreads is < 1 or > 64) throw new ArgumentException(Texts.T("Choose between 1 and 64 Florence CPU threads."));
        await gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            if (maxTokens is < 32 or > 256) throw new ArgumentOutOfRangeException(nameof(maxTokens));
            if (model is not null && loadedDirectory == Path.GetFullPath(directory) && loadedCpuThreads == cpuThreads && Diagnostic.State != "Error") { model.MaxLength = maxTokens; return; }
            Volatile.Write(ref diagnostic, Diagnostic with { State = "Loading", Error = null });
            await Task.Run(() =>
            {
                var bundle = new FlorenceBundle(directory, token);
                DisposeModel();
                options = new SessionOptions { IntraOpNumThreads = cpuThreads, InterOpNumThreads = 1, EnableCpuMemArena = false, EnableMemoryPattern = false };
                var available = OrtEnv.Instance().GetAvailableProviders();
                if (executionProvider == "CUDA" && available.Contains("CUDAExecutionProvider")) options.AppendExecutionProvider_CUDA(0);
                else if (executionProvider == "DirectML" && available.Contains("DmlExecutionProvider")) options.AppendExecutionProvider_DML(0);
                else if (executionProvider != "CPU") throw new NotSupportedException("The requested execution provider is not available in this ONNX Runtime build. Use CPU.");
                model = new Florence2Model(bundle, options, directory) { MaxLength = maxTokens };
                token.ThrowIfCancellationRequested(); loadedDirectory = Path.GetFullPath(directory); loadedCpuThreads = cpuThreads;
            }, token).ConfigureAwait(false);
            Volatile.Write(ref diagnostic, Diagnostic with { State = "Ready", CpuThreads = cpuThreads, Error = null });
        }
        catch (Exception error)
        {
            DisposeModel();
            Volatile.Write(ref diagnostic, Diagnostic with { State = "Error", Error = error.GetType().Name }); throw;
        }
        finally { gate.Release(); }
    }
    public async Task<VisualResult> DetectAsync(byte[] image, bool grounding, string? prompt, CancellationToken token)
    {
        if (image.Length > 16 * 1024 * 1024) throw new ArgumentException(Texts.T("Image exceeds the 16 MB inference limit."));
        if (grounding && (string.IsNullOrWhiteSpace(prompt) || prompt.Length > 256)) throw new ArgumentException(Texts.T("Grounding requires a prompt of 1-256 characters."));
        await gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            if (model is null) throw new InvalidOperationException(Texts.T("Load Florence before running inference."));
            return await Task.Run(() =>
            {
                var clock = Stopwatch.StartNew();
                using (var headerStream = new MemoryStream(image, false))
                using (var codec = SkiaSharp.SKCodec.Create(headerStream))
                    if (codec is null || (long)codec.Info.Width * codec.Info.Height > 20_000_000) throw new ArgumentException(Texts.T("Invalid image or image dimensions too large."));
                using var stream = new MemoryStream(image, false);
                var task = grounding ? TaskTypes.CAPTION_TO_PHRASE_GROUNDING : TaskTypes.OD;
                var output = model.Run(task, [stream], prompt ?? "", token).Single();
                token.ThrowIfCancellationRequested();
                var regions = (output.BoundingBoxes ?? []).SelectMany(group => (group.BBoxes ?? []).Select(box => new VisualRegion(group.Label, box.xmin, box.ymin, box.xmax - box.xmin, box.ymax - box.ymin)))
                    .Where(x => float.IsFinite(x.X) && float.IsFinite(x.Y) && float.IsFinite(x.Width) && float.IsFinite(x.Height) && x.Width > 1 && x.Height > 1).Take(128).ToList();
                Volatile.Write(ref diagnostic, Diagnostic with { State = "Ready", LatencyMs = clock.Elapsed.TotalMilliseconds, Error = null });
                return new VisualResult("florence2", FlorenceBundle.Checkpoint + "@" + FlorenceBundle.Revision[..7] + "/INT8", task.ToString(), output.PureText ?? "", regions, clock.Elapsed.TotalMilliseconds);
            }, token).ConfigureAwait(false);
        }
        catch (Exception) when (token.IsCancellationRequested) { throw new OperationCanceledException(token); }
        catch (Exception error) { Volatile.Write(ref diagnostic, Diagnostic with { State = "Error", Error = error.GetType().Name }); throw; }
        finally { gate.Release(); }
    }
    public async Task UnloadAsync(CancellationToken token)
    {
        await gate.WaitAsync(token).ConfigureAwait(false);
        try { DisposeModel(); Volatile.Write(ref diagnostic, Diagnostic with { State = "Unloaded" }); }
        finally { gate.Release(); }
    }
    private void DisposeModel() { model?.Dispose(); model = null; options?.Dispose(); options = null; loadedDirectory = null; }
    public void Dispose() { gate.Wait(); try { DisposeModel(); } finally { gate.Release(); } }
}
