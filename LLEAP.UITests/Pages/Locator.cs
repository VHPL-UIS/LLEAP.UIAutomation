namespace LLEAP.UITests.Pages;

public readonly record struct Locator
{
    public string? AutomationId { get; init; }
    public string? Name { get; init; }
    public static Locator ById(string automationId) => new() { AutomationId = automationId };
    public static Locator ById(string automationId, string nameHint) => new() { AutomationId = automationId, Name = nameHint };
    public static Locator ByName(string name) => new() { Name = name };
}