[Português](../internationalization.md) | English

# Internationalization

Brazilian Portuguese (pt-BR) is the default independently of Windows. The Language selector works before/after login and switches live to English (en-US) without restarting cameras. Non-secret language.json in the data root preserves it; missing/malformed/unknown values fall back to Portuguese.

Strings.resx is neutral Portuguese; Strings.en.resx builds the English satellite. Texts handles lookup/culture/notifications, TrExtension live WPF bindings, LocalizedValueConverter enum/status/audit presentation, and LanguagePreferences atomic preference storage. Keys derive from English-source SHA-256 with source comments. Keep matching keys/placeholders and run parity, formatting and persistence tests.

Display dates/numbers follow culture, storage stays UTC, and enums/SMTP modes/model classes/integration identifiers remain invariant. User notes are not machine-translated. Queued email content preserves its creation language. Technical logs keep stable identifiers. Language is a harmless installation preference available to every role.

Default docs are Portuguese; readme.en.md and docs/en provide English. Update both, preserving identifiers/paths/commands.
