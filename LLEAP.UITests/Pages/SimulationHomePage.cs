using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using Serilog;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace LLEAP.UITests.Pages;

public class SimulationHomePage : BasePage
{
    public SimulationHomePage(Window rootWindow) : base(rootWindow)
    { }
    public void OpenInstructorApplication()
        => ClickBy(SimulationHomeLocators.InstructorApplicationTile);

    public void OpenHelpContextMenu()
    {
        for (var attempt = 1; attempt <= 2; attempt++)
        {
            TryForegroundSimulationHome();
            WaitFor(SimulationHomeLocators.HelpTile).RightClick();

            try
            {
                // Verify that the physical right-click opened the intended
                // menu while the Help tile is still the active target.
                _ = WaitForVisibleDesktopMenuItem(
                    GetCollectClientLogsMenuItemName(),
                    ProcessId,
                    timeoutSeconds: 5);
                return;
            }
            catch (TimeoutException ex) when (attempt < 2)
            {
                Log.Warning(
                    ex,
                    "Help context menu did not open on attempt {Attempt}; " +
                    "dismissing any popup and retrying.",
                    attempt);
                Keyboard.Type(
                    FlaUI.Core.WindowsAPI.VirtualKeyShort.ESCAPE);
                Thread.Sleep(200);
            }
            catch (TimeoutException ex)
            {
                throw new TimeoutException(
                    "The Help context menu did not open after two " +
                    "right-click attempts.",
                    ex);
            }
        }
    }

    private void TryForegroundSimulationHome()
    {
        try
        {
            RootWindow.SetForeground();
        }
        catch (Exception ex) when (
            ex is COMException or
                InvalidOperationException or
                FlaUI.Core.Exceptions.FlaUIException)
        {
            Log.Warning(
                ex,
                "Could not explicitly foreground Simulation Home before " +
                "opening the Help context menu.");
        }
    }

    public void SelectCollectClientLogs()
    {
        var menuItemName = GetCollectClientLogsMenuItemName();

        AutomationElement menuItem;
        try
        {
            menuItem = WaitForVisibleDesktopMenuItem(
                menuItemName,
                ProcessId,
                timeoutSeconds: 3);
        }
        catch (TimeoutException)
        {
            Log.Warning(
                "The Help context menu closed before selection; reopening it.");
            OpenHelpContextMenu();
            menuItem = WaitForVisibleDesktopMenuItem(
                menuItemName,
                ProcessId,
                timeoutSeconds: 10);
        }

        Log.Information(
            "Client log menu item found: Name='{Name}', Type={ControlType}, " +
            "Framework={FrameworkType}, PID={ProcessId}, Bounds={Bounds}",
            menuItem.Name,
            menuItem.ControlType,
            menuItem.FrameworkType,
            menuItem.Properties.ProcessId.ValueOrDefault,
            menuItem.BoundingRectangle);

        //if (menuItem.Patterns.Invoke.IsSupported)
        //{
        //    menuItem.Patterns.Invoke.Pattern.Invoke();
        //}
        //else
        //{
        //    menuItem.Click();
        //}
        menuItem.Click();
        Log.Information("Physically clicked the client log menu item!");

        try
        {
            _ = WaitForVisibleDesktopMenuItem(
                menuItemName,
                ProcessId,
                timeoutSeconds: 2);
        }
        catch (TimeoutException)
        {
            Log.Information("The help context menu closed!");
            return;
        }

        throw new InvalidOperationException(
            "The 'Collect client log fiels' context ...");
    }

    //public void CollectClientLogs()
    //{
    //    OpenHelpContextMenu();
    //    SelectCollectClientLogs();
    //}

    private static string GetCollectClientLogsMenuItemName()
        => SimulationHomeLocators.CollectClientLogsMenuItem.Name
            ?? throw new InvalidOperationException(
                "The collect-client-logs locator must have a name.");

    private AutomationElement WaitForVisibleDesktopMenuItem(
        string name,
        int processId,
        int timeoutSeconds)
    {
        var stopwatch = Stopwatch.StartNew();
        var observedCandidates = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);

        while (stopwatch.Elapsed < TimeSpan.FromSeconds(timeoutSeconds))
        {
            try
            {
                var candidates = RootWindow.Automation
                    .GetDesktop()
                    .FindAllDescendants(CF.ByName(name));

                var visibleCandidates =
                    new List<(
                        AutomationElement Element,
                        int ProcessId,
                        bool IsMenuItem)>();
                foreach (var candidate in candidates)
                {
                    try
                    {
                        var candidateProcessId =
                            candidate.Properties.ProcessId.ValueOrDefault;
                        observedCandidates.Add(
                            $"Name='{candidate.Name}', " +
                            $"Type={candidate.ControlType}, " +
                            $"PID={candidateProcessId}, " +
                            $"Enabled={candidate.IsEnabled}, " +
                            $"Offscreen={candidate.IsOffscreen}");

                        if (candidate.IsEnabled &&
                            !candidate.IsOffscreen)
                        {
                            visibleCandidates.Add((
                                candidate,
                                candidateProcessId,
                                candidate.ControlType ==
                                    ControlType.MenuItem));
                        }
                    }
                    catch (Exception ex) when (
                        ex is COMException or
                            InvalidOperationException or
                            FlaUI.Core.Exceptions.FlaUIException)
                    {
                        // A popup element may disappear during inspection.
                    }
                }

                var match = visibleCandidates
                    .Where(candidate =>
                        processId <= 0 ||
                        candidate.ProcessId <= 0 ||
                        candidate.ProcessId == processId)
                    .OrderByDescending(
                        candidate => candidate.IsMenuItem)
                    .Select(candidate => candidate.Element)
                    .FirstOrDefault();

                if (match != null)
                {
                    return match;
                }

                var hostedMenuItems = visibleCandidates
                    .Where(candidate => candidate.IsMenuItem)
                    .ToArray();
                if (hostedMenuItems.Length == 1)
                {
                    Log.Warning(
                        "Using the only visible exact-name menu item even " +
                        "though its PID {CandidateProcessId} differs from " +
                        "Simulation Home PID {HomeProcessId}.",
                        hostedMenuItems[0].ProcessId,
                        processId);
                    return hostedMenuItems[0].Element;
                }
            }
            catch (Exception ex) when (
                ex is COMException or
                    InvalidOperationException or
                    FlaUI.Core.Exceptions.FlaUIException)
            {
                // The context menu may still be opening or rebuilding.
            }

            Thread.Sleep(200);
        }

        var candidateDescription = string.Join(
            "; ",
            observedCandidates.OrderBy(value => value));

        throw new TimeoutException(
            $"Visible context-menu item '{name}' was not found for PID " +
            $"{processId} within {timeoutSeconds} seconds. " +
            $"Candidates observed: [{candidateDescription}]");
    }
}