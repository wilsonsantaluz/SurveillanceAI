using System;
using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using SurveillanceAI.Application.Localization;
using SurveillanceAI.Domain;

namespace SurveillanceAI.Desktop;

public sealed class EvidenceWindow : Window
{
    private readonly BitmapSource original;
    private readonly BitmapSource annotated;
    private readonly DetectionEvent item;
    private readonly Image image = new() { Stretch = Stretch.Fill };
    private readonly ScrollViewer viewport = new() { HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    private readonly TextBlock zoomLabel = new() { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8) };
    private readonly CheckBox showBox = new() { IsChecked = true };
    private double zoom = 1;
    private bool fit = true;

    public EvidenceWindow(BitmapSource original, DetectionEvent item)
    {
        this.original = original; this.item = item; annotated = EvidenceRendering.Annotate(original, item);
        Title = Texts.T("Event evidence"); WindowState = WindowState.Maximized;
        Background = (Brush)FindResource("BackgroundBrush"); Foreground = Brushes.White;
        Icon = new BitmapImage(new Uri("pack://application:,,,/Assets/App.ico"));
        var root = new DockPanel { Margin = new Thickness(16) };
        var toolbar = new WrapPanel { Margin = new Thickness(0, 0, 0, 8) }; DockPanel.SetDock(toolbar, Dock.Top);
        AddButton(toolbar, "Fit image", () => { fit = true; Fit(); }, "EvidenceFit");
        AddButton(toolbar, "−", () => SetZoom(zoom / 1.25), "EvidenceZoomOut");
        AddButton(toolbar, "+", () => SetZoom(zoom * 1.25), "EvidenceZoomIn");
        AddButton(toolbar, "100%", () => SetZoom(1), "EvidenceActualSize");
        AddButton(toolbar, "Detected region", FocusBox, "EvidenceRegion");
        showBox.Content = Texts.T("Show detection box"); AutomationProperties.SetAutomationId(showBox, "EvidenceShowBox");
        showBox.Checked += (_, _) => image.Source = annotated; showBox.Unchecked += (_, _) => image.Source = original;
        toolbar.Children.Add(showBox); toolbar.Children.Add(zoomLabel);
        AddButton(toolbar, "Save image…", SaveImage, "EvidenceSave");
        AddButton(toolbar, "Close", Close, "EvidenceClose");
        root.Children.Add(toolbar);
        var caption = new TextBlock { Text = $"{item.TimestampUtc:u} · {item.DetectorId} · {original.PixelWidth} × {original.PixelHeight}\n" + Texts.T("Yellow box: model hypothesis, requiring operator review. Ctrl + mouse wheel to zoom."), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(4, 0, 4, 10) };
        DockPanel.SetDock(caption, Dock.Top); root.Children.Add(caption);
        image.Source = annotated; image.HorizontalAlignment = HorizontalAlignment.Left; image.VerticalAlignment = VerticalAlignment.Top;
        viewport.Content = image; AutomationProperties.SetAutomationId(viewport, "EvidenceViewport"); root.Children.Add(viewport); Content = root;
        Loaded += (_, _) => Fit(); viewport.SizeChanged += (_, _) => { if (fit) Fit(); };
        viewport.PreviewMouseWheel += (_, e) => { if ((Keyboard.Modifiers & ModifierKeys.Control) == 0) return; SetZoom(zoom * (e.Delta > 0 ? 1.25 : .8)); e.Handled = true; };
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) { Close(); e.Handled = true; } };
    }
    private static void AddButton(Panel parent, string label, Action action, string id)
    {
        var button = new Button { Content = Texts.T(label) }; AutomationProperties.SetAutomationId(button, id);
        button.Click += (_, _) => action(); parent.Children.Add(button);
    }
    private void Fit()
    {
        if (viewport.ViewportWidth <= 0 || viewport.ViewportHeight <= 0) return;
        SetZoom(Math.Min(viewport.ViewportWidth / original.PixelWidth, viewport.ViewportHeight / original.PixelHeight), true);
    }
    private void SetZoom(double value, bool fitting = false)
    {
        fit = fitting; zoom = Math.Clamp(value, .05, 16);
        image.Width = original.PixelWidth * zoom; image.Height = original.PixelHeight * zoom;
        zoomLabel.Text = $"{zoom:P0}";
    }
    private void FocusBox()
    {
        var box = EvidenceRendering.Box(original, item); if (box.IsEmpty) return;
        SetZoom(Math.Min(viewport.ViewportWidth / Math.Max(32, box.Width * 2), viewport.ViewportHeight / Math.Max(32, box.Height * 2)));
        viewport.UpdateLayout();
        viewport.ScrollToHorizontalOffset((box.X + box.Width / 2) * zoom - viewport.ViewportWidth / 2);
        viewport.ScrollToVerticalOffset((box.Y + box.Height / 2) * zoom - viewport.ViewportHeight / 2);
    }
    private void SaveImage()
    {
        var dialog = new SaveFileDialog { Filter = "PNG|*.png", FileName = $"evidence-{item.Id:N}.png", AddExtension = true };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(showBox.IsChecked == true ? annotated : original));
            using var stream = File.Create(dialog.FileName); encoder.Save(stream);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        { MessageBox.Show(this, Texts.T("Could not save the image. Check the destination folder."), Title, MessageBoxButton.OK, MessageBoxImage.Error); }
    }
}
