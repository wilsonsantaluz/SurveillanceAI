using SurveillanceAI.Models;

namespace SurveillanceAI.Services;

public class EventAnalyzer
{
    public bool IsSuspicious(List<DetectionResult> detections)
    {
        var pessoas = detections.Where(d => d.Label == "person").ToList();

        if (pessoas.Count >= 2)
        {
            // regra simples inicial
            return true;
        }

        return false;
    }
}