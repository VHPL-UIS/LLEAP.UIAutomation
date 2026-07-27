namespace LLEAP.UITests.Pages;

public static class SessionLocators
{
    public static readonly Locator StartSessionButton = Locator.ByName("Start session");
    public static readonly Locator VoicePanel = Locator.ById("VocalSoundPanel", "Body sounds");
    public static readonly Locator CoughingVoiceItem = Locator.ByName("Coughing");
    public static readonly Locator PlayVoiceButton = Locator.ById("PlayButton", "Play");
}