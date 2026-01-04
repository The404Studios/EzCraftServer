using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using EzCraftModManager.Models;

namespace EzCraftModManager.Services;

public class DownloadService
{
    private readonly HttpClient _httpClient;
    private readonly SemaphoreSlim _downloadSemaphore;
    private const int MaxConcurrentDownloads = 5;
    private const int MaxRetryAttempts = 3;
    private static readonly TimeSpan[] RetryDelays = { TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4) };

    public DownloadService()
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "EzCraftModManager/2.0");
        _httpClient.Timeout = TimeSpan.FromMinutes(10);
        _downloadSemaphore = new SemaphoreSlim(MaxConcurrentDownloads);
    }

    /// <summary>
    /// Downloads a file with retry logic and progress reporting
    /// </summary>
    public async Task<string> DownloadFileAsync(string url, string destinationPath,
        IProgress<DownloadProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(url))
            throw new ArgumentException("Download URL cannot be empty", nameof(url));

        if (string.IsNullOrEmpty(destinationPath))
            throw new ArgumentException("Destination path cannot be empty", nameof(destinationPath));

        await _downloadSemaphore.WaitAsync(cancellationToken);

        Exception? lastException = null;
        var fileName = Path.GetFileName(destinationPath);

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);

            for (int attempt = 0; attempt < MaxRetryAttempts; attempt++)
            {
                try
                {
                    var downloadProgress = new DownloadProgress
                    {
                        FileName = fileName,
                        State = DownloadState.Downloading,
                        Status = attempt > 0 ? $"Retrying download (attempt {attempt + 1})..." : "Starting download..."
                    };
                    progress?.Report(downloadProgress);

                    return await DownloadFileInternalAsync(url, destinationPath, progress, cancellationToken);
                }
                catch (HttpRequestException ex) when (attempt < MaxRetryAttempts - 1)
                {
                    lastException = ex;
                    var delay = RetryDelays[attempt];

                    var retryProgress = new DownloadProgress
                    {
                        FileName = fileName,
                        State = DownloadState.Downloading,
                        Status = $"Download failed, retrying in {delay.TotalSeconds}s... ({ex.Message})"
                    };
                    progress?.Report(retryProgress);

                    await Task.Delay(delay, cancellationToken);
                }
                catch (IOException ex) when (attempt < MaxRetryAttempts - 1)
                {
                    lastException = ex;
                    var delay = RetryDelays[attempt];

                    // Delete partial file before retry
                    if (File.Exists(destinationPath))
                    {
                        try { File.Delete(destinationPath); } catch { }
                    }

                    var retryProgress = new DownloadProgress
                    {
                        FileName = fileName,
                        State = DownloadState.Downloading,
                        Status = $"Write error, retrying in {delay.TotalSeconds}s..."
                    };
                    progress?.Report(retryProgress);

                    await Task.Delay(delay, cancellationToken);
                }
            }

            throw lastException ?? new Exception("Download failed after all retry attempts");
        }
        catch (OperationCanceledException)
        {
            CleanupPartialFile(destinationPath);
            throw;
        }
        catch (Exception ex)
        {
            var errorProgress = new DownloadProgress
            {
                FileName = fileName,
                State = DownloadState.Failed,
                ErrorMessage = GetUserFriendlyError(ex)
            };
            progress?.Report(errorProgress);
            throw;
        }
        finally
        {
            _downloadSemaphore.Release();
        }
    }

    private async Task<string> DownloadFileInternalAsync(string url, string destinationPath,
        IProgress<DownloadProgress>? progress, CancellationToken cancellationToken)
    {
        var downloadProgress = new DownloadProgress
        {
            FileName = Path.GetFileName(destinationPath),
            State = DownloadState.Downloading
        };

        using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? -1;
        downloadProgress.TotalBytes = totalBytes;

        using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

        var buffer = new byte[81920]; // Larger buffer for better performance
        long totalBytesRead = 0;
        int bytesRead;
        var lastProgressUpdate = DateTime.UtcNow;
        var lastBytesForSpeed = 0L;
        var speedUpdateTime = DateTime.UtcNow;

        while ((bytesRead = await contentStream.ReadAsync(buffer, cancellationToken)) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
            totalBytesRead += bytesRead;

            var now = DateTime.UtcNow;
            if (progress != null && (now - lastProgressUpdate).TotalMilliseconds > 100)
            {
                // Calculate download speed
                var timeDiff = (now - speedUpdateTime).TotalSeconds;
                if (timeDiff > 0)
                {
                    downloadProgress.DownloadSpeed = (totalBytesRead - lastBytesForSpeed) / timeDiff;
                    lastBytesForSpeed = totalBytesRead;
                    speedUpdateTime = now;
                }

                downloadProgress.BytesReceived = totalBytesRead;
                downloadProgress.Status = $"Downloading {downloadProgress.FileName}...";
                progress.Report(downloadProgress);
                lastProgressUpdate = now;
            }
        }

        // Verify file was downloaded successfully
        await fileStream.FlushAsync(cancellationToken);
        var fileInfo = new FileInfo(destinationPath);

        if (totalBytes > 0 && fileInfo.Length != totalBytes)
        {
            throw new IOException($"Downloaded file size ({fileInfo.Length}) doesn't match expected size ({totalBytes})");
        }

        downloadProgress.State = DownloadState.Completed;
        downloadProgress.BytesReceived = totalBytesRead;
        downloadProgress.Status = "Download complete";
        progress?.Report(downloadProgress);

        return destinationPath;
    }

    /// <summary>
    /// Downloads a file only if it doesn't already exist
    /// </summary>
    public async Task<string> DownloadFileIfNotExistsAsync(string url, string destinationPath,
        IProgress<DownloadProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        if (File.Exists(destinationPath))
        {
            var skipProgress = new DownloadProgress
            {
                FileName = Path.GetFileName(destinationPath),
                State = DownloadState.Completed,
                Status = "Already downloaded, skipping..."
            };
            progress?.Report(skipProgress);
            return destinationPath;
        }

        return await DownloadFileAsync(url, destinationPath, progress, cancellationToken);
    }

    public async Task<List<(ModInfo Mod, string FilePath)>> DownloadModsAsync(
        IEnumerable<(ModInfo Mod, ModFile File)> modsToDownload,
        string modsFolder,
        IProgress<(int Current, int Total, DownloadProgress Progress)>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var downloadList = modsToDownload.Where(x => x.Mod != null && x.File != null).ToList();
        var results = new List<(ModInfo Mod, string FilePath)>();
        var current = 0;
        var total = downloadList.Count;

        if (total == 0) return results;

        Directory.CreateDirectory(modsFolder);

        var tasks = downloadList.Select(async item =>
        {
            var (mod, file) = item;
            var fileName = file.FileName ?? $"{mod.Slug ?? mod.Name}.jar";
            var destinationPath = Path.Combine(modsFolder, fileName);

            try
            {
                var downloadProgress = new Progress<DownloadProgress>(p =>
                {
                    progress?.Report((Interlocked.Add(ref current, 0), total, p));
                });

                await DownloadFileIfNotExistsAsync(file.DownloadUrl, destinationPath, downloadProgress, cancellationToken);

                Interlocked.Increment(ref current);
                return (mod, destinationPath);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error downloading {mod.Name}: {ex.Message}");
                return (mod, (string?)null);
            }
        });

        var completedDownloads = await Task.WhenAll(tasks);

        foreach (var (mod, filePath) in completedDownloads)
        {
            if (filePath != null)
            {
                results.Add((mod, filePath));
            }
        }

        return results;
    }

    public async Task<bool> DownloadModWithDependenciesAsync(
        ModInfo mod,
        ModFile file,
        string modsFolder,
        CurseForgeService curseForge,
        ModrinthService modrinth,
        string gameVersion,
        IProgress<InstallProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (mod == null || file == null)
        {
            System.Diagnostics.Debug.WriteLine("Cannot download: mod or file is null");
            return false;
        }

        var modName = mod.Name ?? "Unknown Mod";
        var fileName = file.FileName ?? $"{mod.Slug ?? "mod"}.jar";
        var downloadUrl = file.DownloadUrl ?? "";
        var dependencies = file.Dependencies ?? new List<ModDependency>();

        if (string.IsNullOrEmpty(downloadUrl))
        {
            System.Diagnostics.Debug.WriteLine($"Cannot download {modName}: no download URL");
            return false;
        }

        if (string.IsNullOrEmpty(modsFolder))
        {
            System.Diagnostics.Debug.WriteLine($"Cannot download {modName}: no destination folder");
            return false;
        }

        var requiredDeps = dependencies.Where(d => d?.Type == DependencyType.Required).ToList();
        var installProgress = new InstallProgress
        {
            CurrentStep = $"Installing {modName}",
            TotalSteps = 1 + requiredDeps.Count
        };

        try
        {
            Directory.CreateDirectory(modsFolder);

            // Download main mod
            installProgress.CurrentStepIndex = 1;
            installProgress.DetailMessage = $"Downloading {modName}...";
            progress?.Report(installProgress);

            var modPath = Path.Combine(modsFolder, fileName);

            // Skip if already exists
            if (!File.Exists(modPath))
            {
                await DownloadFileAsync(downloadUrl, modPath, null, cancellationToken);
            }
            else
            {
                installProgress.DetailMessage = $"{modName} already exists, skipping...";
                progress?.Report(installProgress);
            }

            // Download required dependencies
            var depIndex = 2;
            var failedDeps = new List<string>();

            foreach (var dep in requiredDeps)
            {
                if (dep == null) continue;

                var depName = !string.IsNullOrEmpty(dep.ModName) ? dep.ModName : $"Dependency #{dep.ModId}";
                installProgress.CurrentStepIndex = depIndex++;
                installProgress.DetailMessage = $"Downloading dependency: {depName}...";
                progress?.Report(installProgress);

                try
                {
                    if (dep.ModId > 0 && curseForge != null)
                    {
                        var depMod = await curseForge.GetModAsync(dep.ModId);
                        if (depMod != null)
                        {
                            var depFile = await curseForge.GetCompatibleFileAsync(dep.ModId, gameVersion ?? "1.20.1");
                            if (depFile != null && !string.IsNullOrEmpty(depFile.DownloadUrl))
                            {
                                var depFileName = depFile.FileName ?? $"{depMod.Slug ?? "dep"}.jar";
                                var depPath = Path.Combine(modsFolder, depFileName);

                                if (!File.Exists(depPath))
                                {
                                    await DownloadFileAsync(depFile.DownloadUrl, depPath, null, cancellationToken);
                                }
                            }
                            else
                            {
                                failedDeps.Add(depName);
                            }
                        }
                        else
                        {
                            failedDeps.Add(depName);
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error downloading dependency {depName}: {ex.Message}");
                    failedDeps.Add(depName);
                }
            }

            installProgress.IsComplete = true;
            if (failedDeps.Count > 0)
            {
                installProgress.DetailMessage = $"{modName} installed (some dependencies failed: {string.Join(", ", failedDeps)})";
            }
            else
            {
                installProgress.DetailMessage = $"{modName} installed successfully!";
            }
            progress?.Report(installProgress);

            return true;
        }
        catch (Exception ex)
        {
            installProgress.HasError = true;
            installProgress.ErrorMessage = GetUserFriendlyError(ex);
            progress?.Report(installProgress);
            System.Diagnostics.Debug.WriteLine($"Error downloading {modName}: {ex}");
            return false;
        }
    }

    private static void CleanupPartialFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch { }
    }

    private static string GetUserFriendlyError(Exception ex)
    {
        return ex switch
        {
            HttpRequestException httpEx when httpEx.Message.Contains("404") =>
                "File not found on server. The mod may have been removed or the version is unavailable.",
            HttpRequestException httpEx when httpEx.Message.Contains("403") =>
                "Access denied. The download may require authentication or is restricted.",
            HttpRequestException httpEx when httpEx.Message.Contains("503") =>
                "Server is temporarily unavailable. Please try again later.",
            HttpRequestException =>
                $"Network error: {ex.Message}. Check your internet connection.",
            IOException ioEx when ioEx.Message.Contains("disk") =>
                "Disk error. Check available disk space and write permissions.",
            TaskCanceledException =>
                "Download timed out. The server may be slow or your connection is unstable.",
            OperationCanceledException =>
                "Download was cancelled.",
            _ => ex.Message
        };
    }
}
