using Microsoft.Extensions.Configuration;
using System.Runtime.InteropServices;

namespace LLEAP.UITests.Configuration;

public class TestSettings
{
    private static TestSettings? _instance;
    private static readonly object _instanceLock = new object();
    public TimeoutSettings Timeouts { get; set; } = new();
    public PathsSettings Paths { get; set; } = new();
    public LanguageSettings Language {  get; set; } = new();
    public static TestSettings Instance
    {
        get
        {
            lock(_instanceLock)
            {
                if (_instance == null)
                {
                    _instance = Load();
                }
                return _instance;
            }
        }
    }

    private static TestSettings Load()
    {
        var configuration = new ConfigurationBuilder().SetBasePath(AppContext.BaseDirectory).AddJsonFile("appsettings.json", optional: false, reloadOnChange: false).Build();
        var settings = new TestSettings();
        configuration.Bind(settings);
        return settings;
    }
}

public class TimeoutSettings
{
    public int DefaultTimeoutSeconds { get; set; } = 30;
    public int ImplicitWaitSeconds { get; set; } = 10;
}

public class PathsSettings
{
    public string SimulationHomeExePath { get; set; } = @"C:\\Program Files (x86)\\Laerdal Medical\\Laerdal Simulation Home\\LaunchPortal.exe";
    public string ScreenshotDirectory { get; set; } = @"TestResults\\Screenshots";
    public string LogDirectory { get; set; } = @"TestResults\\Logs";
    public string[] ClientLogSearchDirectories { get; set; } = [];
    public string ClientLogArtifactPattern { get; set; } = "*";
    public bool ClientLogIncludeSubdirectories { get; set; } = true;
    public string ClientArtifactPathHint { get; set; } = "Laerdal Report Zipped";
}

public class LanguageSettings
{
    public string Ui { get; set; } = "English";
}