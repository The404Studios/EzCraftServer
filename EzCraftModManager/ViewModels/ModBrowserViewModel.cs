using System;
using System.Collections.Generic;
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

    [ObservableProperty]
    private string _versionWarning = string.Empty;

    [ObservableProperty]
    private bool _hasVersionMismatch;

    [ObservableProperty]
    private string _selectedSortBy = "Downloads";

    [ObservableProperty]
    private ObservableCollection<string> _availableSortOptions = new()
    {
        "Downloads", "Newest", "Updated", "Name A-Z", "Name Z-A"
    };

    [ObservableProperty]
    private string _selectedModLoader = "All";

    [ObservableProperty]
    private ObservableCollection<string> _availableModLoaders = new()
    {
        "All", "Forge", "Fabric", "NeoForge", "Quilt"
    };

    // Property to get the server's Minecraft version
    public string? ServerGameVersion => _mainViewModel?.SelectedProfile?.MinecraftVersion;

    // Property to check if a server is selected
    public bool HasServerSelected => _mainViewModel?.SelectedProfile != null;

    // Property to get server name
    public string? ServerName => _mainViewModel?.SelectedProfile?.Name;

    // Check if versions match
    public bool VersionsMatch => string.IsNullOrEmpty(ServerGameVersion) || ServerGameVersion == SelectedGameVersion;

    public ModBrowserViewModel(MainViewModel mainViewModel)
    {
        _mainViewModel = mainViewModel;
        _curseForge = new CurseForgeService();
        _modrinth = new ModrinthService();
        _downloadService = new DownloadService();

        // Auto-sync with server version if profile exists
        SyncWithServerVersion();
    }

    /// <summary>
    /// Sync the selected game version with the server profile's Minecraft version
    /// </summary>
    public void SyncWithServerVersion()
    {
        var serverVersion = ServerGameVersion;
        if (!string.IsNullOrEmpty(serverVersion) && AvailableVersions.Contains(serverVersion))
        {
            SelectedGameVersion = serverVersion;
        }
        UpdateVersionWarning();
    }

    public void RefreshGameVersionDisplay()
    {
        OnPropertyChanged(nameof(SelectedGameVersion));
        OnPropertyChanged(nameof(ServerGameVersion));
        OnPropertyChanged(nameof(HasServerSelected));
        OnPropertyChanged(nameof(ServerName));
        OnPropertyChanged(nameof(VersionsMatch));
        UpdateVersionWarning();
    }

    private void UpdateVersionWarning()
    {
        if (!HasServerSelected)
        {
            VersionWarning = "No server selected - please select a server profile first";
            HasVersionMismatch = true;
        }
        else if (!VersionsMatch)
        {
            VersionWarning = $"Warning: Server '{ServerName}' uses Minecraft {ServerGameVersion}, but you're browsing mods for {SelectedGameVersion}. Mods may not be compatible!";
            HasVersionMismatch = true;
        }
        else
        {
            VersionWarning = string.Empty;
            HasVersionMismatch = false;
        }
    }

    partial void OnSelectedGameVersionChanged(string value)
    {
        UpdateVersionWarning();
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
    private void SyncVersion()
    {
        SyncWithServerVersion();
        StatusMessage = $"Synced to server version: {SelectedGameVersion}";
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
            StatusMessage = $"Found {SearchResults.Count} mods for Minecraft {SelectedGameVersion}";
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
    public async Task LoadPopularAsync()
    {
        IsSearching = true;
        SearchResults.Clear();
        ClearMessages();

        try
        {
            var cfMods = await _curseForge.GetPopularModsAsync(SelectedGameVersion, 15) ?? new List<ModInfo>();
            var mrMods = await _modrinth.GetPopularModsAsync(SelectedGameVersion, 15) ?? new List<ModInfo>();

            var allMods = cfMods.Concat(mrMods)
                .Where(m => m != null && !string.IsNullOrEmpty(m.Name))
                .GroupBy(m => m.Name.ToLower().Replace(" ", ""))
                .Select(g => g.First());

            // Apply sorting
            allMods = ApplySorting(allMods).Take(30);

            foreach (var mod in allMods)
            {
                SearchResults.Add(mod);
            }

            if (SearchResults.Count > 0)
            {
                StatusMessage = $"Showing {SearchResults.Count} popular mods for Minecraft {SelectedGameVersion}";
            }
            else
            {
                StatusMessage = $"No mods found for Minecraft {SelectedGameVersion}. Try a different version.";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error loading popular mods: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"Error in LoadPopularAsync: {ex}");
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
                ErrorMessage = "Server path is not configured. Please set up your server folder in Server Manager first.";
                return;
            }

            // Check version compatibility
            var serverVersion = profile.MinecraftVersion;
            if (!string.IsNullOrEmpty(serverVersion) && serverVersion != SelectedGameVersion)
            {
                ErrorMessage = $"Cannot install mods: Server '{profile.Name}' uses Minecraft {serverVersion}, but selected mods are for {SelectedGameVersion}. Please sync the version or change the server profile.";
                return;
            }

            IsDownloading = true;
            DownloadProgress = 0;

            var modsList = SelectedMods.Where(m => m != null).ToList();
            var totalMods = modsList.Count;
            var completed = 0;
            var failed = 0;
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

                    if (file == null || string.IsNullOrEmpty(file.DownloadUrl))
                    {
                        DownloadStatus = $"No compatible version found for {modName} on Minecraft {gameVersion}";
                        failed++;
                        continue;
                    }

                    if (!string.IsNullOrEmpty(file.DownloadUrl) && !string.IsNullOrEmpty(file.FileName))
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
                            // Check if mod is already installed
                            var existingMod = profile.InstalledMods.FirstOrDefault(m => m.ModId == mod.Id && m.Source == mod.Source);
                            if (existingMod != null)
                            {
                                existingMod.FileName = file.FileName;
                                existingMod.Version = file.DisplayName ?? "";
                                existingMod.FilePath = System.IO.Path.Combine(modsFolder, file.FileName);
                                existingMod.InstalledDate = DateTime.Now;
                            }
                            else
                            {
                                profile.InstalledMods.Add(new InstalledMod
                                {
                                    ModId = mod.Id,
                                    Name = modName,
                                    FileName = file.FileName,
                                    Version = file.DisplayName ?? "",
                                    FilePath = System.IO.Path.Combine(modsFolder, file.FileName),
                                    Source = mod.Source,
                                    InstalledDate = DateTime.Now
                                });
                            }
                        }
                    }
                }
                catch (Exception modEx)
                {
                    System.Diagnostics.Debug.WriteLine($"Error installing {modName}: {modEx.Message}");
                    failed++;
                }

                completed++;
                DownloadProgress = (double)completed / totalMods * 100;
            }

            if (_mainViewModel != null)
            {
                await _mainViewModel.SaveProfileAsync(profile);
            }

            SelectedMods.Clear();

            if (failed > 0)
            {
                DownloadStatus = $"Installed {completed - failed} mods, {failed} failed";
                StatusMessage = $"Installed {completed - failed} mods to {profile.Name ?? "server"}, {failed} failed";
            }
            else
            {
                DownloadStatus = $"Successfully installed {completed} mods!";
                StatusMessage = $"Installed {completed} mods to {profile.Name ?? "server"}";
            }
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
        // Quick validation for quick install
        var profile = _mainViewModel?.SelectedProfile;
        if (profile == null)
        {
            ErrorMessage = "Please select a server profile first";
            return;
        }

        var serverVersion = profile.MinecraftVersion;
        if (!string.IsNullOrEmpty(serverVersion) && serverVersion != SelectedGameVersion)
        {
            ErrorMessage = $"Cannot install: Mod is for Minecraft {SelectedGameVersion}, but server uses {serverVersion}";
            return;
        }

        AddToSelected(mod);
        await InstallSelectedModsAsync();
    }

    private IEnumerable<ModInfo> ApplySorting(IEnumerable<ModInfo> mods)
    {
        return SelectedSortBy switch
        {
            "Downloads" => mods.OrderByDescending(m => m.DownloadCount),
            "Newest" => mods.OrderByDescending(m => m.DateCreated),
            "Updated" => mods.OrderByDescending(m => m.DateModified),
            "Name A-Z" => mods.OrderBy(m => m.Name),
            "Name Z-A" => mods.OrderByDescending(m => m.Name),
            _ => mods.OrderByDescending(m => m.DownloadCount)
        };
    }

    partial void OnSelectedSortByChanged(string value)
    {
        // Re-run search or load with new sort order
        if (!string.IsNullOrEmpty(SearchQuery))
        {
            Task.Run(() => SearchAsync());
        }
        else if (SearchResults.Count > 0)
        {
            Task.Run(() => LoadPopularAsync());
        }
    }

    partial void OnSelectedModLoaderChanged(string value)
    {
        // Re-run search or load with new filter
        if (!string.IsNullOrEmpty(SearchQuery))
        {
            Task.Run(() => SearchAsync());
        }
        else if (SearchResults.Count > 0)
        {
            Task.Run(() => LoadPopularAsync());
        }
    }
}
