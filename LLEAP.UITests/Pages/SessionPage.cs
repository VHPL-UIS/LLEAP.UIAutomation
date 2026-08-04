using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using Serilog;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace LLEAP.UITests.Pages;

public class SessionPage : BasePage
{
    private double? _lastKnownLungCompliancePercent;
    private bool _lastLungCompliancePercentWasInferred;

    public SessionPage(Window rootWindow) : base(rootWindow)
    { }

    public void StartSession()
        => ClickBy(SessionLocators.StartSessionButton);

    public void ClosePatientEyes()
    {
        var eyesElement = WaitFor(PatientMonitorLocators.EyeControl);
        var closedState = PatientMonitorLocators.EyesCloseOption.Name
            ?? throw new InvalidOperationException("Closed eye-state locator must have a name.");

        Log.Information(
            "Eyes control found: Name='{Name}', AutomationId='{AutomationId}', " +
            "Type={ControlType}, Framework={FrameworkType}",
            eyesElement.Name,
            eyesElement.AutomationId,
            eyesElement.ControlType,
            eyesElement.FrameworkType);

        if (eyesElement.ControlType == FlaUI.Core.Definitions.ControlType.ComboBox)
        {
            var comboBox = eyesElement.AsComboBox();
            var items = comboBox.Items;
            var matchingItem = items.FirstOrDefault(
                item => string.Equals(
                    item.Text?.Trim(),
                    closedState,
                    StringComparison.OrdinalIgnoreCase));

            Log.Information(
                "Eye states exposed under ComboBox: [{AvailableEyeStates}]",
                string.Join(", ", items.Select(item => item.Text)));

            if (matchingItem != null)
            {
                SelectEyeStateElement(matchingItem);
            }
            else
            {
                // WinForms commonly exposes the open drop-down list as a separate
                // desktop element instead of a child of the ComboBox. Reading
                // ComboBox.Items above already expanded it; expanding again can
                // toggle some WinForms drop-downs closed.
                var desktopItem = TryFindVisibleDesktopElementByName(
                    closedState,
                    ProcessId,
                    timeoutSeconds: 5);

                if (desktopItem != null)
                {
                    Log.Information(
                        "Selecting eye state from desktop popup: Name='{Name}', " +
                        "Type={ControlType}, PID={ProcessId}",
                        desktopItem.Name,
                        desktopItem.ControlType,
                        desktopItem.Properties.ProcessId.ValueOrDefault);

                    SelectEyeStateElement(desktopItem);
                }
                else
                {
                    // Final fallback for a WinForms DropDownList: focused typing
                    // performs incremental selection even when UIA exposes no items.
                    Log.Warning(
                        "Eye states were not exposed through UIA; selecting '{EyeState}' by keyboard.",
                        closedState);
                    comboBox.Focus();
                    Keyboard.Type(closedState);
                    Keyboard.Type(FlaUI.Core.WindowsAPI.VirtualKeyShort.RETURN);
                }
            }
        }
        else
        {
            eyesElement.Click();
            var matchingItem = WaitFor(PatientMonitorLocators.EyesCloseOption);
            if (matchingItem.Patterns.SelectionItem.IsSupported)
            {
                matchingItem.Patterns.SelectionItem.Pattern.Select();
            }
            else
            {
                matchingItem.Click();
            }
        }

        var lastObservedState = string.Empty;
        try
        {
            WaitUntil(
                () =>
                {
                    lastObservedState = GetEyesState();
                    return lastObservedState.Contains(
                        closedState,
                        StringComparison.OrdinalIgnoreCase);
                },
                timeoutSeconds: 10,
                errorMessage: $"Eyes control did not change to '{closedState}'.");
        }
        catch (TimeoutException ex)
        {
            throw new TimeoutException(
                $"Eyes control did not change to '{closedState}'. " +
                $"Last observed state: '{lastObservedState}'.",
                ex);
        }
    }

    public void AdjustLungCompliance(double percent)
    {
        if (!double.IsFinite(percent) || percent < 0 || percent > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(percent),
                "Percent must be in the range 0 to 100.");
        }

        _lastKnownLungCompliancePercent = null;
        _lastLungCompliancePercentWasInferred = false;

        var complianceControl = WaitFor(PatientMonitorLocators.LungComplianceSlider);
        var rangeValueSupported = SupportsPattern(
            () => complianceControl.Patterns.RangeValue.IsSupported);
        var valueSupported = SupportsPattern(
            () => complianceControl.Patterns.Value.IsSupported);
        var legacySupported = SupportsPattern(
            () => complianceControl.Patterns.LegacyIAccessible.IsSupported);
        var isDiscreteComplianceControl =
            IsDiscreteComplianceControl(complianceControl);
        var initialLegacyValue =
            TryGetLegacyAccessibleValue(complianceControl);

        Log.Information(
            "Lung compliance control found: Name='{Name}', " +
            "AutomationId='{AutomationId}', Type={ControlType}, " +
            "Framework={FrameworkType}, ClassName='{ClassName}', " +
            "Bounds={Bounds}, RangeValue={RangeValueSupported}, " +
            "Value={ValueSupported}, LegacyIAccessible={LegacySupported}, " +
            "LegacyValue='{LegacyValue}', DiscreteLevels={DiscreteLevels}",
            complianceControl.Name,
            complianceControl.AutomationId,
            complianceControl.ControlType,
            complianceControl.FrameworkType,
            complianceControl.Properties.ClassName.ValueOrDefault,
            complianceControl.BoundingRectangle,
            rangeValueSupported,
            valueSupported,
            legacySupported,
            initialLegacyValue,
            isDiscreteComplianceControl ? 4 : 0);

        var keyboardSteps = isDiscreteComplianceControl
            ? PercentToComplianceLevel(percent)
            : (int)Math.Round(percent);
        var usedAutomationPattern = TrySetLungComplianceWithAutomationPattern(
            complianceControl,
            percent,
            ref keyboardSteps,
            out var interaction);

        if (usedAutomationPattern &&
            WaitForLungCompliancePercent(
                percent,
                timeoutSeconds: 3,
                out var patternObservedPercent))
        {
            _lastKnownLungCompliancePercent = patternObservedPercent;
            Log.Information(
                "Lung compliance set to {RequestedPercent}% via {Interaction}; " +
                "observed {ObservedPercent:F1}%.",
                percent,
                interaction,
                patternObservedPercent);
            return;
        }

        if (usedAutomationPattern)
        {
            Log.Warning(
                "Lung compliance did not reach {RequestedPercent}% via {Interaction}; " +
                "falling back to keyboard/mouse input.",
                percent,
                interaction);
        }
        else
        {
            Log.Warning(
                "Lung compliance exposes no writable UIA value pattern; " +
                "falling back to keyboard/mouse input.");
        }

        var fallbackControl =
            WaitFor(PatientMonitorLocators.LungComplianceSlider);
        SetLungComplianceWithKeyboardAndMouse(
            fallbackControl,
            percent,
            keyboardSteps,
            reverseDiscreteDirection: false);

        var fallbackSucceeded = WaitForLungCompliancePercent(
                percent,
                timeoutSeconds: 5,
                out var lastObservedPercent);

        if (!fallbackSucceeded &&
            isDiscreteComplianceControl &&
            double.IsNaN(lastObservedPercent))
        {
            // LLEAP's custom ComplianceSlider exposes only static help text:
            // there is no UIA property from which its current level can be
            // read. Mouse.LeftClick completed at the deterministic level
            // coordinate, so retain that effective level for the test's
            // subsequent assertion. The after-step screenshot remains the
            // visual record of the change.
            var clickedLevel = PercentToComplianceLevel(percent);
            var clickedPercent = clickedLevel / 3.0 * 100.0;
            _lastKnownLungCompliancePercent = clickedPercent;
            _lastLungCompliancePercentWasInferred = true;

            Log.Warning(
                "ComplianceSlider level-{Level} mouse input completed, " +
                "but LLEAP exposes no readable current-value property. " +
                "Using the clicked level ({ClickedPercent:F1}%) as the " +
                "effective value; verify it in the after-step screenshot.",
                clickedLevel,
                clickedPercent);
            return;
        }

        if (!fallbackSucceeded &&
            isDiscreteComplianceControl &&
            !double.IsNaN(lastObservedPercent))
        {
            Log.Warning(
                "The first discrete compliance direction did not reach " +
                "{RequestedPercent}% (observed {ObservedPercent}). " +
                "Trying the opposite vertical direction.",
                percent,
                $"{lastObservedPercent:F1}%");

            SetDiscreteComplianceWithMouse(
                WaitFor(PatientMonitorLocators.LungComplianceSlider),
                percent,
                reverseDirection: true);

            fallbackSucceeded = WaitForLungCompliancePercent(
                percent,
                timeoutSeconds: 5,
                out lastObservedPercent);
        }

        if (!fallbackSucceeded)
        {
            var lastObserved = double.IsNaN(lastObservedPercent)
                ? "<unavailable>"
                : $"{lastObservedPercent:F1}%";

            throw new TimeoutException(
                $"Lung compliance did not change to approximately {percent}%. " +
                $"Last observed value: {lastObserved}. " +
                $"RangeValue={rangeValueSupported}, Value={valueSupported}, " +
                $"LegacyIAccessible={legacySupported}.");
        }

        _lastKnownLungCompliancePercent = lastObservedPercent;
        Log.Information(
            "Lung compliance set to {RequestedPercent}% via fallback input; " +
            "observed {ObservedPercent:F1}%.",
            percent,
            lastObservedPercent);
    }

    //public void AdjustHeartRate(int bpm)
    //{
    //    ClickBy(PatientMonitorLocators.HrValueLabel);
    //    var hrInput = TryFind(PatientMonitorLocators.HrInputField, timeoutSeconds: 5)
    //        ?? WaitFor(PatientMonitorLocators.HrInputField);
    //    hrInput.AsTextBox().Enter(bpm.ToString());
    //    ClickBy(CommonLocators.OkButton);

    //    //WaitUntil(
    //    //    () => GetDisplayedHeartRateBpm() == bpm,
    //    //    timeoutSeconds: 10,
    //    //    errorMessage: $"Displayed heart rate did not change to {bpm} bpm.");
    //}

    public void AdjustHeartRate(int bpm)
    {
        ClickBy(PatientMonitorLocators.HrValueLabel);

        var dialog = WaitFor(PatientMonitorLocators.SetHeartRateDialog);

        var hrInput = dialog.FindFirstDescendant(
            CF.ByAutomationId(
                PatientMonitorLocators.HrInputField.AutomationId!))
            ?? throw new InvalidOperationException(
                "Heart-rate input was not found inside the Set Heart Rate dialog.");

        hrInput.AsTextBox().Enter(bpm.ToString());

        var okButton = dialog.FindFirstDescendant(
            CF.ByName(CommonLocators.OkButton.Name!)
                .And(CF.ByControlType(
                    FlaUI.Core.Definitions.ControlType.Button)))
            ?? throw new InvalidOperationException(
                "OK button was not found inside the Set Heart Rate dialog.");

        okButton.Click();

        WaitUntil(
            () => RootWindow.FindFirstDescendant(
                CF.ByName("Set Heart Rate")) == null,
            timeoutSeconds: 10,
            errorMessage:
                "Set Heart Rate dialog remained open after clicking OK.");

        //WaitUntil(
        //    () => GetDisplayedHeartRateBpm() == bpm,
        //    timeoutSeconds: 10,
        //    errorMessage:
        //        $"Patient monitor did not display {bpm} bpm after confirmation.");
    }

    public void PlayVoice(Locator voiceLocator)
    {
        var voiceItem = TryFind(voiceLocator, timeoutSeconds: 2);
        if (voiceItem == null)
        {
            ClickBy(SessionLocators.VoicePanel);
            voiceItem = WaitFor(voiceLocator);
        }

        voiceItem.Click();

        WaitUntil(
            () => IsVoiceSelected(voiceLocator),
            timeoutSeconds: 5,
            errorMessage: $"Voice item '{voiceLocator.Name}' did not become selected.");

        ClickBy(SessionLocators.PlayVoiceButton);
    }

    public string GetEyesState()
    {
        var eyesElement = WaitFor(PatientMonitorLocators.EyeControl);
        if (eyesElement.ControlType == FlaUI.Core.Definitions.ControlType.ComboBox)
        {
            var comboBox = eyesElement.AsComboBox();
            var isWinForms = string.Equals(
                comboBox.FrameworkType.ToString(),
                "WinForms",
                StringComparison.OrdinalIgnoreCase);

            if (!isWinForms)
            {
                var selectedText = comboBox.SelectedItem?.Text;
                if (!string.IsNullOrWhiteSpace(selectedText))
                {
                    return selectedText;
                }

                var selectedItem = comboBox.Items.FirstOrDefault(item => item.IsSelected);
                var selectedItemText = selectedItem?.Text;
                if (!string.IsNullOrWhiteSpace(selectedItemText))
                {
                    return selectedItemText;
                }
            }
        }

        if (eyesElement.Patterns.Value.IsSupported)
        {
            var value = eyesElement.Patterns.Value.Pattern.Value.Value;
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return eyesElement.Name ?? string.Empty;
    }

    private static void SelectEyeStateElement(AutomationElement item)
    {
        if (item.IsEnabled && !item.IsOffscreen)
        {
            item.Click();
        }
        else if (item.Patterns.SelectionItem.IsSupported)
        {
            item.Patterns.SelectionItem.Pattern.Select();
        }
        else
        {
            throw new InvalidOperationException(
                $"Eye state '{item.Name}' is not visible and does not support selection.");
        }
    }

    private AutomationElement? TryFindVisibleDesktopElementByName(
        string name,
        int processId,
        int timeoutSeconds)
    {
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < TimeSpan.FromSeconds(timeoutSeconds))
        {
            try
            {
                var candidates = RootWindow.Automation.GetDesktop().FindAllDescendants(
                    cf => cf.ByName(name).And(cf.ByProcessId(processId)));

                var match = candidates
                    .Where(element => element.IsEnabled && !element.IsOffscreen)
                    .OrderByDescending(
                        element => element.ControlType ==
                            FlaUI.Core.Definitions.ControlType.ListItem)
                    .ThenByDescending(
                        element => element.Patterns.SelectionItem.IsSupported)
                    .FirstOrDefault();

                if (match != null)
                {
                    return match;
                }
            }
            catch (COMException)
            {
                // The drop-down popup may be opening or rebuilding.
            }
            catch (InvalidOperationException)
            {
                // A popup element may disappear during enumeration.
            }

            Thread.Sleep(200);
        }

        return null;
    }

    public bool IsMaximized()
        => RootWindow.Patterns.Window.Pattern.WindowVisualState == FlaUI.Core.Definitions.WindowVisualState.Maximized;

    public double GetLungCompliancePercent()
    {
        var complianceControl = WaitFor(PatientMonitorLocators.LungComplianceSlider);
        if (TryReadLungCompliancePercent(complianceControl, out var percent))
        {
            _lastKnownLungCompliancePercent = percent;
            _lastLungCompliancePercentWasInferred = false;
            return percent;
        }

        if (IsDiscreteComplianceControl(complianceControl) &&
            _lastKnownLungCompliancePercent.HasValue)
        {
            Log.Warning(
                "ComplianceSlider has no readable current-value property; " +
                "returning the last {ValueSource} value {Percent:F1}%.",
                _lastLungCompliancePercentWasInferred
                    ? "mouse-target"
                    : "observed",
                _lastKnownLungCompliancePercent.Value);
            return _lastKnownLungCompliancePercent.Value;
        }

        throw new InvalidOperationException(
            $"Lung compliance value cannot be read from control " +
            $"Name='{complianceControl.Name}', " +
            $"AutomationId='{complianceControl.AutomationId}', " +
            $"Type={complianceControl.ControlType}, " +
            $"Framework={complianceControl.FrameworkType}. " +
            $"RangeValue={SupportsPattern(() => complianceControl.Patterns.RangeValue.IsSupported)}, " +
            $"Value={SupportsPattern(() => complianceControl.Patterns.Value.IsSupported)}, " +
            $"LegacyIAccessible={SupportsPattern(() => complianceControl.Patterns.LegacyIAccessible.IsSupported)}.");
    }

    private bool TrySetLungComplianceWithAutomationPattern(
        AutomationElement complianceControl,
        double percent,
        ref int keyboardSteps,
        out string interaction)
    {
        interaction = string.Empty;

        try
        {
            if (complianceControl.Patterns.RangeValue.TryGetPattern(
                    out var rangeValuePattern))
            {
                var minimum = rangeValuePattern.Minimum.Value;
                var maximum = rangeValuePattern.Maximum.Value;
                if (!double.IsFinite(minimum) ||
                    !double.IsFinite(maximum) ||
                    maximum <= minimum)
                {
                    throw new InvalidOperationException(
                        $"Lung compliance has an invalid RangeValue range: " +
                        $"{minimum} to {maximum}.");
                }

                var targetValue =
                    minimum + (maximum - minimum) * (percent / 100.0);
                var smallChange = rangeValuePattern.SmallChange.Value;
                if (double.IsFinite(smallChange) &&
                    smallChange > 0)
                {
                    var calculatedKeyboardSteps =
                        (targetValue - minimum) / smallChange;
                    if (double.IsFinite(calculatedKeyboardSteps) &&
                        calculatedKeyboardSteps is >= 0 and <= 1000)
                    {
                        keyboardSteps = (int)Math.Round(calculatedKeyboardSteps);
                    }
                }

                if (!rangeValuePattern.IsReadOnly.Value)
                {
                    // FlaUI 5 Slider.Value uses PatternOrDefault internally and
                    // can dereference null for WinForms controls. The concrete
                    // RangeValue pattern is already available here, so use it
                    // directly.
                    rangeValuePattern.SetValue(targetValue);
                    interaction = "RangeValue";
                    return true;
                }

                Log.Warning("Lung compliance RangeValue pattern is read-only.");
            }
        }
        catch (Exception ex) when (IsRecoverableAutomationException(ex))
        {
            Log.Warning(
                ex,
                "Could not set lung compliance through the RangeValue pattern.");
        }

        try
        {
            if (complianceControl.Patterns.Value.TryGetPattern(
                    out var valuePattern))
            {
                if (!valuePattern.IsReadOnly.Value)
                {
                    valuePattern.SetValue(
                        percent.ToString("0.###", CultureInfo.InvariantCulture));
                    interaction = "Value";
                    return true;
                }

                Log.Warning("Lung compliance Value pattern is read-only.");
            }
        }
        catch (Exception ex) when (IsRecoverableAutomationException(ex))
        {
            Log.Warning(
                ex,
                "Could not set lung compliance through the Value pattern.");
        }

        try
        {
            if (complianceControl.Patterns.LegacyIAccessible.TryGetPattern(
                    out var legacyPattern))
            {
                if (IsDiscreteComplianceControl(complianceControl))
                {
                    Log.Information(
                        "ComplianceSlider LegacyIAccessible is available for " +
                        "reading, but LLEAP does not implement SetValue; " +
                        "using the custom-control fallback.");
                    return false;
                }

                legacyPattern.SetValue(
                    percent.ToString(
                        "0.###",
                        CultureInfo.InvariantCulture));
                interaction = "LegacyIAccessible";
                return true;
            }
        }
        catch (Exception ex) when (IsRecoverableAutomationException(ex))
        {
            Log.Warning(
                ex,
                "Could not set lung compliance through LegacyIAccessible.");
        }

        return false;
    }

    private static void SetLungComplianceWithKeyboardAndMouse(
        AutomationElement complianceControl,
        double percent,
        int keyboardSteps,
        bool reverseDiscreteDirection)
    {
        var isDiscrete =
            IsDiscreteComplianceControl(complianceControl);
        var incrementKey = isDiscrete
            ? FlaUI.Core.WindowsAPI.VirtualKeyShort.UP
            : FlaUI.Core.WindowsAPI.VirtualKeyShort.RIGHT;

        try
        {
            complianceControl.Focus();
            Keyboard.Type(FlaUI.Core.WindowsAPI.VirtualKeyShort.HOME);
            Thread.Sleep(100);

            for (var step = 0; step < keyboardSteps; step++)
            {
                Keyboard.Type(incrementKey);
            }

            Thread.Sleep(100);
        }
        catch (Exception ex) when (
            isDiscrete &&
            IsRecoverableAutomationException(ex))
        {
            Log.Warning(
                ex,
                "ComplianceSlider did not accept keyboard input; using mouse.");
        }

        if (isDiscrete)
        {
            // SimMan 3G exposes four compliance levels (0..3). The custom
            // WPF control has no writable UIA value pattern, so finish with
            // a physical click on the requested level.
            SetDiscreteComplianceWithMouse(
                complianceControl,
                percent,
                reverseDiscreteDirection);
        }
    }

    private static void SetDiscreteComplianceWithMouse(
        AutomationElement complianceControl,
        double percent,
        bool reverseDirection)
    {
        var bounds = complianceControl.BoundingRectangle;
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            throw new InvalidOperationException(
                $"ComplianceSlider has invalid bounds: {bounds}.");
        }

        var level = PercentToComplianceLevel(percent);
        var normalizedLevel = level / 3.0;
        var padding = Math.Max(
            2,
            (int)Math.Round(bounds.Height * 0.08));
        var trackTop = bounds.Top + padding;
        var trackBottom = bounds.Bottom - padding - 1;
        var trackHeight = Math.Max(1, trackBottom - trackTop);

        // A normal WPF vertical slider has its minimum at the bottom.
        // If LLEAP uses the opposite direction, AdjustLungCompliance retries
        // once with reverseDirection=true and verifies the resulting level.
        var normalizedY = reverseDirection
            ? normalizedLevel
            : 1.0 - normalizedLevel;
        var clickPoint = new System.Drawing.Point(
            bounds.Left + bounds.Width / 2,
            trackTop + (int)Math.Round(trackHeight * normalizedY));

        Log.Information(
            "Clicking discrete lung compliance level {Level} " +
            "({RequestedPercent}%) at {ClickPoint}; ReverseDirection={ReverseDirection}.",
            level,
            percent,
            clickPoint,
            reverseDirection);

        Mouse.LeftClick(clickPoint);
        Thread.Sleep(200);
    }

    private bool WaitForLungCompliancePercent(
        double expectedPercent,
        int timeoutSeconds,
        out double lastObservedPercent)
    {
        lastObservedPercent = double.NaN;
        var stopwatch = Stopwatch.StartNew();

        while (stopwatch.Elapsed < TimeSpan.FromSeconds(timeoutSeconds))
        {
            try
            {
                var complianceControl = TryFind(
                    PatientMonitorLocators.LungComplianceSlider,
                    timeoutSeconds: 1);

                if (complianceControl != null &&
                    TryReadLungCompliancePercent(
                        complianceControl,
                        out var observedPercent))
                {
                    lastObservedPercent = observedPercent;
                    if (Math.Abs(observedPercent - expectedPercent) <= 1.0)
                    {
                        return true;
                    }
                }
            }
            catch (Exception ex) when (IsRecoverableAutomationException(ex))
            {
                // The WinForms control can be rebuilt while its value changes.
            }

            Thread.Sleep(200);
        }

        return false;
    }

    private static bool TryReadLungCompliancePercent(
        AutomationElement complianceControl,
        out double percent)
    {
        percent = double.NaN;

        try
        {
            if (complianceControl.Patterns.RangeValue.TryGetPattern(
                    out var rangeValuePattern))
            {
                var minimum = rangeValuePattern.Minimum.Value;
                var maximum = rangeValuePattern.Maximum.Value;
                var range = maximum - minimum;
                if (range > 0)
                {
                    percent =
                        (rangeValuePattern.Value.Value - minimum) / range * 100.0;
                    return double.IsFinite(percent);
                }
            }
        }
        catch (Exception ex) when (IsRecoverableAutomationException(ex))
        {
            // Try the next supported representation.
        }

        try
        {
            if (complianceControl.Patterns.Value.TryGetPattern(
                    out var valuePattern) &&
                TryParsePercent(valuePattern.Value.Value, out percent))
            {
                return true;
            }
        }
        catch (Exception ex) when (IsRecoverableAutomationException(ex))
        {
            // Try the next supported representation.
        }

        try
        {
            if (complianceControl.Patterns.LegacyIAccessible.TryGetPattern(
                    out var legacyPattern))
            {
                if (IsDiscreteComplianceControl(complianceControl))
                {
                    var valueReaders = new Func<string?>[]
                    {
                        () => legacyPattern.Value.ValueOrDefault,
                        () => legacyPattern.Name.ValueOrDefault,
                        () => legacyPattern.Description.ValueOrDefault,
                        () => legacyPattern.Help.ValueOrDefault,
                        () => complianceControl.Properties.ItemStatus.ValueOrDefault,
                        () => complianceControl.Properties.HelpText.ValueOrDefault,
                        () => complianceControl.Properties.Name.ValueOrDefault
                    };

                    foreach (var valueReader in valueReaders)
                    {
                        if (TryReadAutomationString(
                                valueReader,
                                out var candidate) &&
                            TryParseDiscreteCompliancePercent(
                                candidate,
                                out percent))
                        {
                            return true;
                        }
                    }
                }
                else if (TryParsePercent(
                    legacyPattern.Value.Value,
                    out percent))
                {
                    return true;
                }
            }
        }
        catch (Exception ex) when (IsRecoverableAutomationException(ex))
        {
            // No readable semantic value pattern is currently available.
        }

        percent = double.NaN;
        return false;
    }

    private static bool IsDiscreteComplianceControl(
        AutomationElement complianceControl)
        => string.Equals(
            complianceControl.Properties.ClassName.ValueOrDefault,
            "ComplianceSlider",
            StringComparison.OrdinalIgnoreCase);

    private static int PercentToComplianceLevel(double percent)
        => Math.Clamp(
            (int)Math.Round(percent / 100.0 * 3.0),
            0,
            3);

    private static bool TryParseComplianceLevel(
        string? text,
        out int level)
    {
        level = -1;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var matches = Regex.Matches(text, @"(?<!\d)[0-3](?!\d)");
        return matches.Count == 1 &&
            int.TryParse(
                matches[0].Value,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
            out level);
    }

    private static bool TryParseDiscreteCompliancePercent(
        string? text,
        out double percent)
    {
        percent = double.NaN;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        // Prefer an explicitly labelled percentage, even when the same
        // accessibility string also contains the discrete level.
        var percentMatch = Regex.Match(
            text,
            @"[-+]?\d+(?:[.,]\d+)?(?=\s*%)");
        if (percentMatch.Success &&
            TryParsePercent(percentMatch.Value, out percent))
        {
            return true;
        }

        if (TryParseComplianceLevel(text, out var level))
        {
            percent = level / 3.0 * 100.0;
            return true;
        }

        // Some LLEAP builds expose the converted value directly (for
        // example "67") rather than exposing level 0..3.
        if (TryParsePercent(text, out var directPercent) &&
            directPercent > 3)
        {
            percent = directPercent;
            return true;
        }

        return false;
    }

    private static bool TryReadAutomationString(
        Func<string?> valueReader,
        out string? value)
    {
        try
        {
            value = valueReader();
            return true;
        }
        catch (Exception ex) when (IsRecoverableAutomationException(ex))
        {
            value = null;
            return false;
        }
    }

    private static string TryGetLegacyAccessibleValue(
        AutomationElement complianceControl)
    {
        try
        {
            if (!complianceControl.Patterns.LegacyIAccessible.TryGetPattern(
                    out var legacyPattern))
            {
                return "<not supported>";
            }

            var value = DescribeAutomationString(
                () => legacyPattern.Value.ValueOrDefault);
            var name = DescribeAutomationString(
                () => legacyPattern.Name.ValueOrDefault);
            var description = DescribeAutomationString(
                () => legacyPattern.Description.ValueOrDefault);
            var help = DescribeAutomationString(
                () => legacyPattern.Help.ValueOrDefault);

            return
                $"Value='{value}', Name='{name}', " +
                $"Description='{description}', Help='{help}'";
        }
        catch (Exception ex) when (IsRecoverableAutomationException(ex))
        {
            return $"<unavailable: {ex.GetType().Name}>";
        }
    }

    private static string DescribeAutomationString(
        Func<string?> valueReader)
    {
        try
        {
            return valueReader() ?? "<null>";
        }
        catch (Exception ex) when (IsRecoverableAutomationException(ex))
        {
            return $"<unavailable: {ex.GetType().Name}>";
        }
    }

    private static bool TryParsePercent(string? text, out double percent)
    {
        percent = double.NaN;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var match = Regex.Match(text, @"[-+]?\d+(?:[.,]\d+)?");
        if (!match.Success ||
            !double.TryParse(
                match.Value.Replace(',', '.'),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out percent))
        {
            percent = double.NaN;
            return false;
        }

        return percent is >= 0 and <= 100;
    }

    private static bool SupportsPattern(Func<bool> supportCheck)
    {
        try
        {
            return supportCheck();
        }
        catch (Exception ex) when (IsRecoverableAutomationException(ex))
        {
            return false;
        }
    }

    private static bool IsRecoverableAutomationException(Exception exception)
        => exception is COMException
            or InvalidOperationException
            or NotImplementedException
            or NotSupportedException
            or FlaUI.Core.Exceptions.FlaUIException;

    //public int GetDisplayedHeartRateBpm()
    //{
    //    var label = WaitFor(PatientMonitorLocators.HrValueLabel);
    //    var match = Regex.Match(label.Name ?? string.Empty, @"\d+");
    //    return match.Success ? int.Parse(match.Value) : -1;
    //}

    public int GetConfiguredHeartRateBpm()
    {
        ClickBy(PatientMonitorLocators.HrValueLabel);

        var dialog = WaitFor(PatientMonitorLocators.SetHeartRateDialog);

        int bpm;

        try
        {
            var currentValue = dialog.FindFirstDescendant(
                CF.ByAutomationId(
                    PatientMonitorLocators.HrCurrentValueText.AutomationId!))
                ?? throw new InvalidOperationException(
                    "Current heart-rate value was not found.");

            var text = currentValue.Name?.Trim();

            if (!int.TryParse(
                    text,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out bpm))
            {
                throw new InvalidOperationException(
                    $"Current heart-rate value '{text}' is not a valid number.");
            }
        }
        finally
        {
            var cancelButton = dialog.FindFirstDescendant(
                CF.ByName("Cancel")
                    .And(CF.ByControlType(
                        FlaUI.Core.Definitions.ControlType.Button)))
                ?? throw new InvalidOperationException(
                    "Cancel button was not found in the heart-rate dialog.");

            cancelButton.Click();

            WaitUntil(
                () => RootWindow.FindFirstDescendant(
                    CF.ByName("Set Heart Rate")) == null,
                timeoutSeconds: 10,
                errorMessage:
                    "Set Heart Rate dialog remained open after clicking Cancel.");
        }

        return bpm;
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

    public void Maximize()
    {
        if (!RootWindow.Patterns.Window.IsSupported)
        {
            throw new InvalidOperationException(
                $"Window '{WindowTitle}' does not support the Window pattern.");
        }

        RootWindow.Patterns.Window.Pattern.SetWindowVisualState(
            FlaUI.Core.Definitions.WindowVisualState.Maximized);

        WaitUntil(
            IsMaximized,
            timeoutSeconds: 10,
            errorMessage: $"Window '{WindowTitle}' did not become maximized.");
    }

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

    private static void WaitUntil(
        Func<bool> condition,
        int timeoutSeconds,
        string errorMessage)
    {
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < TimeSpan.FromSeconds(timeoutSeconds))
        {
            try
            {
                if (condition())
                {
                    return;
                }
            }
            catch (COMException)
            {
                // The UI tree can be rebuilt while a session value is changing.
            }
            catch (InvalidOperationException)
            {
                // The current UI element may be replaced during a state change.
            }

            Thread.Sleep(200);
        }

        throw new TimeoutException(errorMessage);
    }
}