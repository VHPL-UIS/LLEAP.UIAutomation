using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;

namespace LLEAP.UITests.Pages;

public class InstructorAppPage : BasePage
{
    public InstructorAppPage(Window rootWindow) : base(rootWindow)
    { }

    public void SkipLicenseActivation()
        => ClickBy(InstructorLocators.License.AddLicenseLaterButton);
    public void SelectLocalComputerSimulator()
        => ClickBy(InstructorLocators.SimulatorSelection.LocalComputerTile);
    public void SelectSimMan3GPlus()
        => ClickBy(InstructorLocators.SimulatorSelection.SimMan3GPlusButton);
    public void SkipDebriefingSystem()
        => ClickBy(InstructorLocators.SimulatorSelection.ContinueWithoutDebriefingLink);
    public void OpenInternationalPreferences()
        => ClickBy(InstructorLocators.SessionSetup.InternationalPreferencesButton);
    public void SelectManualMode()
        => ClickBy(InstructorLocators.SessionSetup.ManualModeButton);
    public void SelectThemeButton()
        => ClickBy(InstructorLocators.SessionSetup.ThemesDropdown);
    public void SelectTheme(Locator themeLocator)
        => SelectFromDropdown(InstructorLocators.SessionSetup.ThemesDropdown, themeLocator);
    public void Confirm()
        => ConfirmDialog(InstructorLocators.SessionSetup.OkButton);
    //public void StartSimulation()
    //    => ClickBy(InstructorLocators.SessionSetup.StartSessionButton);
    public void Maximize()
        => RootWindow.Patterns.Window.Pattern.SetWindowVisualState(FlaUI.Core.Definitions.WindowVisualState.Maximized);
    public void Exit()
    {
        var closeButton = TryFind(CommonLocators.CloseButton, timeoutSeconds: 5);
        if (closeButton != null)
        {
            closeButton.AsButton().Click();
        }
        else
        {
            Keyboard.Press(VirtualKeyShort.ALT);
            Keyboard.Press(VirtualKeyShort.F4);
            Keyboard.Release(VirtualKeyShort.F4);
            Keyboard.Release(VirtualKeyShort.ALT);
        }
    }
}