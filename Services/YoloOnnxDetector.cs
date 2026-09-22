using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;
using System.Linq;
using SurveillanceAI.Models;
using System;

namespace SurveillanceAI.Services;

public class YoloOnnxDetector : IDisposable
{
    private readonly InferenceSession _session;
    private readonly string _inputName;

    private readonly int _inputWidth = 640;
    private readonly int _inputHeight = 640;
    private readonly float _confThreshold = 0.65f;
    private readonly float _iouThreshold = 0.45f;

    private readonly string[] _labels = new[]
    {
        "person","bicycle","car","motorcycle","airplane","bus","train","truck","boat","traffic light",
        "fire hydrant","stop sign","parking meter","bench","bird","cat","dog","horse","sheep","cow",
        "elephant","bear","zebra","giraffe","backpack","umbrella","handbag","tie","suitcase","frisbee",
        "skis","snowboard","sports ball","kite","baseball bat","baseball glove","skateboard","surfboard",
        "tennis racket","bottle","wine glass","cup","fork","knife","spoon","bowl","banana","apple",
        "sandwich","orange","broccoli","carrot","hot dog","pizza","donut","cake","chair","couch",
        "potted plant","bed","dining table","toilet","tv","laptop","mouse","remote","keyboard","cell phone",
        "microwave","oven","toaster","sink","refrigerator","book","clock","vase","scissors","teddy bear",
        "hair drier","toothbrush"
    };

    public YoloOnnxDetector(string modelPath)
    {
        var opts = new SessionOptions
        {
            GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL
        };

        _session = new InferenceSession(modelPath, opts);
        _inputName = _session.InputMetadata.Keys.First();

        Console.WriteLine($"[YOLO] Modelo carregado: {modelPath}");
        Console.WriteLine($"[YOLO] Input detectado: {_inputName}");

        foreach (var input in _session.InputMetadata)
            Console.WriteLine($"[YOLO] InputMeta: {input.Key}");

        foreach (var output in _session.OutputMetadata)
            Console.WriteLine($"[YOLO] OutputMeta: {output.Key}");
    }

    public List<DetectionResult> Detect(Mat image)
    {
        var (input, ratio, padX, padY) = Preprocess(image);

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor(_inputName, input)
        };

        using var results = _session.Run(inputs);
        var output = results.First().AsTensor<float>();

        var detections = ParseOutput(output, ratio, padX, padY, image.Width, image.Height);
        var finalDetections = Nms(detections);

      //  Console.WriteLine($"Detections before NMS: {detections.Count}");
      //  Console.WriteLine($"Detections after NMS: {finalDetections.Count}");

        return finalDetections;
    }

    private (DenseTensor<float> input, float ratio, float padX, float padY) Preprocess(Mat src)
    {
        int originalWidth = src.Width;
        int originalHeight = src.Height;

        float ratio = Math.Min((float)_inputWidth / originalWidth, (float)_inputHeight / originalHeight);

        int resizedWidth = (int)(originalWidth * ratio);
        int resizedHeight = (int)(originalHeight * ratio);

        using var resized = src.Resize(new Size(resizedWidth, resizedHeight));

        int dw = _inputWidth - resizedWidth;
        int dh = _inputHeight - resizedHeight;

        int top = dh / 2;
        int bottom = dh - top;
        int left = dw / 2;
        int right = dw - left;

        using var padded = new Mat();
        Cv2.CopyMakeBorder(
            resized,
            padded,
            top,
            bottom,
            left,
            right,
            BorderTypes.Constant,
            new Scalar(114, 114, 114));

        Cv2.CvtColor(padded, padded, ColorConversionCodes.BGR2RGB);

        var input = new DenseTensor<float>(new[] { 1, 3, _inputHeight, _inputWidth });

        for (int y = 0; y < _inputHeight; y++)
        {
            for (int x = 0; x < _inputWidth; x++)
            {
                Vec3b pixel = padded.At<Vec3b>(y, x);

                input[0, 0, y, x] = pixel.Item0 / 255f;
                input[0, 1, y, x] = pixel.Item1 / 255f;
                input[0, 2, y, x] = pixel.Item2 / 255f;
            }
        }

        return (input, ratio, left, top);
    }

    private List<DetectionResult> ParseOutput(
        Tensor<float> output,
        float ratio,
        float padX,
        float padY,
        int originalWidth,
        int originalHeight)
    {
        var detections = new List<DetectionResult>();

       

        int dim0 = output.Dimensions[0];
        int dim1 = output.Dimensions[1];
        int dim2 = output.Dimensions[2];

      //  Console.WriteLine($"dims: {dim0}, {dim1}, {dim2}");

        bool isYoloV8Standard = dim1 < dim2;
        if (!isYoloV8Standard)
        {
            Console.WriteLine("[YOLO] Formato não esperado. Este parser foi ajustado para saída [1, features, boxes].");
            return detections;
        }

        int features = dim1;
        int boxCount = dim2;
        int numClasses = features - 4;

        //Console.WriteLine($"features: {features}");
        //Console.WriteLine($"boxCount: {boxCount}");
        //Console.WriteLine($"numClasses: {numClasses}");

        if (numClasses <= 0)
        {
            Console.WriteLine("[YOLO] Número de classes inválido.");
            return detections;
        }

        for (int i = 0; i < boxCount; i++)
        {
            float centerX = output[0, 0, i];
            float centerY = output[0, 1, i];
            float width = output[0, 2, i];
            float height = output[0, 3, i];

            int classId = -1;
         
            float bestClassScore = 0f;

            for (int c = 0; c < numClasses; c++)
            {
                float raw = output[0, 4 + c, i];
                float score = Sigmoid(raw);

                if (score > bestClassScore)
                {
                    bestClassScore = score;
                    classId = c;
                }
            }

            float confidence = bestClassScore;

           

        

            if (confidence < _confThreshold)
                continue;

            if (classId < 0)
                continue;

            string label = classId < _labels.Length ? _labels[classId] : $"class_{classId}";

            float x1 = centerX - width / 2f;
            float y1 = centerY - height / 2f;

            x1 = (x1 - padX) / ratio;
            y1 = (y1 - padY) / ratio;
            width /= ratio;
            height /= ratio;

            x1 = Math.Max(0, x1);
            y1 = Math.Max(0, y1);

            if (x1 + width > originalWidth)
                width = originalWidth - x1;

            if (y1 + height > originalHeight)
                height = originalHeight - y1;

            if (width <= 1 || height <= 1)
                continue;

            detections.Add(new DetectionResult
            {
                Label = label,
                Confidence = confidence,
                X = x1,
                Y = y1,
                Width = width,
                Height = height
            });
        }

        return detections;
    }

    private List<DetectionResult> Nms(List<DetectionResult> detections)
    {
        var finalDetections = new List<DetectionResult>();

        var ordered = detections
            .OrderByDescending(d => d.Confidence)
            .ToList();

        while (ordered.Count > 0)
        {
            var best = ordered[0];
            finalDetections.Add(best);
            ordered.RemoveAt(0);

            ordered = ordered
                .Where(d => d.Label != best.Label || IoU(best, d) < _iouThreshold)
                .ToList();
        }

        return finalDetections;
    }

    private float IoU(DetectionResult a, DetectionResult b)
    {
        float x1 = Math.Max(a.X, b.X);
        float y1 = Math.Max(a.Y, b.Y);
        float x2 = Math.Min(a.X + a.Width, b.X + b.Width);
        float y2 = Math.Min(a.Y + a.Height, b.Y + b.Height);

        float intersection = Math.Max(0, x2 - x1) * Math.Max(0, y2 - y1);
        float union = (a.Width * a.Height) + (b.Width * b.Height) - intersection;

        return union <= 0 ? 0 : intersection / union;
    }

    private float Sigmoid(float x)
    {
        return 1f / (1f + MathF.Exp(-x));
    }

    public void Dispose()
    {
        _session.Dispose();
    }
}