using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using EzCraftModManager.Models;

namespace EzCraftModManager.Services;

public class DownloadQueueService : INotifyPropertyChanged
{
    private static DownloadQueueService? _instance;
    public static DownloadQueueService Instance => _instance ??= new DownloadQueueService();

    private readonly ConcurrentQueue<QueuedDownload> _downloadQueue = new();
    private readonly SemaphoreSlim _processSemaphore = new(1, 1);
    private readonly DownloadService _downloadService;
    private readonly CurseForgeService _curseForge;
    private readonly ModrinthService _modrinth;
    private CancellationTokenSource? _cancellationTokenSource;
    private bool _isProcessing;

    public ObservableCollection<QueuedDownload> ActiveDownloads { get; } = new();
    public ObservableCollection<QueuedDownload> CompletedDownloads { get; } = new();
    public ObservableCollection<QueuedDownload> FailedDownloads { get; } = new();

    private int _totalQueued;
    public int TotalQueued
    {
        get => _totalQueued;
        private set { _totalQueued = value; OnPropertyChanged(); }
    }

    private int _totalCompleted;
    public int TotalCompleted
    {
        get => _totalCompleted;
        private set { _totalCompleted = value; OnPropertyChanged(); }
    }

    private int _totalFailed;
    public int TotalFailed
    {
        get => _totalFailed;
        private set { _totalFailed = value; OnPropertyChanged(); }
    }

    private bool _isActive;
    public bool IsActive
    {
        get => _isActive;
        private set { _isActive = value; OnPropertyChanged(); }
    }

    private string _currentStatus = "Idle";
    public string CurrentStatus
    {
        get => _currentStatus;
        private set { _currentStatus = value; OnPropertyChanged(); }
    }

    private double _overallProgress;
    public double OverallProgress
    {
        get => _overallProgress;
        private set { _overallProgress = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private DownloadQueueService()
    {
        _downloadService = new DownloadService();
        _curseForge = new CurseForgeService();
        _modrinth = new ModrinthService();
    }

    public void EnqueueDownload(ModInfo mod, string gameVersion, string destinationFolder, ServerProfile? profile = null)
    {
        if (mod == null)
        {
            System.Diagnostics.Debug.WriteLine("Cannot enqueue null mod");
            return;
        }

        if (string.IsNullOrEmpty(destinationFolder))
        {
            System.Diagnostics.Debug.WriteLine("Cannot enqueue download with empty destination folder");
            return;
        }

        var queuedDownload = new QueuedDownload
        {
            Id = Guid.NewGuid().ToString(),
            Mod = mod,
            GameVersion = gameVersion ?? "1.20.1",
            DestinationFolder = destinationFolder,
            Profile = profile,
            Status = DownloadQueueStatus.Queued,
            QueuedTime = DateTime.Now
        };

        _downloadQueue.Enqueue(queuedDownload);
        TotalQueued++;

        UpdateOnUIThread(() => ActiveDownloads.Add(queuedDownload));

        _ = ProcessQueueAsync();
    }

    public void EnqueueMultiple(System.Collections.Generic.IEnumerable<(ModInfo Mod, string GameVersion, string DestinationFolder, ServerProfile? Profile)> items)
    {
        foreach (var item in items)
        {
            EnqueueDownload(item.Mod, item.GameVersion, item.DestinationFolder, item.Profile);
        }
    }

    public void CancelAll()
    {
        _cancellationTokenSource?.Cancel();
        CurrentStatus = "Cancelling...";
    }

    public void ClearCompleted()
    {
        UpdateOnUIThread(() =>
        {
            CompletedDownloads.Clear();
            FailedDownloads.Clear();
        });
        TotalCompleted = 0;
        TotalFailed = 0;
    }

    /// <summary>
    /// Retry all failed downloads that haven't exceeded max retries
    /// </summary>
    public void RetryFailed()
    {
        var toRetry = new System.Collections.Generic.List<QueuedDownload>();

        UpdateOnUIThread(() =>
        {
            foreach (var download in FailedDownloads)
            {
                if (download.CanRetry)
                {
                    toRetry.Add(download);
                }
            }

            foreach (var download in toRetry)
            {
                FailedDownloads.Remove(download);
            }
        });

        foreach (var download in toRetry)
        {
            download.RetryCount++;
            download.Status = DownloadQueueStatus.Queued;
            download.Progress = 0;
            download.ErrorMessage = null;
            download.BytesDownloaded = 0;

            _downloadQueue.Enqueue(download);
            UpdateOnUIThread(() => ActiveDownloads.Add(download));
            TotalFailed--;
        }

        if (toRetry.Count > 0)
        {
            _ = ProcessQueueAsync();
        }
    }

    /// <summary>
    /// Retry a specific failed download
    /// </summary>
    public void RetryDownload(QueuedDownload download)
    {
        if (download == null || !download.CanRetry) return;

        UpdateOnUIThread(() => FailedDownloads.Remove(download));

        download.RetryCount++;
        download.Status = DownloadQueueStatus.Queued;
        download.Progress = 0;
        download.ErrorMessage = null;
        download.BytesDownloaded = 0;

        _downloadQueue.Enqueue(download);
        UpdateOnUIThread(() => ActiveDownloads.Add(download));
        TotalFailed--;

        _ = ProcessQueueAsync();
    }

    private async Task ProcessQueueAsync()
    {
        if (_isProcessing) return;

        await _processSemaphore.WaitAsync();

        try
        {
            if (_isProcessing) return;
            _isProcessing = true;
            IsActive = true;
            _cancellationTokenSource = new CancellationTokenSource();

            while (_downloadQueue.TryDequeue(out var download))
            {
                if (_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    download.Status = DownloadQueueStatus.Cancelled;
                    download.ErrorMessage = "Cancelled by user";
                    UpdateOnUIThread(() =>
                    {
                        ActiveDownloads.Remove(download);
                        FailedDownloads.Add(download);
                    });
                    TotalFailed++;
                    continue;
                }

                await ProcessDownloadAsync(download, _cancellationTokenSource.Token);
            }
        }
        finally
        {
            _isProcessing = false;
            IsActive = false;
            CurrentStatus = "Idle";
            OverallProgress = 0;
            _processSemaphore.Release();
        }
    }

    private async Task ProcessDownloadAsync(QueuedDownload download, CancellationToken cancellationToken)
    {
        if (download?.Mod == null)
        {
            System.Diagnostics.Debug.WriteLine("Cannot process download with null mod");
            return;
        }

        var mod = download.Mod;
        var modName = mod.Name ?? "Unknown Mod";
        var gameVersion = download.GameVersion ?? "1.20.1";

        try
        {
            download.Status = DownloadQueueStatus.Downloading;
            download.StartTime = DateTime.Now;
            CurrentStatus = $"Downloading {modName}...";

            // Get compatible file
            ModFile? file = null;

            if (mod.Source == ModSource.CurseForge && mod.Id > 0)
            {
                file = await _curseForge.GetCompatibleFileAsync(mod.Id, gameVersion);
            }
            else if (mod.Source == ModSource.Modrinth && !string.IsNullOrEmpty(mod.Slug))
            {
                file = await _modrinth.GetCompatibleFileAsync(mod.Slug, gameVersion);
            }

            if (file == null || string.IsNullOrEmpty(file.DownloadUrl))
            {
                throw new Exception($"No compatible file found for {gameVersion}");
            }

            if (string.IsNullOrEmpty(file.FileName))
            {
                throw new Exception("Downloaded file has no filename");
            }

            download.FileName = file.FileName;
            download.FileSize = file.FileSize;
            download.DownloadUrl = file.DownloadUrl;

            var destinationFolder = download.DestinationFolder ?? "";
            if (string.IsNullOrEmpty(destinationFolder))
            {
                throw new Exception("No destination folder specified");
            }

            var destinationPath = Path.Combine(destinationFolder, file.FileName);
            Directory.CreateDirectory(destinationFolder);

            // Download with progress
            var progress = new Progress<DownloadProgress>(p =>
            {
                download.Progress = p.ProgressPercentage;
                download.BytesDownloaded = p.BytesReceived;
                download.DownloadSpeed = p.DownloadSpeed;
                UpdateOverallProgress();
            });

            await _downloadService.DownloadFileAsync(file.DownloadUrl, destinationPath, progress, cancellationToken);

            // Download dependencies if needed
            if (file.Dependencies != null && file.Dependencies.Count > 0)
            {
                download.Status = DownloadQueueStatus.DownloadingDependencies;
                CurrentStatus = $"Downloading dependencies for {modName}...";

                foreach (var dep in file.Dependencies)
                {
                    if (dep == null || dep.Type != DependencyType.Required) continue;

                    try
                    {
                        if (dep.ModId > 0)
                        {
                            var depFile = await _curseForge.GetCompatibleFileAsync(dep.ModId, gameVersion);
                            if (depFile != null && !string.IsNullOrEmpty(depFile.DownloadUrl) && !string.IsNullOrEmpty(depFile.FileName))
                            {
                                var depPath = Path.Combine(destinationFolder, depFile.FileName);
                                if (!File.Exists(depPath))
                                {
                                    await _downloadService.DownloadFileAsync(depFile.DownloadUrl, depPath, null, cancellationToken);
                                }
                            }
                        }
                    }
                    catch (Exception depEx)
                    {
                        // Continue with other dependencies
                        System.Diagnostics.Debug.WriteLine($"Error downloading dependency: {depEx.Message}");
                    }
                }
            }

            // Mark as completed
            download.Status = DownloadQueueStatus.Completed;
            download.Progress = 100;
            download.CompletedTime = DateTime.Now;

            // Add to profile if provided
            if (download.Profile?.InstalledMods != null)
            {
                download.Profile.InstalledMods.Add(new InstalledMod
                {
                    ModId = mod.Id,
                    Name = modName,
                    FileName = file.FileName,
                    Version = file.DisplayName ?? "",
                    FilePath = destinationPath,
                    Source = mod.Source
                });
            }

            UpdateOnUIThread(() =>
            {
                ActiveDownloads.Remove(download);
                CompletedDownloads.Add(download);
            });
            TotalCompleted++;
        }
        catch (OperationCanceledException)
        {
            download.Status = DownloadQueueStatus.Cancelled;
            download.ErrorMessage = "Cancelled";
            UpdateOnUIThread(() =>
            {
                ActiveDownloads.Remove(download);
                FailedDownloads.Add(download);
            });
            TotalFailed++;
        }
        catch (Exception ex)
        {
            download.Status = DownloadQueueStatus.Failed;
            download.ErrorMessage = ex.Message;
            System.Diagnostics.Debug.WriteLine($"Download failed for {modName}: {ex}");
            UpdateOnUIThread(() =>
            {
                ActiveDownloads.Remove(download);
                FailedDownloads.Add(download);
            });
            TotalFailed++;
        }
    }

    private void UpdateOverallProgress()
    {
        if (TotalQueued == 0) return;

        var completed = TotalCompleted + TotalFailed;
        var activeProgress = 0.0;

        foreach (var download in ActiveDownloads)
        {
            activeProgress += download.Progress / 100.0;
        }

        OverallProgress = (completed + activeProgress) / TotalQueued * 100;
    }

    private void UpdateOnUIThread(Action action)
    {
        if (System.Windows.Application.Current?.Dispatcher != null)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(action);
        }
        else
        {
            action();
        }
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public class QueuedDownload : INotifyPropertyChanged
{
    public string Id { get; set; } = string.Empty;
    public ModInfo Mod { get; set; } = null!;
    public string GameVersion { get; set; } = string.Empty;
    public string DestinationFolder { get; set; } = string.Empty;
    public ServerProfile? Profile { get; set; }
    public string? FileName { get; set; }
    public string? DownloadUrl { get; set; }
    public long FileSize { get; set; }
    public DateTime QueuedTime { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? CompletedTime { get; set; }
    public int RetryCount { get; set; }
    public const int MaxRetries = 2;

    private DownloadQueueStatus _status;
    public DownloadQueueStatus Status
    {
        get => _status;
        set { _status = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusText)); }
    }

    private double _progress;
    public double Progress
    {
        get => _progress;
        set { _progress = value; OnPropertyChanged(); }
    }

    private long _bytesDownloaded;
    public long BytesDownloaded
    {
        get => _bytesDownloaded;
        set { _bytesDownloaded = value; OnPropertyChanged(); }
    }

    private double _downloadSpeed;
    public double DownloadSpeed
    {
        get => _downloadSpeed;
        set { _downloadSpeed = value; OnPropertyChanged(); OnPropertyChanged(nameof(SpeedText)); }
    }

    private string? _errorMessage;
    public string? ErrorMessage
    {
        get => _errorMessage;
        set { _errorMessage = value; OnPropertyChanged(); }
    }

    public bool CanRetry => RetryCount < MaxRetries && (Status == DownloadQueueStatus.Failed);

    public string StatusText => Status switch
    {
        DownloadQueueStatus.Queued => RetryCount > 0 ? $"Queued (retry {RetryCount})" : "Queued",
        DownloadQueueStatus.Downloading => $"Downloading... {Progress:F0}%",
        DownloadQueueStatus.DownloadingDependencies => "Getting dependencies...",
        DownloadQueueStatus.Completed => "Completed",
        DownloadQueueStatus.Failed => CanRetry ? $"Failed (will retry): {ErrorMessage}" : $"Failed: {ErrorMessage}",
        DownloadQueueStatus.Cancelled => "Cancelled",
        _ => "Unknown"
    };

    public string SpeedText
    {
        get
        {
            if (DownloadSpeed < 1024) return $"{DownloadSpeed:F0} B/s";
            if (DownloadSpeed < 1024 * 1024) return $"{DownloadSpeed / 1024:F1} KB/s";
            return $"{DownloadSpeed / 1024 / 1024:F1} MB/s";
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public enum DownloadQueueStatus
{
    Queued,
    Downloading,
    DownloadingDependencies,
    Completed,
    Failed,
    Cancelled
}
