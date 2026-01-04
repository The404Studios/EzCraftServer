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

    // Child ViewModels
    public HomeViewModel HomeViewModel { get; }
    public ModBrowserViewModel ModBrowserViewModel { get; }
    public ServerManagerViewModel ServerManagerViewModel { get; }
    public ModPacksViewModel ModPacksViewModel { get; }
    public SettingsViewModel SettingsViewModel { get; }

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

        CurrentView = HomeViewModel;
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
        await _profileService.SaveProfileAsync(profile);
        await RefreshProfilesAsync();
    }

    public async Task DeleteProfileAsync(ServerProfile profile)
    {
        await _profileService.DeleteProfileAsync(profile.Id);
        await RefreshProfilesAsync();

        if (SelectedProfile?.Id == profile.Id)
        {
            SelectedProfile = Profiles.Count > 0 ? Profiles[0] : null;
        }
    }
}
