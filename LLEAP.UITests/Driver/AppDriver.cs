using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;
using LLEAP.UITests.Configuration;

namespace LLEAP.UITests.Drivers;

public sealed class AppDriver : IDisposable
{
    private readonly TestSettings _settings = TestSettings.Instance;
    public UIA3Automation Automation { get; } = new UIA3Automation();
    private FlaUI.Core.Application? _homeApp;
    private FlaUI.Core.Application? _instructorApp;
    // This is ok only if the LaunchSimulationHome be called before accessing HomeWindow and the same for the next one.
    public Window HomeWindow { get; private set; } = null!;
    public Window InstructorWindow { get; private set; } = null!;
    public Window LaunchSimulationHome()
    {
        _homeApp = FlaUI.Core.Application.Launch(_settings.Paths.SimulationHomeExePath);
        HomeWindow = _homeApp.GetMainWindow(Automation, TimeSpan.FromSeconds(_settings.Timeouts.DefaultTimeoutSeconds));
        return HomeWindow;
    }
    public Window AttachToInstructorApp(string windowTitle = "LLEAP", int? timeoutSeconds = null)
    {
        var effectiveTimeout = timeoutSeconds ?? _settings.Timeouts.DefaultTimeoutSeconds;
        var deadline = DateTime.UtcNow.AddSeconds(effectiveTimeout);
        var seenTitles = new HashSet<string>();
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var desktop = Automation.GetDesktop();
                //var match = desktop.FindFirstChild(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Window));
                var allWindows = desktop.FindAllChildren();
                foreach (var w in allWindows)
                {
                    string? title;
                    try
                    {
                        title = w.Name;
                    }
                    catch
                    {
                        continue;
                    }

                    if (!string.IsNullOrEmpty(title))
                    {
                        seenTitles.Add(title);
                    }

                    if (!string.IsNullOrEmpty(title) && title.Contains(windowTitle, StringComparison.OrdinalIgnoreCase))
                    {
                        _instructorApp = FlaUI.Core.Application.Attach(w.Properties.ProcessId.Value);
                        InstructorWindow = _instructorApp.GetMainWindow(Automation, TimeSpan.FromSeconds(_settings.Timeouts.DefaultTimeoutSeconds));
                        return InstructorWindow;
                    }
                }
            }
            catch (System.Runtime.InteropServices.COMException)
            {
                //
            }
            Thread.Sleep(500);
        }

        throw new TimeoutException(
            $"Window containing '{windowTitle}' did not apear within " + $"{_settings.Timeouts.DefaultTimeoutSeconds} seconds." +
            $"Top-level window titles seen during polling: [{string.Join(", ", seenTitles)}]");
    }

    public bool IsInstructorAppClosed => _instructorApp?.HasExited ?? true;

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

    private static void TryClose(FlaUI.Core.Application? app)
    {
        if (app == null) return;
        try { app.Close(); } catch { /* gone */ }
        try { app.Dispose(); } catch { }
    }
}