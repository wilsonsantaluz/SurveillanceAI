using SurveillanceAI.Infrastructure;
using Xunit;

namespace SurveillanceAI.IntegrationTests;

public sealed class LanguagePreferencesTests
{
    [Theory]
    [InlineData("pt-BR")]
    [InlineData("en-US")]
    public async Task PreferenceSurvivesRestartAndRejectsUnsupportedValues(string language)
    {
        await using var directory = new TestDatabase();
        var preference = new LanguagePreferences(directory.Root);
        Assert.Equal("pt-BR", preference.Load());
        preference.Save(language);
        Assert.Equal(language, new LanguagePreferences(directory.Root).Load());
        Assert.Throws<ArgumentException>(() => preference.Save("es-ES"));
        Assert.Equal(language, preference.Load());
        Assert.False(File.Exists(Path.Combine(directory.Root, "language.json.tmp")));
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("\"es-ES\"")]
    [InlineData("null")]
    public async Task CorruptOrUnknownPreferenceFallsBackToPortuguese(string content)
    {
        await using var directory = new TestDatabase();
        await File.WriteAllTextAsync(Path.Combine(directory.Root, "language.json"), content);
        Assert.Equal("pt-BR", new LanguagePreferences(directory.Root).Load());
    }
}
