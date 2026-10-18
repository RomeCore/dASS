# Host the Blazor WebUI from an Android device

Status: open
Type: task
Blocked by:

## Requested

- The WebUI (Blazor Server) should be hostable from the Android build of dASS, not only from the desktop build.
- The hosted UI should be reachable from other devices on the same local network, not only from the phone itself.
- A public proof of concept exists — an Avalonia application hosting a full server-side Blazor app on Android — and it is the reference shape the work should follow.
- Whether the WebUI project is restructured for this, and whether the Android build keeps AOT compilation, was left open.

## Proposed design

- **The supported path does not exist; the proof-of-concept path does.** ASP.NET Core ships only as a shared framework and has no runtime pack for Android targets, so a framework reference fails at build time. The working approach references the ASP.NET Core assemblies from the build machine's .NET installation and packages them into the APK as ordinary managed assemblies.
- **The injection is a build target, not a manual copy.** The reference implementation takes the shared framework's assemblies, excludes a fixed list of ones already supplied by NuGet packages or irrelevant on device, and marks the rest as copied to the output.
- **The WebUI project stops being a web application.** It becomes a Razor class library: no web SDK, no entry point, static web assets embedded as resources and served through an embedded file provider instead of the generated static-asset endpoint manifest. The hosting code itself — builder, Kestrel, interactive server components, cookie authentication, antiforgery, controllers — stays unchanged.
- **Static framework assets still have to arrive somehow.** The reference implementation syncs them from a NuGet package during build rather than relying on the web SDK; the same step is needed for the browser-side Blazor script.
- **The Android head gives up AOT and framework trimming for this.** Assemblies are embedded in the APK, AOT and the publish-time AOT switch are off, and the link mode is restricted so injected framework assemblies are not trimmed away. The APK grows by the framework portion; that cost is accepted.
- **The build becomes coupled to the build machine's .NET installation.** Injected assembly versions come from the installed shared framework, so the Android head needs that installation present at its version. This is the main portability cost of the approach and should be stated in the build documentation.
- **The server must be owned by a foreground service, not by the chat-scoped starter.** Android freezes backgrounded processes, so the web host needs a foreground service with the corresponding service type, notification permission and a wake lock to stay reachable with the screen off. The chat-scoped start/stop contract is kept; it delegates lifetime to the service instead of owning it.
- **The endpoint must be an explicit listen address, not only a URL string.** Network reachability requires binding to any address; a loopback-only bind makes the feature pointless on a phone.
- **Plain HTTP is accepted for the first iteration, with the exposure stated.** Existing cookie authentication keeps working, but over a local network it travels unencrypted. This is a deployment caveat, not a design change.

## Alternatives considered

- **Blazor WebAssembly in a WebView.** No server is needed at all, but the current UI is server-interactive — circuits, server-side dependency injection, cookie authentication, antiforgery — so this is a rewrite of the WebUI plus a bridge to the host, not a hosting change.
- **Blazor Hybrid through MAUI.** A different UI stack and a WebView renderer rather than a server; not applicable to an Avalonia application.

## Prerequisite

The Android build cannot currently start from an APK at all: a service's type initializer initialises Roslyn scripting, which fails on Android, and every self-contained APK dies before the first window (AR-0014). Hosting a web server in the same process is meaningless until that is fixed.

## Not in scope

- 32-bit ARM devices. The current .NET Android runtime has no 32-bit bionic runtime pack, and the assembly-injection approach inherits that limitation; a 64-bit device is assumed.
- A hardened network posture: TLS with a certificate, per-client authorization beyond the existing cookie login, or exposing the server beyond the local network.
- Store distribution, where a foreground service used for a persistent server requires a policy justification.

## Comments

Reference implementation: `russkyc/avalonia-blazor-server`, which credits `JamesNK/aspnetcore-maui` for the original workarounds.
