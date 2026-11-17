using SurveillanceAI.Application.Localization;
using System.Net.Mail;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SurveillanceAI.Application;
using SurveillanceAI.Domain;

namespace SurveillanceAI.Infrastructure;

// Operator-facing operations always authorize here; monitoring persistence has a separate internal workflow.
public sealed class ProductStore(IDbContextFactory<AppDbContext> factory, Session session, ISecretProtector secrets)
{
    public async Task<EventCounts> EventCountsAsync()
    {
        session.Require(); await using var db = await factory.CreateDbContextAsync(); var today = DateTime.UtcNow.Date;
        return new EventCounts(await db.Events.CountAsync(), await db.Events.CountAsync(x => x.Status == ReviewStatus.Pending), await db.Events.CountAsync(x => x.TimestampUtc >= today));
    }
    public async Task<List<Camera>> CamerasAsync() { session.Require(); await using var db = await factory.CreateDbContextAsync(); return await db.Cameras.AsNoTracking().OrderBy(x => x.Name).ToListAsync(); }
    public async Task SaveCameraAsync(Camera camera, string? source)
    {
        session.Require(UserRole.Administrator);
        if (string.IsNullOrWhiteSpace(camera.Name) || camera.InferenceFps is < 1 or > 30 || !Enum.IsDefined(camera.Kind)) throw new ArgumentException(Texts.T("Enter a camera name and inference FPS from 1 to 30."));
        if (!string.IsNullOrWhiteSpace(source)) { ValidateSource(camera.Kind, source); camera.ProtectedSource = secrets.Protect(source); }
        if (string.IsNullOrEmpty(camera.ProtectedSource)) throw new ArgumentException(Texts.T("A camera source is required."));
        await using var db = await factory.CreateDbContextAsync();
        if (await db.Cameras.AnyAsync(x => x.Id == camera.Id)) db.Cameras.Update(camera); else db.Cameras.Add(camera);
        Audit(db, $"Camera saved: {camera.Id}"); await db.SaveChangesAsync();
    }
    public static void ValidateSource(SourceKind kind, string source)
    {
        if (kind == SourceKind.Webcam && (!int.TryParse(source, out var index) || index < 0)) throw new ArgumentException(Texts.T("Webcam source must be a nonnegative device index."));
        if (kind == SourceKind.Rtsp && (!Uri.TryCreate(source, UriKind.Absolute, out var uri) || (uri.Scheme != "rtsp" && uri.Scheme != "rtsps"))) throw new ArgumentException(Texts.T("Enter an RTSP URI."));
        if (kind == SourceKind.VideoFile && !File.Exists(source)) throw new ArgumentException(Texts.T("The video file does not exist."));
    }
    public async Task<List<DetectionEvent>> EventsAsync(EventFilter filter)
    {
        session.Require(); await using var db = await factory.CreateDbContextAsync();
        var query = db.Events.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(filter.Search)) query = query.Where(x => x.Label.Contains(filter.Search) || x.Notes.Contains(filter.Search));
        if (filter.CameraId is { } camera) query = query.Where(x => x.CameraId == camera);
        if (filter.Status is { } status) query = query.Where(x => x.Status == status);
        if (filter.Severity is { } severity) query = query.Where(x => x.Severity == severity);
        if (filter.FromUtc is { } from) query = query.Where(x => x.TimestampUtc >= from);
        if (filter.ToUtc is { } to) query = query.Where(x => x.TimestampUtc <= to);
        return await query.OrderByDescending(x => x.TimestampUtc).Skip(Math.Max(0, filter.Page) * 100).Take(100).ToListAsync();
    }
    public async Task ReviewAsync(Guid id, ReviewStatus status, string notes)
    {
        session.Require(UserRole.Operator); await using var db = await factory.CreateDbContextAsync();
        if (!Enum.IsDefined(status)) throw new ArgumentException(Texts.T("Invalid review status."));
        var item = await db.Events.SingleAsync(x => x.Id == id);
        item.Status = status; item.Notes = notes; item.ReviewerId = session.UserId; item.ReviewedUtc = DateTime.UtcNow;
        Audit(db, $"Event review: {id}, {status}"); await db.SaveChangesAsync();
    }
    public async Task<List<AlertRule>> RulesAsync() { session.Require(); await using var db = await factory.CreateDbContextAsync(); return await db.Rules.AsNoTracking().ToListAsync(); }
    public async Task SaveRuleAsync(AlertRule rule)
    {
        session.Require(UserRole.Administrator);
        if (string.IsNullOrWhiteSpace(rule.Name) || string.IsNullOrWhiteSpace(rule.Label) || !float.IsFinite(rule.Confidence) || rule.Confidence is < 0 or > 1 || rule.Confirmations is < 2 or > 100 || rule.WindowSeconds is < 1 or > 3600 || rule.CooldownSeconds is < 1 or > 86400 || !float.IsFinite(rule.AssociationIou) || rule.AssociationIou is <= 0 or > 1) throw new ArgumentException(Texts.T("Invalid rule: require 2–100 confirmations, a positive window/cooldown and confidence/IoU within 0–1."));
        await using var db = await factory.CreateDbContextAsync();
        if (rule.CameraId is { } camera && !await db.Cameras.AnyAsync(x => x.Id == camera)) throw new ArgumentException(Texts.T("The rule camera does not exist."));
        if (await db.Rules.AnyAsync(x => x.Id == rule.Id)) db.Rules.Update(rule); else db.Rules.Add(rule);
        Audit(db, $"Rule saved: {rule.Id}"); await db.SaveChangesAsync();
    }
    public async Task<List<NotificationRecipient>> RecipientsAsync() { session.Require(UserRole.Administrator); await using var db = await factory.CreateDbContextAsync(); return await db.Recipients.AsNoTracking().ToListAsync(); }
    public async Task SaveRecipientAsync(NotificationRecipient recipient)
    {
        session.Require(UserRole.Administrator);
        if (!MailAddress.TryCreate(recipient.Email, out _) || string.IsNullOrWhiteSpace(recipient.Name)) throw new ArgumentException(Texts.T("Enter a recipient name and valid email address."));
        await using var db = await factory.CreateDbContextAsync();
        if (recipient.CameraId is { } camera && !await db.Cameras.AnyAsync(x => x.Id == camera)) throw new ArgumentException(Texts.T("The recipient camera does not exist."));
        if (await db.Recipients.AnyAsync(x => x.Id == recipient.Id)) db.Recipients.Update(recipient); else db.Recipients.Add(recipient);
        Audit(db, $"Recipient saved: {recipient.Id}"); await db.SaveChangesAsync();
    }
    public async Task<T> SettingAsync<T>(string key, T fallback) { session.Require(); await using var db = await factory.CreateDbContextAsync(); var json = await db.Settings.Where(x => x.Key == key).Select(x => x.Value).SingleOrDefaultAsync(); return json is null ? fallback : JsonSerializer.Deserialize<T>(json) ?? fallback; }
    public async Task SaveSettingAsync<T>(string key, T value)
    {
        session.Require(UserRole.Administrator); await using var db = await factory.CreateDbContextAsync();
        var setting = await db.Settings.FindAsync(key);
        if (setting is null) db.Settings.Add(new SystemSetting { Key = key, Value = JsonSerializer.Serialize(value) }); else setting.Value = JsonSerializer.Serialize(value);
        Audit(db, $"Setting updated: {key}"); await db.SaveChangesAsync();
    }
    public async Task<List<NotificationDelivery>> DeliveriesAsync() { session.Require(UserRole.Administrator); await using var db = await factory.CreateDbContextAsync(); return await db.Deliveries.AsNoTracking().OrderByDescending(x => x.NextAttemptUtc).Take(100).ToListAsync(); }
    public async Task<List<AuditLog>> AuditAsync() { session.Require(UserRole.Administrator); await using var db = await factory.CreateDbContextAsync(); return await db.Audit.AsNoTracking().OrderByDescending(x => x.Id).Take(200).ToListAsync(); }
    private void Audit(AppDbContext db, string action) => db.Audit.Add(new AuditLog { UserId = session.UserId, Action = action });
}
