# Rooms

A lightweight Windows app for creating isolated **rooms** — focused window environments with
their own apps, layout, and optional distraction-blocking — and switching between them with
near-zero friction from the tray or a global hotkey.

## ⬇️ Download & install

**Requirements:** Windows 10 or 11 (64-bit). Nothing else — .NET is bundled in, so there's no runtime to install.

Grab the latest build from the **[Releases page](https://github.com/Prqtection2/rooms/releases/latest)**, then pick one:

| | Download | What it does |
|---|---|---|
| **Portable** *(easiest)* | **[Rooms.exe](https://github.com/Prqtection2/rooms/releases/latest/download/Rooms.exe)** | One file. Double-click to run — no install. Put it anywhere (Desktop, a folder…). |
| **Installer** | **[RoomsSetup.exe](https://github.com/Prqtection2/rooms/releases/latest/download/RoomsSetup.exe)** | Adds a Start-menu shortcut, an uninstaller, and an optional "start at sign-in". |

> [!NOTE]
> The downloads are **not code-signed**, so the first time you run one, Windows **SmartScreen** may say *"Windows protected your PC."* That's expected for a new indie app — click **More info → Run anyway**.

**First run:** Rooms starts in the **system tray** (a blue **R** icon). Windows 11 hides new tray icons, so click the **`^` chevron** near the clock to find it.

- **Left-click** the icon → quick room switcher
- **Double-click**, or press **Ctrl + Alt + R** → full overlay
- **Right-click** → menu (focus session, reset room, show all, settings, exit)

**Uninstall:** if you used the installer, use *Add/Remove Programs*. For the portable exe, just delete it. Your rooms/settings live in `%AppData%\Rooms\` — delete that folder to remove them too.

*(Want to build it yourself instead? See [Build from source](#build-test-run) below.)*

---

> **Status: §4–§15 implemented (build milestones M0–M9 done; M10 packaging scripted).** The full
> layered architecture, DI, logging, persistence, and a working WPF UI build and run; **43 tests
> pass**. Implemented:
> - **Domain model (§4)** and **OS interaction layer (§5)** — window enumeration with Alt-Tab
>   filtering, hide/show/placement, executable-path resolution, WinEvent hooks, global
>   hotkeys, process launch/close/kill, wallpaper.
> - **Switch engine (§6)** — the `WindowRegistry` ownership model (launched / matched / manual /
>   sticky), the full switch algorithm (desired-set diff, batch gating to avoid WinEvent
>   feedback loops, async launch, ambient + foreground), stale-handle `IsWindow` guards, and the
>   **lost-window failsafe** (hidden-handle journal + clean-exit restore).
> - **Application services (§7)** — RoomManager, SwitchOrchestrator, HotkeyService
>   (switch/next/previous/switcher), FocusSessionService (countdown ticks), the reactive
>   RuleEngine enforcer, and StartupService.
> - **App blocking (§8)** — the reactive RuleEngine applies a room's block reaction
>   (warn / minimize / close-gracefully) to disallowed windows, with a constant **safe-list**
>   so an allowlist never fights Explorer or system UI.
> - **Website blocking (§9)** — pluggable `IWebsiteBlocker`. Option A (browser-process gating)
>   is the RuleEngine; Option B (hosts-file rewriting in a delimited managed block, with DNS
>   flush) ships and self-disables when not elevated. Honest about its limits (VPN / DoH /
>   direct-IP bypasses). Option C (browser extension) is post-v1.
> - **Focus / friction (§10)** — a locked focus session (`AllowEarlyExit == false`) makes the
>   SwitchOrchestrator refuse to switch away, returning a "blocked" result; the UI prompts
>   "End focus early?" and the focus bar offers a surrenderable **Give up**.
> - **Persistence (§11)** — single versioned `rooms.json`, `settings.json` (`HotkeyBinding`
>   list + policies), and `state.json` (last-active room + lost-window failsafe). Atomic writes,
>   schema-version envelope + migration hook.
> - **UI (§12, WPF + CommunityToolkit.Mvvm)** — distinctive generated tray icon, tray popup
>   switcher, keyboard-driven switcher overlay (↑↓ / 1-9 / Enter / Esc), room editor (incl.
>   **capture open windows / pick-a-window**), settings with **live hotkey capture + conflict
>   detection**, and the focus-session bar. MVVM: `ActiveRoomChanged` and focus ticks bind
>   straight into view-models.
> - **Focus re-assertion (§10, opt-in)** — when enabled, a locked focus session hides windows
>   that intrude on the focus room as they appear (friction-based, bypassable).
> - **Bootstrap (§13)** — Generic Host composition root; **single-instance** named mutex; website
>   blocking defaults to Option A and is swappable to Option B from Settings.
> - **Testing (§15)** — application layer fully faked + unit-tested, **idempotency property tests**
>   (A→A is a no-op, A→B→A restores the exact visible set), and an opt-in OS integration test.
> - **Packaging (M10)** — app icon, MIT `LICENSE`, an Inno Setup script, and a verified
>   self-contained publish (`Rooms.exe`, no .NET prerequisite on the target).
>
> Remaining niceties: capturing window *placements* (not just process matchers) in the editor,
> and System.Text.Json source-generation (the reflection serializer works today). Skeleton paths
> carry `TODO (§…)` markers.

## Tech stack

| Concern        | Choice                                   |
|----------------|------------------------------------------|
| Language       | C# / .NET 8                              |
| UI             | WPF (MVVM)                               |
| Win32 interop  | [CsWin32](https://github.com/microsoft/CsWin32) source generator |
| Tray icon      | H.NotifyIcon.Wpf                         |
| Global hotkeys | `RegisterHotKey` on a dedicated message-pump thread |
| Host / DI      | Microsoft.Extensions.Hosting             |
| Logging        | Serilog (file + debug sinks)             |
| Config         | System.Text.Json (one file per room)     |
| Tests          | xUnit + FluentAssertions                 |

## Architecture

Strict downward dependency: **UI → Application → (OS, Persistence)**. Lower layers never
reference upper ones. The OS and Persistence layers are reached only through interfaces, so
they are faked in tests. The golden rule: **only the OS layer touches Win32.**

```
src/
  Rooms.Core         Domain models + cross-layer interfaces (ports). No dependencies.
  Rooms.Application  RoomManager, SwitchOrchestrator (the §6 switch engine), WindowRegistry,
                     SwitchGate, RuleEngine + RuleEnforcer, FocusSessionService, HotkeyService,
                     StartupService. Orchestration + rules. No Win32.
  Rooms.Os           Win32 adapters via CsWin32: window service (enum/hide/show/place +
                     WinEvent hooks), process service, hotkey registrar, startup registry.
  Rooms.Persistence  JSON file stores (IRoomStore, IAppSettingsStore).
  Rooms.App          WPF tray app + the composition root that wires it all together.
tests/
  Rooms.Tests        xUnit smoke/regression tests over the application + persistence layers.
```

Interfaces live in `Rooms.Core.Abstractions`; each adapter project exposes an
`AddRooms…()` DI extension that the `Rooms.App` composition root calls.

## Build, test, run

```powershell
dotnet build Rooms.sln
dotnet test  Rooms.sln                       # 53 tests
dotnet run --project src/Rooms.App           # runs Rooms.exe in the tray
```

The app starts in the system tray with a blue **"R"** icon (Windows 11 hides new tray icons —
click the `^` chevron near the clock). **Left-click** the icon for the popup switcher,
**double-click** or press **Ctrl+Alt+R** for the overlay, **right-click** for the menu. Only one
instance runs at a time. The opt-in OS integration test runs with
`$env:ROOMS_RUN_INTEGRATION=1; dotnet test`.

## Packaging & releasing (M10)

**One command** builds the distributable, self-contained `Rooms.exe` (no .NET on the target):

```powershell
./scripts/publish.ps1                 # -> dist\Rooms.exe   (portable, ~64 MB)
./scripts/publish.ps1 -Installer      # also dist\RoomsSetup.exe  (needs Inno Setup 6)
./scripts/publish.ps1 -Version 1.2.0  # stamp a specific version
```

**Automated releases:** pushing a version tag runs [`.github/workflows/release.yml`](.github/workflows/release.yml),
which tests, builds both the portable exe and the installer, and attaches them to a GitHub
Release — giving permanent `…/releases/latest/download/Rooms.exe` links:

```powershell
git tag v1.0.0
git push origin v1.0.0
```

Per-user install needs no admin; hosts-file website blocking (Option B) still needs elevation at
runtime. The downloads are unsigned — see the SmartScreen note under [Download & install](#️-download--install).

## Data locations (`%AppData%\Rooms\`)

- `rooms.json` — all rooms (versioned envelope)
- `settings.json` — hotkey bindings, policies, startup, default room
- `state.json` — last active room + lost-window failsafe set
- `logs\rooms-<date>.log`
