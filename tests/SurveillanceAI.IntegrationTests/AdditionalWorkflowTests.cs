using Microsoft.EntityFrameworkCore;
using SurveillanceAI.Application;
using SurveillanceAI.Domain;
using SurveillanceAI.Infrastructure;
using Xunit;

namespace SurveillanceAI.IntegrationTests;
public sealed class AdditionalWorkflowTests
{
    [Fact] public async Task ActivationRolesAndPasswordChangesAreEnforced()
    {
        await using var factory=new TestDatabase();await factory.InitializeAsync();var session=new Session();var auth=new Authentication(factory,session);
        await auth.SetupAsync("admin","administrator-password");await auth.LoginAsync("admin","administrator-password");
        await auth.SaveUserAsync(null,"viewer","viewer-test-password",UserRole.Viewer,true);var viewer=(await auth.UsersAsync()).Single(x=>x.Role==UserRole.Viewer);
        await auth.SaveUserAsync(viewer.Id,viewer.Username,null,UserRole.Operator,false);auth.Logout();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>auth.LoginAsync("viewer","viewer-test-password"));
        await auth.LoginAsync("admin","administrator-password");await auth.SaveUserAsync(viewer.Id,viewer.Username,null,UserRole.Operator,true);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>auth.SaveUserAsync(session.UserId,"admin",null,UserRole.Viewer,false));
        auth.Logout();await auth.LoginAsync("viewer","viewer-test-password");Assert.Equal(UserRole.Operator,session.Role);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>auth.SaveUserAsync(null,"other","another-password",UserRole.Administrator,true));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>auth.ChangePasswordAsync("wrong-password","new-viewer-password"));
    }
    [Fact] public async Task EventOutboxFiltersDeduplicationAndEvidenceUseRealPersistence()
    {
        await using var factory=new TestDatabase();await factory.InitializeAsync();var session=new Session();session.SignIn(Guid.NewGuid(),UserRole.Administrator);
        var store=new ProductStore(factory,session,new DpapiProtector());var camera=new Camera{Name="Outbox camera"};await store.SaveCameraAsync(camera,"0");
        var rule=new AlertRule{Name="Outbox rule"};await store.SaveRuleAsync(rule);
        var recipient=new NotificationRecipient{Name="Matching",Email="test@example.invalid",Enabled=true,ClassFilter="person",CameraId=camera.Id};await store.SaveRecipientAsync(recipient);
        await store.SaveRecipientAsync(new NotificationRecipient{Name="Other class",Email="other@example.invalid",Enabled=true,ClassFilter="car"});
        await store.SaveSettingAsync("smtp",new SmtpSettings{Enabled=true});await store.SaveSettingAsync("evidenceEnabled",true);
        var evidence=new EvidenceStore(Path.Combine(factory.Root,"evidence"));var writer=new EventWriter(factory,evidence);
        var item=new DetectionEvent{CameraId=camera.Id,RuleId=rule.Id,Label="person",Severity=Severity.Warning};
        await writer.RecordAsync(item,[1,2,3],default);Assert.NotNull(item.EvidencePath);Assert.True(File.Exists(evidence.Resolve(item.EvidencePath)));
        var delivery=Assert.Single(await store.DeliveriesAsync());Assert.Equal(recipient.Id,delivery.RecipientId);
        await using(var db=factory.CreateDbContext())
        {
            db.Deliveries.Add(new NotificationDelivery{EventId=item.Id,RecipientId=recipient.Id});await Assert.ThrowsAsync<DbUpdateException>(()=>db.SaveChangesAsync());
        }
        Assert.Single(await store.DeliveriesAsync());Assert.Equal(1,(await store.EventCountsAsync()).Pending);
        Assert.Single(await store.EventsAsync(new EventFilter(CameraId:camera.Id,Severity:Severity.Warning)));
        Assert.Empty(await store.EventsAsync(new EventFilter(Severity:Severity.Critical)));
    }
    [Fact] public async Task SecretsAreProtectedAndNotificationsDefaultToDisabled()
    {
        await using var factory=new TestDatabase();await factory.InitializeAsync();var session=new Session();session.SignIn(Guid.NewGuid(),UserRole.Administrator);var secrets=new DpapiProtector();var store=new ProductStore(factory,session,secrets);
        var smtp=new SmtpSettings{ProtectedPassword=secrets.Protect("smtp-secret-value")};await store.SaveSettingAsync("smtp",smtp);
        var camera=new Camera{Name="Secret camera",Kind=SourceKind.Rtsp};await store.SaveCameraAsync(camera,"rtsp://user:camera-secret@localhost/live");
        await using var db=factory.CreateDbContext();Assert.DoesNotContain("smtp-secret-value",(await db.Settings.SingleAsync()).Value);Assert.DoesNotContain("camera-secret",(await db.Cameras.SingleAsync()).ProtectedSource);Assert.Empty(await db.Deliveries.ToListAsync());
        Assert.False((await store.SettingAsync("smtp",new SmtpSettings())).Enabled);Assert.Equal("smtp-secret-value",secrets.Unprotect(smtp.ProtectedPassword));
    }
}
