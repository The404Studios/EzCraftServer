using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EzCraftModManager.Models;
using EzCraftModManager.Services;

namespace EzCraftModManager.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly ProfileService _profileService;
    private readonly ForgeService _forgeService;

    [ObservableProperty]
    private ViewModelBase? _currentView;

    [ObservableProperty]
    private string _currentViewName = "Home";

    [ObservableProperty]
    private ServerProfile? _selectedProfile;

    [ObservableProperty]
    private ObservableCollection<ServerProfile> _profiles = new();

    [ObservableProperty]
    private JavaInfo? _javaInfo;

    [ObservableProperty]
    private bool _showDownloadQueue;

    // Download Queue Service for UI binding
    public DownloadQueueService DownloadQueue => DownloadQueueService.Instance;

    // Child ViewModels
    public HomeViewModel HomeViewModel { get; }
    public ModBrowserViewModel ModBrowserViewModel { get; }
    public ServerManagerViewModel ServerManagerViewModel { get; }
    public ModPacksViewModel ModPacksViewModel { get; }
    public SettingsViewModel SettingsViewModel { get; }
    public DownloaderViewModel DownloaderViewModel { get; }

    public MainViewModel()
    {
        _profileService = new ProfileService();
        _forgeService = new ForgeService();

        // Initialize child ViewModels
        HomeViewModel = new HomeViewModel(this);
        ModBrowserViewModel = new ModBrowserViewModel(this);
        ServerManagerViewModel = new ServerManagerViewModel(this);
        ModPacksViewModel = new ModPacksViewModel(this);
        SettingsViewModel = new SettingsViewModel(this);
        DownloaderViewModel = new DownloaderViewModel(this);

        CurrentView = HomeViewModel;

        // Subscribe to download queue changes
        DownloadQueue.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(DownloadQueue.IsActive))
            {
                OnPropertyChanged(nameof(DownloadQueue));
            }
        };
    }

    partial void OnSelectedProfileChanged(ServerProfile? value)
    {
        // Notify child view models about profile change
        ModPacksViewModel?.UpdateServerSelectionStatus();
        ModBrowserViewModel?.OnPropertyChanged(nameof(ModBrowserViewModel.SelectedGameVersion));
    }

    public async Task InitializeAsync()
    {
        IsLoading = true;
        try
        {
            // Check Java installation
            JavaInfo = await _forgeService.CheckJavaInstallationAsync();

            // Load profiles
            var profiles = await _profileService.GetAllProfilesAsync();
            Profiles.Clear();
            foreach (var profile in profiles)
            {
                Profiles.Add(profile);
            }

            if (Profiles.Count > 0)
            {
                SelectedProfile = Profiles[0];
            }

            StatusMessage = JavaInfo?.IsInstalled == true
                ? $"Java {JavaInfo.Version} detected"
                : "Java not detected - please install Java 17+";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Initialization error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void NavigateToHome()
    {
        CurrentView = HomeViewModel;
        CurrentViewName = "Home";
    }

    [RelayCommand]
    private void NavigateToModBrowser()
    {
        CurrentView = ModBrowserViewModel;
        CurrentViewName = "Mod Browser";
    }

    [RelayCommand]
    private void NavigateToServerManager()
    {
        CurrentView = ServerManagerViewModel;
        CurrentViewName = "Server Manager";
    }

    [RelayCommand]
    private void NavigateToModPacks()
    {
        ModPacksViewModel.UpdateServerSelectionStatus();
        CurrentView = ModPacksViewModel;
        CurrentViewName = "Mod Packs";
    }

    [RelayCommand]
    private void NavigateToSettings()
    {
        CurrentView = SettingsViewModel;
        CurrentViewName = "Settings";
    }

    [RelayCommand]
    private void NavigateToDownloader()
    {
        CurrentView = DownloaderViewModel;
        CurrentViewName = "Downloads";
        ShowDownloadQueue = false; // Close side panel when viewing full page
    }

    [RelayCommand]
    private void ToggleDownloadQueue()
    {
        ShowDownloadQueue = !ShowDownloadQueue;
    }

    [RelayCommand]
    private void RetryAllFailed()
    {
        DownloadQueue.RetryFailed();
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
    private async Task RefreshProfilesAsync()
    {
        var profiles = await _profileService.GetAllProfilesAsync();
        Profiles.Clear();
        foreach (var profile in profiles)
        {
            Profiles.Add(profile);
        }
    }

    public async Task SaveProfileAsync(ServerProfile profile)
    {
        if (profile == null)
        {
            System.Diagnostics.Debug.WriteLine("Cannot save null profile");
            return;
        }

        try
        {
            await _profileService.SaveProfileAsync(profile);
            await RefreshProfilesAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error saving profile: {ex.Message}");
            ErrorMessage = $"Error saving profile: {ex.Message}";
        }
    }

    public async Task DeleteProfileAsync(ServerProfile profile)
    {
        if (profile == null)
        {
            System.Diagnostics.Debug.WriteLine("Cannot delete null profile");
            return;
        }

        try
        {
            await _profileService.DeleteProfileAsync(profile.Id);
            await RefreshProfilesAsync();

            if (SelectedProfile?.Id == profile.Id)
            {
                SelectedProfile = Profiles.Count > 0 ? Profiles[0] : null;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error deleting profile: {ex.Message}");
            ErrorMessage = $"Error deleting profile: {ex.Message}";
        }
    }
}
