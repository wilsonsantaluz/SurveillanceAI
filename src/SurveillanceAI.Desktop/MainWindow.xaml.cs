using System.ComponentModel;
using System.Windows;

namespace SurveillanceAI.Desktop;
public partial class MainWindow : Window
{
    private bool stopped;
    public MainWindow(MainViewModel viewModel) { InitializeComponent(); DataContext = viewModel; Closing += OnClosing; }
    private async void OnClosing(object? sender, CancelEventArgs e)
    {
        if (stopped) return;
        e.Cancel = true; IsEnabled = false;
        (DataContext as MainViewModel)?.PrepareForShutdown();
        try { await ((App)System.Windows.Application.Current).StopAsync(); }
        finally { stopped = true; Close(); }
    }
}
