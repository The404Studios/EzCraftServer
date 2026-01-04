using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EzCraftModManager.Models;
using EzCraftModManager.Services;

namespace EzCraftModManager.ViewModels;

public partial class ModPacksViewModel : ViewModelBase
{
    private readonly MainViewModel _mainViewModel;
    private readonly CurseForgeService _curseForge;
    private readonly ModrinthService _modrinth;
    private readonly DownloadQueueService _downloadQueue;

    [ObservableProperty]
    private ObservableCollection<ModPack> _modPacks = new();

    [ObservableProperty]
    private ModPack? _selectedPack;

    [ObservableProperty]
    private ObservableCollection<ModPackItemViewModel> _packMods = new();

    [ObservableProperty]
    private bool _isInstalling;

    [ObservableProperty]
    private double _installProgress;

    [ObservableProperty]
    private string _installStatus = string.Empty;

    [ObservableProperty]
    private int _modsFound;

    [ObservableProperty]
    private int _modsTotal;

    [ObservableProperty]
    private string _selectedVersion = "1.20.1";

    [ObservableProperty]
    private ObservableCollection<string> _availableVersions = new()
    {
        "1.21.4", "1.21.3", "1.21.1", "1.21",
        "1.20.6", "1.20.4", "1.20.2", "1.20.1", "1.20",
        "1.19.4", "1.19.3", "1.19.2", "1.19.1", "1.19",
        "1.18.2", "1.18.1", "1.18",
        "1.17.1", "1.17",
        "1.16.5", "1.16.4", "1.16.3", "1.16.2", "1.16.1",
        "1.15.2", "1.14.4", "1.12.2", "1.7.10"
    };

    [ObservableProperty]
    private bool _hasServerSelected;

    [ObservableProperty]
    private string _serverSelectionMessage = "Please select a server profile to install mods";

    public ModPacksViewModel(MainViewModel mainViewModel)
    {
        _mainViewModel = mainViewModel;
        _curseForge = new CurseForgeService();
        _modrinth = new ModrinthService();
        _downloadQueue = DownloadQueueService.Instance;

        // Load curated packs
        foreach (var pack in CuratedModPacks.AllPacks)
        {
            ModPacks.Add(pack);
        }

        // Update server selection status
        UpdateServerSelectionStatus();
    }

    public void UpdateServerSelectionStatus()
    {
        var profile = _mainViewModel?.SelectedProfile;
        HasServerSelected = profile != null;

        if (HasServerSelected && profile != null)
        {
            ServerSelectionMessage = $"Installing to: {profile.Name ?? "Server"}";
            SelectedVersion = profile.MinecraftVersion ?? "1.20.1";
        }
        else
        {
            ServerSelectionMessage = "Please select a server profile to install mods";
        }
    }

    partial void OnSelectedPackChanged(ModPack? value)
    {
        if (value != null)
        {
            Task.Run(() => LoadPackModsAsync(value));
        }
        else
        {
            Application.Current?.Dispatcher?.Invoke(() => PackMods.Clear());
            ModsFound = 0;
            ModsTotal = 0;
        }
    }

    partial void OnSelectedVersionChanged(string value)
    {
        if (SelectedPack != null && !string.IsNullOrEmpty(value))
        {
            Task.Run(() => LoadPackModsAsync(SelectedPack));
        }
    }

    private async Task LoadPackModsAsync(ModPack pack)
    {
        IsLoading = true;
        _modsFoundCount = 0;
        ModsFound = 0;
        ModsTotal = pack.Mods?.Count ?? 0;
        ClearMessages();

        try
        {
            var gameVersion = SelectedVersion ?? "1.20.1";
            var dispatcher = Application.Current?.Dispatcher;

            if (dispatcher != null)
            {
                await dispatcher.InvokeAsync(() => PackMods.Clear());
            }
            else
            {
                PackMods.Clear();
            }

            if (pack.Mods == null || pack.Mods.Count == 0)
            {
                ErrorMessage = "No mods defined in this pack";
                return;
            }

            // Create all view models first
            var viewModels = new List<ModPackItemViewModel>();
            foreach (var modItem in pack.Mods)
            {
                if (modItem == null) continue;

                var viewModel = new ModPackItemViewModel
                {
                    SearchQuery = modItem.SearchQuery ?? "Unknown",
                    Description = modItem.Description ?? "",
                    IsRequired = modItem.IsRequired,
                    IsSelected = modItem.IsRequired,
                    Status = "Searching..."
                };
                viewModels.Add(viewModel);

                if (dispatcher != null)
                {
                    await dispatcher.InvokeAsync(() => PackMods.Add(viewModel));
                }
                else
                {
                    PackMods.Add(viewModel);
                }
            }

            // Load all mods in parallel with concurrency limit
            var semaphore = new SemaphoreSlim(5); // Limit to 5 concurrent requests
            var tasks = new List<Task>();

            for (int i = 0; i < pack.Mods.Count; i++)
            {
                var modItem = pack.Mods[i];
                var viewModel = viewModels[i];

                if (modItem == null) continue;

                tasks.Add(Task.Run(async () =>
                {
                    await semaphore.WaitAsync();
                    try
                    {
                        ModInfo? foundMod = null;

                        try
                        {
                            // Try CurseForge first with ID if available
                            if (modItem.CurseForgeId.HasValue && modItem.CurseForgeId.Value > 0)
                            {
                                foundMod = await _curseForge.GetModAsync(modItem.CurseForgeId.Value);
                            }

                            // Search by name on CurseForge
                            if (foundMod == null && !string.IsNullOrEmpty(modItem.SearchQuery))
                            {
                                var cfResults = await _curseForge.SearchModsAsync(modItem.SearchQuery, gameVersion, 5);
                                foundMod = cfResults?.FirstOrDefault();
                            }

                            // Try Modrinth with ID if available
                            if (foundMod == null && !string.IsNullOrEmpty(modItem.ModrinthId))
                            {
                                foundMod = await _modrinth.GetModAsync(modItem.ModrinthId);
                            }

                            // Search by name on Modrinth
                            if (foundMod == null && !string.IsNullOrEmpty(modItem.SearchQuery))
                            {
                                var mrResults = await _modrinth.SearchModsAsync(modItem.SearchQuery, gameVersion, 5);
                                foundMod = mrResults?.FirstOrDefault();
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Error searching for {modItem.SearchQuery}: {ex.Message}");
                        }

                        if (foundMod != null)
                        {
                            viewModel.Mod = foundMod;
                            viewModel.Status = "Available";
                            viewModel.IsAvailable = true;
                            viewModel.GameVersions = foundMod.GameVersions != null
                                ? string.Join(", ", foundMod.GameVersions.Take(5))
                                : "";
                            Interlocked.Increment(ref _modsFoundCount);
                            ModsFound = _modsFoundCount;
                        }
                        else
                        {
                            viewModel.Status = $"Not found for {gameVersion}";
                            viewModel.IsAvailable = false;
                            viewModel.IsSelected = false;
                        }
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }));
            }

            await Task.WhenAll(tasks);
            ModsFound = _modsFoundCount;

            StatusMessage = $"Found {ModsFound}/{ModsTotal} mods for {gameVersion}";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error loading pack mods: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
            _modsFoundCount = 0;
        }
    }

    private int _modsFoundCount;

    [RelayCommand]
    private async Task InstallPackAsync()
    {
        try
        {
            // Validate server selection
            var profile = _mainViewModel?.SelectedProfile;
            if (profile == null)
            {
                ErrorMessage = "Please select a server profile first! Go to Server Manager to create or select a server.";
                return;
            }

            if (SelectedPack == null)
            {
                ErrorMessage = "Please select a mod pack first";
                return;
            }

            var modsToInstall = PackMods?.Where(m => m != null && m.IsSelected && m.IsAvailable && m.Mod != null).ToList();

            if (modsToInstall == null || modsToInstall.Count == 0)
            {
                ErrorMessage = "No mods selected for installation. Make sure mods are available for the selected version.";
                return;
            }

            IsInstalling = true;
            InstallProgress = 0;
            ClearMessages();

            var modsFolder = profile.ModsPath;
            var gameVersion = profile.MinecraftVersion ?? "1.20.1";
            var installed = 0;
            var failed = 0;

            if (string.IsNullOrEmpty(modsFolder))
            {
                ErrorMessage = "Server mods folder path is not configured";
                IsInstalling = false;
                return;
            }

            System.IO.Directory.CreateDirectory(modsFolder);

            foreach (var modVm in modsToInstall)
            {
                if (modVm?.Mod == null) continue;

                var mod = modVm.Mod;
                var modName = mod.Name ?? "Unknown Mod";

                InstallStatus = $"Installing {modName} ({installed + failed + 1}/{modsToInstall.Count})...";
                modVm.Status = "Installing...";

                try
                {
                    ModFile? file = null;

                    if (mod.Source == ModSource.CurseForge && mod.Id > 0)
                    {
                        file = await _curseForge.GetCompatibleFileAsync(mod.Id, gameVersion);
                    }
                    else if (mod.Source == ModSource.Modrinth && !string.IsNullOrEmpty(mod.Slug))
                    {
                        file = await _modrinth.GetCompatibleFileAsync(mod.Slug, gameVersion);
                    }

                    if (file != null && !string.IsNullOrEmpty(file.DownloadUrl))
                    {
                        // Use queue service for background download
                        _downloadQueue.EnqueueDownload(mod, gameVersion, modsFolder, profile);

                        modVm.Status = "Queued for download";
                        installed++;
                    }
                    else
                    {
                        modVm.Status = "No compatible file found";
                        failed++;
                    }
                }
                catch (Exception ex)
                {
                    modVm.Status = $"Error: {ex.Message}";
                    failed++;
                    System.Diagnostics.Debug.WriteLine($"Error installing {modName}: {ex}");
                }

                InstallProgress = (double)(installed + failed) / modsToInstall.Count * 100;
            }

            if (profile != null && _mainViewModel != null)
            {
                await _mainViewModel.SaveProfileAsync(profile);
            }

            var packName = SelectedPack?.Name ?? "selected pack";
            if (failed == 0)
            {
                InstallStatus = $"Queued {installed} mods for download!";
                StatusMessage = $"Started downloading {packName}";
            }
            else
            {
                InstallStatus = $"Queued {installed} mods, {failed} failed";
                ErrorMessage = $"{failed} mod(s) could not be installed";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Pack installation error: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"Pack installation error: {ex}");
        }
        finally
        {
            IsInstalling = false;
        }
    }

    [RelayCommand]
    private void SelectAllMods()
    {
        foreach (var mod in PackMods.Where(m => m.IsAvailable))
        {
            mod.IsSelected = true;
        }
    }

    [RelayCommand]
    private void SelectRequiredOnly()
    {
        foreach (var mod in PackMods)
        {
            mod.IsSelected = mod.IsRequired && mod.IsAvailable;
        }
    }

    [RelayCommand]
    private void DeselectAllMods()
    {
        foreach (var mod in PackMods)
        {
            mod.IsSelected = false;
        }
    }

    [RelayCommand]
    private void GoToServerManager()
    {
        _mainViewModel?.NavigateToServerManagerCommand?.Execute(null);
    }

    [RelayCommand]
    private async Task RefreshPackAsync()
    {
        if (SelectedPack != null)
        {
            await LoadPackModsAsync(SelectedPack);
        }
    }
}

public partial class ModPackItemViewModel : ObservableObject
{
    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private bool _isRequired;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isAvailable;

    [ObservableProperty]
    private string _status = string.Empty;

    [ObservableProperty]
    private ModInfo? _mod;

    [ObservableProperty]
    private string _gameVersions = string.Empty;

    public string DisplayName => Mod?.Name ?? SearchQuery;
    public string IconUrl => Mod?.IconUrl ?? string.Empty;
    public string DownloadCountText => Mod != null ? $"{Mod.DownloadCount:N0} downloads" : "";
    public string SourceText => Mod?.Source.ToString() ?? "";
}
