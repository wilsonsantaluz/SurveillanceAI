using System.IO;
using SurveillanceAI.Application.Localization;
using SurveillanceAI.Desktop.Localization;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SurveillanceAI.Application;
using SurveillanceAI.Infrastructure;
using SurveillanceAI.Monitoring;

namespace SurveillanceAI.Desktop;
public partial class App : System.Windows.Application
{
    private IHost? host;
    private Mutex? instance;
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        Texts.Instance.SetLanguage("pt-BR");
        try
        {
            Environment.SetEnvironmentVariable("OPENCV_FFMPEG_LOGLEVEL", "-8");
            OpenCvSharp.Cv2.SetLogLevel(OpenCvSharp.LogLevel.SILENT);
            var root = Environment.GetEnvironmentVariable("SURVEILLANCEAI_DATA") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SurveillanceAI");
            var languagePreferences = new LanguagePreferences(root);
            Texts.Instance.SetLanguage(languagePreferences.Load());
            var instanceName = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(Path.GetFullPath(root).ToUpperInvariant())));
            instance = new Mutex(false, "Local\\SurveillanceAI-" + instanceName);
            if (!instance.WaitOne(0)) { MessageBox.Show(Texts.T("SurveillanceAI is already running for this data directory."), "SurveillanceAI"); instance.Dispose(); instance = null; Shutdown(); return; }
            Directory.CreateDirectory(root);
            var logPreferences = new LogPreferences();
            host = Host.CreateDefaultBuilder().ConfigureLogging(log => { log.ClearProviders(); log.AddProvider(new FileLogProvider(Path.Combine(root, "logs"), logPreferences)); }).ConfigureServices(services =>
            {
                services.AddDbContextFactory<AppDbContext>(options => options.UseSqlite(new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder { DataSource = Path.Combine(root, "surveillance.db"), ForeignKeys = true }.ToString()));
                services.Configure<HostOptions>(options => options.ShutdownTimeout = TimeSpan.FromSeconds(20));
                services.AddSingleton<Session>(); services.AddSingleton<ISecretProtector, DpapiProtector>();
                services.AddSingleton(logPreferences);
                services.AddSingleton(languagePreferences);
                services.AddSingleton<Authentication>(); services.AddSingleton<ProductStore>(); services.AddSingleton<NotificationAdmin>();
                services.AddSingleton<IEvidenceStore>(new EvidenceStore(Path.Combine(root, "evidence")));
                services.AddSingleton<IMailTransport, MailKitTransport>(); services.AddSingleton<EventWriter>();
                services.AddSingleton<IMonitoringStore>(sp => new MonitoringStore(sp.GetRequiredService<IDbContextFactory<AppDbContext>>(), sp.GetRequiredService<EventWriter>(), Path.Combine(AppContext.BaseDirectory, "models", "legacy")));
                services.AddSingleton<CameraManager>(); services.AddSingleton<IMonitoringControl>(sp => sp.GetRequiredService<CameraManager>());
                services.AddHostedService(sp => sp.GetRequiredService<CameraManager>()); services.AddHostedService<NotificationWorker>(); services.AddHostedService<RetentionWorker>();
                services.AddSingleton<MainViewModel>(); services.AddSingleton<MainWindow>();
            }).Build();
            await using (var db = await host.Services.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync())
            {
                await db.Database.MigrateAsync(); await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");
                var savedLevel = await db.Settings.Where(x => x.Key == "loggingLevel").Select(x => x.Value).SingleOrDefaultAsync();
                if (savedLevel is not null && Enum.TryParse<Microsoft.Extensions.Logging.LogLevel>(System.Text.Json.JsonSerializer.Deserialize<string>(savedLevel), out var level) && level >= Microsoft.Extensions.Logging.LogLevel.Information) logPreferences.MinimumLevel = level;
            }
            await host.StartAsync();
            host.Services.GetRequiredService<ILogger<App>>().LogInformation("Desktop started; monitoring ends when this process exits");
            var vm = host.Services.GetRequiredService<MainViewModel>(); await vm.InitializeAsync();
            MainWindow = host.Services.GetRequiredService<MainWindow>(); MainWindow.Show();
        }
        catch (Exception error) { MessageBox.Show(Texts.Format("Startup failed ({0}). Check local data access and installed dependencies.", error.GetType().Name), "SurveillanceAI"); Shutdown(1); }
    }
    public async Task StopAsync()
    {
        if (host is null) return;
        await host.StopAsync(); host.Dispose(); host = null;
    }
    protected override void OnExit(ExitEventArgs e) { instance?.ReleaseMutex(); instance?.Dispose(); base.OnExit(e); }
}
