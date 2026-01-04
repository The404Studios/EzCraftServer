using System;
using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EzCraftModManager.Models;
using EzCraftModManager.Services;
using Microsoft.Win32;

namespace EzCraftModManager.ViewModels;

public partial class DownloaderViewModel : ViewModelBase
{
    private readonly MainViewModel _mainViewModel;

    [ObservableProperty]
    private string _standaloneDownloadFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "EzCraft Mods");

    [ObservableProperty]
    private ObservableCollection<ModInfo> _standaloneCart = new();

    [ObservableProperty]
    private bool _isDownloadingStandalone;

    public DownloaderViewModel(MainViewModel mainViewModel)
    {
        _mainViewModel = mainViewModel;

        // Ensure default folder exists
        if (!Directory.Exists(StandaloneDownloadFolder))
        {
            try
            {
                Directory.CreateDirectory(StandaloneDownloadFolder);
            }
            catch { }
        }
    }

    public DownloadQueueService DownloadQueue => DownloadQueueService.Instance;

    public ServerProfile? SelectedProfile => _mainViewModel?.SelectedProfile;

    public int StandaloneCartCount => StandaloneCart.Count;

    [RelayCommand]
    private void BrowseFolder()
    {
        var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Select folder for standalone mod downloads",
            SelectedPath = StandaloneDownloadFolder,
            ShowNewFolderButton = true
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            StandaloneDownloadFolder = dialog.SelectedPath;
            StatusMessage = $"Download folder set to: {StandaloneDownloadFolder}";
        }
    }

    [RelayCommand]
    private void AddToStandaloneCart(ModInfo? mod)
    {
        if (mod == null) return;

        if (!StandaloneCart.Contains(mod))
        {
            StandaloneCart.Add(mod);
            OnPropertyChanged(nameof(StandaloneCartCount));
            StatusMessage = $"Added '{mod.Name}' to standalone cart";
        }
    }

    [RelayCommand]
    private void RemoveFromStandaloneCart(ModInfo? mod)
    {
        if (mod == null) return;

        if (StandaloneCart.Remove(mod))
        {
            OnPropertyChanged(nameof(StandaloneCartCount));
            StatusMessage = $"Removed '{mod.Name}' from cart";
        }
    }

    [RelayCommand]
    private void ClearStandaloneCart()
    {
        StandaloneCart.Clear();
        OnPropertyChanged(nameof(StandaloneCartCount));
        StatusMessage = "Cart cleared";
    }

    [RelayCommand]
    private async System.Threading.Tasks.Task DownloadStandaloneCartAsync()
    {
        if (StandaloneCart.Count == 0)
        {
            ErrorMessage = "Cart is empty. Add some mods first!";
            return;
        }

        if (string.IsNullOrEmpty(StandaloneDownloadFolder))
        {
            ErrorMessage = "Please select a download folder first";
            return;
        }

        // Ensure folder exists
        try
        {
            Directory.CreateDirectory(StandaloneDownloadFolder);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Cannot create folder: {ex.Message}";
            return;
        }

        IsDownloadingStandalone = true;
        ClearMessages();

        try
        {
            // Get game version from the main view model or default
            var gameVersion = _mainViewModel?.ModBrowserViewModel?.SelectedGameVersion ?? "1.20.1";

            // Queue all mods for download
            foreach (var mod in StandaloneCart)
            {
                DownloadQueue.EnqueueDownload(mod, gameVersion, StandaloneDownloadFolder, null);
            }

            StatusMessage = $"Queued {StandaloneCart.Count} mods for download to: {StandaloneDownloadFolder}";

            // Clear the cart after queueing
            StandaloneCart.Clear();
            OnPropertyChanged(nameof(StandaloneCartCount));
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error queueing downloads: {ex.Message}";
        }
        finally
        {
            IsDownloadingStandalone = false;
        }
    }

    [RelayCommand]
    private async System.Threading.Tasks.Task DownloadSingleStandaloneAsync(ModInfo? mod)
    {
        if (mod == null) return;

        if (string.IsNullOrEmpty(StandaloneDownloadFolder))
        {
            ErrorMessage = "Please select a download folder first";
            return;
        }

        try
        {
            Directory.CreateDirectory(StandaloneDownloadFolder);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Cannot create folder: {ex.Message}";
            return;
        }

        var gameVersion = _mainViewModel?.ModBrowserViewModel?.SelectedGameVersion ?? "1.20.1";
        DownloadQueue.EnqueueDownload(mod, gameVersion, StandaloneDownloadFolder, null);
        StatusMessage = $"Downloading '{mod.Name}' to: {StandaloneDownloadFolder}";
    }

    [RelayCommand]
    private void OpenDownloadFolder()
    {
        if (string.IsNullOrEmpty(StandaloneDownloadFolder)) return;

        try
        {
            if (Directory.Exists(StandaloneDownloadFolder))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = StandaloneDownloadFolder,
                    UseShellExecute = true
                });
            }
            else
            {
                ErrorMessage = "Folder does not exist yet. Download some mods first!";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error opening folder: {ex.Message}";
        }
    }

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
