using Microsoft.EntityFrameworkCore;
using SurveillanceAI.Application;
using SurveillanceAI.Domain;

namespace SurveillanceAI.Infrastructure;
public sealed class MonitoringStore(IDbContextFactory<AppDbContext> factory, EventWriter writer, string defaultModelDirectory, Func<string, string>? resolveFlorence = null) : IMonitoringStore
{
    public async Task<IReadOnlyList<Camera>> EnabledCamerasAsync(CancellationToken token) { await using var db = await factory.CreateDbContextAsync(token); return await db.Cameras.AsNoTracking().Where(x => x.Enabled).ToListAsync(token); }
    public async Task<IReadOnlyList<AlertRule>> RulesAsync(CancellationToken token) { await using var db = await factory.CreateDbContextAsync(token); return await db.Rules.AsNoTracking().Where(x => x.Enabled).ToListAsync(token); }
    public async Task<string> ModelDirectoryAsync(CancellationToken token) { await using var db = await factory.CreateDbContextAsync(token); return await EventWriter.ReadSettingAsync(db, "modelDirectory", defaultModelDirectory, token); }
    public Task ObserveFirearmAbsenceAsync(Guid cameraId, DateTime capturedUtc, CancellationToken token) => writer.ObserveFirearmAbsenceAsync(cameraId, capturedUtc, token);
    public Task RecordAsync(DetectionEvent item, byte[]? jpeg, CancellationToken token) => writer.RecordAsync(item, jpeg, token);
    public async Task<SmartMonitoringSettings> SmartSettingsAsync(CancellationToken token)
    {
        await using var db = await factory.CreateDbContextAsync(token);
        var settings = await EventWriter.ReadSettingAsync(db, "smartMonitoring", new SmartMonitoringSettings(), token);
        settings.FlorenceEnabled = true;
        if (resolveFlorence != null) settings.FlorenceDirectory = resolveFlorence(settings.FlorenceDirectory);
        return settings;
    }
}
