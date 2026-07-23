using NUnit.Framework;
using LLEAP.UITests.Configuration;

namespace LLEAP.UITests.Tests;

[TestFixture]
public class ConfigurationSmokeTest
{
    [Test]
    public void SettingsLoad_WithoutThrowing()
    {
        var settings = TestSettings.Instance;
        Assert.That(settings.Paths.SimulationHomeExePath, Is.Not.Empty, "SimulationHomeExePath should be loaded from appsettings.json");
        Assert.That(settings.Timeouts.DefaultTimeoutSeconds, Is.GreaterThan(0), "DefaultTimeoutSeconds should be a positive number");
        Assert.That(settings.Timeouts.ImplicitWaitSeconds, Is.GreaterThan(0), "ImplicitWaitSeconds should be a positive number");
        Assert.That(settings.Language.Ui, Is.Not.Null.And.Not.Empty, "Language.Ui should be loaded from appsettings.json");
        TestContext.Out.WriteLine($"Exe path: {settings.Paths.SimulationHomeExePath}");
        TestContext.Out.WriteLine($"DefaultTimeout: {settings.Timeouts.DefaultTimeoutSeconds}");
        TestContext.Out.WriteLine($"ImplicitWait: {settings.Timeouts.ImplicitWaitSeconds}");
        TestContext.Out.WriteLine($"Language.ui: {settings.Language.Ui}");
    }
}