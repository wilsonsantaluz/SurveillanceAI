namespace SurveillanceAI.Models;

public class DetectionResult
{
    public string Label { get; set; } = "";
    public float Confidence { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public float Width { get; set; }
    public float Height { get; set; }
    public float CenterX => X + Width / 2f;
    public float CenterY => Y + Height / 2f;
}
