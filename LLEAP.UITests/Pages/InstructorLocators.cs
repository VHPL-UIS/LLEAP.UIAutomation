namespace LLEAP.UITests.Pages;

public static class InstructorLocators
{
    public static class License
    {
        public static readonly Locator AddLicenseLaterButton = Locator.ByName("Add license later");
    }

    public static class SimulatorSelection
    {
        public static readonly Locator LocalComputerTile = Locator.ByName("Local computer");
        public static readonly Locator SimMan3GPlusButton = Locator.ByName("SimMan 3G PLUS");
        public static readonly Locator ContinueWithoutDebriefingLink = Locator.ByName("... or continue without a debriefing system");
    }

    public static class SessionSetup
    {
        public static readonly Locator InternationalPreferencesButton = Locator.ByName("International user preferences");
        public static readonly Locator ManualModeButton = Locator.ByName("Manual Mode");
        public static readonly Locator ThemesDropdown = Locator.ByName("Themes");
        public static readonly Locator HealthyPatientTheme = Locator.ByName("Healthy patient");
        public static readonly Locator OkButton = Locator.ByName("Ok");
        public static readonly Locator StartSessionButton = Locator.ByName("Start session");
    }
}