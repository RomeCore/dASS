# AR-0014 - An Android APK built from this project cannot start: a service's static constructor initialises Roslyn scripting, which Android cannot do

- **Status:** open
- **Severity:** visible
- **Lane:** agent
- **Surface:** Android runtime startup - eager DI resolution in `ServiceRegistry.Initialize` + `CSharpScriptService`
- **First seen:** 2026-10-18

## What I expected

An APK produced by this project's own Android head should install and launch. Two paths promise that: `dotnet build -c Release` (self-contained APK with embedded assemblies and AOT) and `dotnet build -c Debug -p:EmbedAssembliesIntoApk=true` (the same, without AOT). Both complete with `Build succeeded` and produce a signed `*-Signed.apk`.

A debug deploy from an IDE works, so the app itself is not broken - only the packaging path is.

## What happened

Three observations from one session, all on the Android head:

1. **The default `bin/Debug` APK is not installable on a real device and is not self-contained.** It is a FastDev build: `lib/` contains only the ABI of the attached device (x86_64 in this case), `libassembly-store.so` is absent, and the APK holds no managed assemblies at all - they are pushed separately into `files/.__override__/<abi>/`. On a phone it fails with `INSTALL_FAILED_NO_MATCHING_ABIS: Failed to extract native libraries, res=-113`; even on a matching ABI it would not run standalone.

2. **The Release APK installs and crashes at startup, before `MainActivity`.** On an x86_64 emulator:

   ```
   adb install -r ...-Signed.apk      -> Success
   (launch)
   android.runtime.JavaProxyThrowable: [System.TypeInitializationException]:
       TypeInitialization_Type, LLMDesktopAssistant.Scripting.CSX.CSharpScriptService
     at System.Reflection.RuntimeConstructorInfo.Invoke
     at Microsoft.Extensions.DependencyInjection...VisitConstructor
     at LLMDesktopAssistant.Services.ServiceRegistry.Initialize
     at LLMDesktopAssistant.App.Initialize
     at Avalonia.AppBuilder.Setup
   ```

3. **A Debug build with embedded assemblies for 32-bit ARM installs and dies with a native crash in the same code path.** `debug.mono.log=all` shows the last assemblies loaded before `SIGSEGV` in `libmonosgen-2.0.so`:

   ```
   individual_assemblies_open_from_bundles: looking for bundled name: 'Microsoft.CodeAnalysis.Scripting.dll'
   individual_assemblies_open_from_bundles: looking for bundled name: 'Microsoft.CodeAnalysis.dll'
   individual_assemblies_open_from_bundles: looking for bundled name: 'System.Reflection.Metadata.dll'
   F/libc: Fatal signal 11 (SIGSEGV), code 2 (SEGV_ACCERR)
   ```

The common cause (traced in this repo) is the static field of `src/LLMDesktopAssistant/Scripting/CSX/CSharpScriptService.cs`:

```csharp
private static readonly ScriptOptions Options = ScriptOptions.Default
    .AddReferences(GetLoadedAssemblies())
    .WithImports(...);
```

Roslyn scripting is initialised eagerly in the type initializer of a service that **nothing uses on Android**. It fails there because Android assemblies have no usable `Assembly.Location` (assembly store / bundle), and because a full-AOT Android build has no runtime IL compilation anyway. The type initializer turns that into a fatal failure of the whole app, because DI resolves every registered service at startup:

```csharp
// ServiceRegistry.Initialize
foreach (var service in collection)
{
    if (service.ServiceType.ContainsGenericParameters) continue;
    _serviceProvider.GetServices(service.ServiceType);   // constructs every service eagerly
}
```

## Why it costs

- **Every APK build is dead on arrival on Android**, in Release and in Debug-with-embedded-assemblies alike. Only a FastDev deploy from an IDE hides it, so the defect is invisible exactly where most testing happens.
- **The failure is undiagnosable from the message.** `TypeInitialization_Type, <type>` names the type but not the inner exception; `android:debuggable` is off in Release and the managed trace is replaced by `Android.Runtime.JavaProxyThrowable`. On the 32-bit run it is not even a managed exception - it is a `SIGSEGV` with no managed frames at all.
- **The path to the cause is a build bisect, not a stack trace.** Distinguishing "assembly store vs AOT vs Roslyn" required building Debug with `EmbedAssembliesIntoApk=true`, installing on a phone, and reading `debug.mono.log`.
- **One unsafe static initializer costs the entire app.** Any service whose type initializer throws prevents the first window from appearing, with no degradation path.

## Repro

Smallest sequence on any Android device or emulator:

```
1. dotnet build src/LLMDesktopAssistant.Android/LLMDesktopAssistant.Android.csproj -c Release
2. adb install -r src/LLMDesktopAssistant.Android/bin/Release/net10.0-android/com.RomeCore.LLMDesktopAssistant-Signed.apk
3. launch the app
4. adb logcat -d -b crash
   -> TypeInitializationException: TypeInitialization_Type, LLMDesktopAssistant.Scripting.CSX.CSharpScriptService
      at LLMDesktopAssistant.Services.ServiceRegistry.Initialize
```

For the native variant, build Debug for `android-arm` with `EmbedAssembliesIntoApk=true` and set `debug.mono.log=all` before launching.

## Witnesses

- **2026-10-18** - Android phone/emulator session - a Release APK installed on a Pixel 7 API 36 emulator crashed at startup with the managed `TypeInitializationException` above; the same service's static initializer produced a native `SIGSEGV` on a 32-bit Redmi 9C (arm32 Debug build with embedded assemblies). Nothing in the app's UI was ever reached.

## What I did instead

Traced the failing type from the logcat class name to `CSharpScriptService`, then confirmed the difference between working and broken builds by comparing how each loads assemblies:

- FastDev debug: managed assemblies live as files in `files/.__override__/<abi>/` - the static initializer succeeds.
- Release: assemblies live in `libassembly-store.so`; `Assembly.Location` is not a file path.
- Debug + `EmbedAssembliesIntoApk=true`: assemblies are mmap'ed from the APK/bundle (`individual_assemblies_open_from_bundles`), and the initializer still fails.
- The arm32 build additionally has no `libonigwrap.so` (TextMateSharp ships arm64/x64 only), so syntax highlighting has a second, independent problem on that ABI.

Workaround in the session: none inside the app - the APK simply cannot be used. The diagnosis was done externally with `adb install`, `adb logcat`, `debug.mono.log=all`, `apksigner verify`, and by listing the APK zip contents.

## Proposed fix

1. **Do not initialise Roslyn in a static field.** Build `ScriptOptions` in a lazily-created, `try/catch`-guarded factory and keep the failure as state; `RunAsync` then reports "CSX scripting is unavailable on this platform" instead of taking the process down. This alone makes both APK paths start.
2. **Make DI resolution non-fatal.** Wrap the per-service `GetServices` call in `ServiceRegistry.Initialize` in `try/catch`, log the exception, and keep going (or resolve the rest lazily). One broken service should not be able to prevent the first window.
3. **Stop shipping a Debug APK that cannot be installed standalone**, or say so loudly: either default `EmbedAssembliesIntoApk=true` for Debug APKs, or document that the `bin/Debug` APK is a FastDev artifact tied to the attached device's ABI.
4. (Optional, larger) Decide the CSX story on Android explicitly. Even with (1), `Script.RunAsync` needs runtime IL compilation, so on a full-AOT build CSX can never work; on a Mono build it still needs assemblies on disk. Gating the feature per platform is cheaper than making Roslyn fit.

## Unverified

- The exact inner exception on arm64 was not observed: `JavaProxyThrowable` reported only `TypeInitialization_Type`, and the managed trace was not printed (the app logs `liblog` as not loaded, so `__android_log_print` p/invoke fails).
- Whether the arm32 `SIGSEGV` is the same defect or an additional Mono-on-arm32 bug. The assembly-load trace immediately before the crash points at the Roslyn initializer, but no managed frame was captured.
- Whether AOT contributes to the arm64 failure. The Release build has `aotassemblies=true` / `androidaotmode=normal`; a `-p:RunAOTCompilation=false` build was **not** tested, so "AOT vs assembly store" was not separated.
- "`Assembly.Location` is empty in the assembly store" is inferred from .NET Android behaviour, not observed directly.
- Which of the two - Roslyn's own `ScriptOptions` static initializer or `AddReferences(GetLoadedAssemblies())` - throws first. `ScriptOptions.Default` was read from Roslyn sources (traced); the failure point inside it was not.
- 32-bit ARM support in .NET 10 is broken independently of this bug: `Microsoft.NETCore.App.Runtime.linux-bionic-arm` does not exist on NuGet at all (the Android-Mono runtime pack does), so `-p:RuntimeIdentifiers=android-arm` fails restore with `NU1101` unless the RID is set only on the Android project. This is recorded here because it was found while reproducing, not because it is this report's problem.
