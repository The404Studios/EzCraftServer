using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EzCraftModManager.Models;
using EzCraftModManager.Services;

namespace EzCraftModManager.ViewModels;

public partial class DownloaderViewModel : ObservableObject
{
    private readonly MainViewModel _mainViewModel;

    public DownloaderViewModel(MainViewModel mainViewModel)
    {
        _mainViewModel = mainViewModel;
    }

    public DownloadQueueService DownloadQueue => DownloadQueueService.Instance;

    public ServerProfile? SelectedProfile => _mainViewModel?.SelectedProfile;

    [RelayCommand]
    private void ClearCompletedDownloads()
    {
        DownloadQueue.ClearCompleted();
    }

    [RelayCommand]
    private void CancelAllDownloads()
    {
        DownloadQueue.CancelAll();
    }

    [RelayCommand]
    private void RetryAllFailed()
    {
        DownloadQueue.RetryFailed();
    }

    [RelayCommand]
    private void RetryDownload(QueuedDownload? download)
    {
        if (download != null)
        {
            DownloadQueue.RetryDownload(download);
        }
    }
}
