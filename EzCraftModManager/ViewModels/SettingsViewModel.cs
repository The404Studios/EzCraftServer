using System;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EzCraftModManager.Models;
using EzCraftModManager.Services;

namespace EzCraftModManager.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly MainViewModel _mainViewModel;
    private readonly ForgeService _forgeService;

    [ObservableProperty]
    private string _defaultInstallPath;

    [ObservableProperty]
    private string _javaPath = "java";

    [ObservableProperty]
    private int _maxConcurrentDownloads = 5;

    [ObservableProperty]
    private bool _autoCheckUpdates = true;

    [ObservableProperty]
    private bool _downloadDependencies = true;

    [ObservableProperty]
    private JavaInfo? _javaInfo;

    [ObservableProperty]
    private string _cacheSize = "Calculating...";

    [ObservableProperty]
    private string _appVersion = "2.0.0";

    public SettingsViewModel(MainViewModel mainViewModel)
    {
        _mainViewModel = mainViewModel;
        _forgeService = new ForgeService();
        _defaultInstallPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "MinecraftServers");
    }

    public async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            // Check Java
            JavaInfo = await _forgeService.CheckJavaInstallationAsync();

            // Calculate cache size
            var cachePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EzCraftModManager", "cache");
            if (Directory.Exists(cachePath))
            {
                var size = GetDirectorySize(cachePath);
                CacheSize = FormatBytes(size);
            }
            else
            {
                CacheSize = "0 B";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error loading settings: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task CheckJavaAsync()
    {
        IsLoading = true;
        try
        {
            JavaInfo = await _forgeService.CheckJavaInstallationAsync();

            if (JavaInfo.IsInstalled)
            {
                StatusMessage = $"Java {JavaInfo.Version} detected (Compatible: {(JavaInfo.IsCompatible ? "Yes" : "No")})";
            }
            else
            {
                ErrorMessage = "Java not found. Please install Java 17 or newer.";
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task ClearCacheAsync()
    {
        try
        {
            var cachePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EzCraftModManager", "cache");
            if (Directory.Exists(cachePath))
            {
                Directory.Delete(cachePath, true);
                Directory.CreateDirectory(cachePath);
            }
            CacheSize = "0 B";
            StatusMessage = "Cache cleared successfully";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error clearing cache: {ex.Message}";
        }
    }

    [RelayCommand]
    private void BrowseJavaPath()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Select Java Executable",
            Filter = "Java|java.exe;javaw.exe|All Files|*.*",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)
        };

        if (dialog.ShowDialog() == true)
        {
            JavaPath = dialog.FileName;
        }
    }

    [RelayCommand]
    private void BrowseDefaultPath()
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Select Default Installation Folder",
            FileName = "MinecraftServers",
            Filter = "Folder|*.folder"
        };

        if (dialog.ShowDialog() == true)
        {
            DefaultInstallPath = Path.GetDirectoryName(dialog.FileName) ?? DefaultInstallPath;
        }
    }

    [RelayCommand]
    private void OpenAppDataFolder()
    {
        var appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EzCraftModManager");
        Directory.CreateDirectory(appDataPath);
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = appDataPath,
            UseShellExecute = true
        });
    }

    [RelayCommand]
    private void OpenGitHub()
    {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "https://github.com/The404Studios/EzCraftServer",
            UseShellExecute = true
        });
    }

    [RelayCommand]
    private void OpenDiscord()
    {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "https://discord.gg/CrtPtMwcDA",
            UseShellExecute = true
        });
    }

    private long GetDirectorySize(string path)
    {
        long size = 0;
        try
        {
            foreach (var file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
            {
                size += new FileInfo(file).Length;
            }
        }
        catch { }
        return size;
    }

    private string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        int order = 0;
        double size = bytes;
        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }
        return $"{size:0.##} {sizes[order]}";
    }
}
