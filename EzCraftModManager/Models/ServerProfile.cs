using System;
using System.Collections.Generic;

namespace EzCraftModManager.Models;

public class ServerProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "My Minecraft Server";
    public string MinecraftVersion { get; set; } = "1.21.4";
    public string ForgeVersion { get; set; } = string.Empty;
    public string ServerPath { get; set; } = string.Empty;
    public string ModsPath => System.IO.Path.Combine(ServerPath, "mods");
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
    public List<InstalledMod> InstalledMods { get; set; } = new();
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
