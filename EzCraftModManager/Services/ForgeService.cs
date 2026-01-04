using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using EzCraftModManager.Models;
using Newtonsoft.Json.Linq;

namespace EzCraftModManager.Services;

public class ForgeService
{
    private readonly HttpClient _httpClient;
    private const string ForgeFilesUrl = "https://files.minecraftforge.net/net/minecraftforge/forge";
    private const string ForgePromosUrl = "https://files.minecraftforge.net/net/minecraftforge/forge/promotions_slim.json";
    private const string MavenBaseUrl = "https://maven.minecraftforge.net/net/minecraftforge/forge";
    private const string MinecraftVersionManifestUrl = "https://piston-meta.mojang.com/mc/game/version_manifest_v2.json";

    private Dictionary<string, List<ForgeVersionInfo>>? _cachedForgeVersions;
    private List<MinecraftVersion>? _cachedMinecraftVersions;

    public ForgeService()
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "EzCraftModManager/2.0");
        _httpClient.Timeout = TimeSpan.FromMinutes(5);
    }

    public async Task<List<MinecraftVersion>> GetMinecraftVersionsAsync(bool includeSnapshots = false)
    {
        if (_cachedMinecraftVersions != null) return _cachedMinecraftVersions;

        try
        {
            var response = await _httpClient.GetAsync(MinecraftVersionManifestUrl);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var manifest = JObject.Parse(content);
            var versionsArray = manifest["versions"] as JArray;

            if (versionsArray == null) return new List<MinecraftVersion>();

            var versions = new List<MinecraftVersion>();
            foreach (var v in versionsArray)
            {
                var type = v["type"]?.ToString() ?? "";
                if (!includeSnapshots && type != "release") continue;

                var version = new MinecraftVersion
                {
                    Id = v["id"]?.ToString() ?? "",
                    Type = type,
                    Url = v["url"]?.ToString() ?? ""
                };

                if (DateTime.TryParse(v["releaseTime"]?.ToString(), out var releaseTime))
                    version.ReleaseTime = releaseTime;

                versions.Add(version);
            }

            // Get Forge versions for each Minecraft version
            var forgePromos = await GetForgePromotionsAsync();
            foreach (var version in versions)
            {
                version.HasForge = forgePromos.ContainsKey($"{version.Id}-recommended") ||
                                   forgePromos.ContainsKey($"{version.Id}-latest");
            }

            _cachedMinecraftVersions = versions;
            return versions;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error getting Minecraft versions: {ex.Message}");
            return new List<MinecraftVersion>();
        }
    }

    public async Task<Dictionary<string, string>> GetForgePromotionsAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync(ForgePromosUrl);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var json = JObject.Parse(content);
            var promos = json["promos"] as JObject;

            return promos?.ToObject<Dictionary<string, string>>() ?? new Dictionary<string, string>();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error getting Forge promotions: {ex.Message}");
            return new Dictionary<string, string>();
        }
    }

    public async Task<List<ForgeVersionInfo>> GetForgeVersionsForMinecraftAsync(string minecraftVersion)
    {
        try
        {
            var promos = await GetForgePromotionsAsync();
            var versions = new List<ForgeVersionInfo>();

            // Get recommended version
            if (promos.TryGetValue($"{minecraftVersion}-recommended", out var recommended))
            {
                versions.Add(new ForgeVersionInfo
                {
                    MinecraftVersion = minecraftVersion,
                    ForgeVersion = recommended,
                    IsRecommended = true,
                    DownloadUrl = GetForgeInstallerUrl(minecraftVersion, recommended)
                });
            }

            // Get latest version
            if (promos.TryGetValue($"{minecraftVersion}-latest", out var latest) && latest != recommended)
            {
                versions.Add(new ForgeVersionInfo
                {
                    MinecraftVersion = minecraftVersion,
                    ForgeVersion = latest,
                    IsLatest = true,
                    DownloadUrl = GetForgeInstallerUrl(minecraftVersion, latest)
                });
            }

            // Try to get more versions from the files page
            var allVersions = await GetAllForgeVersionsAsync(minecraftVersion);
            foreach (var v in allVersions)
            {
                if (!versions.Any(existing => existing.ForgeVersion == v.ForgeVersion))
                {
                    versions.Add(v);
                }
            }

            return versions.OrderByDescending(v => v.IsRecommended)
                          .ThenByDescending(v => v.IsLatest)
                          .ThenByDescending(v => Version.TryParse(v.ForgeVersion.Split('-').Last(), out var ver) ? ver : new Version())
                          .ToList();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error getting Forge versions: {ex.Message}");
            return new List<ForgeVersionInfo>();
        }
    }

    private async Task<List<ForgeVersionInfo>> GetAllForgeVersionsAsync(string minecraftVersion)
    {
        try
        {
            // Try to get from Maven metadata
            var metadataUrl = $"{MavenBaseUrl}/maven-metadata.xml";
            var response = await _httpClient.GetAsync(metadataUrl);

            if (!response.IsSuccessStatusCode) return new List<ForgeVersionInfo>();

            var content = await response.Content.ReadAsStringAsync();
            var versions = new List<ForgeVersionInfo>();

            // Parse XML for versions matching this Minecraft version
            var regex = new Regex($@"<version>({Regex.Escape(minecraftVersion)}-[\d.]+)</version>");
            var matches = regex.Matches(content);

            foreach (Match match in matches)
            {
                var fullVersion = match.Groups[1].Value;
                var forgePart = fullVersion.Replace($"{minecraftVersion}-", "");

                versions.Add(new ForgeVersionInfo
                {
                    MinecraftVersion = minecraftVersion,
                    ForgeVersion = forgePart,
                    DownloadUrl = GetForgeInstallerUrl(minecraftVersion, forgePart)
                });
            }

            return versions.Take(20).ToList(); // Limit to most recent 20
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error getting all Forge versions: {ex.Message}");
            return new List<ForgeVersionInfo>();
        }
    }

    private string GetForgeInstallerUrl(string mcVersion, string forgeVersion)
    {
        var fullVersion = $"{mcVersion}-{forgeVersion}";
        return $"{MavenBaseUrl}/{fullVersion}/forge-{fullVersion}-installer.jar";
    }

    public async Task<string> DownloadForgeInstallerAsync(ForgeVersionInfo forgeVersion, string destinationFolder,
        IProgress<DownloadProgress>? progress = null)
    {
        var installerPath = Path.Combine(destinationFolder, $"forge-{forgeVersion.FullVersion}-installer.jar");

        try
        {
            Directory.CreateDirectory(destinationFolder);

            using var response = await _httpClient.GetAsync(forgeVersion.DownloadUrl, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1;
            var downloadProgress = new DownloadProgress
            {
                FileName = Path.GetFileName(installerPath),
                TotalBytes = totalBytes,
                State = DownloadState.Downloading
            };

            using var contentStream = await response.Content.ReadAsStreamAsync();
            using var fileStream = new FileStream(installerPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

            var buffer = new byte[8192];
            long totalBytesRead = 0;
            int bytesRead;
            var lastProgressUpdate = DateTime.UtcNow;

            while ((bytesRead = await contentStream.ReadAsync(buffer)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));
                totalBytesRead += bytesRead;

                if (progress != null && (DateTime.UtcNow - lastProgressUpdate).TotalMilliseconds > 100)
                {
                    downloadProgress.BytesReceived = totalBytesRead;
                    downloadProgress.Status = $"Downloading Forge installer... {downloadProgress.ProgressPercentage:F1}%";
                    progress.Report(downloadProgress);
                    lastProgressUpdate = DateTime.UtcNow;
                }
            }

            downloadProgress.State = DownloadState.Completed;
            downloadProgress.BytesReceived = totalBytesRead;
            progress?.Report(downloadProgress);

            return installerPath;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error downloading Forge installer: {ex.Message}");
            throw;
        }
    }

    public async Task<bool> InstallForgeServerAsync(string installerPath, string serverFolder,
        IProgress<InstallProgress>? progress = null)
    {
        try
        {
            Directory.CreateDirectory(serverFolder);

            var installProgress = new InstallProgress
            {
                CurrentStep = "Installing Forge Server",
                TotalSteps = 3,
                CurrentStepIndex = 1
            };
            progress?.Report(installProgress);

            // Run the Forge installer
            var processInfo = new ProcessStartInfo
            {
                FileName = "java",
                Arguments = $"-jar \"{installerPath}\" --installServer \"{serverFolder}\"",
                WorkingDirectory = serverFolder,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            installProgress.DetailMessage = "Running Forge installer (this may take a few minutes)...";
            progress?.Report(installProgress);

            using var process = Process.Start(processInfo);
            if (process == null)
            {
                throw new Exception("Failed to start Java process");
            }

            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                Debug.WriteLine($"Forge install error: {error}");
                installProgress.HasError = true;
                installProgress.ErrorMessage = $"Forge installation failed: {error}";
                progress?.Report(installProgress);
                return false;
            }

            installProgress.CurrentStepIndex = 2;
            installProgress.DetailMessage = "Creating server configuration...";
            progress?.Report(installProgress);

            // Create EULA
            var eulaPath = Path.Combine(serverFolder, "eula.txt");
            await File.WriteAllTextAsync(eulaPath, "eula=true");

            installProgress.CurrentStepIndex = 3;
            installProgress.IsComplete = true;
            installProgress.DetailMessage = "Forge installation complete!";
            progress?.Report(installProgress);

            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error installing Forge: {ex.Message}");
            var errorProgress = new InstallProgress
            {
                HasError = true,
                ErrorMessage = ex.Message
            };
            progress?.Report(errorProgress);
            return false;
        }
    }

    public async Task<JavaInfo> CheckJavaInstallationAsync()
    {
        var javaInfo = new JavaInfo();

        try
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = "java",
                Arguments = "-version",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(processInfo);
            if (process == null) return javaInfo;

            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode == 0)
            {
                javaInfo.IsInstalled = true;

                // Parse version from output like: java version "21.0.1" or openjdk version "21.0.1"
                var match = Regex.Match(error, @"version ""(\d+)(?:\.(\d+))?");
                if (match.Success)
                {
                    javaInfo.MajorVersion = int.Parse(match.Groups[1].Value);
                    javaInfo.Version = match.Groups[0].Value.Replace("version ", "").Trim('"');
                    javaInfo.IsCompatible = javaInfo.MajorVersion >= 17;
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error checking Java: {ex.Message}");
        }

        return javaInfo;
    }

    public string CreateStartupScript(ServerProfile profile, bool isWindows = true)
    {
        var jvmArgs = GetOptimizedJvmArgs(profile.RamGB, profile.MaxPlayers);
        var jarFile = FindServerJar(profile.ServerPath);

        if (isWindows)
        {
            return $@"@echo off
title {profile.Name}
cd /d ""%~dp0""
echo Starting {profile.Name}...
echo.
""{profile.JavaPath}"" {jvmArgs} -jar ""{jarFile}"" nogui
echo.
echo Server stopped. Press any key to exit...
pause > nul
";
        }
        else
        {
            return $@"#!/bin/bash
cd ""$(dirname ""$0"")""
echo ""Starting {profile.Name}...""
{profile.JavaPath} {jvmArgs} -jar ""{jarFile}"" nogui
echo ""Server stopped.""
";
        }
    }

    private string GetOptimizedJvmArgs(int ramGb, int maxPlayers)
    {
        var args = new List<string>
        {
            $"-Xms{ramGb}G",
            $"-Xmx{ramGb}G",
            "-XX:+UseG1GC",
            "-XX:+ParallelRefProcEnabled",
            "-XX:MaxGCPauseMillis=200",
            "-XX:+UnlockExperimentalVMOptions",
            "-XX:+DisableExplicitGC",
            "-XX:+AlwaysPreTouch",
            "-XX:G1NewSizePercent=30",
            "-XX:G1MaxNewSizePercent=40",
            "-XX:G1HeapRegionSize=8M",
            "-XX:G1ReservePercent=20",
            "-XX:G1HeapWastePercent=5",
            "-XX:G1MixedGCCountTarget=4",
            "-XX:InitiatingHeapOccupancyPercent=15",
            "-XX:G1MixedGCLiveThresholdPercent=90",
            "-XX:G1RSetUpdatingPauseTimePercent=5",
            "-XX:SurvivorRatio=32",
            "-XX:+PerfDisableSharedMem",
            "-XX:MaxTenuringThreshold=1",
            "-Dusing.aikars.flags=https://mcflags.emc.gs",
            "-Daikars.new.flags=true"
        };

        return string.Join(" ", args);
    }

    private string FindServerJar(string serverPath)
    {
        // Look for Forge server jar
        var forgeJars = Directory.GetFiles(serverPath, "forge-*-server.jar")
            .Concat(Directory.GetFiles(serverPath, "forge-*.jar"))
            .Where(f => !f.Contains("installer"))
            .ToList();

        if (forgeJars.Any())
        {
            return Path.GetFileName(forgeJars.First());
        }

        // Look for run script that specifies the jar
        var runScripts = Directory.GetFiles(serverPath, "run.*");
        if (runScripts.Any())
        {
            var content = File.ReadAllText(runScripts.First());
            var match = Regex.Match(content, @"-jar\s+""?([^""\s]+\.jar)");
            if (match.Success)
            {
                return match.Groups[1].Value;
            }
        }

        return "server.jar";
    }
}
