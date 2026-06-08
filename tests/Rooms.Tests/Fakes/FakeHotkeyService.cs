using Rooms.Application.Hotkeys;

namespace Rooms.Tests.Fakes;

/// <summary>Records hotkey-service calls so tests can assert the seeder re-armed bindings.</summary>
public sealed class FakeHotkeyService : IHotkeyService
{
    public int ReloadCount { get; private set; }
    public int SuspendCount { get; private set; }

    public Task InitializeAsync(CancellationToken ct = default) => ReloadAsync(ct);

    public Task ReloadAsync(CancellationToken ct = default)
    {
        ReloadCount++;
        return Task.CompletedTask;
    }

    public void Suspend() => SuspendCount++;

#pragma warning disable CS0067 // events unused in tests
    public event EventHandler? SwitcherHotkeyPressed;
    public event EventHandler? ToggleFocusPressed;
#pragma warning restore CS0067
}
