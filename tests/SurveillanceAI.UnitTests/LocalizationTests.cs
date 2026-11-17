using System.Globalization;
using System.Resources;
using SurveillanceAI.Application.Localization;
using SurveillanceAI.Domain;
using Xunit;

namespace SurveillanceAI.UnitTests;

[CollectionDefinition("Localization", DisableParallelization = true)]
public sealed class LocalizationCollection;

[Collection("Localization")]
public sealed class LocalizationTests : IDisposable
{
    private readonly string original = Texts.Instance.LanguageTag;

    [Fact] public void PortugueseIsTheNeutralResourceAndEnglishHasCompleteParity()
    {
        var resources = new ResourceManager("SurveillanceAI.Application.Localization.Strings", typeof(Texts).Assembly);
        var portuguese = resources.GetResourceSet(CultureInfo.InvariantCulture, true, false)!;
        var english = resources.GetResourceSet(CultureInfo.GetCultureInfo("en"), true, false)!;
        Assert.NotEmpty(portuguese.Cast<System.Collections.DictionaryEntry>());
        foreach (System.Collections.DictionaryEntry entry in portuguese)
        {
            Assert.False(string.IsNullOrWhiteSpace((string?)entry.Value));
            Assert.False(string.IsNullOrWhiteSpace(english.GetString((string)entry.Key)));
        }
        Assert.Equal(portuguese.Cast<System.Collections.DictionaryEntry>().Count(), english.Cast<System.Collections.DictionaryEntry>().Count());
    }

    [Fact] public void LanguageChangesNotifyLiveBindingsAndUseCorrectCulture()
    {
        var notifications = new List<string?>();
        void Changed(object? sender, System.ComponentModel.PropertyChangedEventArgs e) => notifications.Add(e.PropertyName);
        Texts.Instance.PropertyChanged += Changed;
        try
        {
            Texts.Instance.SetLanguage("pt-BR");
            Assert.Equal("Câmeras", Texts.T("Cameras"));
            Assert.Equal("Administrador", Texts.T(UserRole.Administrator.ToString()));
            Assert.Equal("0,75", Texts.Format("{0:F2}", .75));
            Assert.Equal("pt-BR", CultureInfo.CurrentCulture.Name);
            Texts.Instance.SetLanguage("en-US");
            Assert.Equal("Cameras", Texts.T("Cameras"));
            Assert.Equal("Administrator", Texts.T(UserRole.Administrator.ToString()));
            Assert.Equal("0.75", Texts.Format("{0:F2}", .75));
            Assert.Contains("Item[]", notifications);
            Assert.Contains("LanguageTag", notifications);
        }
        finally { Texts.Instance.PropertyChanged -= Changed; }
    }

    [Fact] public void TranslatedDisplayDoesNotChangePersistedEnumsOrModelLabels()
    {
        Texts.Instance.SetLanguage("pt-BR");
        Assert.Equal("Falso positivo", Texts.Display(ReviewStatus.FalsePositive.ToString()));
        Assert.Equal("Arquivo de vídeo", Texts.Display(SourceKind.VideoFile.ToString()));
        Assert.Equal("FalsePositive", ReviewStatus.FalsePositive.ToString());
        Assert.Equal("person", Texts.T("person"));
        Assert.Equal("Regra salva: 123", Texts.Display("Rule saved: 123"));
        Assert.Contains("IOException", Texts.Display("Delivery failed (IOException). Check SMTP configuration and connectivity."));
        Assert.StartsWith("Falha no envio", Texts.Display("Delivery failed (IOException). Check SMTP configuration and connectivity."));
        Assert.Throws<ArgumentException>(() => Texts.Instance.SetLanguage("es-ES"));
        Assert.Equal("pt-BR", Texts.Instance.LanguageTag);
    }

    public void Dispose() => Texts.Instance.SetLanguage(original);
}
