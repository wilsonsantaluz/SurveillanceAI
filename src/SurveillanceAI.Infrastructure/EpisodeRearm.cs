using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SurveillanceAI.Application;
using SurveillanceAI.Domain;

namespace SurveillanceAI.Infrastructure;

// Persistent state survives event deletion, retention and application restarts.
internal static class EpisodeRearm
{
    public static string ClearKey(Guid camera) => $"firearm-clear:{camera:N}";
    private static string ClosedKey(Guid camera) => $"firearm-closed:{camera:N}";
    public static async Task<DateTime> LastClosedAsync(AppDbContext db, Guid camera, CancellationToken token)
    {
        var saved = await EventWriter.ReadSettingAsync(db, ClosedKey(camera), DateTime.MinValue, token);
        var legacy = await db.Events.Where(e => e.CameraId == camera && e.TemplateId == TemplateIds.Firearms && e.ClosedUtc != null).MaxAsync(e => e.ClosedUtc, token);
        return legacy > saved ? legacy.Value : saved;
    }
    public static async Task CloseAsync(AppDbContext db, DetectionEvent item, CancellationToken token = default)
    {
        if (item.TemplateId != TemplateIds.Firearms) return;
        await SetAsync(db, ClosedKey(item.CameraId), DateTime.UtcNow, token);
    }
    public static async Task SetAsync(AppDbContext db, string key, DateTime value, CancellationToken token)
    {
        var setting = await db.Settings.FindAsync([key], token);
        if (setting is null) db.Settings.Add(new SystemSetting { Key = key, Value = JsonSerializer.Serialize(value) });
        else setting.Value = JsonSerializer.Serialize(value);
    }
}
