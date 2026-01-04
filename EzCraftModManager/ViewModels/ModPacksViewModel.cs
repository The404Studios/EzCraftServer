using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
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
    private readonly DownloadService _downloadService;

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

    public ModPacksViewModel(MainViewModel mainViewModel)
    {
        _mainViewModel = mainViewModel;
        _curseForge = new CurseForgeService();
        _modrinth = new ModrinthService();
        _downloadService = new DownloadService();

        // Load curated packs
        foreach (var pack in CuratedModPacks.AllPacks)
        {
            ModPacks.Add(pack);
        }
    }

    partial void OnSelectedPackChanged(ModPack? value)
    {
        if (value != null)
        {
            Task.Run(() => LoadPackModsAsync(value));
        }
    }

    private async Task LoadPackModsAsync(ModPack pack)
    {
        IsLoading = true;
        ModsFound = 0;
        ModsTotal = pack.Mods.Count;

        try
        {
            var gameVersion = _mainViewModel.SelectedProfile?.MinecraftVersion ?? pack.RecommendedMinecraftVersion;

            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => PackMods.Clear());

            foreach (var modItem in pack.Mods)
            {
                var viewModel = new ModPackItemViewModel
                {
                    SearchQuery = modItem.SearchQuery,
                    Description = modItem.Description,
                    IsRequired = modItem.IsRequired,
                    IsSelected = modItem.IsRequired,
                    Status = "Searching..."
                };

                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => PackMods.Add(viewModel));

                // Search for mod
                ModInfo? foundMod = null;

                // Try CurseForge first
                if (modItem.CurseForgeId.HasValue)
                {
                    foundMod = await _curseForge.GetModAsync(modItem.CurseForgeId.Value);
                }

                if (foundMod == null)
                {
                    var cfResults = await _curseForge.SearchModsAsync(modItem.SearchQuery, gameVersion, 5);
                    foundMod = cfResults.FirstOrDefault();
                }

                // Try Modrinth if not found on CurseForge
                if (foundMod == null)
                {
                    var mrResults = await _modrinth.SearchModsAsync(modItem.SearchQuery, gameVersion, 5);
                    foundMod = mrResults.FirstOrDefault();
                }

                if (foundMod != null)
                {
                    viewModel.Mod = foundMod;
                    viewModel.Status = "Available";
                    viewModel.IsAvailable = true;
                    ModsFound++;
                }
                else
                {
                    viewModel.Status = "Not found for this version";
                    viewModel.IsAvailable = false;
                }
            }

            StatusMessage = $"Found {ModsFound}/{ModsTotal} mods for {gameVersion}";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error loading pack mods: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task InstallPackAsync()
    {
        if (_mainViewModel.SelectedProfile == null)
        {
            ErrorMessage = "Please select or create a server profile first";
            return;
        }

        var modsToInstall = PackMods.Where(m => m.IsSelected && m.IsAvailable && m.Mod != null).ToList();

        if (modsToInstall.Count == 0)
        {
            ErrorMessage = "No mods selected for installation";
            return;
        }

        IsInstalling = true;
        InstallProgress = 0;
        var modsFolder = _mainViewModel.SelectedProfile.ModsPath;
        var gameVersion = _mainViewModel.SelectedProfile.MinecraftVersion;
        var installed = 0;

        try
        {
            foreach (var modVm in modsToInstall)
            {
                var mod = modVm.Mod!;
                InstallStatus = $"Installing {mod.Name} ({installed + 1}/{modsToInstall.Count})...";
                modVm.Status = "Installing...";

                try
                {
                    ModFile? file = null;

                    if (mod.Source == ModSource.CurseForge)
                    {
                        file = await _curseForge.GetCompatibleFileAsync(mod.Id, gameVersion);
                    }
                    else if (mod.Source == ModSource.Modrinth)
                    {
                        file = await _modrinth.GetCompatibleFileAsync(mod.Slug, gameVersion);
                    }

                    if (file != null && !string.IsNullOrEmpty(file.DownloadUrl))
                    {
                        await _downloadService.DownloadModWithDependenciesAsync(
                            mod, file, modsFolder, _curseForge, _modrinth, gameVersion);

                        _mainViewModel.SelectedProfile.InstalledMods.Add(new InstalledMod
                        {
                            ModId = mod.Id,
                            Name = mod.Name,
                            FileName = file.FileName,
                            Version = file.DisplayName,
                            FilePath = System.IO.Path.Combine(modsFolder, file.FileName),
                            Source = mod.Source
                        });

                        modVm.Status = "Installed!";
                        installed++;
                    }
                    else
                    {
                        modVm.Status = "No compatible file found";
                    }
                }
                catch (Exception ex)
                {
                    modVm.Status = $"Error: {ex.Message}";
                }

                InstallProgress = (double)(installed + 1) / modsToInstall.Count * 100;
            }

            await _mainViewModel.SaveProfileAsync(_mainViewModel.SelectedProfile);

            InstallStatus = $"Successfully installed {installed}/{modsToInstall.Count} mods!";
            StatusMessage = $"Installed {SelectedPack?.Name} pack to {_mainViewModel.SelectedProfile.Name}";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Pack installation error: {ex.Message}";
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
}
