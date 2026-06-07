using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Rooms.Core.Abstractions;
using Rooms.Core.Models;
using Windows.Win32;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Rooms.Os.Windows;

/// <summary>
/// Registers global hotkeys via Win32 RegisterHotKey. Because thread-associated hotkeys
/// deliver WM_HOTKEY to the registering thread's message queue, all registration and the
/// message pump run on one dedicated background thread - no window class required.
/// </summary>
public sealed class Win32HotkeyRegistrar : IHotkeyRegistrar, IDisposable
{
    private const uint WM_QUIT = 0x0012;
    private const uint WM_HOTKEY = 0x0312;
    private const uint WM_APP = 0x8000; // used to wake the pump to run pending work
    private const uint MOD_NOREPEAT = 0x4000;

    private readonly ILogger<Win32HotkeyRegistrar> _logger;
    private readonly Thread _thread;
    private readonly ManualResetEventSlim _ready = new(false);
    private readonly ConcurrentQueue<Action> _pending = new();

    private uint _threadId;
    private int _nextId;
    private bool _disposed;

    public Win32HotkeyRegistrar(ILogger<Win32HotkeyRegistrar> logger)
    {
        _logger = logger;
        _thread = new Thread(RunMessageLoop)
        {
            IsBackground = true,
            Name = "Rooms.Hotkeys",
        };
        _thread.Start();
    }

    public event EventHandler<int>? HotkeyPressed;

    public int Register(HotkeyDefinition hotkey)
    {
        if (_disposed || !_ready.Wait(TimeSpan.FromSeconds(2)))
            return -1;

        var id = Interlocked.Increment(ref _nextId);
        var success = false;

        RunOnLoopThread(() =>
        {
            var modifiers = (HOT_KEY_MODIFIERS)((uint)hotkey.Modifiers | MOD_NOREPEAT);
            success = PInvoke.RegisterHotKey(default, id, modifiers, hotkey.VirtualKey);
        });

        if (!success)
        {
            _logger.LogWarning("RegisterHotKey failed for {Hotkey}.", hotkey);
            return -1;
        }

        return id;
    }

    public void Unregister(int hotkeyId)
    {
        if (_disposed || hotkeyId < 0 || !_ready.IsSet)
            return;

        RunOnLoopThread(() => PInvoke.UnregisterHotKey(default, hotkeyId));
    }

    private void RunMessageLoop()
    {
        // Force creation of the thread message queue so PostThreadMessage from other
        // threads succeeds, then publish our thread id and signal readiness.
        PInvoke.PeekMessage(out _, default, 0, 0, PEEK_MESSAGE_REMOVE_TYPE.PM_NOREMOVE);
        _threadId = PInvoke.GetCurrentThreadId();
        _ready.Set();

        while (PInvoke.GetMessage(out var msg, default, 0, 0))
        {
            switch (msg.message)
            {
                case WM_HOTKEY:
                    var id = (int)msg.wParam.Value;
                    HotkeyPressed?.Invoke(this, id);
                    break;

                case WM_APP:
                    while (_pending.TryDequeue(out var work))
                        work();
                    break;
            }
        }
    }

    /// <summary>Marshal an action onto the pump thread and block until it has run.</summary>
    private void RunOnLoopThread(Action action)
    {
        using var done = new ManualResetEventSlim(false);
        _pending.Enqueue(() =>
        {
            try
            {
                action();
            }
            finally
            {
                done.Set();
            }
        });

        PInvoke.PostThreadMessage(_threadId, WM_APP, default, default);
        done.Wait(TimeSpan.FromSeconds(2));
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        if (_ready.IsSet)
            PInvoke.PostThreadMessage(_threadId, WM_QUIT, default, default);

        _thread.Join(TimeSpan.FromSeconds(2));
        _ready.Dispose();
    }
}
