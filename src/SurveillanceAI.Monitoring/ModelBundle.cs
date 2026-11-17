using SurveillanceAI.Application.Localization;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.ML.OnnxRuntime;

namespace SurveillanceAI.Monitoring;

public sealed class ModelManifest
{
    public int SchemaVersion { get; set; } = 1;
    public string ModelFile { get; set; } = "";
    public string Sha256 { get; set; } = "";
    public string Version { get; set; } = "";
    public string[] Labels { get; set; } = [];
    public string InputName { get; set; } = "images";
    public string OutputName { get; set; } = "output0";
    public int Width { get; set; } = 640;
    public int Height { get; set; } = 640;
    public string Layout { get; set; } = "featuresFirst";
    public string Scores { get; set; } = "probabilities";
    public string Preprocessing { get; set; } = "rgb-letterbox-114-div255";
    public float Confidence { get; set; } = .65f;
    public float Iou { get; set; } = .45f;
    public string ClassMapVersion { get; set; } = "";
    public void Validate()
    {
        if (SchemaVersion != 1 || string.IsNullOrWhiteSpace(Version) || string.IsNullOrWhiteSpace(ClassMapVersion)) throw new InvalidDataException(Texts.T("Unsupported detector manifest or missing version. Classifier manifests are not detection bundles."));
        if (string.IsNullOrWhiteSpace(ModelFile) || Path.GetFileName(ModelFile) != ModelFile || ModelFile.Contains(':') || !ModelFile.EndsWith(".onnx", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException(Texts.T("Model file must be a plain ONNX filename."));
        if (Sha256 is null || Sha256.Length != 64 || !Sha256.All(Uri.IsHexDigit)) throw new InvalidDataException(Texts.T("Invalid model SHA-256."));
        if (Labels is null || Labels.Length == 0 || Labels.Any(string.IsNullOrWhiteSpace) || Labels.Distinct().Count() != Labels.Length) throw new InvalidDataException(Texts.T("Class mapping must be unique and nonempty."));
        if (Width is < 32 or > 2048 || Height is < 32 or > 2048 || Width % 32 != 0 || Height % 32 != 0 || Preprocessing != "rgb-letterbox-114-div255") throw new InvalidDataException(Texts.T("Unsupported preprocessing or image dimensions."));
        if (Layout is not ("featuresFirst" or "boxesFirst") || Scores is not ("probabilities" or "logits") || !float.IsFinite(Confidence) || Confidence is < 0 or > 1 || !float.IsFinite(Iou) || Iou is <= 0 or > 1 || string.IsNullOrWhiteSpace(InputName) || string.IsNullOrWhiteSpace(OutputName)) throw new InvalidDataException(Texts.T("Unsupported tensor layout, score semantics, names or thresholds."));
    }
    public static (ModelManifest Manifest, string Path) Load(string directory)
    {
        var manifest = JsonSerializer.Deserialize<ModelManifest>(File.ReadAllText(Path.Combine(directory, "manifest.json")), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? throw new InvalidDataException(Texts.T("Empty manifest."));
        manifest.Validate();
        var path = Path.Combine(Path.GetFullPath(directory), manifest.ModelFile);
        using var file = File.OpenRead(path);
        if (!Convert.ToHexString(SHA256.HashData(file)).Equals(manifest.Sha256, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException(Texts.T("Model checksum does not match manifest."));
        return (manifest, path);
    }
    public void ValidateSession(InferenceSession session)
    {
        if (session.InputMetadata.Count != 1 || session.OutputMetadata.Count != 1 || !session.InputMetadata.TryGetValue(InputName, out var input) || !session.OutputMetadata.TryGetValue(OutputName, out var output)) throw new InvalidDataException(Texts.T("Model tensor names/count do not match the detection contract."));
        var shape = input.Dimensions;
        if (input.ElementType != typeof(float) || shape.Length != 4 || shape[0] > 1 || shape[1] != 3 || (shape[2] > 0 && shape[2] != Height) || (shape[3] > 0 && shape[3] != Width)) throw new InvalidDataException(Texts.T("Expected float32 NCHW RGB input, batch 1 or dynamic."));
        var dims = output.Dimensions;
        var featureAxis = Layout == "featuresFirst" ? 1 : 2;
        if (output.ElementType != typeof(float) || dims.Length != 3 || dims[0] > 1 || dims[featureAxis] != Labels.Length + 4) throw new InvalidDataException(Texts.T("Expected decoded YOLO cx/cy/w/h plus class scores. Objectness and end-to-end NMS exports are unsupported."));
    }
}
