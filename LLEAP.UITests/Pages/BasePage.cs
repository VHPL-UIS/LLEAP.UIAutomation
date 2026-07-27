using FlaUI.Core.AutomationElements;
using FlaUI.Core.Conditions;
using FlaUI.Core.Tools;
using LLEAP.UITests.Configuration;

namespace LLEAP.UITests.Pages;

public abstract class BasePage
{
    protected readonly Window RootWindow;
    protected readonly int TimeoutSeconds;
    protected readonly ConditionFactory CF;
    protected BasePage(Window rootWindow)
    {
        RootWindow = rootWindow;
        TimeoutSeconds = TestSettings.Instance.Timeouts.DefaultTimeoutSeconds;
        CF = rootWindow.ConditionFactory;
    }

    private AutomationElement WaitForElement(FlaUI.Core.Conditions.PropertyCondition condition, int? timeoutSeconds)
    {
        var timeout = TimeSpan.FromSeconds(timeoutSeconds ?? TimeoutSeconds);
        var result = Retry.WhileNull(
            () => RootWindow.FindFirstDescendant(condition),
            timeout: timeout,
            interval: TimeSpan.FromMilliseconds(300),
            throwOnTimeout: true,
            ignoreException: false);
        return result.Result!;
    }

    protected AutomationElement WaitForElementById(string automationId, int? timeoutSeconds = null)
        => WaitForElement(CF.ByAutomationId(automationId), timeoutSeconds);

    protected AutomationElement WaitForElementByName(string name, int? timeoutSeconds = null)
        => WaitForElement(CF.ByName(name), timeoutSeconds);

    protected AutomationElement WaitFor(Locator locator, int? timeoutSeconds = null)
    {
        if (locator.AutomationId != null)
        {
            return WaitForElementById(locator.AutomationId, timeoutSeconds);
        }
        if (locator.Name != null)
        {
            return WaitForElementByName(locator.Name, timeoutSeconds);
        }
        throw new InvalidOperationException("Locator must have either AutomationId or Name!");
    }

    protected AutomationElement? TryFindById(string automationId, int timeoutSeconds = 5)
    {
        var result = Retry.WhileNull(
            () => RootWindow.FindFirstDescendant(CF.ByAutomationId(automationId)),
            timeout: TimeSpan.FromSeconds(timeoutSeconds),
            interval: TimeSpan.FromMilliseconds(300),
            throwOnTimeout: false,
            ignoreException: true);
        return result.Success ? result.Result : null;
    }

    protected AutomationElement? TryFindByName(string name, int  timeoutSeconds = 5)
    {
        var result = Retry.WhileNull(
            () => RootWindow.FindFirstDescendant(CF.ByName(name)),
            timeout: TimeSpan.FromSeconds(timeoutSeconds),
            interval: TimeSpan.FromMilliseconds(300),
            throwOnTimeout: false,
            ignoreException: true);
        return result.Success ? result.Result : null;
    }

    protected AutomationElement? TryFind(Locator locator, int timeoutSeconds = 5)
    {
        if (locator.AutomationId != null)
        {
            return TryFindById(locator.AutomationId, timeoutSeconds);
        }
        if (locator.Name != null)
        {
            return TryFindByName(locator.Name, timeoutSeconds);
        }
        return null;
    }

    protected void ClickByName(string name)
        => WaitForElementByName(name).Click();

    protected void ClickById(string automationId)
        => WaitForElementById(automationId).Click();

    protected void ClickBy(Locator locator)
    {
        var element = WaitFor(locator);
        Retry.WhileFalse(
            () => element.IsEnabled && !element.IsOffscreen,
            timeout: TimeSpan.FromSeconds(TimeoutSeconds),
            interval: TimeSpan.FromMilliseconds(200));
        //RootWindow.SetForeground();
        if (element.Patterns.Invoke.IsSupported)
        {
            element.Patterns.Invoke.Pattern.Invoke();
        }
        else
        { 
            element.Click();
        }
    }

    protected void ConfirmDialog(Locator confirmButton)
        => WaitFor(confirmButton).Click();

    protected void SelectFromDropdown(Locator trigger, Locator item)
    {
        WaitFor(trigger).Click();
        WaitFor(item).Click();
    }

    public int ProcessId => RootWindow.Properties.ProcessId.ValueOrDefault;
    public string WindowTitle => RootWindow.Title;
}