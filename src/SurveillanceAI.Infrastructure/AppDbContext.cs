using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using SurveillanceAI.Domain;

namespace SurveillanceAI.Infrastructure;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Camera> Cameras => Set<Camera>();
    public DbSet<DetectionEvent> Events => Set<DetectionEvent>();
    public DbSet<AlertRule> Rules => Set<AlertRule>();
    public DbSet<NotificationRecipient> Recipients => Set<NotificationRecipient>();
    public DbSet<NotificationDelivery> Deliveries => Set<NotificationDelivery>();
    public DbSet<SystemSetting> Settings => Set<SystemSetting>();
    public DbSet<AuditLog> Audit => Set<AuditLog>();
    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<User>().HasIndex(x => x.Username).IsUnique();
        model.Entity<SystemSetting>().HasKey(x => x.Key);
        model.Entity<DetectionEvent>().HasIndex(x => new { x.TimestampUtc, x.CameraId });
        model.Entity<DetectionEvent>().HasIndex(x => new { x.Status, x.Severity });
        model.Entity<NotificationDelivery>().HasIndex(x => new { x.EventId, x.RecipientId }).IsUnique();
        model.Entity<NotificationDelivery>().HasIndex(x => new { x.Status, x.NextAttemptUtc });
        model.Entity<DetectionEvent>().HasOne<Camera>().WithMany().HasForeignKey(x => x.CameraId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<DetectionEvent>().HasOne<AlertRule>().WithMany().HasForeignKey(x => x.RuleId).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class DesignTimeFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite("Data Source=surveillance-design.db").Options);
}
