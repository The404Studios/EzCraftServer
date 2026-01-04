using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EzCraftModManager.Models;
using EzCraftModManager.Services;

namespace EzCraftModManager.ViewModels;

public partial class ModBrowserViewModel : ViewModelBase
{
    private readonly MainViewModel _mainViewModel;
    private readonly CurseForgeService _curseForge;
    private readonly ModrinthService _modrinth;
    private readonly DownloadService _downloadService;
    private CancellationTokenSource? _searchCts;

    [ObservableProperty]
    private ObservableCollection<ModInfo> _searchResults = new();

    [ObservableProperty]
    private ObservableCollection<ModInfo> _selectedMods = new();

    [ObservableProperty]
    private ModInfo? _selectedMod;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private string _selectedGameVersion = "1.20.1";

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
    private string _selectedSource = "All";

    [ObservableProperty]
    private ObservableCollection<string> _availableSources = new() { "All", "CurseForge", "Modrinth" };

    [ObservableProperty]
    private string _selectedCategory = "All";

    [ObservableProperty]
    private ObservableCollection<string> _availableCategories = new()
    {
        "All", "Adventure", "Magic", "Technology", "Storage", "Library",
        "World Gen", "Mobs", "Food", "Equipment", "Utility"
    };

    [ObservableProperty]
    private bool _isSearching;

    [ObservableProperty]
    private bool _isDownloading;

    [ObservableProperty]
    private double _downloadProgress;

    [ObservableProperty]
    private string _downloadStatus = string.Empty;

    [ObservableProperty]
    private int _currentPage = 0;

    [ObservableProperty]
    private bool _hasMoreResults = true;

    public ModBrowserViewModel(MainViewModel mainViewModel)
    {
        _mainViewModel = mainViewModel;
        _curseForge = new CurseForgeService();
        _modrinth = new ModrinthService();
        _downloadService = new DownloadService();
    }

    partial void OnSearchQueryChanged(string value)
    {
        // Debounce search
        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();

        Task.Delay(300, _searchCts.Token).ContinueWith(async _ =>
        {
            if (!_searchCts.Token.IsCancellationRequested)
            {
                await SearchAsync();
            }
        }, TaskScheduler.FromCurrentSynchronizationContext());
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        if (string.IsNullOrWhiteSpace(SearchQuery) && SelectedCategory == "All")
        {
            await LoadPopularAsync();
            return;
        }

        IsSearching = true;
        CurrentPage = 0;
        SearchResults.Clear();

        try
        {
            var tasks = new System.Collections.Generic.List<Task<System.Collections.Generic.List<ModInfo>>>();

            if (SelectedSource == "All" || SelectedSource == "CurseForge")
            {
                if (SelectedCategory != "All")
                {
                    tasks.Add(_curseForge.GetModsByCategory(SelectedCategory, SelectedGameVersion, 20));
                }
                else
                {
                    tasks.Add(_curseForge.SearchModsAsync(SearchQuery, SelectedGameVersion, 20, CurrentPage));
                }
            }

            if (SelectedSource == "All" || SelectedSource == "Modrinth")
            {
                if (SelectedCategory != "All")
                {
                    tasks.Add(_modrinth.GetModsByCategory(SelectedCategory.ToLower().Replace(" ", ""), SelectedGameVersion, 20));
                }
                else
                {
                    tasks.Add(_modrinth.SearchModsAsync(SearchQuery, SelectedGameVersion, 20, CurrentPage * 20));
                }
            }

            var results = await Task.WhenAll(tasks);
            var allMods = results.SelectMany(r => r).ToList();

            // Remove duplicates based on name similarity
            var uniqueMods = allMods
                .GroupBy(m => m.Name.ToLower().Replace(" ", "").Replace("-", ""))
                .Select(g => g.First())
                .OrderByDescending(m => m.DownloadCount)
                .ToList();

            foreach (var mod in uniqueMods)
            {
                SearchResults.Add(mod);
            }

            HasMoreResults = SearchResults.Count >= 20;
            StatusMessage = $"Found {SearchResults.Count} mods";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Search error: {ex.Message}";
        }
        finally
        {
            IsSearching = false;
        }
    }

    [RelayCommand]
    private async Task LoadPopularAsync()
    {
        IsSearching = true;
        SearchResults.Clear();

        try
        {
            var cfMods = await _curseForge.GetPopularModsAsync(SelectedGameVersion, 15);
            var mrMods = await _modrinth.GetPopularModsAsync(SelectedGameVersion, 15);

            var allMods = cfMods.Concat(mrMods)
                .GroupBy(m => m.Name.ToLower().Replace(" ", ""))
                .Select(g => g.First())
                .OrderByDescending(m => m.DownloadCount)
                .Take(30);

            foreach (var mod in allMods)
            {
                SearchResults.Add(mod);
            }

            StatusMessage = "Showing popular mods";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error loading popular mods: {ex.Message}";
        }
        finally
        {
            IsSearching = false;
        }
    }

    [RelayCommand]
    private async Task LoadMoreAsync()
    {
        if (!HasMoreResults || IsSearching) return;

        CurrentPage++;
        IsSearching = true;

        try
        {
            var cfMods = await _curseForge.SearchModsAsync(SearchQuery, SelectedGameVersion, 20, CurrentPage);
            var mrMods = await _modrinth.SearchModsAsync(SearchQuery, SelectedGameVersion, 20, CurrentPage * 20);

            var allMods = cfMods.Concat(mrMods)
                .GroupBy(m => m.Name.ToLower().Replace(" ", ""))
                .Select(g => g.First());

            foreach (var mod in allMods)
            {
                SearchResults.Add(mod);
            }

            HasMoreResults = cfMods.Count >= 20 || mrMods.Count >= 20;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error loading more: {ex.Message}";
        }
        finally
        {
            IsSearching = false;
        }
    }

    [RelayCommand]
    private void AddToSelected(ModInfo mod)
    {
        if (!SelectedMods.Any(m => m.Id == mod.Id && m.Source == mod.Source))
        {
            SelectedMods.Add(mod);
            StatusMessage = $"Added {mod.Name} to install queue ({SelectedMods.Count} mods selected)";
        }
    }

    [RelayCommand]
    private void RemoveFromSelected(ModInfo mod)
    {
        var toRemove = SelectedMods.FirstOrDefault(m => m.Id == mod.Id && m.Source == mod.Source);
        if (toRemove != null)
        {
            SelectedMods.Remove(toRemove);
            StatusMessage = $"Removed {mod.Name} from install queue";
        }
    }

    [RelayCommand]
    private void ClearSelected()
    {
        SelectedMods.Clear();
        StatusMessage = "Cleared install queue";
    }

    [RelayCommand]
    private async Task InstallSelectedModsAsync()
    {
        try
        {
            var profile = _mainViewModel?.SelectedProfile;
            if (profile == null)
            {
                ErrorMessage = "Please select or create a server profile first";
                return;
            }

            if (SelectedMods == null || SelectedMods.Count == 0)
            {
                ErrorMessage = "No mods selected for installation";
                return;
            }

            var modsFolder = profile.ModsPath;
            if (string.IsNullOrEmpty(modsFolder))
            {
                ErrorMessage = "Server mods folder path is not configured";
                return;
            }

            IsDownloading = true;
            DownloadProgress = 0;

            var modsList = SelectedMods.Where(m => m != null).ToList();
            var totalMods = modsList.Count;
            var completed = 0;
            var gameVersion = SelectedGameVersion ?? "1.20.1";

            System.IO.Directory.CreateDirectory(modsFolder);

            foreach (var mod in modsList)
            {
                if (mod == null) continue;

                var modName = mod.Name ?? "Unknown Mod";
                DownloadStatus = $"Installing {modName} ({completed + 1}/{totalMods})...";

                ModFile? file = null;

                try
                {
                    // Get compatible file
                    if (mod.Source == ModSource.CurseForge && mod.Id > 0)
                    {
                        file = await _curseForge.GetCompatibleFileAsync(mod.Id, gameVersion);
                    }
                    else if (mod.Source == ModSource.Modrinth && !string.IsNullOrEmpty(mod.Slug))
                    {
                        file = await _modrinth.GetCompatibleFileAsync(mod.Slug, gameVersion);
                    }

                    if (file != null && !string.IsNullOrEmpty(file.DownloadUrl) && !string.IsNullOrEmpty(file.FileName))
                    {
                        var progress = new Progress<DownloadProgress>(p =>
                        {
                            DownloadProgress = (completed + p.ProgressPercentage / 100.0) / totalMods * 100;
                        });

                        await _downloadService.DownloadModWithDependenciesAsync(
                            mod, file, modsFolder, _curseForge, _modrinth, gameVersion,
                            null, CancellationToken.None);

                        // Update profile
                        if (profile.InstalledMods != null)
                        {
                            profile.InstalledMods.Add(new InstalledMod
                            {
                                ModId = mod.Id,
                                Name = modName,
                                FileName = file.FileName,
                                Version = file.DisplayName ?? "",
                                FilePath = System.IO.Path.Combine(modsFolder, file.FileName),
                                Source = mod.Source
                            });
                        }
                    }
                }
                catch (Exception modEx)
                {
                    System.Diagnostics.Debug.WriteLine($"Error installing {modName}: {modEx.Message}");
                }

                completed++;
                DownloadProgress = (double)completed / totalMods * 100;
            }

            if (_mainViewModel != null)
            {
                await _mainViewModel.SaveProfileAsync(profile);
            }

            SelectedMods.Clear();
            DownloadStatus = $"Successfully installed {completed} mods!";
            StatusMessage = $"Installed {completed} mods to {profile.Name ?? "server"}";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Installation error: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"Installation error: {ex}");
        }
        finally
        {
            IsDownloading = false;
        }
    }

    [RelayCommand]
    private async Task QuickInstallModAsync(ModInfo mod)
    {
        AddToSelected(mod);
        await InstallSelectedModsAsync();
    }
}
