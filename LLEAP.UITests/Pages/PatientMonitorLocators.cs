namespace LLEAP.UITests.Pages;

public static class PatientMonitorLocators
{
    public static readonly Locator EyeControl = Locator.ById("EyesComboBox", "Eyes");
    public static readonly Locator EyesCloseOption = Locator.ByName("Closed");
    public static readonly Locator LungComplianceSlider = Locator.ById("compliance", "Lung Compliance");
    //public static readonly Locator LungComplianceSlider = Locator.ByName("Total lung compliance");
    public static readonly Locator HrValueLabel = Locator.ById("11", "HR");
    public static readonly Locator HrInputField = Locator.ById("2093", "HR Value");
}