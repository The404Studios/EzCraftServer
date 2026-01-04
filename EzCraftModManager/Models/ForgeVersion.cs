using System;
using System.Collections.Generic;

namespace EzCraftModManager.Models;

public class ForgeVersionInfo
{
    public string MinecraftVersion { get; set; } = string.Empty;
    public string ForgeVersion { get; set; } = string.Empty;
    public string FullVersion => $"{MinecraftVersion}-{ForgeVersion}";
    public string DownloadUrl { get; set; } = string.Empty;
    public bool IsRecommended { get; set; }
    public bool IsLatest { get; set; }
    public DateTime ReleaseDate { get; set; }
}

public class MinecraftVersion
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // release, snapshot
    public DateTime ReleaseTime { get; set; }
    public string Url { get; set; } = string.Empty;
    public bool HasForge { get; set; }
    public List<ForgeVersionInfo> ForgeVersions { get; set; } = new();
}

public class ForgePromoVersions
{
    public Dictionary<string, string> Promos { get; set; } = new();
}

public class JavaInfo
{
    public bool IsInstalled { get; set; }
    public string Version { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public int MajorVersion { get; set; }
    public bool IsCompatible { get; set; }
}
