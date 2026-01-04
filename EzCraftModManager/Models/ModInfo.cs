using System;
using System.Collections.Generic;

namespace EzCraftModManager.Models;

public class ModInfo
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string IconUrl { get; set; } = string.Empty;
    public string WebsiteUrl { get; set; } = string.Empty;
    public long DownloadCount { get; set; }
    public DateTime DateCreated { get; set; }
    public DateTime DateModified { get; set; }
    public List<string> Categories { get; set; } = new();
    public List<ModFile> Files { get; set; } = new();
    public List<string> GameVersions { get; set; } = new();
    public ModSource Source { get; set; }
    public bool IsInstalled { get; set; }
    public string? InstalledVersion { get; set; }
    public string? InstalledFilePath { get; set; }
    public List<Screenshot> Screenshots { get; set; } = new();
}

public class ModFile
{
    public long Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public DateTime FileDate { get; set; }
    public long FileSize { get; set; }
    public string DownloadUrl { get; set; } = string.Empty;
    public List<string> GameVersions { get; set; } = new();
    public List<ModDependency> Dependencies { get; set; } = new();
    public ReleaseType ReleaseType { get; set; }
    public List<string> ModLoaders { get; set; } = new();
}

public class ModDependency
{
    public long ModId { get; set; }
    public string ModName { get; set; } = string.Empty;
    public DependencyType Type { get; set; }
}

public class Screenshot
{
    public string Url { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public enum ModSource
{
    CurseForge,
    Modrinth,
    Local
}

public enum ReleaseType
{
    Release,
    Beta,
    Alpha
}

public enum DependencyType
{
    Required,
    Optional,
    Incompatible,
    Embedded,
    Tool
}
