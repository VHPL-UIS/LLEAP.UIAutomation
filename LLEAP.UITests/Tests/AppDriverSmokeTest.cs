using FlaUI.Core;
using FlaUI.UIA3;
using NUnit.Framework;
using LLEAP.UITests.Drivers;
using FlaUI.Core.Tools;

namespace LLEAP.UITests.Tests;

[TestFixture]
[Category("Smoke")]
public class AppDriverSmokeTest
{
    [Test]
    public void CanLaunchAndAttachToApplication()
    {
        using var automation = new UIA3Automation();
        var app = FlaUI.Core.Application.Launch("notepad.exe");
        try
        {
           var desktop = automation.GetDesktop();
            var window = Retry.WhileNull(() => desktop.FindFirstChild(cf => cf.ByName("Untitled - Notepad")), timeout: TimeSpan.FromSeconds(10), throwOnTimeout: false);
            Assert.That(window.Result, Is.Not.Null);
        }
        finally
        {
            app.Close();
            app.Dispose();
        }
    }
}