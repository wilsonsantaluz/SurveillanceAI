using SurveillanceAI.Application;
using SurveillanceAI.Domain;
using SurveillanceAI.Monitoring;
using Xunit;

namespace SurveillanceAI.UnitTests;
public sealed class RulesTests
{
    private static readonly Detection Person = new("person", .9f, 10, 10, 100, 100);
    [Fact] public void ConfirmationCooldownAndDisconnectAreIndependentPerCamera()
    {
        var engine = new TemporalRules(); var a = Guid.NewGuid(); var b = Guid.NewGuid(); var now = DateTime.UtcNow;
        var rule = new AlertRule { Enabled = true, Confirmations = 2, WindowSeconds = 5, CooldownSeconds = 10 };
        Assert.Empty(engine.Evaluate(a, [Person], [rule], now));
        Assert.Empty(engine.Evaluate(b, [Person], [rule], now.AddSeconds(1)));
        Assert.Single(engine.Evaluate(a, [Person], [rule], now.AddSeconds(1)));
        Assert.Empty(engine.Evaluate(a, [Person], [rule], now.AddSeconds(2)));
        Assert.Empty(engine.Evaluate(a, [Person], [rule], now.AddSeconds(3)));
        engine.Reset(a);
        Assert.Empty(engine.Evaluate(a, [Person], [rule], now.AddSeconds(4)));
        Assert.Single(engine.Evaluate(b, [Person], [rule], now.AddSeconds(4)));
    }
    [Fact] public void DistinctObjectsAndExpiredHitsCannotConfirmEachOther()
    {
        var engine = new TemporalRules(); var camera = Guid.NewGuid(); var now = DateTime.UtcNow;
        var rule = new AlertRule { Enabled = true, Confirmations = 2, WindowSeconds = 2 };
        Assert.Empty(engine.Evaluate(camera, [Person, Person], [rule], now));
        Assert.Empty(engine.Evaluate(camera, [Person with { X = 1000 }], [rule], now.AddSeconds(1)));
        Assert.Empty(engine.Evaluate(camera, [Person], [rule], now.AddSeconds(4)));
    }
    [Fact] public void NoRulesMeansNoAlertsRegardlessOfPeopleOrProximity() => Assert.Empty(new TemporalRules().Evaluate(Guid.NewGuid(), [Person, Person with { X = 11 }], [], DateTime.UtcNow));
    [Theory][InlineData(1,1)][InlineData(2,2)][InlineData(5,16)][InlineData(20,30)] public void BackoffIsBounded(int attempt, int seconds) => Assert.Equal(seconds, CameraWorker.Backoff(attempt).TotalSeconds);
    [Theory][InlineData(UserRole.Viewer,UserRole.Operator,false)][InlineData(UserRole.Operator,UserRole.Operator,true)][InlineData(UserRole.Operator,UserRole.Administrator,false)][InlineData(UserRole.Administrator,UserRole.Administrator,true)]
    public void ServiceAuthorizationEnforcesRoles(UserRole actual, UserRole required, bool allowed)
    {
        var session = new Session(); session.SignIn(Guid.NewGuid(), actual);
        if (allowed) session.Require(required); else Assert.Throws<UnauthorizedAccessException>(() => session.Require(required));
        session.Logout(); Assert.Throws<UnauthorizedAccessException>(() => session.Require());
    }
    [Fact] public void ManifestRejectsUnsupportedAndMalformedContracts()
    {
        var manifest = new ModelManifest(); Assert.Throws<InvalidDataException>(manifest.Validate);
        manifest.ModelFile = "../model.onnx"; Assert.Throws<InvalidDataException>(manifest.Validate);
    }
}
