using LLEAP.UITests.Configuration;
using Serilog;
using System.Collections.Concurrent;
using System.IO.Compression;

namespace LLEAP.UITests.Helpers;

public sealed record CollectedClientLogArtifact(
    string FilePath,
    long Length,
    int ContainedFileCount,
    bool IsZipArchive);

public sealed class ClientLogCollectionWatcher : IDisposable
{
    private const string MyDocumentsToken = "{MyDocuments}";
    private const string CommonDocumentsToken = "{CommonDocuments}";

    private readonly record struct ArtifactSnapshot(
        long Length,
        DateTime LastWriteTimeUtc);

    private static readonly ILogger Log =
        Serilog.Log.ForContext<ClientLogCollectionWatcher>();

    private readonly List<FileSystemWatcher> _watchers = [];
    private readonly ConcurrentDictionary<string, byte> _candidatePaths =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentQueue<string> _watcherErrors = new();
    private readonly string[] _searchDirectories;
    private readonly string _artifactPattern;
    private readonly string _artifactPathHint;
    private readonly bool _includeSubdirectories;
    private readonly Dictionary<string, ArtifactSnapshot> _baselineArtifacts;
    private readonly Dictionary<
        string,
        (ArtifactSnapshot Snapshot, DateTime StableSinceUtc)>
        _artifactStability = new(StringComparer.OrdinalIgnoreCase);

    private ClientLogCollectionWatcher(
        string[] searchDirectories,
        string artifactPattern,
        string artifactPathHint,
        bool includeSubdirectories)
    {
        _searchDirectories = searchDirectories;
        _artifactPattern = artifactPattern;
        _artifactPathHint = artifactPathHint;
        _includeSubdirectories = includeSubdirectories;
        _baselineArtifacts = CaptureCurrentArtifacts();

        Log.Information(
            "Client-log baseline contains {ArtifactCount} file(s): " +
            "[{BaselineArtifacts}]",
            _baselineArtifacts.Count,
            _baselineArtifacts.Count == 0
                ? "<none>"
                : string.Join(
                    "; ",
                    _baselineArtifacts
                        .OrderBy(artifact => artifact.Key)
                        .Select(artifact =>
                            $"'{artifact.Key}' " +
                            $"(Length={artifact.Value.Length}, " +
                            $"LastWriteUtc=" +
                            $"{artifact.Value.LastWriteTimeUtc:O})")));

        foreach (var directory in searchDirectories)
        {
            FileSystemWatcher? watcher = null;
            try
            {
                watcher = new FileSystemWatcher(
                    directory,
                    artifactPattern)
                {
                    IncludeSubdirectories = includeSubdirectories,
                    NotifyFilter =
                        NotifyFilters.CreationTime |
                        NotifyFilters.DirectoryName |
                        NotifyFilters.FileName |
                        NotifyFilters.LastWrite |
                        NotifyFilters.Size
                };

                watcher.Created += OnArtifactChanged;
                watcher.Changed += OnArtifactChanged;
                watcher.Renamed += OnArtifactRenamed;
                watcher.Error += OnWatcherError;
                watcher.EnableRaisingEvents = true;
                _watchers.Add(watcher);
            }
            catch (Exception ex) when (
                ex is IOException or
                    UnauthorizedAccessException or
                    ArgumentException)
            {
                watcher?.Dispose();
                _watcherErrors.Enqueue(
                    $"Could not watch '{directory}': " +
                    $"{ex.GetType().Name}: {ex.Message}");
            }
        }

        if (_watchers.Count == 0)
        {
            throw new InvalidOperationException(
                "No configured client-log search directory could be watched. " +
                $"Configured directories: [{string.Join(
                    ", ",
                    searchDirectories)}]. " +
                $"Errors: [{string.Join("; ", _watcherErrors)}]");
        }

        Log.Information(
            "Watching for collected client-log artifacts matching " +
            "'{ArtifactPattern}' under: [{SearchDirectories}]; " +
            "IncludeSubdirectories={IncludeSubdirectories}",
            _artifactPattern,
            string.Join(", ", _searchDirectories),
            _includeSubdirectories);
    }

    public static ClientLogCollectionWatcher Start()
    {
        var configuredDirectories =
            TestSettings.Instance.Paths.ClientLogSearchDirectories;
        var resolvedDirectories = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var configuredDirectory in configuredDirectories)
        {
            if (string.IsNullOrWhiteSpace(configuredDirectory))
            {
                continue;
            }

            try
            {
                var expandedDirectory =
                    ExpandSearchDirectory(configuredDirectory);
                var fullPath = Path.GetFullPath(expandedDirectory);
                if (!Directory.Exists(fullPath))
                {
                    Log.Warning(
                        "Resolved client-log search directory does not " +
                        "exist and will not be watched: {SearchDirectory}.",
                        fullPath);
                    continue;
                }

                Log.Information(
                    "Resolved client-log search directory " +
                    "'{ConfiguredDirectory}' to '{ResolvedDirectory}'.",
                    configuredDirectory,
                    fullPath);
                resolvedDirectories.Add(fullPath);
            }
            catch (Exception ex) when (
                ex is ArgumentException or
                    IOException or
                    InvalidOperationException or
                    NotSupportedException or
                    PathTooLongException or
                    UnauthorizedAccessException)
            {
                Log.Warning(
                    ex,
                    "Could not prepare client-log search directory: " +
                    "{SearchDirectory}",
                    configuredDirectory);
            }
        }

        if (resolvedDirectories.Count == 0)
        {
            throw new InvalidOperationException(
                "No Paths:ClientLogSearchDirectories entry could be created " +
                "or accessed. Configure a writable directory where Laerdal " +
                "writes the collected client-log report.");
        }

        var artifactPattern =
            TestSettings.Instance.Paths.ClientLogArtifactPattern;
        if (string.IsNullOrWhiteSpace(artifactPattern) ||
            Path.GetFileName(artifactPattern) != artifactPattern)
        {
            throw new InvalidOperationException(
                "Paths:ClientLogArtifactPattern must be a file-name pattern " +
                "such as '*', without a directory component.");
        }

        return new ClientLogCollectionWatcher(
            resolvedDirectories.OrderBy(path => path).ToArray(),
            artifactPattern,
            TestSettings.Instance.Paths.ClientArtifactPathHint?.Trim()
                ?? string.Empty,
            TestSettings.Instance.Paths.ClientLogIncludeSubdirectories);
    }

    private static string ExpandSearchDirectory(
        string configuredDirectory)
    {
        var expandedDirectory = ReplaceKnownFolderToken(
            configuredDirectory,
            MyDocumentsToken,
            Environment.SpecialFolder.MyDocuments);
        expandedDirectory = ReplaceKnownFolderToken(
            expandedDirectory,
            CommonDocumentsToken,
            Environment.SpecialFolder.CommonDocuments);

        return Environment.ExpandEnvironmentVariables(expandedDirectory);
    }

    private static string ReplaceKnownFolderToken(
        string path,
        string token,
        Environment.SpecialFolder specialFolder)
    {
        if (!path.Contains(
                token,
                StringComparison.OrdinalIgnoreCase))
        {
            return path;
        }

        var knownFolderPath =
            Environment.GetFolderPath(
                specialFolder,
                Environment.SpecialFolderOption.DoNotVerify);
        if (string.IsNullOrWhiteSpace(knownFolderPath))
        {
            throw new InvalidOperationException(
                $"Windows did not return a filesystem path for " +
                $"{specialFolder}.");
        }

        return path.Replace(
            token,
            knownFolderPath,
            StringComparison.OrdinalIgnoreCase);
    }

    public CollectedClientLogArtifact WaitForCompletedArtifact(
        TimeSpan timeout)
    {
        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(timeout),
                "Timeout must be greater than zero.");
        }

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var nextRescan = TimeSpan.Zero;
        while (stopwatch.Elapsed < timeout)
        {
            if (stopwatch.Elapsed >= nextRescan)
            {
                DiscoverNewOrModifiedArtifacts();
                nextRescan = stopwatch.Elapsed.Add(
                    TimeSpan.FromSeconds(1));
            }

            foreach (var candidatePath in
                     _candidatePaths.Keys.OrderBy(path => path))
            {
                if (IsNewOrModifiedSinceBaseline(candidatePath) &&
                    IsArtifactStableFor(
                        candidatePath,
                        TimeSpan.FromSeconds(1)) &&
                    TryReadCompletedArtifact(
                        candidatePath,
                        out var artifact))
                {
                    Log.Information(
                        "Collected client-log artifact found: Path='{ArtifactPath}', " +
                        "Length={ArtifactLength}, ContainedFiles={ContainedFileCount}, " +
                        "IsZipArchive={IsZipArchive}",
                        artifact.FilePath,
                        artifact.Length,
                        artifact.ContainedFileCount,
                        artifact.IsZipArchive);
                    return artifact;
                }
            }

            Thread.Sleep(250);
        }

        var candidates = _candidatePaths.IsEmpty
            ? "<none>"
            : string.Join(
                "; ",
                _candidatePaths.Keys.OrderBy(path => path));
        var errors = _watcherErrors.IsEmpty
            ? "<none>"
            : string.Join("; ", _watcherErrors);
        var directoryContents = DescribeSearchDirectoryContents();

        throw new TimeoutException(
            $"No new, readable, non-empty client-log artifact was completed " +
            $"within {timeout.TotalSeconds:0} seconds. " +
            $"Pattern: '{_artifactPattern}'. " +
            $"Path hint: '{_artifactPathHint}'. " +
            $"Search directories: [{string.Join(", ", _searchDirectories)}]. " +
            $"Include subdirectories: {_includeSubdirectories}. " +
            $"Artifact paths observed: [{candidates}]. " +
            $"Watcher errors: [{errors}]. " +
            $"Search-directory contents at timeout: " +
            $"[{directoryContents}]. " +
            "If Laerdal displayed a Save As or completion dialog instead, " +
            "use the failure screenshot and add its exact locator; if it " +
            "writes elsewhere, update Paths:ClientLogSearchDirectories.");
    }

    public void Dispose()
    {
        foreach (var watcher in _watchers)
        {
            watcher.Dispose();
        }

        _watchers.Clear();
    }

    private void OnArtifactChanged(
        object sender,
        FileSystemEventArgs args)
    {
        if (IsRelevantArtifactPath(args.FullPath))
        {
            _candidatePaths.TryAdd(args.FullPath, 0);
        }
    }

    private void OnArtifactRenamed(
        object sender,
        RenamedEventArgs args)
    {
        if (IsRelevantArtifactPath(args.FullPath))
        {
            _candidatePaths.TryAdd(args.FullPath, 0);
        }
    }

    private void OnWatcherError(
        object sender,
        ErrorEventArgs args)
        => _watcherErrors.Enqueue(
            $"Watcher '{(sender as FileSystemWatcher)?.Path ?? "<unknown>"}': " +
            (args.GetException()?.Message ??
             "Unknown FileSystemWatcher error."));

    private Dictionary<string, ArtifactSnapshot>
        CaptureCurrentArtifacts()
    {
        var snapshot = new Dictionary<string, ArtifactSnapshot>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var directory in _searchDirectories)
        {
            foreach (var filePath in EnumerateArtifactPaths(directory))
            {
                if (TryGetArtifactSnapshot(
                        filePath,
                        out var artifactSnapshot))
                {
                    snapshot[filePath] = artifactSnapshot;
                }
            }
        }

        return snapshot;
    }

    private void DiscoverNewOrModifiedArtifacts()
    {
        foreach (var directory in _searchDirectories)
        {
            foreach (var filePath in EnumerateArtifactPaths(directory))
            {
                if (IsNewOrModifiedSinceBaseline(filePath))
                {
                    _candidatePaths.TryAdd(filePath, 0);
                }
            }
        }
    }

    private string[] EnumerateArtifactPaths(string directory)
    {
        try
        {
            var artifactPaths = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);
            var enumerationOptions = new EnumerationOptions
            {
                RecurseSubdirectories =
                    _includeSubdirectories &&
                    IsRelevantArtifactPath(directory),
                IgnoreInaccessible = true,
                // The report may be in a redirected/OneDrive Documents
                // folder, where valid files can carry ReparsePoint.
                AttributesToSkip = 0
            };

            foreach (var filePath in Directory.GetFiles(
                         directory,
                         _artifactPattern,
                         enumerationOptions))
            {
                if (IsRelevantArtifactPath(filePath))
                {
                    artifactPaths.Add(filePath);
                }
            }

            if (_includeSubdirectories &&
                !IsRelevantArtifactPath(directory))
            {
                foreach (var reportDirectory in Directory
                             .EnumerateDirectories(
                                 directory,
                                 "*",
                                 SearchOption.TopDirectoryOnly)
                             .Where(IsRelevantArtifactPath))
                {
                    var reportOptions = new EnumerationOptions
                    {
                        RecurseSubdirectories = true,
                        IgnoreInaccessible = true,
                        AttributesToSkip = 0
                    };

                    foreach (var filePath in Directory.GetFiles(
                                 reportDirectory,
                                 _artifactPattern,
                                 reportOptions))
                    {
                        artifactPaths.Add(filePath);
                    }
                }
            }

            return artifactPaths.OrderBy(path => path).ToArray();
        }
        catch (Exception ex) when (
            ex is IOException or
                UnauthorizedAccessException or
                ArgumentException)
        {
            _watcherErrors.Enqueue(
                $"Could not enumerate '{directory}': " +
                $"{ex.GetType().Name}: {ex.Message}");
            return [];
        }
    }

    private bool IsRelevantArtifactPath(string path)
        => string.IsNullOrWhiteSpace(_artifactPathHint) ||
            path.Contains(
                _artifactPathHint,
                StringComparison.OrdinalIgnoreCase);

    private string DescribeSearchDirectoryContents()
    {
        var directoryDescriptions = new List<string>();

        foreach (var directory in _searchDirectories)
        {
            try
            {
                if (!Directory.Exists(directory))
                {
                    directoryDescriptions.Add(
                        $"'{directory}' => <directory does not exist>");
                    continue;
                }

                var entriesQuery = Directory
                    .EnumerateFileSystemEntries(
                        directory,
                        "*",
                        SearchOption.TopDirectoryOnly)
                    .Where(path =>
                        IsRelevantArtifactPath(directory) ||
                        IsRelevantArtifactPath(path))
                    .OrderBy(path => path);
                var entries = entriesQuery
                    .Take(50)
                    .Select(DescribeFileSystemEntry)
                    .ToArray();

                directoryDescriptions.Add(
                    $"'{directory}' => " +
                    (entries.Length == 0
                        ? $"<no top-level entry containing " +
                          $"'{_artifactPathHint}'>"
                        : string.Join("; ", entries)));
            }
            catch (Exception ex) when (
                ex is IOException or
                    UnauthorizedAccessException or
                    ArgumentException)
            {
                directoryDescriptions.Add(
                    $"'{directory}' => " +
                    $"<{ex.GetType().Name}: {ex.Message}>");
            }
        }

        return directoryDescriptions.Count == 0
            ? "<none>"
            : string.Join(" | ", directoryDescriptions);
    }

    private static string DescribeFileSystemEntry(string path)
    {
        try
        {
            var attributes = File.GetAttributes(path);
            if (attributes.HasFlag(FileAttributes.Directory))
            {
                return $"Directory='{path}', Attributes={attributes}";
            }

            var fileInfo = new FileInfo(path);
            fileInfo.Refresh();
            return
                $"File='{path}', Length={fileInfo.Length}, " +
                $"LastWriteUtc={fileInfo.LastWriteTimeUtc:O}, " +
                $"Attributes={attributes}";
        }
        catch (Exception ex) when (
            ex is IOException or
                UnauthorizedAccessException or
                ArgumentException)
        {
            return
                $"Entry='{path}', " +
                $"Error={ex.GetType().Name}: {ex.Message}";
        }
    }

    private bool IsNewOrModifiedSinceBaseline(string filePath)
    {
        if (!TryGetArtifactSnapshot(
                filePath,
                out var currentSnapshot))
        {
            return false;
        }

        return !_baselineArtifacts.TryGetValue(
                   filePath,
                   out var originalSnapshot) ||
            currentSnapshot != originalSnapshot;
    }

    private bool IsArtifactStableFor(
        string filePath,
        TimeSpan requiredDuration)
    {
        if (!TryGetArtifactSnapshot(
                filePath,
                out var currentSnapshot))
        {
            _artifactStability.Remove(filePath);
            return false;
        }

        var now = DateTime.UtcNow;
        if (!_artifactStability.TryGetValue(
                filePath,
                out var stability) ||
            stability.Snapshot != currentSnapshot)
        {
            _artifactStability[filePath] = (currentSnapshot, now);
            return false;
        }

        return now - stability.StableSinceUtc >= requiredDuration;
    }

    private static bool TryGetArtifactSnapshot(
        string filePath,
        out ArtifactSnapshot snapshot)
    {
        snapshot = default;

        try
        {
            var fileInfo = new FileInfo(filePath);
            fileInfo.Refresh();
            if (!fileInfo.Exists)
            {
                return false;
            }

            snapshot = new ArtifactSnapshot(
                fileInfo.Length,
                fileInfo.LastWriteTimeUtc);
            return true;
        }
        catch (Exception ex) when (
            ex is IOException or
                UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool TryReadCompletedArtifact(
        string filePath,
        out CollectedClientLogArtifact artifact)
    {
        artifact = null!;

        try
        {
            var fileInfo = new FileInfo(filePath);
            fileInfo.Refresh();
            if (!fileInfo.Exists || fileInfo.Length <= 0)
            {
                return false;
            }

            using var stream = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read);

            if (LooksLikeZipArchive(stream))
            {
                try
                {
                    using var zip = new ZipArchive(
                        stream,
                        ZipArchiveMode.Read,
                        leaveOpen: true);

                    var fileEntryCount = zip.Entries.Count(
                        entry => !string.IsNullOrWhiteSpace(entry.Name));
                    if (fileEntryCount == 0)
                    {
                        return false;
                    }

                    artifact = new CollectedClientLogArtifact(
                        fileInfo.FullName,
                        stream.Length,
                        fileEntryCount,
                        IsZipArchive: true);
                    return true;
                }
                catch (InvalidDataException)
                {
                    // A ZIP central directory is written last. Retry while
                    // the collector is still producing the archive.
                    return false;
                }
            }

            artifact = new CollectedClientLogArtifact(
                fileInfo.FullName,
                stream.Length,
                ContainedFileCount: 1,
                IsZipArchive: false);
            return true;
        }
        catch (Exception ex) when (
            ex is IOException or
                UnauthorizedAccessException or
                InvalidDataException)
        {
            // The collector may still be writing the archive.
            return false;
        }
    }

    private static bool LooksLikeZipArchive(FileStream stream)
    {
        if (stream.Length < 4)
        {
            return false;
        }

        var signature = new byte[4];
        var bytesRead = stream.Read(signature, 0, signature.Length);
        stream.Position = 0;

        return bytesRead == signature.Length &&
            signature[0] == 0x50 &&
            signature[1] == 0x4B &&
            ((signature[2] == 0x03 && signature[3] == 0x04) ||
             (signature[2] == 0x05 && signature[3] == 0x06) ||
             (signature[2] == 0x07 && signature[3] == 0x08));
    }
}