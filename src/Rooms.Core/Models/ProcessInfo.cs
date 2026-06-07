namespace Rooms.Core.Models;

/// <summary>Immutable snapshot of a running process as seen by the OS layer. (§5.2)</summary>
public sealed record ProcessInfo(
    int ProcessId,
    string ProcessName,
    string? ExecutablePath);
