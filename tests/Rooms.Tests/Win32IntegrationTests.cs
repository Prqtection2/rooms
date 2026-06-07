using System.Diagnostics;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Rooms.Os.Windows;

namespace Rooms.Tests;

/// <summary>
/// Thin OS-layer integration test (§15). Manipulates a real window, so it is OPT-IN: it runs only
/// when the environment variable ROOMS_RUN_INTEGRATION=1 is set (otherwise it passes as a no-op).
/// Run with:  $env:ROOMS_RUN_INTEGRATION=1; dotnet test
/// Note: on Windows 11 the modern Notepad may host its window in a different process; if so this
/// test is best run against classic notepad or another simple app.
/// </summary>
public class Win32IntegrationTests
{
    private static bool Enabled =>
        Environment.GetEnvironmentVariable("ROOMS_RUN_INTEGRATION") == "1";

    [Fact]
    public void Enumerate_hide_and_restore_a_real_window()
    {
        if (!Enabled)
            return; // opt-in only

        var service = new Win32WindowService(NullLogger<Win32WindowService>.Instance);
        var notepad = Process.Start("notepad.exe")!;
        try
        {
            var handle = IntPtr.Zero;
            for (var i = 0; i < 50 && handle == IntPtr.Zero; i++)
            {
                Thread.Sleep(100);
                var match = service.EnumerateTopLevelWindows().FirstOrDefault(w => w.ProcessId == notepad.Id);
                handle = match?.Handle ?? IntPtr.Zero;
            }

            handle.Should().NotBe(IntPtr.Zero, "the Notepad window should appear");

            service.Hide(handle);
            service.EnumerateTopLevelWindows().Any(w => w.Handle == handle && w.IsVisible)
                .Should().BeFalse("the window should be hidden");

            service.RestoreAllHidden();
            service.EnumerateTopLevelWindows().Any(w => w.Handle == handle && w.IsVisible)
                .Should().BeTrue("the window should be restored");
        }
        finally
        {
            try { notepad.Kill(); } catch { /* best effort */ }
        }
    }
}
