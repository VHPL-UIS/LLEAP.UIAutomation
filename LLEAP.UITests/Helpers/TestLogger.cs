using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting;
using Serilog.Formatting.Display;
using LLEAP.UITests.Configuration;

namespace LLEAP.UITests.Helpers;

public static class TestLogger
{
    private const string NUnitTemplate = "[{Level:u3}] {SourceContext}: {Message:lj}{Exception}";
    private const string FileTemplate = "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {SourceContext} | {Message:lj}{NewLine}{Exception}";
    public static void Initialize()
    {
        string logDir = TestSettings.Instance.Paths.LogDirectory;
        Directory.CreateDirectory(logDir);
        string logFile = Path.Combine(logDir, $"run_{DateTime.Now:yyyyMMdd_HHmmss}.log");
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .Enrich.WithProperty("Suite", "LLEAP.UITests")
            .WriteTo.Sink(new NUnitSink(
                new MessageTemplateTextFormatter(NUnitTemplate)))
            .WriteTo.File(logFile,
                outputTemplate: FileTemplate,
                flushToDiskInterval: TimeSpan.FromSeconds(1))
            .CreateLogger();

        Log.Logger.Information("Test run started. Log fiel: {LogFile}", logFile);
    }

    public static void CloseAndFlush()
    {
        Log.Logger.Information("Test run finished.");
        Log.CloseAndFlush();
    }

    private sealed class NUnitSink : ILogEventSink
    {
        private readonly ITextFormatter _formatter;
        internal NUnitSink( ITextFormatter formatter ) => _formatter = formatter;
        public void Emit(LogEvent logEvent)
        {
            var writer = new StringWriter();
            _formatter.Format(logEvent, writer);
            try
            {
                TestContext.Out.WriteLine(writer.ToString().TrimEnd('\r', '\n'));
            }
            catch
            { }
        }
    }
}