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
        if (mod == null || file == null)
        {
            System.Diagnostics.Debug.WriteLine("Cannot download: mod or file is null");
            return false;
        }

        var modName = mod.Name ?? "Unknown Mod";
        var fileName = file.FileName ?? "unknown.jar";
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

        var installProgress = new InstallProgress
        {
            CurrentStep = $"Installing {modName}",
            TotalSteps = 1 + dependencies.Count(d => d?.Type == DependencyType.Required)
        };

        try
        {
            Directory.CreateDirectory(modsFolder);

            // Download main mod
            installProgress.CurrentStepIndex = 1;
            installProgress.DetailMessage = $"Downloading {modName}...";
            progress?.Report(installProgress);

            var modPath = Path.Combine(modsFolder, fileName);
            await DownloadFileAsync(downloadUrl, modPath, null, cancellationToken);

            // Download required dependencies
            var requiredDeps = dependencies.Where(d => d != null && d.Type == DependencyType.Required).ToList();
            var depIndex = 2;

            foreach (var dep in requiredDeps)
            {
                if (dep == null) continue;

                var depName = dep.ModName ?? "Unknown dependency";
                installProgress.CurrentStepIndex = depIndex++;
                installProgress.DetailMessage = $"Downloading dependency: {depName}...";
                progress?.Report(installProgress);

                try
                {
                    // Try to get dependency from CurseForge
                    if (dep.ModId > 0 && curseForge != null)
                    {
                        var depMod = await curseForge.GetModAsync(dep.ModId);
                        if (depMod != null)
                        {
                            var depFile = await curseForge.GetCompatibleFileAsync(dep.ModId, gameVersion ?? "1.20.1");
                            if (depFile != null && !string.IsNullOrEmpty(depFile.DownloadUrl) && !string.IsNullOrEmpty(depFile.FileName))
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
                    System.Diagnostics.Debug.WriteLine($"Error downloading dependency {depName}: {ex.Message}");
                    // Continue with other dependencies
                }
            }

            installProgress.IsComplete = true;
            installProgress.DetailMessage = $"{modName} installed successfully!";
            progress?.Report(installProgress);

            return true;
        }
        catch (Exception ex)
        {
            installProgress.HasError = true;
            installProgress.ErrorMessage = ex.Message;
            progress?.Report(installProgress);
            System.Diagnostics.Debug.WriteLine($"Error downloading {modName}: {ex}");
            return false;
        }
    }
}
