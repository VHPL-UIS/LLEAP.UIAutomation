using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using System.Text.RegularExpressions;

namespace LLEAP.UITests.Pages;

public class SessionPage : BasePage
{
    public SessionPage(Window rootWindow) : base(rootWindow)
    { }
    public void ClosePatientEyes()
    {
        var eyesElement = WaitFor(PatientMonitorLocators.EyeControl);
        if (eyesElement.ControlType == FlaUI.Core.Definitions.ControlType.ComboBox)
        {
            eyesElement.AsComboBox().Select(PatientMonitorLocators.EyesCloseOption.Name!);
        }
        else
        {
            eyesElement.Click();
            ClickBy(PatientMonitorLocators.EyesCloseOption);
        }
    }

    public void AdjustLungCompliance(double precent)
    {
        if (precent < 0 || precent > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(precent), "Percent must be in range of 0 t0 100!");
        }
        var slider = WaitFor(PatientMonitorLocators.LungComplianceSlider).AsSlider();
        slider.Value = slider.Minimum + (slider.Maximum - slider.Minimum) * (precent / 100.0);
    }

    public void AdjustHeartRate(int bpm)
    {
        ClickBy(PatientMonitorLocators.HrValueLabel);
        var hrInput = TryFind(PatientMonitorLocators.HrInputField, timeoutSeconds: 5)
            ?? WaitFor(PatientMonitorLocators.HrInputField);
        hrInput.AsTextBox().Enter(bpm.ToString());
        Keyboard.Press(FlaUI.Core.WindowsAPI.VirtualKeyShort.RETURN);
    }

    public void PlayVoice(Locator voiceLocator)
    {
        TryFind(SessionLocators.VoicePanel, timeoutSeconds: 5)?.Click();
        ClickBy(voiceLocator);
        ClickBy(SessionLocators.PlayVoiceButton);
    }

    public string GetEyesState()
        => WaitFor(PatientMonitorLocators.EyeControl).Name ?? string.Empty;

    public bool IsMaximized()
        => RootWindow.Patterns.Window.Pattern.WindowVisualState == FlaUI.Core.Definitions.WindowVisualState.Maximized;
    public double GetLungCompliancePercent()
    {
        var slider = WaitFor(PatientMonitorLocators.LungComplianceSlider).AsSlider();
        return (slider.Value - slider.Minimum) / (slider.Maximum - slider.Minimum) * 100.0; 
    }

    public int GetDisplayedHeartRateBpm()
    {
        var label = WaitFor(PatientMonitorLocators.HrValueLabel);
        var match = Regex.Match(label.Name ?? string.Empty, @"\d+");
        return match.Success ? int.Parse(match.Value) : -1;
    }

    public bool IsVoiceSelected(Locator voiceLocator)
    {
        var item = TryFind(voiceLocator, timeoutSeconds: 3);
        if (item == null)
        {
            return false;
        }
        return item.Patterns.SelectionItem.IsSupported && item.Patterns.SelectionItem.Pattern.IsSelected;
    }

    public void Maximized()
        => RootWindow.Patterns.Window.Pattern.SetWindowVisualState(FlaUI.Core.Definitions.WindowVisualState.Maximized);

    public void EndSession()
    {
        var closeButton = TryFind(CommonLocators.CloseButton, timeoutSeconds: 5);
        if (closeButton != null)
        {
            closeButton.AsButton().Click();
        }
        else
        {
            Keyboard.Press(FlaUI.Core.WindowsAPI.VirtualKeyShort.ALT);
            Keyboard.Press(FlaUI.Core.WindowsAPI.VirtualKeyShort.F4);
            Keyboard.Release(FlaUI.Core.WindowsAPI.VirtualKeyShort.F4);
            Keyboard.Release(FlaUI.Core.WindowsAPI.VirtualKeyShort.ALT);
        }
        TryFind(CommonLocators.OkButton, timeoutSeconds: 5)?.AsButton().Click();
        TryFind(CommonLocators.YesButton, timeoutSeconds: 3)?.AsButton().Click();
    }
}