using SurveillanceAI.Application.Localization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SurveillanceAI.Application;
using SurveillanceAI.Domain;

namespace SurveillanceAI.Infrastructure;

public sealed class SmartConfigStore(IDbContextFactory<AppDbContext> factory, Session session, IFlorenceDetector florence, Func<string, string[]> readLabels, string defaultModelDirectory, Func<string, string>? resolveFlorence = null)
{
    public async Task<SmartMonitoringSettings> GetAsync()
    {
        session.Require(UserRole.Administrator); await using var db = await factory.CreateDbContextAsync();
        var settings = await EventWriter.ReadSettingAsync(db, "smartMonitoring", new SmartMonitoringSettings(), default);
        settings.FlorenceEnabled = true;
        if (resolveFlorence != null) settings.FlorenceDirectory = resolveFlorence(settings.FlorenceDirectory);
        return settings;
    }
    public async Task<string[]> LabelsAsync()
    {
        session.Require(UserRole.Administrator); await using var db = await factory.CreateDbContextAsync();
        var directory = await EventWriter.ReadSettingAsync(db, "modelDirectory", defaultModelDirectory, default);
        return await Task.Run(() => readLabels(directory));
    }
    public async Task SaveAsync(SmartMonitoringSettings settings, CancellationToken token = default)
    {
        session.Require(UserRole.Administrator);
        settings.FlorenceEnabled = true;
        if (resolveFlorence != null) settings.FlorenceDirectory = resolveFlorence(settings.FlorenceDirectory);
        var labels = await LabelsAsync();
        if (settings.FlorenceEnabled) await florence.LoadAsync(settings.FlorenceDirectory, settings.MaxTokens, settings.CpuThreads, token);
        settings.Validate(labels, florence.Diagnostic.State == "Ready");
        await using var db = await factory.CreateDbContextAsync(token);
        if (settings.CameraIds.Any() && await db.Cameras.CountAsync(c => settings.CameraIds.Contains(c.Id), token) != settings.CameraIds.Distinct().Count()) throw new ArgumentException(Texts.T("Select existing cameras."));
        await using var transaction = await db.Database.BeginTransactionAsync(token);
        var managed = await db.Rules.Where(r => r.TemplateId != null).ToListAsync(token);
        foreach (var old in managed) old.Enabled = false;
        var desired = new List<(string Template, string Label)>();
        if (settings.FirearmsEnabled) desired.Add((TemplateIds.Firearms, "possible_firearm"));
        if (settings.ObjectsEnabled) desired.AddRange(settings.SelectedClasses.Distinct().Select(label => (TemplateIds.Objects, label)));
        foreach (var (template, label) in desired)
        {
            var rule = managed.SingleOrDefault(r => r.TemplateId == template && r.Label == label);
            if (rule is null) { rule = new AlertRule { TemplateId = template, Label = label, Name = template + "/" + label }; db.Rules.Add(rule); }
            rule.Enabled = true; rule.Confidence = template == TemplateIds.Firearms ? 0 : .65f;
            rule.Confirmations = template == TemplateIds.Firearms ? 1 : 2; rule.WindowSeconds = 5; rule.CooldownSeconds = 60;
            rule.Severity = template == TemplateIds.Firearms ? Severity.Warning : Severity.Information;
        }
        var value = await db.Settings.FindAsync(["smartMonitoring"], token);
        if (value is null) db.Settings.Add(new SystemSetting { Key = "smartMonitoring", Value = JsonSerializer.Serialize(settings) }); else value.Value = JsonSerializer.Serialize(settings);
        db.Audit.Add(new AuditLog { UserId = session.UserId, Action = "Smart monitoring templates saved" });
        await db.SaveChangesAsync(token); await transaction.CommitAsync(token);
    }
}
