using SurveillanceAI.Application.Localization;
using System.Text.Json;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SurveillanceAI.Application;
using SurveillanceAI.Domain;

namespace SurveillanceAI.Infrastructure;

public sealed class EvidenceStore(string root) : IEvidenceStore
{
    private readonly string root = Path.GetFullPath(root);
    public string Resolve(string relativePath)
    {
        if (Path.IsPathRooted(relativePath) || relativePath.Contains(':')) throw new ArgumentException(Texts.T("Evidence path must be relative."));
        var full = Path.GetFullPath(Path.Combine(root, relativePath));
        if (!full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException(Texts.T("Evidence path escapes storage."));
        // Reject reparse points to prevent junction/symlink traversal.
        for (var current = new DirectoryInfo(Path.GetDirectoryName(full)!); current is not null; current = current.Parent)
        {
            if (current.Exists && current.Attributes.HasFlag(FileAttributes.ReparsePoint)) throw new IOException(Texts.T("Evidence storage cannot use reparse points."));
            if (string.Equals(current.FullName, root, StringComparison.OrdinalIgnoreCase)) break;
        }
        if (File.Exists(full) && File.GetAttributes(full).HasFlag(FileAttributes.ReparsePoint)) throw new IOException(Texts.T("Evidence file cannot be a reparse point."));
        return full;
    }
    public async Task<string> SaveAsync(Guid eventId, byte[] jpeg, CancellationToken token)
    {
        var relative = eventId.ToString("N") + ".jpg";
        var full = Resolve(relative); Directory.CreateDirectory(root);
        await File.WriteAllBytesAsync(full, jpeg, token); return relative;
    }
    public void Delete(string relativePath) { var full = Resolve(relativePath); if (File.Exists(full)) File.Delete(full); }
}
public sealed class MailKitTransport(ISecretProtector secrets) : IMailTransport
{
    public async Task TestConnectionAsync(SmtpSettings settings, CancellationToken token)
    {
        using var client = new SmtpClient { Timeout = 10000 };
        var mode = TlsOption(settings);
        await client.ConnectAsync(settings.Host, settings.Port, mode, token);
        if (!string.IsNullOrWhiteSpace(settings.Username)) await client.AuthenticateAsync(settings.Username, secrets.Unprotect(settings.ProtectedPassword), token);
        await client.DisconnectAsync(true, token);
    }
    private static SecureSocketOptions TlsOption(SmtpSettings settings) => settings.TlsMode switch { "SslOnConnect" => SecureSocketOptions.SslOnConnect, "StartTls" => SecureSocketOptions.StartTls, "LocalTest" when settings.Host is "localhost" or "127.0.0.1" or "::1" => SecureSocketOptions.None, _ => throw new ArgumentException(Texts.T("Use StartTls or SslOnConnect. LocalTest allows only loopback SMTP.")) };
    public async Task SendAsync(SmtpSettings settings, NotificationDelivery delivery, CancellationToken token)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(settings.SenderName, settings.Sender));
        message.To.Add(MailboxAddress.Parse(delivery.Address));
        message.Subject = Texts.T(delivery.Subject); message.Body = new TextPart("plain") { Text = delivery.Body };
        using var client = new SmtpClient { Timeout = 10000 };
        var mode = TlsOption(settings);
        await client.ConnectAsync(settings.Host, settings.Port, mode, token);
        if (!string.IsNullOrWhiteSpace(settings.Username)) await client.AuthenticateAsync(settings.Username, secrets.Unprotect(settings.ProtectedPassword), token);
        await client.SendAsync(message, token); await client.DisconnectAsync(true, token);
    }
}
public sealed class EventWriter(IDbContextFactory<AppDbContext> factory, IEvidenceStore evidence)
{
    public async Task RecordAsync(DetectionEvent item, byte[]? jpeg, CancellationToken token)
    {
        await using var db = await factory.CreateDbContextAsync(token);
        var evidenceEnabled = await ReadSettingAsync(db, "evidenceEnabled", false, token);
        if (evidenceEnabled && jpeg is not null) item.EvidencePath = await evidence.SaveAsync(item.Id, jpeg, token);
        db.Events.Add(item);
        var smtp = await ReadSettingAsync(db, "smtp", new SmtpSettings(), token);
        if (smtp.Enabled)
        {
            var recipients = await db.Recipients.Where(x => x.Enabled && (x.CameraId == null || x.CameraId == item.CameraId) && x.MinimumSeverity <= item.Severity).ToListAsync(token);
            foreach (var recipient in recipients.Where(r => string.IsNullOrWhiteSpace(r.ClassFilter) || r.ClassFilter.Split(',', StringSplitOptions.TrimEntries).Contains(item.Label)))
                db.Deliveries.Add(new NotificationDelivery { EventId = item.Id, RecipientId = recipient.Id, Address = recipient.Email, Subject = Texts.Format("Potential {0} detection — review required", item.Label), Body = Texts.Format("Camera: {0}\nUTC: {1:O}\nClass: {2}\nConfidence: {3:P1}\nModel: {4}\nEvent: {5}\nThis is an unverified detection requiring operator review.", item.CameraId, item.TimestampUtc, item.Label, item.Confidence, item.ModelVersion, item.Id) });
        }
        try { await db.SaveChangesAsync(token); }
        catch { if (item.EvidencePath is not null) evidence.Delete(item.EvidencePath); throw; }
    }
    internal static async Task<T> ReadSettingAsync<T>(AppDbContext db, string key, T fallback, CancellationToken token)
    {
        var json = await db.Settings.Where(x => x.Key == key).Select(x => x.Value).SingleOrDefaultAsync(token);
        return json is null ? fallback : JsonSerializer.Deserialize<T>(json) ?? fallback;
    }
}
public sealed class NotificationWorker(IDbContextFactory<AppDbContext> factory, IMailTransport transport, ILogger<NotificationWorker> log) : BackgroundService
{
    public static TimeSpan RetryDelay(int attempt) => TimeSpan.FromSeconds(Math.Min(300, 5 * Math.Pow(2, Math.Clamp(attempt - 1, 0, 10))));
    public async Task<int> ProcessOnceAsync(CancellationToken token)
    {
        await using var db = await factory.CreateDbContextAsync(token);
        var settings = await EventWriter.ReadSettingAsync(db, "smtp", new SmtpSettings(), token);
        // Explicit administrative tests bypass channel enablement, regular alerts do not.
        var now = DateTime.UtcNow;
        var deliveries = await db.Deliveries.Where(x => x.Status == "Pending" && x.NextAttemptUtc <= now).OrderBy(x => x.NextAttemptUtc).Take(20).ToListAsync(token);
        var processed = 0;
        foreach (var delivery in deliveries)
        {
            if (!settings.Enabled && delivery.Subject != "SurveillanceAI test notification") continue;
            token.ThrowIfCancellationRequested();
            delivery.Attempts++;
            try { await transport.SendAsync(settings, delivery, token); delivery.Status = "Delivered"; delivery.DeliveredUtc = DateTime.UtcNow; delivery.Error = null; }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
            catch (Exception error)
            {
                // Never retain exception messages from network libraries: they can contain credentials or URLs.
                delivery.Error = $"Delivery failed ({error.GetType().Name}). Check SMTP configuration and connectivity.";
                delivery.Status = delivery.Attempts >= Math.Clamp(settings.MaxAttempts, 1, 10) ? "Failed" : "Pending";
                delivery.NextAttemptUtc = DateTime.UtcNow + RetryDelay(delivery.Attempts);
                log.LogWarning("Notification {DeliveryId} failed with {ErrorType}", delivery.Id, error.GetType().Name);
            }
            await db.SaveChangesAsync(token); processed++;
        }
        return processed;
    }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await ProcessOnceAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception error) { log.LogError("Notification queue failure: {ErrorType}", error.GetType().Name); }
            await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
        }
    }
}
public sealed class RetentionWorker(IDbContextFactory<AppDbContext> factory, IEvidenceStore evidence, ILogger<RetentionWorker> log) : BackgroundService
{
    public async Task<int> CleanupAsync(CancellationToken token)
    {
        await using var db = await factory.CreateDbContextAsync(token);
        var days = Math.Clamp(await EventWriter.ReadSettingAsync(db, "retentionDays", 30, token), 1, 3650);
        var cutoff = DateTime.UtcNow.AddDays(-days);
        var expired = await db.Events.Where(x => x.TimestampUtc < cutoff).OrderBy(x => x.TimestampUtc).Take(200).ToListAsync(token);
        foreach (var item in expired)
        {
            if (item.EvidencePath is not null) evidence.Delete(item.EvidencePath);
            await db.Deliveries.Where(x => x.EventId == item.Id).ExecuteDeleteAsync(token);
            db.Events.Remove(item);
        }
        await db.SaveChangesAsync(token); return expired.Count;
    }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { while (await CleanupAsync(stoppingToken) == 200) stoppingToken.ThrowIfCancellationRequested(); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception error) { log.LogError("Retention cleanup failed: {ErrorType}", error.GetType().Name); }
            await Task.Delay(TimeSpan.FromHours(6), stoppingToken);
        }
    }
}
public sealed class NotificationAdmin(IDbContextFactory<AppDbContext> factory, Session session)
{
    public async Task QueueTestAsync(string address)
    {
        session.Require(UserRole.Administrator);
        if (!System.Net.Mail.MailAddress.TryCreate(address, out _)) throw new ArgumentException(Texts.T("Enter a valid test recipient."));
        await using var db = await factory.CreateDbContextAsync();
        db.Deliveries.Add(new NotificationDelivery { EventId = Guid.NewGuid(), RecipientId = Guid.NewGuid(), Address = address, Subject = "SurveillanceAI test notification", Body = Texts.T("Administrative delivery test. No surveillance incident is associated with this message.") });
        db.Audit.Add(new AuditLog { UserId = session.UserId, Action = "Test notification queued" }); await db.SaveChangesAsync();
    }
}
