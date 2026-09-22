using SurveillanceAI.Models;
using SurveillanceAI.Utils;

namespace SurveillanceAI.Services;

public class ProximityAnalyzer
{
    private List<(float x, float y)> _lastCenters = new();

    public void Analyze(List<DetectionResult> detections)
    {
        var persons = detections
            .Where(d => d.Label == "person")
            .ToList();

        var currentCenters = persons
            .Select(p => (p.CenterX, p.CenterY))
            .ToList();

        // comparar pares
        for (int i = 0; i < persons.Count; i++)
        {
            for (int j = i + 1; j < persons.Count; j++)
            {
                float dist = Distance(persons[i], persons[j]);

                // 🔴 distância pequena = interação
                //MELHORAR PARA PARAMETROS
                if (dist < 250) // ajustar conforme câmera
                {
                   
                    Logger.Warn($"⚠️ Pessoas próximas: {dist:0}");

                    // se antes estavam longe → aproximação suspeita
                    if (_lastCenters.Count == currentCenters.Count)
                    {
                        float lastDist = Distance(
                            new DetectionResult { X = _lastCenters[i].x, Y = _lastCenters[i].y },
                            new DetectionResult { X = _lastCenters[j].x, Y = _lastCenters[j].y }
                        );

                        if (lastDist - dist > 50) // aproximou rápido
                        {
                            Logger.Error("🚨 POSSÍVEL ABORDAGEM!");
                        }
                    }
                }
            }
        }

        _lastCenters = currentCenters;
    }

    private float Distance(DetectionResult a, DetectionResult b)
    {
        float dx = a.CenterX - b.CenterX;
        float dy = a.CenterY - b.CenterY;
        return MathF.Sqrt(dx * dx + dy * dy);
    }
}