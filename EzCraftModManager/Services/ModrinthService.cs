using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using EzCraftModManager.Models;
using Newtonsoft.Json.Linq;

namespace EzCraftModManager.Services;

public class ModrinthService
{
    private readonly HttpClient _httpClient;
    private const string ApiBaseUrl = "https://api.modrinth.com/v2";

    public ModrinthService()
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "EzCraftModManager/2.0 (contact@example.com)");
        _httpClient.Timeout = TimeSpan.FromMinutes(2);
    }

    public async Task<List<ModInfo>> SearchModsAsync(string query, string? gameVersion = null, int limit = 20, int offset = 0)
    {
        try
        {
            var facets = new List<string> { "[\"project_type:mod\"]", "[\"categories:forge\"]" };

            if (!string.IsNullOrEmpty(gameVersion))
            {
                facets.Add($"[\"versions:{gameVersion}\"]");
            }

            var facetsJson = $"[{string.Join(",", facets)}]";
            var url = $"{ApiBaseUrl}/search?query={Uri.EscapeDataString(query)}&limit={limit}&offset={offset}&facets={Uri.EscapeDataString(facetsJson)}";

            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var result = JObject.Parse(content);
            var hitsArray = result["hits"] as JArray;

            if (hitsArray == null) return new List<ModInfo>();

            var mods = new List<ModInfo>();
            foreach (var hit in hitsArray)
            {
                var mod = ParseModFromSearchHit(hit);
                if (mod != null) mods.Add(mod);
            }

            return mods;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Modrinth search error: {ex.Message}");
            return new List<ModInfo>();
        }
    }

    public async Task<ModInfo?> GetModAsync(string projectId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{ApiBaseUrl}/project/{projectId}");
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var projectJson = JObject.Parse(content);

            return ParseModFromProject(projectJson);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Modrinth get mod error: {ex.Message}");
            return null;
        }
    }

    public async Task<List<ModFile>> GetModVersionsAsync(string projectId, string? gameVersion = null)
    {
        try
        {
            var url = $"{ApiBaseUrl}/project/{projectId}/version";

            if (!string.IsNullOrEmpty(gameVersion))
            {
                url += $"?game_versions=[\"{gameVersion}\"]&loaders=[\"forge\"]";
            }
            else
            {
                url += "?loaders=[\"forge\"]";
            }

            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var versionsArray = JArray.Parse(content);

            var files = new List<ModFile>();
            foreach (var version in versionsArray)
            {
                var file = ParseVersionToFile(version);
                if (file != null) files.Add(file);
            }

            return files.OrderByDescending(f => f.FileDate).ToList();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Modrinth get versions error: {ex.Message}");
            return new List<ModFile>();
        }
    }

    public async Task<ModFile?> GetCompatibleFileAsync(string projectId, string gameVersion)
    {
        var files = await GetModVersionsAsync(projectId, gameVersion);

        // Try exact version match first
        var exactMatch = files.FirstOrDefault(f => f.GameVersions.Contains(gameVersion));
        if (exactMatch != null) return exactMatch;

        // Try minor version match
        var versionParts = gameVersion.Split('.');
        if (versionParts.Length >= 2)
        {
            var minorVersion = $"{versionParts[0]}.{versionParts[1]}";
            var minorMatch = files.FirstOrDefault(f => f.GameVersions.Any(v => v.StartsWith(minorVersion)));
            if (minorMatch != null) return minorMatch;
        }

        // Return latest
        return files.FirstOrDefault();
    }

    public async Task<List<ModInfo>> GetPopularModsAsync(string? gameVersion = null, int count = 20)
    {
        try
        {
            var facets = new List<string> { "[\"project_type:mod\"]", "[\"categories:forge\"]" };

            if (!string.IsNullOrEmpty(gameVersion))
            {
                facets.Add($"[\"versions:{gameVersion}\"]");
            }

            var facetsJson = $"[{string.Join(",", facets)}]";
            var url = $"{ApiBaseUrl}/search?limit={count}&index=downloads&facets={Uri.EscapeDataString(facetsJson)}";

            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var result = JObject.Parse(content);
            var hitsArray = result["hits"] as JArray;

            if (hitsArray == null) return new List<ModInfo>();

            var mods = new List<ModInfo>();
            foreach (var hit in hitsArray)
            {
                var mod = ParseModFromSearchHit(hit);
                if (mod != null) mods.Add(mod);
            }

            return mods;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Modrinth popular mods error: {ex.Message}");
            return new List<ModInfo>();
        }
    }

    public async Task<List<ModInfo>> GetModsByCategory(string category, string? gameVersion = null, int count = 20)
    {
        try
        {
            var facets = new List<string>
            {
                "[\"project_type:mod\"]",
                "[\"categories:forge\"]",
                $"[\"categories:{category.ToLower()}\"]"
            };

            if (!string.IsNullOrEmpty(gameVersion))
            {
                facets.Add($"[\"versions:{gameVersion}\"]");
            }

            var facetsJson = $"[{string.Join(",", facets)}]";
            var url = $"{ApiBaseUrl}/search?limit={count}&index=downloads&facets={Uri.EscapeDataString(facetsJson)}";

            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var result = JObject.Parse(content);
            var hitsArray = result["hits"] as JArray;

            if (hitsArray == null) return new List<ModInfo>();

            var mods = new List<ModInfo>();
            foreach (var hit in hitsArray)
            {
                var mod = ParseModFromSearchHit(hit);
                if (mod != null) mods.Add(mod);
            }

            return mods;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Modrinth category search error: {ex.Message}");
            return new List<ModInfo>();
        }
    }

    private ModInfo? ParseModFromSearchHit(JToken hit)
    {
        try
        {
            var mod = new ModInfo
            {
                Slug = hit["slug"]?.ToString() ?? "",
                Name = hit["title"]?.ToString() ?? "Unknown",
                Summary = hit["description"]?.ToString() ?? "",
                IconUrl = hit["icon_url"]?.ToString() ?? "",
                DownloadCount = hit["downloads"]?.Value<long>() ?? 0,
                Source = ModSource.Modrinth,
                Author = hit["author"]?.ToString() ?? ""
            };

            // Parse date
            if (DateTime.TryParse(hit["date_created"]?.ToString(), out var created))
                mod.DateCreated = created;
            if (DateTime.TryParse(hit["date_modified"]?.ToString(), out var modified))
                mod.DateModified = modified;

            // Parse versions
            var versions = hit["versions"] as JArray;
            if (versions != null)
            {
                mod.GameVersions = versions.Select(v => v.ToString()).Where(v => !string.IsNullOrEmpty(v) && char.IsDigit(v[0])).ToList();
            }

            // Parse categories
            var categories = hit["categories"] as JArray;
            if (categories != null)
            {
                mod.Categories = categories.Select(c => c.ToString()).ToList();
            }

            // Parse gallery
            var gallery = hit["gallery"] as JArray;
            if (gallery != null)
            {
                mod.Screenshots = gallery.Select(g => new Screenshot
                {
                    Url = g.ToString()
                }).ToList();
            }

            return mod;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error parsing search hit: {ex.Message}");
            return null;
        }
    }

    private ModInfo? ParseModFromProject(JObject projectJson)
    {
        try
        {
            var mod = new ModInfo
            {
                Slug = projectJson["slug"]?.ToString() ?? "",
                Name = projectJson["title"]?.ToString() ?? "Unknown",
                Summary = projectJson["description"]?.ToString() ?? "",
                Description = projectJson["body"]?.ToString() ?? "",
                IconUrl = projectJson["icon_url"]?.ToString() ?? "",
                DownloadCount = projectJson["downloads"]?.Value<long>() ?? 0,
                Source = ModSource.Modrinth
            };

            // Parse date
            if (DateTime.TryParse(projectJson["published"]?.ToString(), out var created))
                mod.DateCreated = created;
            if (DateTime.TryParse(projectJson["updated"]?.ToString(), out var modified))
                mod.DateModified = modified;

            // Parse versions
            var versions = projectJson["game_versions"] as JArray;
            if (versions != null)
            {
                mod.GameVersions = versions.Select(v => v.ToString()).Where(v => !string.IsNullOrEmpty(v)).ToList();
            }

            // Parse categories
            var categories = projectJson["categories"] as JArray;
            if (categories != null)
            {
                mod.Categories = categories.Select(c => c.ToString()).ToList();
            }

            // Parse gallery
            var gallery = projectJson["gallery"] as JArray;
            if (gallery != null)
            {
                mod.Screenshots = gallery.Select(g => new Screenshot
                {
                    Url = g["url"]?.ToString() ?? "",
                    Title = g["title"]?.ToString() ?? "",
                    Description = g["description"]?.ToString() ?? ""
                }).ToList();
            }

            return mod;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error parsing project: {ex.Message}");
            return null;
        }
    }

    private ModFile? ParseVersionToFile(JToken version)
    {
        try
        {
            var file = new ModFile
            {
                DisplayName = version["name"]?.ToString() ?? "",
                DownloadUrl = "",
                FileName = ""
            };

            if (DateTime.TryParse(version["date_published"]?.ToString(), out var date))
                file.FileDate = date;

            var versionType = version["version_type"]?.ToString() ?? "release";
            file.ReleaseType = versionType switch
            {
                "release" => ReleaseType.Release,
                "beta" => ReleaseType.Beta,
                "alpha" => ReleaseType.Alpha,
                _ => ReleaseType.Release
            };

            // Get game versions
            var gameVersions = version["game_versions"] as JArray;
            if (gameVersions != null)
            {
                file.GameVersions = gameVersions.Select(v => v.ToString()).ToList();
            }

            // Get loaders
            var loaders = version["loaders"] as JArray;
            if (loaders != null)
            {
                file.ModLoaders = loaders.Select(l => l.ToString()).ToList();
            }

            // Get primary file
            var files = version["files"] as JArray;
            if (files != null && files.Count > 0)
            {
                var primaryFile = files.FirstOrDefault(f => f["primary"]?.Value<bool>() == true) ?? files[0];
                file.FileName = primaryFile["filename"]?.ToString() ?? "";
                file.DownloadUrl = primaryFile["url"]?.ToString() ?? "";
                file.FileSize = primaryFile["size"]?.Value<long>() ?? 0;
            }

            // Get dependencies
            var dependencies = version["dependencies"] as JArray;
            if (dependencies != null)
            {
                foreach (var dep in dependencies)
                {
                    var depType = dep["dependency_type"]?.ToString() ?? "optional";
                    file.Dependencies.Add(new ModDependency
                    {
                        ModName = dep["project_id"]?.ToString() ?? "",
                        Type = depType switch
                        {
                            "required" => DependencyType.Required,
                            "optional" => DependencyType.Optional,
                            "incompatible" => DependencyType.Incompatible,
                            "embedded" => DependencyType.Embedded,
                            _ => DependencyType.Optional
                        }
                    });
                }
            }

            return file;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error parsing version: {ex.Message}");
            return null;
        }
    }
}
