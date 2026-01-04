using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EzCraftModManager.Models;
using EzCraftModManager.Services;

namespace EzCraftModManager.ViewModels;

public partial class HomeViewModel : ViewModelBase
{
    private readonly MainViewModel _mainViewModel;
    private readonly CurseForgeService _curseForge;
    private readonly ModrinthService _modrinth;

    [ObservableProperty]
    private ObservableCollection<ModInfo> _popularMods = new();

    [ObservableProperty]
    private ObservableCollection<ModInfo> _recentMods = new();

    [ObservableProperty]
    private ObservableCollection<ModPack> _featuredPacks = new();

    [ObservableProperty]
    private string _welcomeMessage = "Welcome to EzCraft Mod Manager";

    [ObservableProperty]
    private int _totalProfiles;

    [ObservableProperty]
    private int _totalInstalledMods;

    public HomeViewModel(MainViewModel mainViewModel)
    {
        _mainViewModel = mainViewModel;
        _curseForge = new CurseForgeService();
        _modrinth = new ModrinthService();

        // Add featured packs
        foreach (var pack in CuratedModPacks.AllPacks)
        {
            FeaturedPacks.Add(pack);
        }
    }

    public async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            // Load popular mods from CurseForge with better error handling
            try
            {
                var popular = await _curseForge.GetPopularModsAsync("1.20.1", 8);
                PopularMods.Clear();
                if (popular != null)
                {
                    foreach (var mod in popular)
                    {
                        if (mod != null)
                        {
                            PopularMods.Add(mod);
                        }
                    }
                }
            }
            catch (Exception apiEx)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading popular mods: {apiEx.Message}");
                // Don't fail completely if API call fails - app can still work
            }

            // Update stats with null checks
            TotalProfiles = _mainViewModel?.Profiles?.Count ?? 0;
            TotalInstalledMods = _mainViewModel?.Profiles?.Sum(p => p.InstalledMods?.Count ?? 0) ?? 0;

            if (PopularMods.Count > 0)
            {
                StatusMessage = $"Loaded {PopularMods.Count} popular mods";
            }
            else
            {
                StatusMessage = "Welcome! Create a server to get started.";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error loading home: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"Error in HomeViewModel.LoadAsync: {ex}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void OpenModBrowser()
    {
        _mainViewModel?.NavigateToModBrowserCommand?.Execute(null);
    }

    [RelayCommand]
    private void OpenServerManager()
    {
        _mainViewModel?.NavigateToServerManagerCommand?.Execute(null);
    }

    [RelayCommand]
    private void OpenModPacks()
    {
        _mainViewModel?.NavigateToModPacksCommand?.Execute(null);
    }

    [RelayCommand]
    private async Task QuickInstallPackAsync(ModPack? pack)
    {
        if (pack == null || _mainViewModel?.ModPacksViewModel == null) return;

        _mainViewModel.ModPacksViewModel.SelectedPack = pack;
        _mainViewModel.NavigateToModPacksCommand?.Execute(null);
    }
}
