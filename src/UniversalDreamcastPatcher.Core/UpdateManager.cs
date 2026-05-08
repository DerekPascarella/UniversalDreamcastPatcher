using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

// Written by Derek Pascarella (ateam)

namespace UniversalDreamcastPatcher.Core;

public enum ManualUpdateReason
{
    None,
    UnsupportedPlatform,
    KillSwitch,
}

public class UpdateCheckResult
{
    public bool UpdateAvailable { get; set; }
    public bool ManualUpdateRequired { get; set; }
    public ManualUpdateReason ManualReason { get; set; }
    public string LatestTag { get; set; } = "";
    public string LatestVersion { get; set; } = "";
}

public class DownloadProgress
{
    public long BytesRead { get; set; }
    public long TotalBytes { get; set; }
    public double SpeedBytesPerSecond { get; set; }
}

// Message is already user-facing text. The wizard displays it as-is.
public class UpdateException : Exception
{
    public UpdateException(string message) : base(message) { }
}

public static class UpdateManager
{
    private static readonly HttpClient _client;
    private const string StagingDirName = "UniversalDreamcastPatcher_update";
    private const string LockFileName = "UniversalDreamcastPatcher_update.lock";
    private const string AutoUpdateKillSwitch = "This release cannot be auto-updated.";
    private const string InstallMarker = "INSTALLING";
    private const string WindowsScriptName = "_udp_updater.bat";
    private const string UnixScriptName = "_udp_updater.sh";
    private static readonly TimeSpan DownloadIdleTimeout = TimeSpan.FromMinutes(5);
    // If the install marker is older than this, assume the updater script crashed.
    // A normal install only takes a few seconds.
    private static readonly TimeSpan InstallMarkerStaleAfter = TimeSpan.FromMinutes(10);

    static UpdateManager()
    {
        _client = new HttpClient();
        _client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github.v3+json");
        _client.DefaultRequestHeaders.UserAgent.ParseAdd("UniversalDreamcastPatcher-UpdateCheck/1.0");
    }

    private static Version ParseVersion(string? versionString)
    {
        if (string.IsNullOrWhiteSpace(versionString))
            return new Version(0, 0);

        var cleaned = versionString.TrimStart('v', 'V');
        var hyphenIndex = cleaned.IndexOf('-');
        if (hyphenIndex > 0)
            cleaned = cleaned.Substring(0, hyphenIndex);

        return Version.TryParse(cleaned, out var v) ? v : new Version(0, 0);
    }

    public static async Task<UpdateCheckResult> CheckForUpdateAsync(CancellationToken cancellationToken = default)
    {
        var result = new UpdateCheckResult();

        try
        {
            var url = $"https://api.github.com/repos/{Constants.Repo}/releases/latest";

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(10));

            using var response = await _client.GetAsync(url, cts.Token);
            response.EnsureSuccessStatusCode();

            using var stream = await response.Content.ReadAsStreamAsync(cts.Token);
            var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cts.Token);
            var tagName = doc.RootElement.GetProperty("tag_name").GetString() ?? "";

            var body = "";
            if (doc.RootElement.TryGetProperty("body", out var bodyElement))
                body = bodyElement.GetString() ?? "";

            result.LatestTag = tagName;
            result.LatestVersion = "v" + tagName.TrimStart('v', 'V');

            var currentVersion = ParseVersion(Constants.Version);
            var latestVersion = ParseVersion(tagName);

            var isNewer = latestVersion > currentVersion;

            // Match the kill-switch sentinel only on its own line, so quoting it inline
            // in release notes doesn't activate it.
            var killSwitchActive = body
                .Split('\n')
                .Any(line => line.Trim().Equals(AutoUpdateKillSwitch, StringComparison.OrdinalIgnoreCase));

            var isMacOS = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

            result.UpdateAvailable = isNewer && !killSwitchActive && !isMacOS;
            result.ManualUpdateRequired = isNewer && (killSwitchActive || isMacOS);
            if (result.ManualUpdateRequired)
                result.ManualReason = killSwitchActive ? ManualUpdateReason.KillSwitch : ManualUpdateReason.UnsupportedPlatform;
        }
        catch
        {
            result.UpdateAvailable = false;
        }

        return result;
    }

    private static string GetAssetSuffix()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return RuntimeInformation.ProcessArchitecture == Architecture.X86
                ? "win-x86.zip"
                : "win-x64.zip";
        }
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return RuntimeInformation.ProcessArchitecture == Architecture.Arm64
                ? "osx-arm64-AppBundle.tar.gz"
                : "osx-x64-AppBundle.tar.gz";
        }
        return "linux-x64.tar.gz";
    }

    private static string GetAssetUrl(string tag)
    {
        var suffix = GetAssetSuffix();
        var cleanTag = tag.TrimStart('v', 'V');
        return $"https://github.com/{Constants.Repo}/releases/download/{tag}/{Constants.AppExecutableBase}.v{cleanTag}-{suffix}";
    }

    private static string GetStagingDir() => Path.Combine(Path.GetTempPath(), StagingDirName);
    private static string GetLockFilePath() => Path.Combine(Path.GetTempPath(), LockFileName);

    // Cross-instance lock so two UDP processes (or one process plus a running updater
    // script) can't step on the same staging dir. The lock file holds the owner PID,
    // or "INSTALLING" while the updater script is copying. EndUpdate deletes the file.
    public static bool TryBeginUpdate()
    {
        var path = GetLockFilePath();

        if (File.Exists(path))
        {
            string content;
            try { content = File.ReadAllText(path).Trim(); }
            catch { content = ""; }

            if (content.Equals(InstallMarker, StringComparison.OrdinalIgnoreCase))
            {
                // Refuse if the marker is fresh. Reclaim it if it's older than
                // InstallMarkerStaleAfter, in case the updater script died.
                if (!IsMarkerStale(path))
                    return false;
            }
            else if (int.TryParse(content, out var pid) && pid != Process.GetCurrentProcess().Id)
            {
                try
                {
                    using var p = Process.GetProcessById(pid);
                    if (p != null && !p.HasExited)
                        return false;
                }
                catch (ArgumentException)
                {
                    // PID is dead; fall through and reclaim the lock.
                }
                catch
                {
                    // Unexpected error reading process state; treat as stale.
                }
            }
        }

        try
        {
            File.WriteAllText(path, Process.GetCurrentProcess().Id.ToString());
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsMarkerStale(string path)
    {
        try
        {
            var age = DateTime.UtcNow - File.GetLastWriteTimeUtc(path);
            return age > InstallMarkerStaleAfter;
        }
        catch
        {
            return true;
        }
    }

    public static void EndUpdate()
    {
        try
        {
            var path = GetLockFilePath();
            if (File.Exists(path))
                File.Delete(path);
        }
        catch { }
    }

    private static bool IsAnotherInstanceUpdating()
    {
        var path = GetLockFilePath();
        if (!File.Exists(path)) return false;

        string content;
        try { content = File.ReadAllText(path).Trim(); }
        catch { return false; }

        if (content.Equals(InstallMarker, StringComparison.OrdinalIgnoreCase))
            return !IsMarkerStale(path);

        if (int.TryParse(content, out var pid) && pid != Process.GetCurrentProcess().Id)
        {
            try
            {
                using var p = Process.GetProcessById(pid);
                return p != null && !p.HasExited;
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch
            {
                return false;
            }
        }

        return false;
    }

    public static async Task DownloadUpdateAsync(string tag, IProgress<DownloadProgress>? progress, CancellationToken cancellationToken)
    {
        var stagingDir = GetStagingDir();
        var downloadDir = Path.Combine(stagingDir, "download");

        if (Directory.Exists(stagingDir))
            Directory.Delete(stagingDir, true);

        Directory.CreateDirectory(downloadDir);

        var url = GetAssetUrl(tag);
        var suffix = GetAssetSuffix();
        var cleanTag = tag.TrimStart('v', 'V');
        var fileName = $"{Constants.AppExecutableBase}.v{cleanTag}-{suffix}";
        var downloadPath = Path.Combine(downloadDir, fileName);

        // Cancel the download if no bytes arrive for DownloadIdleTimeout.
        // The timer resets on each chunk.
        using var idleCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        using var idleTimer = new Timer(_ =>
        {
            try { idleCts.Cancel(); } catch { }
        }, null, DownloadIdleTimeout, Timeout.InfiniteTimeSpan);

        try
        {
            using var response = await _client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, idleCts.Token);
            response.EnsureSuccessStatusCode();
            var totalBytes = response.Content.Headers.ContentLength ?? -1;

            using var contentStream = await response.Content.ReadAsStreamAsync(idleCts.Token);
            using var fileStream = new FileStream(downloadPath, FileMode.Create, FileAccess.Write, FileShare.None, 65536, true);

            var buffer = new byte[65536];
            long bytesRead = 0;
            int read;
            var sw = Stopwatch.StartNew();
            long lastReportBytes = 0;
            double lastReportTime = 0;

            while ((read = await contentStream.ReadAsync(buffer, idleCts.Token)) > 0)
            {
                idleTimer.Change(DownloadIdleTimeout, Timeout.InfiniteTimeSpan);

                await fileStream.WriteAsync(buffer.AsMemory(0, read), idleCts.Token);
                bytesRead += read;

                var elapsed = sw.Elapsed.TotalSeconds;
                if (elapsed - lastReportTime >= 0.25)
                {
                    var speed = (elapsed - lastReportTime) > 0
                        ? (bytesRead - lastReportBytes) / (elapsed - lastReportTime)
                        : 0;
                    lastReportBytes = bytesRead;
                    lastReportTime = elapsed;

                    progress?.Report(new DownloadProgress
                    {
                        BytesRead = bytesRead,
                        TotalBytes = totalBytes,
                        SpeedBytesPerSecond = speed,
                    });
                }
            }

            // Final report so the progress bar reaches 100%.
            // The loop's rate-limiting may have skipped the last chunk's report.
            progress?.Report(new DownloadProgress
            {
                BytesRead = bytesRead,
                TotalBytes = totalBytes,
                SpeedBytesPerSecond = 0,
            });
        }
        catch (OperationCanceledException) when (idleCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            CleanupStagingDirectory();
            throw new TimeoutException(
                "The download stalled.\n\n" +
                "No data was received for several minutes. Please check your internet connection and try again.");
        }
        catch
        {
            CleanupStagingDirectory();
            throw;
        }
    }

    public static async Task ExtractUpdateAsync(string tag, CancellationToken cancellationToken)
    {
        var stagingDir = GetStagingDir();
        var downloadDir = Path.Combine(stagingDir, "download");
        var extractedDir = Path.Combine(stagingDir, "extracted");

        Directory.CreateDirectory(extractedDir);

        var suffix = GetAssetSuffix();
        var cleanTag = tag.TrimStart('v', 'V');
        var fileName = $"{Constants.AppExecutableBase}.v{cleanTag}-{suffix}";
        var archivePath = Path.Combine(downloadDir, fileName);

        try
        {
            if (suffix.EndsWith(".zip"))
                await Task.Run(() => ZipFile.ExtractToDirectory(archivePath, extractedDir), cancellationToken);
            else
                await ExtractTarGzAsync(archivePath, extractedDir, cancellationToken);
        }
        catch
        {
            CleanupStagingDirectory();
            throw;
        }
    }

    private static async Task ExtractTarGzAsync(string archivePath, string extractedDir, CancellationToken cancellationToken)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "tar",
                Arguments = $"-xzf \"{archivePath}\" -C \"{extractedDir}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
            };

            using var process = Process.Start(psi);
            if (process != null)
            {
                await process.WaitForExitAsync(cancellationToken);
                if (process.ExitCode == 0)
                    return;
            }
        }
        catch
        {
            // tar not available
        }

        throw new UpdateException(
            "The downloaded update could not be extracted.\n\n" +
            "The 'tar' command-line tool is required to extract this update. " +
            "Please install it through your distribution's package manager and try again.");
    }

    public static async Task PrepareUpdateAsync()
    {
        var stagingDir = GetStagingDir();
        var extractedDir = Path.Combine(stagingDir, "extracted");

        var contentRoot = FindContentRoot(extractedDir);
        if (contentRoot == null)
            throw new UpdateException(
                "The downloaded update file does not appear to be a valid Universal Dreamcast Patcher release.\n\n" +
                "The archive was downloaded successfully but did not contain the expected files. Please try again later.");

        if (contentRoot != extractedDir)
        {
            var tempMove = Path.Combine(stagingDir, "content_temp");
            Directory.Move(contentRoot, tempMove);
            if (Directory.Exists(extractedDir))
                Directory.Delete(extractedDir, true);
            Directory.Move(tempMove, extractedDir);
        }

        await Task.Run(() => MergeSettings(extractedDir));
    }

    private static string? FindContentRoot(string extractedDir)
    {
        if (HasAppFiles(extractedDir))
            return extractedDir;

        foreach (var subDir in Directory.GetDirectories(extractedDir))
        {
            if (HasAppFiles(subDir))
                return subDir;

            var macosDir = Path.Combine(subDir, "Contents", "MacOS");
            if (Directory.Exists(macosDir) && HasAppFiles(macosDir))
                return macosDir;
        }

        foreach (var subDir in Directory.GetDirectories(extractedDir))
        {
            foreach (var subSubDir in Directory.GetDirectories(subDir))
            {
                if (HasAppFiles(subSubDir))
                    return subSubDir;
            }
        }

        return null;
    }

    private static bool HasAppFiles(string dir)
    {
        return File.Exists(Path.Combine(dir, $"{Constants.AppExecutableBase}.exe")) ||
               File.Exists(Path.Combine(dir, Constants.AppExecutableBase)) ||
               File.Exists(Path.Combine(dir, $"{Constants.AppExecutableBase}.dll"));
    }

    private static void MergeSettings(string extractedDir)
    {
        var appDir = AppSettings.GetAppDirectory();
        var currentSettingsPath = Path.Combine(appDir, "settings.json");

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            currentSettingsPath = Path.Combine(home, "Library", "Application Support", Constants.AppExecutableBase, "settings.json");
        }

        var newSettingsPath = Path.Combine(extractedDir, "settings.json");

        if (!File.Exists(currentSettingsPath))
        {
            // Fresh install. Drop any settings file from the release. The app writes
            // its own with defaults on first close.
            DeleteIfExists(newSettingsPath);
            return;
        }

        try
        {
            var currentJson = File.ReadAllText(currentSettingsPath);
            using var currentDoc = JsonDocument.Parse(currentJson);

            // Merge: keep current values, add any new keys from the release's settings.json.
            if (File.Exists(newSettingsPath))
            {
                var newJson = File.ReadAllText(newSettingsPath);
                using var newDoc = JsonDocument.Parse(newJson);

                var merged = new Dictionary<string, JsonElement>();

                foreach (var prop in newDoc.RootElement.EnumerateObject())
                    merged[prop.Name] = prop.Value;
                foreach (var prop in currentDoc.RootElement.EnumerateObject())
                    merged[prop.Name] = prop.Value;

                var options = new JsonSerializerOptions { WriteIndented = true };
                var mergedJson = JsonSerializer.Serialize(merged, options);
                File.WriteAllText(newSettingsPath, mergedJson);
            }
            else
            {
                File.Copy(currentSettingsPath, newSettingsPath);
            }
        }
        catch
        {
            DeleteIfExists(newSettingsPath);
        }
    }

    public static void LaunchUpdaterAndExit()
    {
        var stagingDir = GetStagingDir();
        var extractedDir = Path.Combine(stagingDir, "extracted");
        var appDir = AppSettings.GetAppDirectory();
        var pid = Process.GetCurrentProcess().Id;

        // Pre-set the install marker so any UDP launched between our exit and the
        // updater's first write still sees the install in progress.
        try { File.WriteAllText(GetLockFilePath(), InstallMarker); } catch { }

        string scriptPath;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            scriptPath = Path.Combine(Path.GetTempPath(), WindowsScriptName);
            var script = GenerateWindowsScript(pid, extractedDir, appDir);
            File.WriteAllText(scriptPath, script);

            Process.Start(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c \"{scriptPath}\"",
                CreateNoWindow = true,
                UseShellExecute = false,
            });
        }
        else
        {
            scriptPath = Path.Combine(Path.GetTempPath(), UnixScriptName);
            var script = GenerateUnixScript(pid, extractedDir, appDir);
            File.WriteAllText(scriptPath, script);

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "chmod",
                    Arguments = $"+x \"{scriptPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                })?.WaitForExit(2000);
            }
            catch { }

            Process.Start(new ProcessStartInfo
            {
                FileName = "/bin/bash",
                Arguments = $"\"{scriptPath}\"",
                CreateNoWindow = true,
                UseShellExecute = false,
            });
        }

        Environment.Exit(0);
    }

    private static string GenerateWindowsScript(int pid, string extractedDir, string appDir)
    {
        var escaped_extracted = extractedDir.Replace("/", "\\");
        var escaped_app = appDir.TrimEnd('\\').Replace("/", "\\");
        var stagingDir = GetStagingDir().Replace("/", "\\");
        var lockPath = GetLockFilePath().Replace("/", "\\");
        var exePath = Path.Combine(escaped_app, $"{Constants.AppExecutableBase}.exe");

        // Wait-Process avoids parsing tasklist output and returns immediately
        // if the PID is already gone.
        // xcopy overwrites the install dir's files in place. It does NOT remove
        // unrelated files, so users who placed the exe inside a shared folder
        // like Downloads keep their other files intact across updates.
        return $@"@echo off
powershell -NoProfile -Command ""Wait-Process -Id {pid} -ErrorAction SilentlyContinue""

echo {InstallMarker}> ""{lockPath}""

xcopy /E /Y ""{escaped_extracted}\*"" ""{escaped_app}\""

rmdir /S /Q ""{escaped_extracted}"" 2>NUL
rmdir /S /Q ""{stagingDir}"" 2>NUL

del /F /Q ""{lockPath}"" 2>NUL

start """" ""{exePath}""

del ""%~f0""
";
    }

    private static string GenerateUnixScript(int pid, string extractedDir, string appDir)
    {
        var escaped_app = appDir.TrimEnd('/');
        var stagingDir = GetStagingDir();
        var lockPath = GetLockFilePath();
        var exePath = $"{escaped_app}/{Constants.AppExecutableBase}";

        // cp overwrites the install dir's files in place. It does NOT remove
        // unrelated files, so users who placed the binary inside a shared folder
        // like ~/Downloads keep their other files intact across updates. The
        // "/." idiom copies dotfiles too.
        return $@"#!/bin/bash

# Wait for the previous process to exit.
while kill -0 {pid} 2>/dev/null; do
    sleep 1
done

# Mark the install in progress so a concurrent UDP launch stays out.
echo {InstallMarker} > ""{lockPath}""

cp -rf ""{extractedDir}/."" ""{escaped_app}/""

# Clean up staging directory.
rm -rf ""{stagingDir}""

# Remove the install marker so the relaunched app may proceed.
rm -f ""{lockPath}""

# Fix permissions and relaunch the app.
chmod +x ""{exePath}""
""{exePath}"" &

# Self-delete this script.
rm ""$0""
";
    }

    public static void CleanupStaleStagingData()
    {
        // Don't touch the staging dir or scripts while another instance (or an
        // active updater script) is installing. Removing them would corrupt the install.
        if (IsAnotherInstanceUpdating())
            return;

        try
        {
            var stagingDir = GetStagingDir();
            if (Directory.Exists(stagingDir))
                Directory.Delete(stagingDir, true);
        }
        catch { }

        try
        {
            var batScript = Path.Combine(Path.GetTempPath(), WindowsScriptName);
            if (File.Exists(batScript))
                File.Delete(batScript);
        }
        catch { }

        try
        {
            var shScript = Path.Combine(Path.GetTempPath(), UnixScriptName);
            if (File.Exists(shScript))
                File.Delete(shScript);
        }
        catch { }
    }

    public static void CleanupStagingDirectory()
    {
        try
        {
            var stagingDir = GetStagingDir();
            if (Directory.Exists(stagingDir))
                Directory.Delete(stagingDir, true);
        }
        catch { }
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
            File.Delete(path);
    }
}
