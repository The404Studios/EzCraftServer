using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using EzCraftModManager.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace EzCraftModManager.Services;

public class CurseForgeService
{
    private readonly HttpClient _httpClient;
    private const string ApiBaseUrl = "https://api.curseforge.com/v1";
    private const string ApiKey = "$2a$10$6Y15zxWaENIAeUySYwtK0uM5STstUVkvvqmwZl1xIBxdWxVd3YJ4W";
    private const int MinecraftGameId = 432;
    private const int ModsClassId = 6;

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
        try
        {
            var url = $"{ApiBaseUrl}/mods/search?gameId={MinecraftGameId}&classId={ModsClassId}&searchFilter={Uri.EscapeDataString(query)}&pageSize={pageSize}&index={page * pageSize}&sortField=2&sortOrder=desc";

            if (!string.IsNullOrEmpty(gameVersion))
            {
                url += $"&gameVersion={Uri.EscapeDataString(gameVersion)}";
            }

            // Add modLoader filter for Forge (1 = Forge)
            url += "&modLoaderType=1";

            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var result = JObject.Parse(content);
            var modsArray = result["data"] as JArray;

            if (modsArray == null) return new List<ModInfo>();

            var mods = new List<ModInfo>();
            foreach (var modJson in modsArray)
            {
                var mod = ParseModFromJson(modJson, ModSource.CurseForge);
                if (mod != null) mods.Add(mod);
            }

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
        try
        {
            var response = await _httpClient.GetAsync($"{ApiBaseUrl}/mods/{modId}");
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var result = JObject.Parse(content);
            var modJson = result["data"];

            return modJson != null ? ParseModFromJson(modJson, ModSource.CurseForge) : null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"CurseForge get mod error: {ex.Message}");
            return null;
        }
    }

    public async Task<List<ModFile>> GetModFilesAsync(long modId, string? gameVersion = null)
    {
        try
        {
            var url = $"{ApiBaseUrl}/mods/{modId}/files?pageSize=50";
            if (!string.IsNullOrEmpty(gameVersion))
            {
                url += $"&gameVersion={Uri.EscapeDataString(gameVersion)}";
            }
            url += "&modLoaderType=1"; // Forge

            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var result = JObject.Parse(content);
            var filesArray = result["data"] as JArray;

            if (filesArray == null) return new List<ModFile>();

            var files = new List<ModFile>();
            foreach (var fileJson in filesArray)
            {
                var file = ParseFileFromJson(fileJson);
                if (file != null) files.Add(file);
            }

            return files.OrderByDescending(f => f.FileDate).ToList();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"CurseForge get files error: {ex.Message}");
            return new List<ModFile>();
        }
    }

    public async Task<ModFile?> GetCompatibleFileAsync(long modId, string gameVersion)
    {
        var files = await GetModFilesAsync(modId, gameVersion);

        // Try exact version match first
        var exactMatch = files.FirstOrDefault(f =>
            f.GameVersions.Contains(gameVersion) &&
            f.ModLoaders.Any(ml => ml.Equals("Forge", StringComparison.OrdinalIgnoreCase)));

        if (exactMatch != null) return exactMatch;

        // Try minor version match (e.g., 1.20.x)
        var versionParts = gameVersion.Split('.');
        if (versionParts.Length >= 2)
        {
            var minorVersion = $"{versionParts[0]}.{versionParts[1]}";
            var minorMatch = files.FirstOrDefault(f =>
                f.GameVersions.Any(v => v.StartsWith(minorVersion)) &&
                f.ModLoaders.Any(ml => ml.Equals("Forge", StringComparison.OrdinalIgnoreCase)));

            if (minorMatch != null) return minorMatch;
        }

        // Return latest Forge-compatible file
        return files.FirstOrDefault(f =>
            f.ModLoaders.Any(ml => ml.Equals("Forge", StringComparison.OrdinalIgnoreCase)));
    }

    public async Task<string?> GetDownloadUrlAsync(long modId, long fileId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{ApiBaseUrl}/mods/{modId}/files/{fileId}/download-url");
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
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
        try
        {
            var url = $"{ApiBaseUrl}/mods/search?gameId={MinecraftGameId}&classId={ModsClassId}&pageSize={count}&sortField=2&sortOrder=desc&modLoaderType=1";

            if (!string.IsNullOrEmpty(gameVersion))
            {
                url += $"&gameVersion={Uri.EscapeDataString(gameVersion)}";
            }

            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var result = JObject.Parse(content);
            var modsArray = result["data"] as JArray;

            if (modsArray == null) return new List<ModInfo>();

            var mods = new List<ModInfo>();
            foreach (var modJson in modsArray)
            {
                var mod = ParseModFromJson(modJson, ModSource.CurseForge);
                if (mod != null) mods.Add(mod);
            }

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
        var categoryIds = new Dictionary<string, int>
        {
            { "adventure", 422 },
            { "magic", 4473 },
            { "technology", 412 },
            { "storage", 420 },
            { "library", 421 },
            { "worldgen", 406 },
            { "mobs", 411 },
            { "food", 436 },
            { "equipment", 434 },
            { "utility", 5191 }
        };

        if (!categoryIds.TryGetValue(category.ToLower(), out var categoryId))
        {
            return await SearchModsAsync(category, gameVersion, count);
        }

        try
        {
            var url = $"{ApiBaseUrl}/mods/search?gameId={MinecraftGameId}&classId={ModsClassId}&categoryId={categoryId}&pageSize={count}&sortField=2&sortOrder=desc&modLoaderType=1";

            if (!string.IsNullOrEmpty(gameVersion))
            {
                url += $"&gameVersion={Uri.EscapeDataString(gameVersion)}";
            }

            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var result = JObject.Parse(content);
            var modsArray = result["data"] as JArray;

            if (modsArray == null) return new List<ModInfo>();

            var mods = new List<ModInfo>();
            foreach (var modJson in modsArray)
            {
                var mod = ParseModFromJson(modJson, ModSource.CurseForge);
                if (mod != null) mods.Add(mod);
            }

            return mods;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"CurseForge category search error: {ex.Message}");
            return new List<ModInfo>();
        }
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
                mod.Author = string.Join(", ", authors.Select(a => a["name"]?.ToString() ?? ""));
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
                            if (!string.IsNullOrEmpty(version) && char.IsDigit(version[0]))
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
                        else if (char.IsDigit(version[0]))
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
