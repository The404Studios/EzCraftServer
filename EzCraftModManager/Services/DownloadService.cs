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

    public DownloadService()
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "EzCraftModManager/2.0");
        _httpClient.Timeout = TimeSpan.FromMinutes(10);
        _downloadSemaphore = new SemaphoreSlim(MaxConcurrentDownloads);
    }

    public async Task<string> DownloadFileAsync(string url, string destinationPath,
        IProgress<DownloadProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        await _downloadSemaphore.WaitAsync(cancellationToken);

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);

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
            using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

            var buffer = new byte[8192];
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

            downloadProgress.State = DownloadState.Completed;
            downloadProgress.BytesReceived = totalBytesRead;
            downloadProgress.Status = "Download complete";
            progress?.Report(downloadProgress);

            return destinationPath;
        }
        catch (OperationCanceledException)
        {
            if (File.Exists(destinationPath))
                File.Delete(destinationPath);
            throw;
        }
        catch (Exception ex)
        {
            var errorProgress = new DownloadProgress
            {
                FileName = Path.GetFileName(destinationPath),
                State = DownloadState.Failed,
                ErrorMessage = ex.Message
            };
            progress?.Report(errorProgress);
            throw;
        }
        finally
        {
            _downloadSemaphore.Release();
        }
    }

    public async Task<List<(ModInfo Mod, string FilePath)>> DownloadModsAsync(
        IEnumerable<(ModInfo Mod, ModFile File)> modsToDownload,
        string modsFolder,
        IProgress<(int Current, int Total, DownloadProgress Progress)>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var downloadList = modsToDownload.ToList();
        var results = new List<(ModInfo Mod, string FilePath)>();
        var current = 0;
        var total = downloadList.Count;

        Directory.CreateDirectory(modsFolder);

        var tasks = downloadList.Select(async item =>
        {
            var (mod, file) = item;
            var destinationPath = Path.Combine(modsFolder, file.FileName);

            try
            {
                var downloadProgress = new Progress<DownloadProgress>(p =>
                {
                    progress?.Report((Interlocked.Add(ref current, 0), total, p));
                });

                await DownloadFileAsync(file.DownloadUrl, destinationPath, downloadProgress, cancellationToken);

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
        var installProgress = new InstallProgress
        {
            CurrentStep = $"Installing {mod.Name}",
            TotalSteps = 1 + file.Dependencies.Count(d => d.Type == DependencyType.Required)
        };

        try
        {
            // Download main mod
            installProgress.CurrentStepIndex = 1;
            installProgress.DetailMessage = $"Downloading {mod.Name}...";
            progress?.Report(installProgress);

            var modPath = Path.Combine(modsFolder, file.FileName);
            await DownloadFileAsync(file.DownloadUrl, modPath, null, cancellationToken);

            // Download required dependencies
            var requiredDeps = file.Dependencies.Where(d => d.Type == DependencyType.Required).ToList();
            var depIndex = 2;

            foreach (var dep in requiredDeps)
            {
                installProgress.CurrentStepIndex = depIndex++;
                installProgress.DetailMessage = $"Downloading dependency: {dep.ModName}...";
                progress?.Report(installProgress);

                try
                {
                    // Try to get dependency from CurseForge
                    if (dep.ModId > 0)
                    {
                        var depMod = await curseForge.GetModAsync(dep.ModId);
                        if (depMod != null)
                        {
                            var depFile = await curseForge.GetCompatibleFileAsync(dep.ModId, gameVersion);
                            if (depFile != null && !string.IsNullOrEmpty(depFile.DownloadUrl))
                            {
                                var depPath = Path.Combine(modsFolder, depFile.FileName);
                                if (!File.Exists(depPath))
                                {
                                    await DownloadFileAsync(depFile.DownloadUrl, depPath, null, cancellationToken);
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error downloading dependency {dep.ModName}: {ex.Message}");
                    // Continue with other dependencies
                }
            }

            installProgress.IsComplete = true;
            installProgress.DetailMessage = $"{mod.Name} installed successfully!";
            progress?.Report(installProgress);

            return true;
        }
        catch (Exception ex)
        {
            installProgress.HasError = true;
            installProgress.ErrorMessage = ex.Message;
            progress?.Report(installProgress);
            return false;
        }
    }
}
