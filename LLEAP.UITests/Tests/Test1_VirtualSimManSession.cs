using LLEAP.UITests.Pages;

namespace LLEAP.UITests.Tests;

[TestFixture]
[Category("Smoke")]
public class Test1_VirtualSimManSession : TestBase
{
    [Test]
    [Description("Verifies that a full session can be started with Virtual SimMan3G Plus without a license.")]
    public void CanRunSessionWithVirtualSimManWithoutLicense()
    {
        const string pausedSessionTitle = "PAUSE - Healthy patient - Virtual SimMan 3G - Manual Mode - LLEAP";
        const string runningSessionTitle = "Healthy patient - Virtual SimMan 3G - Manual Mode - LLEAP";

        CaptureStep("01_SimulationHome");
        Assert.That(SimulationHome, Is.Not.Null, "Step 1 - Simulation Home should be open.");

        Step("02_OpenInstructorApp", SimulationHome.OpenInstructorApplication);
        InstructorAppPage instructorApp = SwitchToInstructorApp("Startup");

        Step("03_SkipLicense", instructorApp.SkipLicenseActivation);

        Step("04_SelectLocalComputer", instructorApp.SelectLocalComputerSimulator);

        Step("05_SelectSimMan3GPlus", instructorApp.SelectSimMan3GPlus);

        var instructorProcessId = instructorApp.ProcessId;
        Step("06_SelectManualMode", instructorApp.SelectManualMode);

        var selectThemeWindow = AppDriver.WaitForTopLevelWindow(
            exactTitle: "Select theme",
            expectedProcessId: instructorProcessId,
            timeoutSeconds: 60);
        var selectThemePage = new SelectThemePage(selectThemeWindow);

        Step("07_SelectHealthyPatientTheme",
            () => selectThemePage.SelectPatient("Healthy patient"));

        Step("08_ConfirmSessionConfig", selectThemePage.Confirm);

        var pausedSessionWindow = AppDriver.WaitForTopLevelWindow(
            exactTitle: pausedSessionTitle,
            expectedProcessId: instructorProcessId,
            timeoutSeconds: 60);
        var pausedSessionPage = new SessionPage(pausedSessionWindow);
        var sessionProcessId = pausedSessionPage.ProcessId;

        Step("09_StartSimulation", pausedSessionPage.StartSession);

        var runningSessionWindow = AppDriver.WaitForTopLevelWindow(
            exactTitle: runningSessionTitle,
            expectedProcessId: sessionProcessId,
            timeoutSeconds: 60);
        var session = new SessionPage(runningSessionWindow);

        Assert.That(
            session.WindowTitle.Trim(),
            Is.EqualTo(runningSessionTitle).IgnoreCase,
            "Step 9 - Session window should leave PAUSE mode after Start session is clicked!");
        CaptureStep("09-SessionRunning");

        Step("10_Maximize", session.Maximize);
        Assert.That(session.IsMaximized(), Is.True,
            "Step 10 – Session window should be maximized.");

        Step("11_ClosePatientEyes", session.ClosePatientEyes);
        Assert.That(session.GetEyesState(), Does.Contain("Closed").IgnoreCase,
            "Step 11 – Eyes control should display 'Closed'.");

        Step("12_LungCompliance67", () => session.AdjustLungCompliance(67));
        Assert.That(session.GetLungCompliancePercent(), Is.EqualTo(67.0).Within(1.0),
            "Step 12 – Lung compliance should be set to approximately 67 %.");

        Step("13_HeartRate100", () => session.AdjustHeartRate(100));
        //Assert.That(session.GetDisplayedHeartRateBpm(), Is.EqualTo(100),
        //    "Step 13 – Heart rate on the patient monitor should read 100 bpm.");

        Step("14_PlayCoughingVoice",
            () => session.PlayVoice(SessionLocators.CoughingVoiceItem));
        Assert.That(session.IsVoiceSelected(SessionLocators.CoughingVoiceItem), Is.True,
            "Step 14 – 'Coughing' voice should be selected after playback was triggered.");

        Step("15_EndSession", session.EndSession);
        Assert.That(
            AppDriver.WaitForTopLevelWindowToClose(
                exactTitle: runningSessionTitle,
                expectedProcessId: sessionProcessId,
                timeoutSeconds: 30),
            Is.True,
            "Step 15 - The running session window should have closed.");
    }
}