using System.Runtime.InteropServices;
using Lemon.Template.Wpf.Infrastructures.Data;

namespace Lemon.Template.Wpf.Infrastructures.Shell;

/// <summary>
/// One instance per login session. Required once the window can hide in the tray: a user who cannot see the
/// app starts it again, and two instances would each write the same settings database.
/// </summary>
internal static class SingleInstance
{
    // Local\ = one lock per login session, so another user on the same machine (Remote Desktop) runs their own copy.
    private static readonly string MutexName = $@"Local\{AppSqlitePaths.ApplicationName}.SingleInstance";

    /// <summary>
    /// RegisterWindowMessage returns the same system-wide number for the same string in every process;
    /// a WM_APP+n value is only unique within one window class and would be misread by other windows on broadcast.
    /// </summary>
    internal static readonly uint ShowExistingMessage =
        RegisterWindowMessage($"{AppSqlitePaths.ApplicationName}.ShowExistingInstance");

    // Must live in a field: a Mutex held only by a local is collected and releases the lock, which makes the
    // guard fail intermittently.
    private static Mutex? _instanceLock;

    /// <summary>True when this process is the first instance in the session and now holds the lock.</summary>
    public static bool TryAcquire()
    {
        _instanceLock = new Mutex(initiallyOwned: true, MutexName, out var isFirst);
        if (!isFirst)
        {
            _instanceLock.Dispose();
            _instanceLock = null;
        }

        return isFirst;
    }

    /// <summary>Asks the running instance to show its main window (it may be hidden in the tray).</summary>
    public static void SignalExistingInstance()
    {
        // Only the foreground process may bring a window to the front; anyone else just flashes the taskbar.
        // This process was just started by the user, so it is in the foreground: hand that right over.
        AllowSetForegroundWindow(AsfwAny);

        // Broadcast instead of looking up the window: it may be hidden, and matching by title or class is fragile.
        PostMessage(HwndBroadcast, ShowExistingMessage, IntPtr.Zero, IntPtr.Zero);
    }

    /// <summary>Called on the thread that acquired the lock (the UI thread), as a Mutex has thread affinity.</summary>
    public static void Release()
    {
        var instanceLock = Interlocked.Exchange(ref _instanceLock, null);
        if (instanceLock is null)
        {
            return;
        }

        try
        {
            instanceLock.ReleaseMutex();
        }
        catch (ApplicationException)
        {
            // Not owned by this thread; the OS releases it when the process ends anyway.
        }

        instanceLock.Dispose();
    }

    private const int AsfwAny = -1;
    private static readonly IntPtr HwndBroadcast = new(0xFFFF);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint RegisterWindowMessage(string lpString);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AllowSetForegroundWindow(int dwProcessId);
}
