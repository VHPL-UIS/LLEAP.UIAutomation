using FlaUI.Core.AutomationElements;
using FlaUI.Core.Capturing;
using NUnit.Framework.Interfaces;
using Serilog;
using System.Collections.Concurrent;

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
            //TestContext.AddTestAttachment(filePath, $"Screenshot - {stepName}");
            RememberAttachment(filePath, $"Screenshot - {stepName}");
            _log.Debug("Screenshot saved: {FilePath}", filePath);
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "Full screen capture failed for step {StepName}", stepName);
        }
        return filePath;
    }

    private static void RememberAttachment(string filePath, string description)
    {
        string testId = TestContext.CurrentContext.Test.ID;
        if (!Runs.TryGetValue(testId, out ScreenshotRun? run))
        {
            return;
        }

        lock (run.SyncRoot)
        {
            run.Attachments.Add((Path.GetFileName(filePath), description));
        }
    }

    private static string BuildFilePath(string stepName)
    {
        string testId = TestContext.CurrentContext.Test.ID;
        if (!Runs.TryGetValue(testId, out ScreenshotRun? run))
        {
            throw new InvalidOperationException("BeginTestRun must be called before capturing screenshots!");
        }

        string timestamp = DateTime.Now.ToString("HHmmss_fff");
        string safeStep = SanitizeFileName(stepName);
        return Path.Combine(run.WorkingDirectory, $"{safeStep}_{timestamp}.png");
        //    string screenshotDir = Configuration.TestSettings.Instance.Paths.ScreenshotDirectory;
        //    string testName = SanitizeFileName(TestContext.CurrentContext.Test.Name);
        //    string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
        //    string safeStep = SanitizeFileName(stepName);
        //    return Path.Combine(screenshotDir, $"{testName}_{safeStep}_{timestamp}.png");
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
            //TestContext.AddTestAttachment(filePath, $"Element screenshot - {stepName}");
            RememberAttachment(filePath, $"Element screenshot - {stepName}");
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "Element capture failed for step {StepName} - falling back to full screen", stepName);
            return CaptureAndAttach(stepName);
        }
        return filePath;
    }

    private sealed class ScreenshotRun
    {
        public required string RootDirectory { get; init; }
        public required string FolderBaseName { get; init; }
        public required string WorkingDirectory { get; init; }
        public List<(string FileName, string Description)> Attachments { get; } = [];
        public object SyncRoot { get; } = new();
    }

    private static readonly ConcurrentDictionary<string, ScreenshotRun> Runs = new();

    public static void BeginTestRun(string fixtureName, string testName)
    {
        string rootDirectory = Configuration.TestSettings.Instance.Paths.ScreenshotDirectory;
        string startedAt = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss-fff");
        string folderBaseName = $"{SanitizeFileName(fixtureName)}__" +
            $"{SanitizeFileName(testName)}__" +
            startedAt;
        string workingDirectory = Path.Combine(rootDirectory, folderBaseName + "__RUNNING");
        Directory.CreateDirectory(workingDirectory);

        var run = new ScreenshotRun
        {
            RootDirectory = rootDirectory,
            FolderBaseName = folderBaseName,
            WorkingDirectory = workingDirectory,
        };

        string testId = TestContext.CurrentContext.Test.ID;

        if (!Runs.TryAdd(testId, run))
        {
            throw new InvalidOperationException($"A screenshot directory already exists for test ID '{testId}");
        }

        _log.Information("Screenshot directory created: {ScreenshotDirectory}", workingDirectory);
    }

    public static void CompleteTestRun(TestStatus status)
    {
        string testId = TestContext.CurrentContext.Test.ID;
        if (!Runs.TryRemove(testId, out ScreenshotRun? run))
        {
            _log.Warning("No screenshot run was registered for test ID {TestId}", testId);
            return;
        }
        string statusText = status switch
        {
            TestStatus.Passed => "PASSED",
            TestStatus.Failed => "FAILED",
            TestStatus.Skipped => "SKIPPED",
            _ => status.ToString().ToUpperInvariant(),
        };

        string finalDirectory = Path.Combine(run.RootDirectory, $"{run.FolderBaseName}__{statusText}");
        string attachmentDirectory = run.WorkingDirectory;

        try
        {
            Directory.Move(run.WorkingDirectory, finalDirectory);
            attachmentDirectory = finalDirectory;
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "Could not rename screenshot directory to include test result!");
        }

        lock (run.SyncRoot)
        {
            foreach (var attachment in run.Attachments)
            {
                string finalPath = Path.Combine(attachmentDirectory, attachment.FileName);
                if (File.Exists(finalPath))
                {
                    TestContext.AddTestAttachment(finalPath, attachment.Description);
                }
            }
        }

        _log.Information("Screenshot directory finalized: {ScreenshotDirectory}", attachmentDirectory);
    }
}