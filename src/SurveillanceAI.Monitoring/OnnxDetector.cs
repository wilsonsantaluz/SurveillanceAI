using SurveillanceAI.Application.Localization;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;
using SurveillanceAI.Domain;

namespace SurveillanceAI.Monitoring;

// Preserves the existing RGB letterbox, coordinate mapping and class-aware NMS pipeline.
public sealed class OnnxDetector : IDisposable
{
    private readonly InferenceSession session;
    public ModelManifest Manifest { get; }
    public OnnxDetector(string bundleDirectory)
    {
        var bundle = ModelManifest.Load(bundleDirectory); Manifest = bundle.Manifest;
        using var options = new SessionOptions { GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL, IntraOpNumThreads = 2 };
        session = new InferenceSession(bundle.Path, options);
        try { Manifest.ValidateSession(session); } catch { session.Dispose(); throw; }
    }
    public IReadOnlyList<Detection> Detect(Mat image)
    {
        if (image.Empty() || image.Channels() != 3) throw new InvalidDataException(Texts.T("Expected nonempty BGR frame."));
        var ratio = Math.Min((float)Manifest.Width / image.Width, (float)Manifest.Height / image.Height);
        var width = Math.Max(1, (int)(image.Width * ratio)); var height = Math.Max(1, (int)(image.Height * ratio));
        using var resized = image.Resize(new Size(width, height));
        var left = (Manifest.Width - width) / 2; var top = (Manifest.Height - height) / 2;
        using var padded = new Mat();
        Cv2.CopyMakeBorder(resized, padded, top, Manifest.Height - height - top, left, Manifest.Width - width - left, BorderTypes.Constant, new Scalar(114, 114, 114));
        Cv2.CvtColor(padded, padded, ColorConversionCodes.BGR2RGB);
        var input = new DenseTensor<float>(new[] { 1, 3, Manifest.Height, Manifest.Width });
        for (var y = 0; y < Manifest.Height; y++) for (var x = 0; x < Manifest.Width; x++)
        {
            var pixel = padded.At<Vec3b>(y, x);
            input[0, 0, y, x] = pixel.Item0 / 255f; input[0, 1, y, x] = pixel.Item1 / 255f; input[0, 2, y, x] = pixel.Item2 / 255f;
        }
        using var output = session.Run([NamedOnnxValue.CreateFromTensor(Manifest.InputName, input)]);
        return Parse(output.Single().AsTensor<float>(), ratio, left, top, image.Width, image.Height);
    }
    public IReadOnlyList<Detection> Parse(Tensor<float> tensor, float ratio, int padX, int padY, int imageWidth, int imageHeight)
    {
        var first = Manifest.Layout == "featuresFirst";
        if (tensor.Dimensions.Length != 3 || tensor.Dimensions[0] != 1 || tensor.Dimensions[first ? 1 : 2] != Manifest.Labels.Length + 4) throw new InvalidDataException(Texts.T("Runtime tensor shape does not match manifest."));
        var count = tensor.Dimensions[first ? 2 : 1];
        float At(int feature, int box) => first ? tensor[0, feature, box] : tensor[0, box, feature];
        var detections = new List<Detection>();
        for (var i = 0; i < count; i++)
        {
            var label = -1; var score = 0f;
            for (var c = 0; c < Manifest.Labels.Length; c++)
            {
                var raw = At(4 + c, i);
                if (!float.IsFinite(raw)) throw new InvalidDataException(Texts.T("Nonfinite class score."));
                if (Manifest.Scores == "probabilities" && raw is < 0 or > 1) throw new InvalidDataException(Texts.T("Class scores contradict probability semantics."));
                var confidence = Manifest.Scores == "logits" ? 1 / (1 + MathF.Exp(-raw)) : raw;
                if (confidence > score) { score = confidence; label = c; }
            }
            if (label < 0 || score < Manifest.Confidence) continue;
            var cx = At(0, i); var cy = At(1, i); var w = At(2, i); var h = At(3, i);
            if (!float.IsFinite(cx) || !float.IsFinite(cy) || !float.IsFinite(w) || !float.IsFinite(h) || w <= 0 || h <= 0) continue;
            var x1 = Math.Clamp((cx - w / 2 - padX) / ratio, 0, imageWidth); var y1 = Math.Clamp((cy - h / 2 - padY) / ratio, 0, imageHeight);
            var x2 = Math.Clamp((cx + w / 2 - padX) / ratio, 0, imageWidth); var y2 = Math.Clamp((cy + h / 2 - padY) / ratio, 0, imageHeight);
            if (x2 - x1 > 1 && y2 - y1 > 1) detections.Add(new Detection(Manifest.Labels[label], score, x1, y1, x2 - x1, y2 - y1));
        }
        var ordered = detections.OrderByDescending(x => x.Confidence).ToList(); var result = new List<Detection>();
        while (ordered.Count > 0) { var best = ordered[0]; result.Add(best); ordered.RemoveAll(x => ReferenceEquals(x, best) || (x.Label == best.Label && best.Iou(x) >= Manifest.Iou)); }
        return result;
    }
    public void Dispose() => session.Dispose();
}
