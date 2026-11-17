using System.Text.Json;
using SurveillanceAI.Application.Localization;

namespace SurveillanceAI.Infrastructure;

/// <summary>Non-secret installation preference, available before login.</summary>
public sealed class LanguagePreferences(string directory)
{
    private readonly string path = Path.Combine(directory, "language.json");
    public string Load()
    {
        try
        {
            var language = JsonSerializer.Deserialize<string>(File.ReadAllText(path));
            return language is not null && Texts.SupportedLanguages.Contains(language) ? language : "pt-BR";
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException) { return "pt-BR"; }
    }
    public void Save(string language)
    {
        if (!Texts.SupportedLanguages.Contains(language)) throw new ArgumentException("Unsupported language.", nameof(language));
        Directory.CreateDirectory(directory);
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(language));
        File.Move(temporary, path, overwrite: true);
    }
}
