using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using EzCraftModManager.Models;
using Newtonsoft.Json.Linq;

namespace EzCraftModManager.Services;

public class CurseForgeService
{
    private readonly HttpClient _httpClient;
    private const string ApiBaseUrl = "https://api.curseforge.com/v1";
    private const string ApiKey = "$2a$10$6Y15zxWaENIAeUySYwtK0uM5STstUVkvvqmwZl1xIBxdWxVd3YJ4W";
    private const int MinecraftGameId = 432;
    private const int ModsClassId = 6;
    private const int MaxRetryAttempts = 3;

    // Cache for mod info and files to reduce API calls
    private static readonly ConcurrentDictionary<long, CachedItem<ModInfo>> _modCache = new();
    private static readonly ConcurrentDictionary<string, CachedItem<List<ModFile>>> _filesCache = new();
    private static readonly ConcurrentDictionary<string, CachedItem<List<ModInfo>>> _searchCache = new();
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(15);

    public CurseForgeService()
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        _httpClient.DefaultRequestHeaders.Add("x-api-key", ApiKey);
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "EzCraftModManager/2.0");
        _httpClient.Timeout = TimeSpan.FromMinutes(2);
    }

    public async Task<List<ModInfo>> SearchModsAsync(string query, string? gameVersion = null, int pageSize = 20, int page = 0)
    {
        var cacheKey = $"search:{query}:{gameVersion}:{pageSize}:{page}";

        if (_searchCache.TryGetValue(cacheKey, out var cached) && !cached.IsExpired)
        {
            return cached.Value ?? new List<ModInfo>();
        }

        try
        {
            var url = $"{ApiBaseUrl}/mods/search?gameId={MinecraftGameId}&classId={ModsClassId}&searchFilter={Uri.EscapeDataString(query)}&pageSize={pageSize}&index={page * pageSize}&sortField=2&sortOrder=desc";

            if (!string.IsNullOrEmpty(gameVersion))
            {
                url += $"&gameVersion={Uri.EscapeDataString(gameVersion)}";
            }

            url += "&modLoaderType=1"; // Forge

            var content = await GetWithRetryAsync(url);
            if (content == null) return new List<ModInfo>();

            var result = JObject.Parse(content);
            var modsArray = result["data"] as JArray;

            if (modsArray == null) return new List<ModInfo>();

            var mods = new List<ModInfo>();
            foreach (var modJson in modsArray)
            {
                var mod = ParseModFromJson(modJson, ModSource.CurseForge);
                if (mod != null)
                {
                    mods.Add(mod);
                    // Also cache individual mods
                    _modCache[mod.Id] = new CachedItem<ModInfo>(mod);
                }
            }

            _searchCache[cacheKey] = new CachedItem<List<ModInfo>>(mods);
            return mods;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"CurseForge search error: {ex.Message}");
            return new List<ModInfo>();
        }
    }

    public async Task<ModInfo?> GetModAsync(long modId)
    {
        if (modId <= 0) return null;

        // Check cache first
        if (_modCache.TryGetValue(modId, out var cached) && !cached.IsExpired)
        {
            return cached.Value;
        }

        try
        {
            var content = await GetWithRetryAsync($"{ApiBaseUrl}/mods/{modId}");
            if (content == null) return null;

            var result = JObject.Parse(content);
            var modJson = result["data"];

            var mod = modJson != null ? ParseModFromJson(modJson, ModSource.CurseForge) : null;

            if (mod != null)
            {
                _modCache[modId] = new CachedItem<ModInfo>(mod);
            }

            return mod;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"CurseForge get mod error: {ex.Message}");
            return null;
        }
    }

    public async Task<List<ModFile>> GetModFilesAsync(long modId, string? gameVersion = null)
    {
        if (modId <= 0) return new List<ModFile>();

        var cacheKey = $"files:{modId}:{gameVersion ?? "all"}";

        if (_filesCache.TryGetValue(cacheKey, out var cached) && !cached.IsExpired)
        {
            return cached.Value ?? new List<ModFile>();
        }

        try
        {
            var url = $"{ApiBaseUrl}/mods/{modId}/files?pageSize=50";
            if (!string.IsNullOrEmpty(gameVersion))
            {
                url += $"&gameVersion={Uri.EscapeDataString(gameVersion)}";
            }
            url += "&modLoaderType=1"; // Forge

            var content = await GetWithRetryAsync(url);
            if (content == null) return new List<ModFile>();

            var result = JObject.Parse(content);
            var filesArray = result["data"] as JArray;

            if (filesArray == null) return new List<ModFile>();

            var files = new List<ModFile>();
            foreach (var fileJson in filesArray)
            {
                var file = ParseFileFromJson(fileJson);
                if (file != null) files.Add(file);
            }

            var sortedFiles = files.OrderByDescending(f => f.FileDate).ToList();
            _filesCache[cacheKey] = new CachedItem<List<ModFile>>(sortedFiles);

            return sortedFiles;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"CurseForge get files error: {ex.Message}");
            return new List<ModFile>();
        }
    }

    public async Task<ModFile?> GetCompatibleFileAsync(long modId, string gameVersion)
    {
        if (modId <= 0 || string.IsNullOrEmpty(gameVersion))
            return null;

        var files = await GetModFilesAsync(modId, gameVersion);

        if (files.Count == 0)
        {
            // Try without version filter
            files = await GetModFilesAsync(modId, null);
        }

        if (files.Count == 0) return null;

        // Priority 1: Exact version match with Forge
        var exactMatch = files.FirstOrDefault(f =>
            f.GameVersions.Contains(gameVersion) &&
            f.ModLoaders.Any(ml => ml.Equals("Forge", StringComparison.OrdinalIgnoreCase) ||
                                   ml.Equals("NeoForge", StringComparison.OrdinalIgnoreCase)));

        if (exactMatch != null) return exactMatch;

        // Priority 2: Minor version match (e.g., 1.20.x for 1.20.1)
        var versionParts = gameVersion.Split('.');
        if (versionParts.Length >= 2)
        {
            var minorVersion = $"{versionParts[0]}.{versionParts[1]}";
            var minorMatch = files.FirstOrDefault(f =>
                f.GameVersions.Any(v => v.StartsWith(minorVersion)) &&
                f.ModLoaders.Any(ml => ml.Equals("Forge", StringComparison.OrdinalIgnoreCase) ||
                                       ml.Equals("NeoForge", StringComparison.OrdinalIgnoreCase)));

            if (minorMatch != null) return minorMatch;
        }

        // Priority 3: Any Forge-compatible file
        return files.FirstOrDefault(f =>
            f.ModLoaders.Any(ml => ml.Equals("Forge", StringComparison.OrdinalIgnoreCase) ||
                                   ml.Equals("NeoForge", StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>
    /// Checks if a mod is compatible with a specific game version
    /// </summary>
    public async Task<bool> IsModCompatibleAsync(long modId, string gameVersion)
    {
        var file = await GetCompatibleFileAsync(modId, gameVersion);
        return file != null && !string.IsNullOrEmpty(file.DownloadUrl);
    }

    /// <summary>
    /// Gets the compatibility status of a mod for a specific version
    /// </summary>
    public async Task<ModCompatibilityResult> CheckCompatibilityAsync(long modId, string gameVersion)
    {
        var mod = await GetModAsync(modId);
        if (mod == null)
        {
            return new ModCompatibilityResult
            {
                IsCompatible = false,
                Status = CompatibilityStatus.NotFound,
                Message = "Mod not found on CurseForge"
            };
        }

        var file = await GetCompatibleFileAsync(modId, gameVersion);
        if (file == null)
        {
            return new ModCompatibilityResult
            {
                IsCompatible = false,
                Status = CompatibilityStatus.NoCompatibleVersion,
                Message = $"No version available for Minecraft {gameVersion}",
                AvailableVersions = mod.GameVersions
            };
        }

        if (string.IsNullOrEmpty(file.DownloadUrl))
        {
            return new ModCompatibilityResult
            {
                IsCompatible = false,
                Status = CompatibilityStatus.DownloadRestricted,
                Message = "Download is restricted by the mod author"
            };
        }

        return new ModCompatibilityResult
        {
            IsCompatible = true,
            Status = CompatibilityStatus.Compatible,
            Message = $"Compatible ({file.DisplayName})",
            CompatibleFile = file
        };
    }

    public async Task<string?> GetDownloadUrlAsync(long modId, long fileId)
    {
        try
        {
            var content = await GetWithRetryAsync($"{ApiBaseUrl}/mods/{modId}/files/{fileId}/download-url");
            if (content == null) return null;

            var result = JObject.Parse(content);
            return result["data"]?.ToString();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"CurseForge get download URL error: {ex.Message}");

            // Fallback: construct URL manually
            return $"https://edge.forgecdn.net/files/{fileId / 1000}/{fileId % 1000}/";
        }
    }

    public async Task<List<ModInfo>> GetPopularModsAsync(string? gameVersion = null, int count = 20)
    {
        var cacheKey = $"popular:{gameVersion}:{count}";

        if (_searchCache.TryGetValue(cacheKey, out var cached) && !cached.IsExpired)
        {
            return cached.Value ?? new List<ModInfo>();
        }

        try
        {
            var url = $"{ApiBaseUrl}/mods/search?gameId={MinecraftGameId}&classId={ModsClassId}&pageSize={count}&sortField=2&sortOrder=desc&modLoaderType=1";

            if (!string.IsNullOrEmpty(gameVersion))
            {
                url += $"&gameVersion={Uri.EscapeDataString(gameVersion)}";
            }

            var content = await GetWithRetryAsync(url);
            if (content == null) return new List<ModInfo>();

            var result = JObject.Parse(content);
            var modsArray = result["data"] as JArray;

            if (modsArray == null) return new List<ModInfo>();

            var mods = new List<ModInfo>();
            foreach (var modJson in modsArray)
            {
                var mod = ParseModFromJson(modJson, ModSource.CurseForge);
                if (mod != null) mods.Add(mod);
            }

            _searchCache[cacheKey] = new CachedItem<List<ModInfo>>(mods);
            return mods;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"CurseForge popular mods error: {ex.Message}");
            return new List<ModInfo>();
        }
    }

    public async Task<List<ModInfo>> GetModsByCategory(string category, string? gameVersion = null, int count = 20)
    {
        var categoryIds = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            { "adventure", 422 },
            { "magic", 4473 },
            { "technology", 412 },
            { "storage", 420 },
            { "library", 421 },
            { "worldgen", 406 },
            { "world gen", 406 },
            { "mobs", 411 },
            { "food", 436 },
            { "equipment", 434 },
            { "utility", 5191 }
        };

        if (!categoryIds.TryGetValue(category, out var categoryId))
        {
            return await SearchModsAsync(category, gameVersion, count);
        }

        var cacheKey = $"category:{categoryId}:{gameVersion}:{count}";

        if (_searchCache.TryGetValue(cacheKey, out var cached) && !cached.IsExpired)
        {
            return cached.Value ?? new List<ModInfo>();
        }

        try
        {
            var url = $"{ApiBaseUrl}/mods/search?gameId={MinecraftGameId}&classId={ModsClassId}&categoryId={categoryId}&pageSize={count}&sortField=2&sortOrder=desc&modLoaderType=1";

            if (!string.IsNullOrEmpty(gameVersion))
            {
                url += $"&gameVersion={Uri.EscapeDataString(gameVersion)}";
            }

            var content = await GetWithRetryAsync(url);
            if (content == null) return new List<ModInfo>();

            var result = JObject.Parse(content);
            var modsArray = result["data"] as JArray;

            if (modsArray == null) return new List<ModInfo>();

            var mods = new List<ModInfo>();
            foreach (var modJson in modsArray)
            {
                var mod = ParseModFromJson(modJson, ModSource.CurseForge);
                if (mod != null) mods.Add(mod);
            }

            _searchCache[cacheKey] = new CachedItem<List<ModInfo>>(mods);
            return mods;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"CurseForge category search error: {ex.Message}");
            return new List<ModInfo>();
        }
    }

    /// <summary>
    /// Clears all cached data
    /// </summary>
    public static void ClearCache()
    {
        _modCache.Clear();
        _filesCache.Clear();
        _searchCache.Clear();
    }

    private async Task<string?> GetWithRetryAsync(string url)
    {
        Exception? lastException = null;

        for (int attempt = 0; attempt < MaxRetryAttempts; attempt++)
        {
            try
            {
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync();
            }
            catch (HttpRequestException ex) when (attempt < MaxRetryAttempts - 1)
            {
                lastException = ex;
                var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                System.Diagnostics.Debug.WriteLine($"API request failed, retrying in {delay.TotalSeconds}s: {ex.Message}");
                await Task.Delay(delay);
            }
            catch (TaskCanceledException ex) when (attempt < MaxRetryAttempts - 1)
            {
                lastException = ex;
                var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                System.Diagnostics.Debug.WriteLine($"API request timed out, retrying in {delay.TotalSeconds}s");
                await Task.Delay(delay);
            }
        }

        if (lastException != null)
        {
            System.Diagnostics.Debug.WriteLine($"API request failed after {MaxRetryAttempts} attempts: {lastException.Message}");
        }

        return null;
    }

    private ModInfo? ParseModFromJson(JToken modJson, ModSource source)
    {
        try
        {
            var mod = new ModInfo
            {
                Id = modJson["id"]?.Value<long>() ?? 0,
                Name = modJson["name"]?.ToString() ?? "Unknown",
                Slug = modJson["slug"]?.ToString() ?? "",
                Summary = modJson["summary"]?.ToString() ?? "",
                DownloadCount = modJson["downloadCount"]?.Value<long>() ?? 0,
                Source = source
            };

            // Get icon/logo URL
            var logo = modJson["logo"];
            if (logo != null)
            {
                mod.IconUrl = logo["thumbnailUrl"]?.ToString() ?? logo["url"]?.ToString() ?? "";
            }

            // Get website URL
            var links = modJson["links"];
            if (links != null)
            {
                mod.WebsiteUrl = links["websiteUrl"]?.ToString() ?? "";
            }

            // Get authors
            var authors = modJson["authors"] as JArray;
            if (authors != null && authors.Count > 0)
            {
                mod.Author = string.Join(", ", authors.Select(a => a["name"]?.ToString() ?? "").Where(n => !string.IsNullOrEmpty(n)));
            }

            // Get categories
            var categories = modJson["categories"] as JArray;
            if (categories != null)
            {
                mod.Categories = categories.Select(c => c["name"]?.ToString() ?? "").Where(n => !string.IsNullOrEmpty(n)).ToList();
            }

            // Get screenshots
            var screenshots = modJson["screenshots"] as JArray;
            if (screenshots != null)
            {
                mod.Screenshots = screenshots.Select(s => new Screenshot
                {
                    Url = s["url"]?.ToString() ?? "",
                    Title = s["title"]?.ToString() ?? "",
                    Description = s["description"]?.ToString() ?? ""
                }).ToList();
            }

            // Get game versions from latest files
            var latestFiles = modJson["latestFiles"] as JArray;
            if (latestFiles != null)
            {
                var versions = new HashSet<string>();
                foreach (var file in latestFiles)
                {
                    var gameVersions = file["gameVersions"] as JArray;
                    if (gameVersions != null)
                    {
                        foreach (var v in gameVersions)
                        {
                            var version = v.ToString();
                            if (!string.IsNullOrEmpty(version) && version.Length > 0 && char.IsDigit(version[0]))
                            {
                                versions.Add(version);
                            }
                        }
                    }
                }
                mod.GameVersions = versions.OrderByDescending(v => v).ToList();
            }

            // Get date information
            if (DateTime.TryParse(modJson["dateCreated"]?.ToString(), out var created))
                mod.DateCreated = created;
            if (DateTime.TryParse(modJson["dateModified"]?.ToString(), out var modified))
                mod.DateModified = modified;

            return mod;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error parsing mod: {ex.Message}");
            return null;
        }
    }

    private ModFile? ParseFileFromJson(JToken fileJson)
    {
        try
        {
            var file = new ModFile
            {
                Id = fileJson["id"]?.Value<long>() ?? 0,
                FileName = fileJson["fileName"]?.ToString() ?? "",
                DisplayName = fileJson["displayName"]?.ToString() ?? "",
                FileSize = fileJson["fileLength"]?.Value<long>() ?? 0,
                DownloadUrl = fileJson["downloadUrl"]?.ToString() ?? ""
            };

            if (DateTime.TryParse(fileJson["fileDate"]?.ToString(), out var date))
                file.FileDate = date;

            var releaseType = fileJson["releaseType"]?.Value<int>() ?? 1;
            file.ReleaseType = releaseType switch
            {
                1 => ReleaseType.Release,
                2 => ReleaseType.Beta,
                3 => ReleaseType.Alpha,
                _ => ReleaseType.Release
            };

            // Get game versions
            var gameVersions = fileJson["gameVersions"] as JArray;
            if (gameVersions != null)
            {
                foreach (var v in gameVersions)
                {
                    var version = v.ToString();
                    if (!string.IsNullOrEmpty(version))
                    {
                        if (version.Equals("Forge", StringComparison.OrdinalIgnoreCase) ||
                            version.Equals("NeoForge", StringComparison.OrdinalIgnoreCase))
                        {
                            file.ModLoaders.Add(version);
                        }
                        else if (version.Length > 0 && char.IsDigit(version[0]))
                        {
                            file.GameVersions.Add(version);
                        }
                    }
                }
            }

            // Get dependencies
            var dependencies = fileJson["dependencies"] as JArray;
            if (dependencies != null)
            {
                foreach (var dep in dependencies)
                {
                    var depType = dep["relationType"]?.Value<int>() ?? 0;
                    file.Dependencies.Add(new ModDependency
                    {
                        ModId = dep["modId"]?.Value<long>() ?? 0,
                        Type = depType switch
                        {
                            1 => DependencyType.Embedded,
                            2 => DependencyType.Optional,
                            3 => DependencyType.Required,
                            4 => DependencyType.Tool,
                            5 => DependencyType.Incompatible,
                            _ => DependencyType.Optional
                        }
                    });
                }
            }

            return file;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error parsing file: {ex.Message}");
            return null;
        }
    }
}

/// <summary>
/// Simple cache item with expiration
/// </summary>
internal class CachedItem<T>
{
    public T? Value { get; }
    public DateTime ExpiresAt { get; }
    public bool IsExpired => DateTime.UtcNow > ExpiresAt;

    public CachedItem(T value, TimeSpan? duration = null)
    {
        Value = value;
        ExpiresAt = DateTime.UtcNow.Add(duration ?? TimeSpan.FromMinutes(15));
    }
}

/// <summary>
/// Result of mod compatibility check
/// </summary>
public class ModCompatibilityResult
{
    public bool IsCompatible { get; set; }
    public CompatibilityStatus Status { get; set; }
    public string Message { get; set; } = string.Empty;
    public ModFile? CompatibleFile { get; set; }
    public List<string> AvailableVersions { get; set; } = new();
}

public enum CompatibilityStatus
{
    Compatible,
    NoCompatibleVersion,
    NotFound,
    DownloadRestricted,
    Unknown
}
