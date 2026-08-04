using LLEAP.UITests.Drivers;
using LLEAP.UITests.Helpers;
using LLEAP.UITests.Pages;
using NUnit.Framework.Interfaces;

namespace LLEAP.UITests.Tests;

[TestFixture]
public abstract class TestBase
{
    protected Serilog.ILogger Log { get; private set; } = Serilog.Log.Logger;
    protected AppDriver AppDriver { get; private set; } = null!;
    protected SimulationHomePage SimulationHome { get; private set; } = null!;

    [SetUp]
    public void SetUp()
    {
        ScreenshotHelper.BeginTestRun(GetType().Name, TestContext.CurrentContext.Test.Name);
        Log = Serilog.Log.Logger.ForContext(GetType());
        Log.Information("Test starting: {TestName}", TestContext.CurrentContext.Test.Name);
        AppDriver = new AppDriver();
        var homeWindow = AppDriver.LaunchSimulationHome();
        SimulationHome = new SimulationHomePage(homeWindow);
    }

    [TearDown]
    public void TearDown()
    {
        var result = TestContext.CurrentContext.Result;
        var finalStatus = result.Outcome.Status;
        try
        {
            if (finalStatus == TestStatus.Failed)
            {
                Log.Error("Test FAILED: {TestName} - {Message}",
                    TestContext.CurrentContext.Test.Name, result.Message);
                try
                {
                    ScreenshotHelper.CaptureAndAttach("FAILURE_" + TestContext.CurrentContext.Test.Name);
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failure screenshot could not be captured!");
                }
            }
            else
            {
                Log.Information("Test passed: {TestName}",
                    TestContext.CurrentContext.Test.Name);
            }
            AppDriver?.Dispose();
        }
        catch
        {
            finalStatus = TestStatus.Failed;
            throw;
        }
        finally
        {
            ScreenshotHelper.CompleteTestRun(finalStatus);
        }
    }

    protected InstructorAppPage SwitchToInstructorApp(string windowTitle = "LLEAP")
    {
        var instructorWindow = AppDriver.AttachToInstructorApp(windowTitle, 90);
        return new InstructorAppPage(instructorWindow);
    }

    protected void CaptureStep(string stepDescription)
        => ScreenshotHelper.CaptureAndAttach(stepDescription);

    protected void Step(string name, Action action)
    {
        Log.Debug("Step {StepName} - before", name);
        CaptureStep($"{name}_before");
        action();
        CaptureStep($"{name}_after");
        Log.Debug("Step {StepName} - after", name);
    }
}