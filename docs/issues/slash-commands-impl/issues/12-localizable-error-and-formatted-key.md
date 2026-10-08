# 12: Localizable message error and `LocaleFormattedKey`

Status: ready-for-agent
Type: task
Blocked by:

## What to build

Runtime errors are user-facing text, but `ChatMessage.Error` stores a pre-rendered `string?`: the text is frozen at the
moment it is produced (a language switch leaves stale text), and every producer has to localize eagerly instead of
handing over the key. Turn the stored error into a **locale-backed key** and add the missing key type that carries
format arguments — the prefactor the command host (ticket 14) and the fingerprint (ticket 15) lean on, because they
carry `LocaleKeyBase` errors.

This is the "replace `Error`'s type with `LocaleKeyBase` + bring in `LocaleFormattedKey`" decision from the Stage 2
grilling. No slash-command behaviour is added here; this ticket only makes the error pipeline typed and localizable.

### Domain / storage

```csharp
// LLM/Domain/ChatMessage.cs
public LocaleKeyBase? Error { get; set; }        // was string?

// Data/ChatModels/MessageModel.cs
public LocaleKeyBase? Error { get; set; }        // was string? — a ToolCallModel.Title precedent already exists
```

`MessageDatabaseSynchronizer` must copy `Error` back and forth for **every** role. Today it does so only inside the
`AssistantMessage` branch, so a `UserMessage` (which is exactly what a command message is) would not persist its error
at all — fix that in both `CreateFromModel` and `CopyToModel`.

`ChatExecutionService` currently assigns raw text (`domainResponseMessage.Error = ex.ToString()`): wrap it as
`Locale.GetConstKey(ex.ToString())` (the exception text is not a key).

### `LocaleFormattedKey`

A new `LocaleKeyBase` that carries immutable format arguments and renders itself through `string.Format`:

```csharp
public sealed class LocaleFormattedKey(string key, ImmutableArray<string?> formatArgs) : LocaleKeyBase(key)
{
    public ImmutableArray<string?> FormatArgs { get; } = formatArgs;
    // RawValue: string.Format(LocalizationManager.TryLocalizeStatic(Key), FormatArgs) — rendered lazily and cached.
}
```

- It caches its **own** rendered value and invalidates that cache (raising `PropertyChanged`) when the language changes,
  exactly like `LocaleKey` does — but it is **not** cached as an instance (the facade always returns a new object, since
  the arguments vary); the arguments are immutable, so the instance is a stable value.
- `Equals`/`GetHashCode` are by `(Key, FormatArgs)`.
- Facade: `Locale.GetFormattedKey(string key, params string?[] args)`.

### Serialization (both encodings)

`LocaleKeyBase` already round-trips in BSON (`LiteDB_BSON_SerializerConfig.RegisterType<LocaleKeyBase>`, discriminator
`type` = `const` / `default` + `key`) and in JSON (`JsonLocaleKeyConverter`). Extend **both** with a third
discriminator `formatted` that also stores the arguments array:

```
{ "type": "formatted", "key": "…", "args": ["…", null, …] }
```

`const` / `default` stay as they are.

### UI

`MessageViewModelBase.Error` becomes `LocaleKeyBase?`. The two desktop views that paint it (`UserMessageView`,
`AssistantMessageView`) bind through `LocExtension` (`{loc:Loc {Binding Error}}`, reactive to a language switch) and
gate visibility on a non-empty check that works for a key (a `bool` exposed by the VM or a non-empty converter — no
`StringNonEmptyToBooleanConverter` on a key). The Blazor `AssistantMessageComponent` reads `Message.Error` as a string
today and must be adapted (`Message.Error is not null` → `Message.Error.Value`).

## Acceptance criteria

- [ ] `ChatMessage.Error`, `MessageModel.Error` and `MessageViewModelBase.Error` are `LocaleKeyBase?`.
- [ ] `MessageDatabaseSynchronizer` persists and restores `Error` for **all** message roles (assistant **and** user).
- [ ] `ChatExecutionService` stores a generation failure as `Locale.GetConstKey(ex.ToString())`.
- [ ] `LocaleFormattedKey` exists with `ImmutableArray<string?> FormatArgs`, caches its own value, invalidates on
      language change, is **not** cached by the facade, and compares by `(Key, FormatArgs)`.
- [ ] `Locale.GetFormattedKey(string key, params string?[] args)` exists.
- [ ] BSON and JSON round-trip a `LocaleFormattedKey` (key + args), and still round-trip `LocaleKey`/`ConstLocaleKey`.
- [ ] Desktop message views render the error through the localization layer; Blazor's assistant component compiles and
      shows the resolved text.
- [ ] The solution builds (main + desktop + Blazor); the full test suite stays green; new unit tests cover
      `LocaleFormattedKey` rendering/caching and its BSON/JSON round-trip.

## Answer

<!-- appended on resolution -->

## Comments

- Stage 2 grilling: `Error` becomes a key rather than a localized string; `LocaleFormattedKey` is a distinct type (not
  an overload of `LocaleKey`) because the format arguments are part of the value identity and must survive persistence.
