using FlaUI.Core.AutomationElements;
using FlaUI.Core.Capturing;
using NUnit.Framework;
using Serilog;

namespace LLEAP.UITests.Helpers;

public static class ScreenshotHelper
{
    private static readonly ILogger _log = Log.ForContext(typeof(ScreenshotHelper));
    public static string CaptureAndAttach(string stepName)
    {
        string filePath = BuildFilePath(stepName);
        EnsureDirectory(filePath);
        try
        {
            using var image = Capture.Screen();
            image.ToFile(filePath);
            TestContext.AddTestAttachment(filePath, $"Screenshot - {stepName}");
            _log.Debug("Screenshot saved: {FilePath}", filePath);
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "Full screen capture failed for step {StepName}", stepName);
        }
        return filePath;
    }

    private static string BuildFilePath(string stepName)
    {
        string screenshotDir = Configuration.TestSettings.Instance.Paths.ScreenshotDirectory;
        string testName = SanitizeFileName(TestContext.CurrentContext.Test.Name);
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
        string safeStep = SanitizeFileName(stepName);
        return Path.Combine(screenshotDir, $"{testName}_{safeStep}_{timestamp}.png");
    }

    private static void EnsureDirectory(string filePath)
    {
        string? dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
    }

    private static string SanitizeFileName(string name)
        => string.Concat(name.Split(Path.GetInvalidFileNameChars())).Replace(' ', '_');

    public static string CaptureElementAndAttach(AutomationElement element, string stepName)
    {
        string filePath = BuildFilePath(stepName + "_element");
        EnsureDirectory(filePath);
        try
        {
            using var image = Capture.Element(element);
            image.ToFile(filePath);
            TestContext.AddTestAttachment(filePath, $"Element screenshot - {stepName}");
        }
        catch(Exception ex)
        {
            _log.Warning(ex, "Element capture failed for step {StepName} - falling back to full screen", stepName);
            return CaptureAndAttach(stepName);
        }
        return filePath;
    }
}