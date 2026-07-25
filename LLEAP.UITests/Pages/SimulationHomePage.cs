using FlaUI.Core.AutomationElements;

namespace LLEAP.UITests.Pages;

public class SimulationHomePage : BasePage
{
    public SimulationHomePage(Window rootWindow) : base(rootWindow)
    { }
    public void OpenInstructorApplication()
        => ClickBy(SimulationHomeLocators.InstructorApplicationTile);

    public void CollectClientLogs()
    {
        WaitFor(SimulationHomeLocators.HelpTile).RightClick();
        ClickBy(SimulationHomeLocators.CollectClientLogsMenuItem);
    }
}