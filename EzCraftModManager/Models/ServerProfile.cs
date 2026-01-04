using System;
using System.Collections.Generic;

namespace EzCraftModManager.Models;

public class ServerProfile
{
    private List<InstalledMod>? _installedMods;

    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "My Minecraft Server";
    public string MinecraftVersion { get; set; } = "1.20.1";
    public string ForgeVersion { get; set; } = string.Empty;
    public string ServerPath { get; set; } = string.Empty;
    public string ModsPath => !string.IsNullOrEmpty(ServerPath) ? System.IO.Path.Combine(ServerPath, "mods") : string.Empty;
    public int MaxPlayers { get; set; } = 20;
    public int RamGB { get; set; } = 4;
    public int Port { get; set; } = 25565;
    public string GameMode { get; set; } = "survival";
    public string Difficulty { get; set; } = "normal";
    public bool EnablePvP { get; set; } = true;
    public bool EnableWhitelist { get; set; } = false;
    public int ViewDistance { get; set; } = 10;
    public string OperatorUsername { get; set; } = string.Empty;
    public string Motd { get; set; } = "A Minecraft Server";

    // InstalledMods is guaranteed to never be null
    public List<InstalledMod> InstalledMods
    {
        get => _installedMods ??= new List<InstalledMod>();
        set => _installedMods = value ?? new List<InstalledMod>();
    }

    public DateTime CreatedDate { get; set; } = DateTime.Now;
    public DateTime LastPlayed { get; set; }
    public ServerStatus Status { get; set; } = ServerStatus.Stopped;
    public bool IsForgeInstalled { get; set; }
    public string JavaPath { get; set; } = "java";
}

public class InstalledMod
{
    public long ModId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public DateTime InstalledDate { get; set; } = DateTime.Now;
    public ModSource Source { get; set; }
    public bool IsEnabled { get; set; } = true;
}

public enum ServerStatus
{
    Stopped,
    Starting,
    Running,
    Stopping,
    Error
}
