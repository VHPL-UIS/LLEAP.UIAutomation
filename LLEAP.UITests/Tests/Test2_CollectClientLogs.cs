using LLEAP.UITests.Helpers;

namespace LLEAP.UITests.Tests;

[TestFixture]
[Category("Smoke")]
[Category("RequiresAdministrator")]
public sealed class Test2_CollectClientLogs : TestBase
{
    [Test]
    [Description("Verifies that Simulation Home can collect client log files.")]
    public void CanCollectClientLogs()
    {
        CaptureStep("01_SimulationHome");
        Assert.That(
            SimulationHome,
            Is.Not.Null,
            "Step 1 - Laerdal Simulation Home should be open.");

        Assert.That(
            AppDriver.IsCurrentProcessElevated,
            Is.True,
            "Step 4 - Run this test from an elevated interactive terminal. " +
            "An ordinary FlaUI test cannot approve a UAC prompt on Windows' " +
            "secure desktop.");

        Step(
            "02_OpenHelpContextMenu",
            SimulationHome.OpenHelpContextMenu);

        // Capture the output baseline immediately before invoking the
        // collector so pre-existing reports cannot satisfy verification.
        using var logWatcher =
            ClientLogCollectionWatcher.Start();

        Step(
            "03_SelectCollectClientLogs",
            SimulationHome.SelectCollectClientLogs);

        CollectedClientLogArtifact collectedArtifact = null!;
        Step(
            "04_05_WaitForLogsCollected",
            () =>
            {
                try
                {
                    collectedArtifact =
                        logWatcher.WaitForCompletedArtifact(
                            TimeSpan.FromMinutes(3));
                }
                catch (TimeoutException ex)
                {
                    throw new TimeoutException(
                        ex.Message +
                        Environment.NewLine +
                        "Visible desktop windows at timeout:" +
                        Environment.NewLine +
                        AppDriver.DescribeVisibleDesktopWindows(),
                        ex);
                }
            });

        Assert.Multiple(() =>
        {
            Assert.That(
                collectedArtifact.Length,
                Is.GreaterThan(0),
                "Step 5 - The collected client-log artifact should not be empty.");
            Assert.That(
                collectedArtifact.ContainedFileCount,
                Is.GreaterThan(0),
                "Step 5 - The collected result should contain files.");
        });

        Log.Information(
            "Client logs verified: Path='{ArtifactPath}', " +
            "Length={ArtifactLength}, ContainedFiles={ContainedFileCount}, " +
            "IsZipArchive={IsZipArchive}",
            collectedArtifact.FilePath,
            collectedArtifact.Length,
            collectedArtifact.ContainedFileCount,
            collectedArtifact.IsZipArchive);
    }
}