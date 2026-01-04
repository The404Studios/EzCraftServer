using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EzCraftModManager.Models;
using EzCraftModManager.Services;

namespace EzCraftModManager.ViewModels;

public partial class ServerManagerViewModel : ViewModelBase
{
    private readonly MainViewModel _mainViewModel;
    private readonly ForgeService _forgeService;
    private readonly ProfileService _profileService;
    private readonly DownloadService _downloadService;
    private Process? _serverProcess;

    [ObservableProperty]
    private ServerProfile? _selectedProfile;

    [ObservableProperty]
    private ObservableCollection<MinecraftVersion> _minecraftVersions = new();

    [ObservableProperty]
    private ObservableCollection<ForgeVersionInfo> _forgeVersions = new();

    [ObservableProperty]
    private MinecraftVersion? _selectedMinecraftVersion;

    [ObservableProperty]
    private ForgeVersionInfo? _selectedForgeVersion;

    [ObservableProperty]
    private ObservableCollection<InstalledMod> _installedMods = new();

    [ObservableProperty]
    private bool _isInstalling;

    [ObservableProperty]
    private double _installProgress;

    [ObservableProperty]
    private string _installStatus = string.Empty;

    [ObservableProperty]
    private bool _isServerRunning;

    [ObservableProperty]
    private string _serverOutput = string.Empty;

    // New profile form
    [ObservableProperty]
    private string _newProfileName = "My Minecraft Server";

    [ObservableProperty]
    private string _newProfilePath = string.Empty;

    [ObservableProperty]
    private int _newProfileMaxPlayers = 20;

    [ObservableProperty]
    private int _newProfileRam = 4;

    [ObservableProperty]
    private string _newProfileGameMode = "survival";

    [ObservableProperty]
    private string _newProfileDifficulty = "normal";

    [ObservableProperty]
    private bool _newProfilePvP = true;

    [ObservableProperty]
    private string _newProfileOperator = string.Empty;

    public ObservableCollection<string> GameModes { get; } = new() { "survival", "creative", "adventure", "spectator" };
    public ObservableCollection<string> Difficulties { get; } = new() { "peaceful", "easy", "normal", "hard" };

    public ServerManagerViewModel(MainViewModel mainViewModel)
    {
        _mainViewModel = mainViewModel;
        _forgeService = new ForgeService();
        _profileService = new ProfileService();
        _downloadService = new DownloadService();

        // Set default path
        NewProfilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "MinecraftServer");
    }

    public async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            // Load Minecraft versions
            var versions = await _forgeService.GetMinecraftVersionsAsync();
            MinecraftVersions.Clear();
            foreach (var v in versions.Where(v => v.HasForge).Take(30))
            {
                MinecraftVersions.Add(v);
            }

            if (MinecraftVersions.Count > 0)
            {
                SelectedMinecraftVersion = MinecraftVersions.FirstOrDefault(v => v.Id == "1.20.1")
                    ?? MinecraftVersions[0];
            }

            StatusMessage = $"Loaded {MinecraftVersions.Count} Minecraft versions with Forge support";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error loading versions: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    partial void OnSelectedMinecraftVersionChanged(MinecraftVersion? value)
    {
        if (value != null)
        {
            Task.Run(async () => await LoadForgeVersionsAsync(value.Id));
        }
    }

    private async Task LoadForgeVersionsAsync(string minecraftVersion)
    {
        try
        {
            var versions = await _forgeService.GetForgeVersionsForMinecraftAsync(minecraftVersion);

            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                ForgeVersions.Clear();
                foreach (var v in versions)
                {
                    ForgeVersions.Add(v);
                }

                if (ForgeVersions.Count > 0)
                {
                    SelectedForgeVersion = ForgeVersions.FirstOrDefault(v => v.IsRecommended)
                        ?? ForgeVersions[0];
                }
            });
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error loading Forge versions: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task CreateServerAsync()
    {
        if (string.IsNullOrWhiteSpace(NewProfileName))
        {
            ErrorMessage = "Please enter a server name";
            return;
        }

        if (string.IsNullOrWhiteSpace(NewProfilePath))
        {
            ErrorMessage = "Please select an installation path";
            return;
        }

        if (SelectedMinecraftVersion == null || SelectedForgeVersion == null)
        {
            ErrorMessage = "Please select Minecraft and Forge versions";
            return;
        }

        IsInstalling = true;
        InstallProgress = 0;

        try
        {
            var profile = new ServerProfile
            {
                Name = NewProfileName,
                ServerPath = Path.Combine(NewProfilePath, NewProfileName.Replace(" ", "_")),
                MinecraftVersion = SelectedMinecraftVersion.Id,
                ForgeVersion = SelectedForgeVersion.ForgeVersion,
                MaxPlayers = NewProfileMaxPlayers,
                RamGB = NewProfileRam,
                GameMode = NewProfileGameMode,
                Difficulty = NewProfileDifficulty,
                EnablePvP = NewProfilePvP,
                OperatorUsername = NewProfileOperator,
                Motd = $"Welcome to {NewProfileName}!"
            };

            // Step 1: Create directories
            InstallStatus = "Creating server directories...";
            InstallProgress = 10;
            await _profileService.CreateServerFilesAsync(profile);

            // Step 2: Download Forge installer
            InstallStatus = "Downloading Forge installer...";
            var progress = new Progress<DownloadProgress>(p =>
            {
                InstallProgress = 10 + (p.ProgressPercentage * 0.4);
                InstallStatus = $"Downloading Forge... {p.ProgressPercentage:F0}%";
            });

            var installerPath = await _forgeService.DownloadForgeInstallerAsync(
                SelectedForgeVersion, profile.ServerPath, progress);

            // Step 3: Install Forge
            InstallStatus = "Installing Forge server (this may take several minutes)...";
            InstallProgress = 50;

            var installProgress = new Progress<InstallProgress>(p =>
            {
                InstallProgress = 50 + (p.OverallProgress * 0.4);
                InstallStatus = p.DetailMessage;
            });

            var success = await _forgeService.InstallForgeServerAsync(installerPath, profile.ServerPath, installProgress);

            if (!success)
            {
                ErrorMessage = "Forge installation failed. Please check Java installation.";
                return;
            }

            // Step 4: Create startup scripts
            InstallStatus = "Creating startup scripts...";
            InstallProgress = 90;

            var windowsScript = _forgeService.CreateStartupScript(profile, true);
            var linuxScript = _forgeService.CreateStartupScript(profile, false);

            await File.WriteAllTextAsync(Path.Combine(profile.ServerPath, "start_server.bat"), windowsScript);
            await File.WriteAllTextAsync(Path.Combine(profile.ServerPath, "start_server.sh"), linuxScript);

            // Step 5: Save profile
            profile.IsForgeInstalled = true;
            await _mainViewModel.SaveProfileAsync(profile);

            InstallProgress = 100;
            InstallStatus = "Server created successfully!";
            StatusMessage = $"Server '{profile.Name}' created at {profile.ServerPath}";

            // Select the new profile
            _mainViewModel.SelectedProfile = profile;
            SelectedProfile = profile;

            // Refresh installed mods
            await RefreshInstalledModsAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error creating server: {ex.Message}";
        }
        finally
        {
            IsInstalling = false;
        }
    }

    [RelayCommand]
    private void SelectProfile(ServerProfile profile)
    {
        SelectedProfile = profile;
        _mainViewModel.SelectedProfile = profile;
        Task.Run(RefreshInstalledModsAsync);
    }

    [RelayCommand]
    private async Task RefreshInstalledModsAsync()
    {
        if (SelectedProfile == null) return;

        try
        {
            var mods = await _profileService.ScanInstalledModsAsync(SelectedProfile.ModsPath);
            InstalledMods.Clear();
            foreach (var mod in mods)
            {
                InstalledMods.Add(mod);
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error scanning mods: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task ToggleModAsync(InstalledMod mod)
    {
        try
        {
            await _profileService.ToggleModAsync(mod);
            StatusMessage = mod.IsEnabled ? $"Enabled {mod.Name}" : $"Disabled {mod.Name}";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error toggling mod: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task DeleteModAsync(InstalledMod mod)
    {
        try
        {
            await _profileService.DeleteModAsync(mod);
            InstalledMods.Remove(mod);
            StatusMessage = $"Deleted {mod.Name}";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error deleting mod: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task StartServerAsync(ServerProfile? profile)
    {
        var targetProfile = profile ?? SelectedProfile;
        if (targetProfile == null) return;

        try
        {
            var startScript = Path.Combine(targetProfile.ServerPath, "start_server.bat");
            if (!File.Exists(startScript))
            {
                ErrorMessage = "Start script not found. Please reinstall the server.";
                return;
            }

            var processInfo = new ProcessStartInfo
            {
                FileName = startScript,
                WorkingDirectory = targetProfile.ServerPath,
                UseShellExecute = true
            };

            _serverProcess = Process.Start(processInfo);
            IsServerRunning = true;
            targetProfile.Status = ServerStatus.Running;
            targetProfile.LastPlayed = DateTime.Now;
            await _mainViewModel.SaveProfileAsync(targetProfile);

            StatusMessage = $"Server '{targetProfile.Name}' started";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error starting server: {ex.Message}";
        }
    }

    [RelayCommand]
    private void StopServer()
    {
        if (_serverProcess != null && !_serverProcess.HasExited)
        {
            _serverProcess.Kill();
            _serverProcess = null;
        }

        IsServerRunning = false;
        if (SelectedProfile != null)
        {
            SelectedProfile.Status = ServerStatus.Stopped;
        }

        StatusMessage = "Server stopped";
    }

    [RelayCommand]
    private void OpenServerFolder(ServerProfile? profile)
    {
        var targetProfile = profile ?? SelectedProfile;
        if (targetProfile == null) return;

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = targetProfile.ServerPath,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error opening folder: {ex.Message}";
        }
    }

    [RelayCommand]
    private void OpenModsFolder()
    {
        if (SelectedProfile == null) return;

        try
        {
            Directory.CreateDirectory(SelectedProfile.ModsPath);
            Process.Start(new ProcessStartInfo
            {
                FileName = SelectedProfile.ModsPath,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error opening mods folder: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task DeleteServerAsync(ServerProfile profile)
    {
        try
        {
            // Delete profile from database
            await _mainViewModel.DeleteProfileAsync(profile);

            if (SelectedProfile?.Id == profile.Id)
            {
                SelectedProfile = null;
                InstalledMods.Clear();
            }

            StatusMessage = $"Deleted server profile '{profile.Name}'";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error deleting server: {ex.Message}";
        }
    }

    [RelayCommand]
    private void BrowseForPath()
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Select Server Location",
            FileName = "MinecraftServer",
            Filter = "Folder|*.folder"
        };

        if (dialog.ShowDialog() == true)
        {
            NewProfilePath = Path.GetDirectoryName(dialog.FileName) ?? NewProfilePath;
        }
    }
}
