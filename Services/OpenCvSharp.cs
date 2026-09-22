using OpenCvSharp;

namespace SurveillanceAI.Services;

public class CameraService
{
    private readonly string _rtspUrl;

    public CameraService(string rtspUrl)
    {
        _rtspUrl = rtspUrl;
    }

    public VideoCapture Start()
    {
        var capture = new VideoCapture(_rtspUrl);

        if (!capture.IsOpened())
            throw new Exception("Erro ao abrir câmera");

        return capture;
    }
}