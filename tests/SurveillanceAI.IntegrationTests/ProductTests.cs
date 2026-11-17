using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SurveillanceAI.Application;
using SurveillanceAI.Domain;
using SurveillanceAI.Infrastructure;
using Xunit;

namespace SurveillanceAI.IntegrationTests;
public sealed class TestDatabase : IDbContextFactory<AppDbContext>, IAsyncDisposable
{
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "SurveillanceAI-tests", Guid.NewGuid().ToString("N"));
    public TestDatabase() => Directory.CreateDirectory(Root);
    public AppDbContext CreateDbContext() => new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={Path.Combine(Root, "test.db")};Pooling=False").Options);
    public async Task InitializeAsync() { await using var db = CreateDbContext(); await db.Database.MigrateAsync(); }
    public ValueTask DisposeAsync() { Directory.Delete(Root, true); return ValueTask.CompletedTask; }
}
public sealed class ProductTests
{
    [Fact] public async Task SetupHashLoginLockoutAndPasswordChange()
    {
        await using var factory = new TestDatabase(); await factory.InitializeAsync(); var session = new Session(); var auth = new Authentication(factory, session);
        Assert.True(await auth.NeedsSetupAsync()); await auth.SetupAsync("admin", "long-test-password"); Assert.False(await auth.NeedsSetupAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => auth.SetupAsync("other", "long-test-password"));
        await using (var db = factory.CreateDbContext()) Assert.DoesNotContain("long-test-password", (await db.Users.SingleAsync()).PasswordHash);
        await auth.LoginAsync("admin", "long-test-password"); session.Require(UserRole.Administrator);
        await auth.ChangePasswordAsync("long-test-password", "replacement-password"); auth.Logout();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => auth.LoginAsync("admin", "long-test-password"));
        await auth.LoginAsync("admin", "replacement-password");
        auth.Logout(); for (var i=0;i<5;i++) await Assert.ThrowsAsync<UnauthorizedAccessException>(() => auth.LoginAsync("admin", "incorrect"));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => auth.LoginAsync("admin", "replacement-password"));
    }
    [Fact] public async Task CameraCrudPermissionsRecipientAndEventReviewPersist()
    {
        await using var factory = new TestDatabase(); await factory.InitializeAsync(); var session = new Session(); session.SignIn(Guid.NewGuid(), UserRole.Administrator);
        var secrets = new DpapiProtector(); var store = new ProductStore(factory, session, secrets);
        var camera = new Camera { Name = "Test camera", Kind = SourceKind.Webcam };
        await store.SaveCameraAsync(camera, "0"); Assert.NotEqual("0", camera.ProtectedSource); Assert.Equal("0", secrets.Unprotect(camera.ProtectedSource));
        camera.Name = "Updated"; camera.Enabled = false; await store.SaveCameraAsync(camera, null);
        Assert.Equal("Updated", (await store.CamerasAsync()).Single().Name);
        var rule = new AlertRule { Name = "Test", Enabled = true }; await store.SaveRuleAsync(rule);
        await store.SaveRecipientAsync(new NotificationRecipient { Name = "Test recipient", Email = "test@example.invalid" }); Assert.Single(await store.RecipientsAsync());
        var writer = new EventWriter(factory, new EvidenceStore(Path.Combine(factory.Root,"evidence")));
        var item = new DetectionEvent { CameraId = camera.Id, RuleId = rule.Id, Label = "person", Confidence = .9f, ModelVersion = "test" };
        await writer.RecordAsync(item, null, default);
        session.SignIn(Guid.NewGuid(), UserRole.Viewer);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => store.SaveCameraAsync(camera, "0"));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => store.ReviewAsync(item.Id, ReviewStatus.Reviewed, "note"));
        Assert.Single(await store.EventsAsync(new EventFilter(Search:"person")));
        Assert.Empty(await store.EventsAsync(new EventFilter(Search:"car")));
        session.SignIn(Guid.NewGuid(), UserRole.Operator); await store.ReviewAsync(item.Id, ReviewStatus.FalsePositive, "Reflection");
        var persisted = Assert.Single(await store.EventsAsync(new EventFilter(Status:ReviewStatus.FalsePositive))); Assert.Equal("Reflection", persisted.Notes); Assert.NotNull(persisted.ReviewedUtc);
        Assert.Empty(await store.EventsAsync(new EventFilter(FromUtc:DateTime.UtcNow.AddDays(1))));
        Assert.Empty(await store.EventsAsync(new EventFilter(Page:1)));
    }
    [Fact] public void CameraSourceValidationAcceptsHttpStreamsAndRejectsInvalidSchemes()
    {
        ProductStore.ValidateSource(SourceKind.HttpStream, "http://192.168.1.246:8080/stream");
        ProductStore.ValidateSource(SourceKind.HttpStream, "https://example.test/live.mjpg");
        ProductStore.ValidateSource(SourceKind.Rtsp, "rtsp://192.168.1.100/live");
        ProductStore.ValidateSource(SourceKind.Webcam, "0");
        Assert.Throws<ArgumentException>(() => ProductStore.ValidateSource(SourceKind.HttpStream, "rtsp://example.test/live"));
        Assert.Throws<ArgumentException>(() => ProductStore.ValidateSource(SourceKind.HttpStream, "file:///etc/passwd"));
        Assert.Throws<ArgumentException>(() => ProductStore.ValidateSource(SourceKind.Webcam, "http://example.test/live"));
    }
    [Fact] public async Task NotificationsRetryAndFailuresArePersistent()
    {
        await using var factory = new TestDatabase(); await factory.InitializeAsync();
        await using (var db = factory.CreateDbContext())
        {
            db.Settings.Add(new SystemSetting { Key="smtp", Value=JsonSerializer.Serialize(new SmtpSettings { Enabled=true, MaxAttempts=2 }) });
            db.Deliveries.Add(new NotificationDelivery { Address="test@example.invalid" }); await db.SaveChangesAsync();
        }
        var fake = new FakeTransport(); var worker = new NotificationWorker(factory, fake, NullLogger<NotificationWorker>.Instance);
        Assert.Equal(1, await worker.ProcessOnceAsync(default));
        await using (var db = factory.CreateDbContext()) { var delivery=await db.Deliveries.SingleAsync(); Assert.Equal("Pending",delivery.Status); Assert.Equal(1,delivery.Attempts); Assert.DoesNotContain("secret",delivery.Error!); delivery.NextAttemptUtc=DateTime.UtcNow.AddSeconds(-1); await db.SaveChangesAsync(); }
        Assert.Equal(1, await worker.ProcessOnceAsync(default));
        await using (var db = factory.CreateDbContext()) Assert.Equal("Failed",(await db.Deliveries.SingleAsync()).Status);
        Assert.Equal(2,fake.Calls);
    }
    private sealed class FakeTransport : IMailTransport { public int Calls; public Task SendAsync(SmtpSettings settings, NotificationDelivery delivery, CancellationToken token) { Calls++; throw new IOException("secret must not be persisted"); } }
    [Fact] public async Task EvidencePathsAndRetentionAreControlled()
    {
        await using var factory=new TestDatabase(); await factory.InitializeAsync(); var evidence=new EvidenceStore(Path.Combine(factory.Root,"evidence"));
        Assert.Throws<ArgumentException>(()=>evidence.Resolve("../outside.jpg")); Assert.Throws<ArgumentException>(()=>evidence.Resolve("C:\\outside.jpg"));
        var path=await evidence.SaveAsync(Guid.NewGuid(),[1,2,3],default); Assert.True(File.Exists(evidence.Resolve(path)));
        await using (var db=factory.CreateDbContext())
        {
            var camera=new Camera { Name="Retention test" }; var rule=new AlertRule { Name="Retention test" }; db.Cameras.Add(camera);db.Rules.Add(rule);
            db.Events.Add(new DetectionEvent { CameraId=camera.Id,RuleId=rule.Id,TimestampUtc=DateTime.UtcNow.AddDays(-40),EvidencePath=path });await db.SaveChangesAsync();
        }
        var worker=new RetentionWorker(factory,evidence,NullLogger<RetentionWorker>.Instance); Assert.Equal(1,await worker.CleanupAsync(default));Assert.False(File.Exists(evidence.Resolve(path)));
        await using var check=factory.CreateDbContext();Assert.Empty(await check.Events.ToListAsync());
    }
}
