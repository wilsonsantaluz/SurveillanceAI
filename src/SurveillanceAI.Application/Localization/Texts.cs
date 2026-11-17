using System.ComponentModel;
using System.Globalization;
using System.Resources;
using System.Security.Cryptography;
using System.Text;

namespace SurveillanceAI.Application.Localization;

/// <summary>Shared UI/message resources. Persisted identifiers and model class names stay invariant.</summary>
public sealed class Texts : INotifyPropertyChanged
{
    private static readonly ResourceManager Resources = new("SurveillanceAI.Application.Localization.Strings", typeof(Texts).Assembly);
    public static Texts Instance { get; } = new();
    public static IReadOnlyList<string> SupportedLanguages { get; } = ["pt-BR", "en-US"];
    public string LanguageTag { get; private set; } = "pt-BR";
    public string this[string key] => Resources.GetString(key, CultureInfo.GetCultureInfo(LanguageTag)) ?? key;
    public event PropertyChangedEventHandler? PropertyChanged;

    public static string Key(string english) => "T" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(english))).ToLowerInvariant()[..16];
    public static string T(string english) => Resources.GetString(Key(english), CultureInfo.GetCultureInfo(Instance.LanguageTag)) ?? english;
    public static string Format(string english, params object?[] args) => string.Format(CultureInfo.GetCultureInfo(Instance.LanguageTag), T(english), args);

    public static string Display(string value)
    {
        var translated = T(value);
        if (translated != value) return translated;
        // Historical audit/error rows keep their invariant identifiers and remain readable in either language.
        foreach (var action in new[] { "Camera saved", "Rule saved", "Recipient saved", "User updated", "Setting updated", "Event review" })
            if (value.StartsWith(action + ": ", StringComparison.Ordinal))
            {
                var detail = value[(action.Length + 2)..];
                if (action == "Event review")
                {
                    var parts = detail.Split(", ", 2);
                    if (parts.Length == 2) detail = parts[0] + ", " + T(parts[1]);
                }
                return T(action) + ": " + detail;
            }
        const string prefix = "Delivery failed (";
        const string suffix = "). Check SMTP configuration and connectivity.";
        if (value.StartsWith(prefix, StringComparison.Ordinal) && value.EndsWith(suffix, StringComparison.Ordinal))
            return Format("Delivery failed ({0}). Check SMTP configuration and connectivity.", value[prefix.Length..^suffix.Length]);
        return value;
    }

    public void SetLanguage(string language)
    {
        if (!SupportedLanguages.Contains(language)) throw new ArgumentException("Unsupported language.", nameof(language));
        LanguageTag = language;
        var culture = CultureInfo.GetCultureInfo(language);
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LanguageTag)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
    }
}
