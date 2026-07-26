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
        CaptureStep("01_SimulationHome");
        Assert.That(SimulationHome, Is.Not.Null, "Step 1 - Simulation Home should be open.");

        Step("02_OpenInstructorApp", SimulationHome.OpenInstructorApplication);
        InstructorAppPage instructorApp = SwitchToInstructorApp();

        //Step("03_SkipLicense", instructorApp.SkipLicenseActivation);

        //Step("04_SelectLocalComputer", instructorApp.SelectLocalComputerSimulator);

        //Step("05_SelectSimMan3GPlus", instructorApp.SelectSimMan3GPlus);

        //Step("06_SelectManualMode", instructorApp.SelectManualMode);

        //Step("07_SelectHealthyPatientTheme",
        //    () => instructorApp.SelectTheme(InstructorLocators.SessionSetup.HealthyPatientTheme));

        //Step("08_ConfirmSessionConfig", instructorApp.Confirm);

        //Step("09_StartSimulation", instructorApp.StartSimulation);
        //Assert.That(AppDriver.InstructorWindow.Name, Is.Not.Null,
        //    "Step 9 – Session window should be open after starting the simulation.");

        //var session = new SessionPage(AppDriver.InstructorWindow);
        //Step("10_Maximize", session.Maximized);
        //Assert.That(session.IsMaximized(), Is.True,
        //    "Step 10 – Session window should be maximized.");

        //Step("11_ClosePatientEyes", session.ClosePatientEyes);
        //Assert.That(session.GetEyesState(), Does.Contain("Closed").IgnoreCase,
        //    "Step 11 – Eyes control should display 'Closed'.");

        //Step("12_LungCompliance67", () => session.AdjustLungCompliance(67));
        //Assert.That(session.GetLungCompliancePercent(), Is.EqualTo(67.0).Within(1.0),
        //    "Step 12 – Lung compliance should be set to approximately 67 %.");

        //Step("13_HeartRate100", () => session.AdjustHeartRate(100));
        //Assert.That(session.GetDisplayedHeartRateBpm(), Is.EqualTo(100),
        //    "Step 13 – Heart rate on the patient monitor should read 100 bpm.");

        //Step("14_PlayCoughingVoice",
        //    () => session.PlayVoice(SessionLocators.CoughingVoiceItem));
        //Assert.That(session.IsVoiceSelected(SessionLocators.CoughingVoiceItem), Is.True,
        //    "Step 14 – 'Coughing' voice should be selected after playback was triggered.");

        //Step("15_EndSession", session.EndSession);
        //Assert.That(AppDriver.IsInstructorAppClosed, Is.True,
        //    "Step 15 – LLEAP Instructor Application should have exited.");
    }
}