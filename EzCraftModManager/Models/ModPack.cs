using System.Collections.Generic;

namespace EzCraftModManager.Models;

public class ModPack
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string IconPath { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public List<ModPackItem> Mods { get; set; } = new();
    public string RecommendedMinecraftVersion { get; set; } = "1.20.1";
    public string Author { get; set; } = "EzCraft";
    public int TotalDownloads { get; set; }
}

public class ModPackItem
{
    public string SearchQuery { get; set; } = string.Empty;
    public long? CurseForgeId { get; set; }
    public string? ModrinthId { get; set; }
    public bool IsRequired { get; set; } = true;
    public string Description { get; set; } = string.Empty;
}

public static class CuratedModPacks
{
    public static ModPack ZombieSurvival => new()
    {
        Id = "zombie-survival",
        Name = "Zombie Apocalypse",
        Description = "Survive waves of zombies with enhanced AI, new zombie types, and survival mechanics",
        Category = "Survival",
        IconPath = "/Assets/Images/zombie_pack.png",
        RecommendedMinecraftVersion = "1.20.1",
        Mods = new List<ModPackItem>
        {
            new() { SearchQuery = "Zombie Awareness", Description = "Smarter zombies that track you by sound and light", IsRequired = true },
            new() { SearchQuery = "Epic Siege Mod", Description = "Zombies break blocks and siege your base", IsRequired = true },
            new() { SearchQuery = "Tough As Nails", Description = "Thirst, temperature, and survival mechanics", IsRequired = true },
            new() { SearchQuery = "First Aid", Description = "Realistic body part damage system", IsRequired = true },
            new() { SearchQuery = "Corpse", Description = "Dead bodies that can reanimate", IsRequired = false },
            new() { SearchQuery = "Hostile Villages", Description = "Zombie-infested abandoned villages", IsRequired = false },
            new() { SearchQuery = "Spore", Description = "Infected zombies that spread disease", IsRequired = true },
            new() { SearchQuery = "Zombie Extreme", Description = "More zombie variants and behaviors", IsRequired = true },
            new() { SearchQuery = "Day Z", Description = "DayZ-style survival experience", IsRequired = false },
            new() { SearchQuery = "Lost Cities", Description = "Abandoned city worldgen", IsRequired = false },
        }
    };

    public static ModPack GunsAndWeapons => new()
    {
        Id = "guns-weapons",
        Name = "Arsenal & Firearms",
        Description = "Modern and futuristic weapons including guns, explosives, and tactical gear",
        Category = "Combat",
        IconPath = "/Assets/Images/guns_pack.png",
        RecommendedMinecraftVersion = "1.20.1",
        Mods = new List<ModPackItem>
        {
            new() { SearchQuery = "MrCrayfish's Gun Mod", Description = "High-quality 3D guns with attachments", IsRequired = true },
            new() { SearchQuery = "Timeless and Classics Guns", Description = "Classic firearms with realistic mechanics", IsRequired = true },
            new() { SearchQuery = "Tech Guns", Description = "Sci-fi weapons and power armor", IsRequired = false },
            new() { SearchQuery = "Vic's Modern Warfare", Description = "Modern military weapons", IsRequired = false },
            new() { SearchQuery = "CGM", CurseForgeId = 478351, Description = "Customizable guns with attachments", IsRequired = true },
            new() { SearchQuery = "Scorched Guns", Description = "Post-apocalyptic weapons", IsRequired = false },
            new() { SearchQuery = "Simple Guns", Description = "Easy-to-craft basic guns", IsRequired = false },
            new() { SearchQuery = "Security Craft", Description = "Security systems and turrets", IsRequired = false },
            new() { SearchQuery = "Immersive Armors", Description = "New armor sets and equipment", IsRequired = false },
            new() { SearchQuery = "Better Combat", Description = "Enhanced melee combat", IsRequired = true },
        }
    };

    public static ModPack HorrorCraft => new()
    {
        Id = "horror-craft",
        Name = "Horror & Terror",
        Description = "Terrifying creatures, dark atmospheres, and jump scares for horror fans",
        Category = "Horror",
        IconPath = "/Assets/Images/horror_pack.png",
        RecommendedMinecraftVersion = "1.20.1",
        Mods = new List<ModPackItem>
        {
            new() { SearchQuery = "The Midnight", Description = "Dark dimension with terrifying creatures", IsRequired = true },
            new() { SearchQuery = "Weeping Angels", Description = "Don't blink! Doctor Who Weeping Angels", IsRequired = true },
            new() { SearchQuery = "Grue", Description = "Something lurks in the darkness", IsRequired = true },
            new() { SearchQuery = "Horror Elements", Description = "Jump scares and horror ambiance", IsRequired = true },
            new() { SearchQuery = "Stalker Creepers", Description = "Creepers that stalk and hunt you", IsRequired = true },
            new() { SearchQuery = "Sons of Sins", Description = "Demonic creatures and dark rituals", IsRequired = true },
            new() { SearchQuery = "The Conjuring", Description = "Paranormal entities", IsRequired = false },
            new() { SearchQuery = "SCP Lockdown", Description = "SCP Foundation creatures", IsRequired = false },
            new() { SearchQuery = "Sanity", Description = "Sanity mechanics that affect gameplay", IsRequired = false },
            new() { SearchQuery = "Nightmare Creatures", Description = "Terrifying nightmare monsters", IsRequired = true },
            new() { SearchQuery = "Blood Moon", Description = "Dangerous blood moon events", IsRequired = true },
            new() { SearchQuery = "Enhanced Visuals", Description = "Blood, gore, and visual effects", IsRequired = true },
        }
    };

    public static ModPack UltimateApocalypse => new()
    {
        Id = "ultimate-apocalypse",
        Name = "Ultimate Apocalypse",
        Description = "The complete post-apocalyptic experience: zombies, guns, horror, and survival combined",
        Category = "Modpack",
        IconPath = "/Assets/Images/apocalypse_pack.png",
        RecommendedMinecraftVersion = "1.20.1",
        Mods = new List<ModPackItem>
        {
            // Zombies & Creatures
            new() { SearchQuery = "Zombie Awareness", Description = "Smart zombies", IsRequired = true },
            new() { SearchQuery = "Epic Siege Mod", Description = "Base sieges", IsRequired = true },
            new() { SearchQuery = "Stalker Creepers", Description = "Stalking creepers", IsRequired = true },
            new() { SearchQuery = "Weeping Angels", Description = "Weeping Angels", IsRequired = true },
            new() { SearchQuery = "Grue", Description = "Darkness creature", IsRequired = true },
            // Guns & Combat
            new() { SearchQuery = "MrCrayfish's Gun Mod", Description = "Modern guns", IsRequired = true },
            new() { SearchQuery = "Timeless and Classics Guns", Description = "Classic guns", IsRequired = true },
            new() { SearchQuery = "Better Combat", Description = "Combat improvements", IsRequired = true },
            // Survival
            new() { SearchQuery = "Tough As Nails", Description = "Survival mechanics", IsRequired = true },
            new() { SearchQuery = "First Aid", Description = "Body damage", IsRequired = true },
            // World & Atmosphere
            new() { SearchQuery = "Lost Cities", Description = "City ruins", IsRequired = true },
            new() { SearchQuery = "Horror Elements", Description = "Horror ambiance", IsRequired = true },
            new() { SearchQuery = "Blood Moon", Description = "Blood moon events", IsRequired = true },
            new() { SearchQuery = "Dynamic Surroundings", Description = "Atmospheric effects", IsRequired = false },
        }
    };

    public static List<ModPack> AllPacks => new()
    {
        ZombieSurvival,
        GunsAndWeapons,
        HorrorCraft,
        UltimateApocalypse
    };
}
