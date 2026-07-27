using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;
using LLEAP.UITests.Configuration;
using Serilog;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace LLEAP.UITests.Drivers;

public sealed class AppDriver : IDisposable
{
    private readonly TestSettings _settings = TestSettings.Instance;
    public UIA3Automation Automation { get; } = new();
    private FlaUI.Core.Application? _homeApp;
    private FlaUI.Core.Application? _instructorApp;

    // These properties are initialized by the matching launch/attach methods.
    public Window HomeWindow { get; private set; } = null!;
    public Window InstructorWindow { get; private set; } = null!;

    public Window LaunchSimulationHome()
    {
        EnsureInteractiveDesktop();

        var executablePath = ResolveExecutablePath(_settings.Paths.SimulationHomeExePath);
        if (!File.Exists(executablePath))
        {
            throw new FileNotFoundException(
                $"Simulation Home executable was not found: '{executablePath}'. " +
                "Verify that LLEAP is installed for the account/machine running the automated test.",
                executablePath);
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            WorkingDirectory = Path.GetDirectoryName(executablePath)
                ?? throw new InvalidOperationException($"Could not determine working directory for '{executablePath}'."),
            UseShellExecute = true
        };

        _homeApp = FlaUI.Core.Application.Launch(startInfo);
        HomeWindow = _homeApp.GetMainWindow(
                Automation,
                TimeSpan.FromSeconds(_settings.Timeouts.DefaultTimeoutSeconds))
            ?? throw new TimeoutException(
                $"'{Path.GetFileName(executablePath)}' started as PID {_homeApp.ProcessId}, " +
                $"but no main window appeared within {_settings.Timeouts.DefaultTimeoutSeconds} seconds. " +
                $"Current user: '{Environment.UserName}', session: {Process.GetCurrentProcess().SessionId}, " +
                $"working directory: '{startInfo.WorkingDirectory}'.");

        return HomeWindow;
    }

    public Window AttachToInstructorApp(string windowTitle = "LLEAP", int? timeoutSeconds = null)
    {
        var effectiveTimeout = timeoutSeconds ?? _settings.Timeouts.DefaultTimeoutSeconds;
        var deadline = DateTime.UtcNow.AddSeconds(effectiveTimeout);
        var seenTitles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var desktop = Automation.GetDesktop();
                var candidates = new List<(AutomationElement Element, string Title, int ProcessId, DateTime StartTime)>();

                foreach (var element in desktop.FindAllChildren())
                {
                    try
                    {
                        var title = element.Name;
                        if (!string.IsNullOrWhiteSpace(title))
                        {
                            seenTitles.Add(title);
                        }

                        if (string.IsNullOrWhiteSpace(title) ||
                            !title.Contains(windowTitle, StringComparison.OrdinalIgnoreCase) ||
                            element.IsOffscreen)
                        {
                            continue;
                        }

                        var processId = element.Properties.ProcessId.Value;
                        if (_homeApp != null && processId == _homeApp.ProcessId)
                        {
                            continue;
                        }
                        DateTime startTime;
                        try
                        {
                            startTime = Process.GetProcessById(processId).StartTime;
                        }
                        catch
                        {
                            startTime = DateTime.MinValue;
                        }

                        candidates.Add((element, title, processId, startTime));
                    }
                    catch
                    {
                        // A top-level UIA element may disappear while being inspected.
                    }
                }

                // Prefer an exact title, then the newest matching process. This avoids
                // attaching to a stale LLEAP window left over from an earlier test run.
                var match = candidates
                    .OrderByDescending(x => string.Equals(x.Title, windowTitle, StringComparison.OrdinalIgnoreCase))
                    .ThenByDescending(x => x.StartTime)
                    .FirstOrDefault();

                if (match.Element != null)
                {
                    _instructorApp = FlaUI.Core.Application.Attach(match.ProcessId);
                    InstructorWindow = match.Element.AsWindow();
                    return InstructorWindow;
                }
            }
            catch (System.Runtime.InteropServices.COMException)
            {
                // UI Automation can transiently fail while windows are being created.
            }

            Thread.Sleep(500);
        }

        throw new TimeoutException(
            $"A visible top-level window containing '{windowTitle}' did not appear within {effectiveTimeout} seconds. " +
            $"Top-level window titles seen: [{string.Join(", ", seenTitles.OrderBy(x => x))}]. " +
            $"UserInteractive={Environment.UserInteractive}, SessionId={Process.GetCurrentProcess().SessionId}, " +
            $"User='{Environment.UserName}', CurrentDirectory='{Environment.CurrentDirectory}'.");
    }

    public bool IsInstructorAppClosed => _instructorApp?.HasExited ?? true;

    public bool WaitForTopLevelWindowToClose(
        string exactTitle,
        int? expectedProcessId = null,
        int timeoutSeconds = 30)
    {
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < TimeSpan.FromSeconds(timeoutSeconds))
        {
            try
            {
                var isOpen = false;
                var windows = Automation.GetDesktop().FindAllChildren(
                    cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Window));

                foreach (var element in windows)
                {
                    try
                    {
                        var title = element.Name?.Trim();
                        var processId = element.Properties.ProcessId.ValueOrDefault;
                        if (string.Equals(title, exactTitle, StringComparison.OrdinalIgnoreCase) &&
                            (!expectedProcessId.HasValue || processId == expectedProcessId.Value))
                        {
                            isOpen = true;
                            break;
                        }
                    }
                    catch (COMException)
                    {
                        // A top level window may disapear during enumeration.
                    }
                    catch (InvalidOperationException)
                    {
                        // A prorcess/window may change during enumeration.
                    }
                }

                if (!isOpen)
                {
                    return true;
                }
            }
            catch (COMException)
            {
                // UI automation may fail while the window is closing.
            }

            Thread.Sleep(200);
        }

        return false;
    }

    public void Quit()
    {
        TryClose(_instructorApp);
        TryClose(_homeApp);
        _instructorApp = null;
        _homeApp = null;
    }

    public void Dispose()
    {
        Quit();
        Automation.Dispose();
    }

    private static string ResolveExecutablePath(string configuredPath)
    {
        var expandedPath = Environment.ExpandEnvironmentVariables(configuredPath);
        return Path.IsPathRooted(expandedPath)
            ? Path.GetFullPath(expandedPath)
            : Path.GetFullPath(expandedPath, AppContext.BaseDirectory);
    }

    private static void EnsureInteractiveDesktop()
    {
        var sessionId = Process.GetCurrentProcess().SessionId;
        if (!Environment.UserInteractive || sessionId == 0)
        {
            throw new InvalidOperationException(
                "Desktop UI automation requires an interactive, logged-on Windows user session. " +
                $"The current process is running with UserInteractive={Environment.UserInteractive}, SessionId={sessionId}. " +
                "Do not run this test from a Windows service or from a scheduled task configured as " +
                "'Run whether user is logged on or not'. Run the agent/task only while the test user is logged on, " +
                "and keep that desktop session unlocked.");
        }
    }

    private static void TryClose(FlaUI.Core.Application? app)
    {
        if (app == null) return;
        try { app.Close(); } catch { /* already gone or inaccessible */ }
        try { app.Dispose(); } catch { }
    }

    public Window WaitForTopLevelWindow(string exactTitle, int? expectedProcessId = null, int timeoutSeconds = 60)
    {
        var stopwatch = Stopwatch.StartNew();
        var observedWindows = new Dictionary<string, string>();
        while (stopwatch.Elapsed < TimeSpan.FromSeconds(timeoutSeconds))
        {
            try
            {
                var desktop = Automation.GetDesktop();
                var windows = desktop.FindAllChildren(
                    cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Window));
                foreach (var element in windows)
                {
                    try
                    {
                        var title = element.Name?.Trim();
                        if (string.IsNullOrWhiteSpace(title))
                        {
                            continue;
                        }
                        var processId = element.Properties.ProcessId.ValueOrDefault;
                        var handle = element.Properties.NativeWindowHandle.ValueOrDefault;
                        observedWindows[$"{processId}:{handle}"] =
                            $"Title='{title}', PID={processId}, " +
                            $"HWND={handle}, Offscreen={element.IsOffscreen}";
                        if (expectedProcessId.HasValue && processId != expectedProcessId.Value)
                        {
                            continue;
                        }

                        if (!string.Equals(title, exactTitle, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        if (element.IsOffscreen)
                        {
                            continue;
                        }

                        var window = element.AsWindow();
                        Log.Information(
                            "Top level window found: Title='{Title}', " +
                            "PID={ProcessId}, HWND={Handle}, IsModal={IsModal}",
                            window.Title,
                            processId,
                            handle,
                            TryGetIsModal(window));

                        try
                        {
                            window.Focus();
                        }
                        catch (Exception ex)
                        {
                            Log.Debug(ex, "Could not found '{WindowTitle}'", window.Title);
                        }

                        return window;
                    }
                    catch (COMException)
                    {
                        // window disappeared while being inspected.
                    }
                    catch (InvalidOperationException)
                    {
                        // process/window changed during enumeration.
                    }
                }
            }
            catch (COMException)
            {
                // UI automation fails temporarily during transitions.
            }

            Thread.Sleep(200);
        }

        throw new TimeoutException(
            $"Top-level window '{exactTitle}' did not appear within " +
            $"{timeoutSeconds} seconds.{Environment.NewLine}" +
            $"Observed windows:{Environment.NewLine}" +
            string.Join(
                Environment.NewLine,
                observedWindows.Values.OrderBy(value => value)));
    }

    private static bool? TryGetIsModal(Window window)
    {
        try
        {
            return window.IsModal;
        }
        catch
        {
            return null;
        }
    }
}