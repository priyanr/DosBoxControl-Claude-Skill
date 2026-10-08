using System.Diagnostics;
using DosBoxControl.Native;

namespace DosBoxControl.Services;

public sealed class DosBoxSession
{
    public required Process Process { get; init; }
    public required string ExePath { get; init; }
    public nint WindowHandle { get; set; }
    public string WindowTitle { get; set; } = string.Empty;
}

/// <summary>
/// Launches DOSBox against a target executable, locates its window, and tracks
/// the single active session so screenshot/keys/close calls know what to target.
/// </summary>
public sealed class DosBoxSessionManager
{
    private readonly string _dosboxDir;
    private readonly ILogger<DosBoxSessionManager> _logger;
    private DosBoxSession? _session;
    private readonly object _lock = new();

    public DosBoxSessionManager(IConfiguration configuration, ILogger<DosBoxSessionManager> logger)
    {
        _dosboxDir = configuration["DosBox:InstallDir"]
            ?? throw new InvalidOperationException("DosBox:InstallDir is not configured.");
        _logger = logger;
    }

    public DosBoxSession? Current
    {
        get
        {
            lock (_lock)
            {
                if (_session is { } s && s.Process.HasExited)
                {
                    _session = null;
                }
                return _session;
            }
        }
    }

    public async Task<DosBoxSession> OpenAsync(string exePath, string? extraArgs, bool fullscreen, CancellationToken ct)
    {
        if (!File.Exists(exePath))
        {
            throw new FileNotFoundException($"Executable not found: {exePath}", exePath);
        }

        var existing = Current;
        if (existing is not null)
        {
            _logger.LogInformation("Closing previous DOSBox session (pid {Pid}) before opening a new one.", existing.Process.Id);
            await CloseAsync(force: true, ct);
        }

        string dosboxExe = Path.Combine(_dosboxDir, "DOSBox.exe");
        if (!File.Exists(dosboxExe))
        {
            throw new FileNotFoundException($"DOSBox.exe not found in configured install dir: {_dosboxDir}", dosboxExe);
        }

        var args = new List<string> { Quote(exePath) };
        if (fullscreen)
        {
            args.Add("-fullscreen");
        }
        if (!string.IsNullOrWhiteSpace(extraArgs))
        {
            args.Add(extraArgs);
        }

        var psi = new ProcessStartInfo
        {
            FileName = dosboxExe,
            Arguments = string.Join(' ', args),
            WorkingDirectory = _dosboxDir,
            UseShellExecute = false,
        };

        var process = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start DOSBox.exe");

        var session = new DosBoxSession { Process = process, ExePath = exePath };

        var (hWnd, title) = await WaitForWindowAsync(process.Id, ct);
        session.WindowHandle = hWnd;
        session.WindowTitle = title;

        lock (_lock)
        {
            _session = session;
        }

        return session;
    }

    public async Task CloseAsync(bool force, CancellationToken ct)
    {
        DosBoxSession? session;
        lock (_lock)
        {
            session = _session;
        }

        if (session is null || session.Process.HasExited)
        {
            lock (_lock) { _session = null; }
            return;
        }

        if (!force && session.WindowHandle != 0)
        {
            Win32.PostMessage(session.WindowHandle, Win32.WM_CLOSE, 0, 0);
            try
            {
                await session.Process.WaitForExitAsync(LinkTimeout(ct, TimeSpan.FromSeconds(3)));
            }
            catch (OperationCanceledException)
            {
                // fall through to force-kill
            }
        }

        if (!session.Process.HasExited)
        {
            session.Process.Kill(entireProcessTree: true);
            try { await session.Process.WaitForExitAsync(ct); } catch { /* ignore */ }
        }

        lock (_lock) { _session = null; }
    }

    private static CancellationToken LinkTimeout(CancellationToken ct, TimeSpan timeout)
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeout);
        return cts.Token;
    }

    private async Task<(nint hWnd, string title)> WaitForWindowAsync(int processId, CancellationToken ct)
    {
        var deadline = DateTime.UtcNow.AddSeconds(8);
        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();

            var renderWindow = WindowFinder.FindDosBoxRenderWindow(processId);
            if (renderWindow is { } hWnd)
            {
                return (hWnd, WindowFinder.GetWindowTitle(hWnd));
            }

            await Task.Delay(150, ct);
        }

        throw new TimeoutException($"Timed out waiting for DOSBox window to appear for process {processId}.");
    }

    private static string Quote(string s) => s.Contains(' ') ? $"\"{s}\"" : s;
}
