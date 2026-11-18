using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SurveillanceAI.Domain;

namespace SurveillanceAI.Desktop;

public static class EvidenceRendering
{
    public static Rect Box(BitmapSource image, DetectionEvent item)
    {
        if (!float.IsFinite(item.X) || !float.IsFinite(item.Y) || !float.IsFinite(item.Width) || !float.IsFinite(item.Height)) return Rect.Empty;
        var x = Math.Clamp(item.X, 0, image.PixelWidth); var y = Math.Clamp(item.Y, 0, image.PixelHeight);
        var width = Math.Clamp(item.Width, 0, image.PixelWidth - x); var height = Math.Clamp(item.Height, 0, image.PixelHeight - y);
        return width > 0 && height > 0 ? new Rect(x, y, width, height) : Rect.Empty;
    }

    // Annotate a separate rendering: the saved original remains unchanged.
    public static BitmapSource Annotate(BitmapSource original, DetectionEvent item)
    {
        var visual = new DrawingVisual();
        using (var drawing = visual.RenderOpen())
        {
            drawing.DrawImage(original, new Rect(0, 0, original.PixelWidth, original.PixelHeight));
            var box = Box(original, item);
            if (!box.IsEmpty)
            {
                var width = Math.Max(2, Math.Min(original.PixelWidth, original.PixelHeight) / 250d);
                drawing.DrawRectangle(null, new Pen(Brushes.Black, width + 2), box);
                drawing.DrawRectangle(null, new Pen(Brushes.Yellow, width), box);
            }
        }
        var rendered = new RenderTargetBitmap(original.PixelWidth, original.PixelHeight, 96, 96, PixelFormats.Pbgra32);
        rendered.Render(visual); rendered.Freeze(); return rendered;
    }
}
