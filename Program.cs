using OpenCvSharp;
using SurveillanceAI.Services;
using SurveillanceAI.Utils;
//O MODELO LOCAL todo parametrizaçao
using var detector = new YoloOnnxDetector(Path.Combine(AppContext.BaseDirectory, "onnxmodel/yolov8n.onnx"));
//USA A CAMERA LOCAL
var source = args.FirstOrDefault(x => !x.StartsWith("--"));
using var cap = source is not null ? new VideoCapture(source) : new VideoCapture(0);
//USAR CAMERAS REMOTAS USANDO PROTOCOLO rstsp  V2 VARIAS CAMERAS
//using var cap = new VideoCapture("rtsp://sua_camera");
using var frame = new Mat();
if (!cap.IsOpened()) { Logger.Error("Camera unavailable."); return; }

Console.ForegroundColor = ConsoleColor.Cyan;
Console.OutputEncoding = System.Text.Encoding.UTF8;
Console.WriteLine(@"
  ███████╗██╗   ██╗██████╗ ██╗   ██╗███████╗██╗██╗     ██╗      █████╗ ███╗   ██╗ ██████╗███████╗
  ██╔════╝██║   ██║██╔══██╗██║   ██║██╔════╝██║██║     ██║     ██╔══██╗████╗  ██║██╔════╝██╔════╝
  ███████╗██║   ██║██████╔╝██║   ██║█████╗  ██║██║     ██║     ███████║██╔██╗ ██║██║     █████╗  
  ╚════██║██║   ██║██╔══██╗╚██╗ ██╔╝██╔══╝  ██║██║     ██║     ██╔══██║██║╚██╗██║██║     ██╔══╝  
  ███████║╚██████╔╝██║  ██║ ╚████╔╝ ███████╗██║███████╗███████╗██║  ██║██║ ╚████║╚██████╗███████╗
  ╚══════╝ ╚═════╝ ╚═╝  ╚═╝  ╚═══╝  ╚══════╝╚═╝╚══════╝╚══════╝╚═╝  ╚═╝╚═╝  ╚═══╝ ╚═════╝╚══════╝

                🛡️  SurveillanceAI v1.0  by ɸWRL.null- ©2026/JLC
        Object detection preview - operator review required
");
Logger.Success("Sistema SurveillanceAI iniciado ...");

Console.ResetColor();

while (true)
{
    if (!cap.Read(frame) || frame.Empty())
        break;

    var detections = detector.Detect(frame);

    //  ANALISA COMPORTAMENTO
    // Experimental proximity analysis is not a validated security alert source.

    //  DESENHAR PESSOAS
    var persons = detections.Where(x => x.Label == "person").ToList();
    foreach (var d in persons)
    {
        int x = (int)d.X;
        int y = (int)d.Y;
        int w = (int)d.Width;
        int h = (int)d.Height;

        
        x = Math.Max(0, x);
        y = Math.Max(0, y);

        var rect = new Rect(x, y, w, h);

        // 🔥 cor baseada na confiança
        var color = d.Confidence > 0.6 ? Scalar.LimeGreen : Scalar.Orange;

        // retângulo
        Cv2.Rectangle(frame, rect, color, 2);

        // texto mais completo
        string label = $"Person {d.Confidence:0.00}";

        int baseline;
        var textSize = Cv2.GetTextSize(label, HersheyFonts.HersheySimplex, 0.5, 1, out baseline);

        // 🔲 fundo do texto (fica muito mais visível)
        var textBg = new Rect(
            x,
            y - textSize.Height - 5 < 0 ? y + 5 : y - textSize.Height - 5,
            textSize.Width,
            textSize.Height + 4
        );

        Cv2.Rectangle(frame, textBg, color, -1);

        // ✏️ texto
        var textPoint = new Point(
            textBg.X,
            textBg.Y + textSize.Height + 2
        );

        Cv2.PutText(frame, label, textPoint,
            HersheyFonts.HersheySimplex, 0.5, Scalar.Black, 1);

    }

    // LINHAS ENTRE PESSOAS (visual da distância)
    // Pixel distances are optional diagnostics, never a threat assessment.
    if (args.Contains("--proximity"))
    for (int i = 0; i < persons.Count; i++)
    {
        for (int j = i + 1; j < persons.Count; j++)
        {
            var p1 = persons[i];
            var p2 = persons[j];

            float dx = p1.CenterX - p2.CenterX;
            float dy = p1.CenterY - p2.CenterY;
            float dist = MathF.Sqrt(dx * dx + dy * dy);

            // 🔲 linha entre pessoas
            Cv2.Line(frame,
                new Point(p1.CenterX, p1.CenterY),
                new Point(p2.CenterX, p2.CenterY),
                Scalar.Yellow, 1);

            // 🧠 TEXTO DA DISTÂNCIA (AQUI)
            Cv2.PutText(frame, $"{dist:0}",
                new Point(
                    (int)((p1.CenterX + p2.CenterX) / 2),
                    (int)((p1.CenterY + p2.CenterY) / 2)
                ),
                HersheyFonts.HersheySimplex,
                0.5,
                Scalar.Yellow,
                1
            );

            // 🔴 opcional: alerta visual

        }
    }

    // 🔊 ALERTAS SIMPLES REMOVER DEPOIS
    if (detections.Any(d => d.Label == "cell phone"))
    {
        Logger.Info("Celular detectado");
    }

    if (detections.Any(d => d.Label == "scissors"))
    {
        
        Logger.Info("✂️ Objeto cortante detectado");
    }

    Cv2.ImShow("Detecção", frame);

    if (Cv2.WaitKey(1) == 27)
        break;
}
