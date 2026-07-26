using LLEAP.UITests.Helpers;

namespace LLEAP.UITests.Tests;

[SetUpFixture]
public class AssemblySetup
{
    [OneTimeSetUp]
    public void InitializeLogging() => TestLogger.Initialize();
    [OneTimeTearDown]
    public void FlushLogging() => TestLogger.CloseAndFlush();
}