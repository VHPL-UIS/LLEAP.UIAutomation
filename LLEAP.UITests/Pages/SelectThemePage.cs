using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using LLEAP.UITests.Configuration;
using Serilog;
using System.Diagnostics;
using System.Runtime.InteropServices;
using ComboBox = FlaUI.Core.AutomationElements.ComboBox;

namespace LLEAP.UITests.Pages;

public sealed class SelectThemePage : BasePage
{
    private static readonly Locator PatientDropdown = InstructorLocators.SessionSetup.ThemesDropdown;

    private static readonly Locator ConfirmButton = InstructorLocators.SessionSetup.OkButton;

    public SelectThemePage(
        Window rootWindow)
        : base(rootWindow)
    {
        if (!string.Equals(
                rootWindow.Title?.Trim(),
                "Select theme",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                $"Expected the 'Select theme' window, " +
                $"but received '{rootWindow.Title}'.",
                nameof(rootWindow));
        }
    }

    public void SelectPatient(string patientName)
    {
        var dropdownElement =
            WaitFor(PatientDropdown, timeoutSeconds: 30);

        Log.Information(
            "Patient dropdown found: Name='{Name}', " +
            "AutomationId='{AutomationId}', Type={ControlType}, " +
            "Framework={FrameworkType}",
            dropdownElement.Name,
            dropdownElement.AutomationId,
            dropdownElement.ControlType,
            dropdownElement.FrameworkType);

        if (dropdownElement.ControlType == ControlType.ComboBox)
        {
            SelectNativeComboBoxItem(
                dropdownElement.AsComboBox(),
                patientName);

            return;
        }

        // Fallback for a custom dropdown control.
        SelectCustomDropdownItem(
            dropdownElement,
            patientName);
    }

    public void Confirm()
    {
        ClickBy(ConfirmButton);
    }

    private void SelectNativeComboBoxItem(
        ComboBox comboBox,
        string patientName)
    {
        var stopwatch = Stopwatch.StartNew();
        ComboBoxItem? matchingItem = null;
        string[] lastKnownItems = [];

        while (stopwatch.Elapsed < TimeSpan.FromSeconds(15))
        {
            try
            {
                comboBox.Expand();

                var items = comboBox.Items;

                lastKnownItems = items
                    .Select(item => item.Text)
                    .Where(text => !string.IsNullOrWhiteSpace(text))
                    .ToArray();

                matchingItem = items.FirstOrDefault(
                    item => string.Equals(
                        item.Text?.Trim(),
                        patientName.Trim(),
                        StringComparison.OrdinalIgnoreCase));

                if (matchingItem != null)
                {
                    break;
                }
            }
            catch (COMException)
            {
                // Dropdown is being populated or rebuilt.
            }
            catch (InvalidOperationException)
            {
                // UI element changed while reading items.
            }

            Thread.Sleep(250);
        }

        if (matchingItem == null)
        {
            throw new TimeoutException(
                $"Patient '{patientName}' was not found in the dropdown. " +
                $"Available items: [{string.Join(", ", lastKnownItems)}]");
        }

        Log.Information(
            "Selecting patient '{PatientName}'",
            matchingItem.Text);

        matchingItem.Select(); WaitUntil(
            condition: () =>
            {
                try
                {
                    return string.Equals(
                        comboBox.SelectedItem?.Text?.Trim(),
                        patientName.Trim(),
                        StringComparison.OrdinalIgnoreCase);
                }
                catch
                {
                    return false;
                }
            },
            timeoutSeconds: 10,
            errorMessage:
                $"Patient '{patientName}' was clicked but did not become selected.");
    }

    private void SelectCustomDropdownItem(
        AutomationElement dropdown,
        string patientName)
    {
        Log.Information(
            "Dropdown is a custom control; clicking trigger.");

        dropdown.Focus();
        dropdown.Click();

        var processId =
            RootWindow.Properties.ProcessId.ValueOrDefault;

        var item = WaitForVisibleDesktopElement(
            name: patientName,
            processId: processId,
            timeoutSeconds: 15);

        Log.Information(
            "Custom dropdown item found: Name='{Name}', " +
            "Type={ControlType}, PID={ProcessId}",
            item.Name,
            item.ControlType,
            item.Properties.ProcessId.ValueOrDefault);

        item.Click();
    }

    private AutomationElement WaitForVisibleDesktopElement(
        string name,
        int processId,
        int timeoutSeconds)
    {
        var stopwatch = Stopwatch.StartNew();

        while (stopwatch.Elapsed <
               TimeSpan.FromSeconds(timeoutSeconds))
        {
            try
            {
                var desktop = RootWindow.Automation.GetDesktop();

                var candidates = desktop.FindAllDescendants(
                    cf => cf.ByName(name)
                        .And(cf.ByProcessId(processId)));

                var match = candidates.FirstOrDefault(
                    element =>
                        !element.IsOffscreen &&
                        element.IsEnabled);

                if (match != null)
                {
                    return match;
                }
            }
            catch (COMException)
            {
                // Popup is opening or changing.
            }
            catch (InvalidOperationException)
            {
                // Candidate disappeared during inspection.
            }

            Thread.Sleep(200);
        }

        throw new TimeoutException(
            $"Visible dropdown item '{name}' was not found " +
            $"for PID {processId} within {timeoutSeconds} seconds.");
    }

    private static void WaitUntil(
        Func<bool> condition,
        int timeoutSeconds,
        string errorMessage)
    {
        var stopwatch = Stopwatch.StartNew();

        while (stopwatch.Elapsed <
               TimeSpan.FromSeconds(timeoutSeconds))
        {
            if (condition())
            {
                return;
            }

            Thread.Sleep(200);
        }

        throw new TimeoutException(errorMessage);
    }
}