using System.Text;

namespace DosBoxControl.Native;

internal static class WindowFinder
{
    public static IReadOnlyList<nint> FindTopLevelWindowsForProcess(int processId)
    {
        var result = new List<nint>();
        Win32.EnumWindows((hWnd, _) =>
        {
            Win32.GetWindowThreadProcessId(hWnd, out uint pid);
            if (pid == (uint)processId && Win32.IsWindowVisible(hWnd))
            {
                result.Add(hWnd);
            }
            return true;
        }, 0);
        return result;
    }

    public static string GetWindowTitle(nint hWnd)
    {
        var sb = new StringBuilder(256);
        Win32.GetWindowText(hWnd, sb, sb.Capacity);
        return sb.ToString();
    }

    public static string GetWindowClassName(nint hWnd)
    {
        var sb = new StringBuilder(256);
        Win32.GetClassName(hWnd, sb, sb.Capacity);
        return sb.ToString();
    }

    /// <summary>
    /// DOSBox spawns two top-level windows: the SDL render window ("SDL_app") that
    /// actually displays the game, and (on builds with the debug console enabled) a
    /// "DOSBox Status Window" console (class "ConsoleWindowClass"). We always want the
    /// former for screenshots/input.
    /// </summary>
    public static nint? FindDosBoxRenderWindow(int processId)
    {
        var candidates = FindTopLevelWindowsForProcess(processId)
            .Select(w => (Handle: w, Title: GetWindowTitle(w), ClassName: GetWindowClassName(w)))
            .Where(w => !string.IsNullOrWhiteSpace(w.Title))
            .ToList();

        var sdlWindow = candidates.FirstOrDefault(w => w.ClassName == "SDL_app");
        if (sdlWindow.Handle != 0)
        {
            return sdlWindow.Handle;
        }

        var nonConsole = candidates.FirstOrDefault(w => w.ClassName != "ConsoleWindowClass");
        return nonConsole.Handle != 0 ? nonConsole.Handle : null;
    }

    /// <summary>
    /// Brings a window to the foreground even when the calling process does not
    /// currently own the foreground, by temporarily attaching input queues.
    /// </summary>
    public static void ForceForeground(nint hWnd)
    {
        if (Win32.IsIconic(hWnd))
        {
            Win32.ShowWindow(hWnd, Win32.SW_RESTORE);
        }

        nint foreground = Win32.GetForegroundWindow();
        uint foregroundThread = Win32.GetWindowThreadProcessId(foreground, out _);
        uint targetThread = Win32.GetWindowThreadProcessId(hWnd, out _);
        uint currentThread = Win32.GetCurrentThreadId();

        bool attachedToForeground = foregroundThread != currentThread && Win32.AttachThreadInput(currentThread, foregroundThread, true);
        bool attachedToTarget = targetThread != currentThread && Win32.AttachThreadInput(currentThread, targetThread, true);

        try
        {
            Win32.BringWindowToTop(hWnd);
            Win32.ShowWindow(hWnd, Win32.SW_SHOW);
            Win32.SetForegroundWindow(hWnd);
        }
        finally
        {
            if (attachedToTarget)
            {
                Win32.AttachThreadInput(currentThread, targetThread, false);
            }
            if (attachedToForeground)
            {
                Win32.AttachThreadInput(currentThread, foregroundThread, false);
            }
        }
    }
}
