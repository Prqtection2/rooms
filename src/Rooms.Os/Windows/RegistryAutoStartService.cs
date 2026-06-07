using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Rooms.Core.Abstractions;

namespace Rooms.Os.Windows;

/// <summary>
/// Controls launch-at-sign-in via the per-user Run registry key. Per-user (HKCU) needs no
/// elevation, which suits an OSS app installed without admin rights.
/// </summary>
public sealed class RegistryAutoStartService : IAutoStartService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Rooms";

    private readonly ILogger<RegistryAutoStartService> _logger;

    public RegistryAutoStartService(ILogger<RegistryAutoStartService> logger)
    {
        _logger = logger;
    }

    public bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        return key?.GetValue(ValueName) is string value && !string.IsNullOrEmpty(value);
    }

    public void Enable()
    {
        var executablePath = Environment.ProcessPath;
        if (string.IsNullOrEmpty(executablePath))
        {
            _logger.LogWarning("Cannot enable startup: executable path is unknown.");
            return;
        }

        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
        key.SetValue(ValueName, $"\"{executablePath}\"");
        _logger.LogInformation("Enabled launch at startup.");
    }

    public void Disable()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
        if (key?.GetValue(ValueName) is not null)
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
            _logger.LogInformation("Disabled launch at startup.");
        }
    }
}
