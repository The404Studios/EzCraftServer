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
    // ===========================================
    // ZOMBIE SURVIVAL PACK
    // ===========================================
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
            // Core Zombie Mods - Verified IDs for 1.20.1
            new() { SearchQuery = "Zombie Awareness", CurseForgeId = 223237, ModrinthId = "zombie-awareness", Description = "Smarter zombies that track you by sound and light", IsRequired = true },
            new() { SearchQuery = "Epic Siege Mod", CurseForgeId = 240617, Description = "Zombies break blocks and siege your base", IsRequired = true },

            // Survival Mechanics - Verified IDs
            new() { SearchQuery = "Tough As Nails", CurseForgeId = 238676, ModrinthId = "tough-as-nails", Description = "Thirst, temperature, and survival mechanics", IsRequired = true },
            new() { SearchQuery = "First Aid", CurseForgeId = 319818, Description = "Realistic body part damage system", IsRequired = true },
            new() { SearchQuery = "Spice of Life Carrot Edition", CurseForgeId = 311557, Description = "Food diversity health bonuses", IsRequired = false },

            // World Generation - Verified IDs
            new() { SearchQuery = "Lost Cities", CurseForgeId = 269024, Description = "Abandoned city worldgen", IsRequired = true },
            new() { SearchQuery = "Recurrent Complex", CurseForgeId = 223150, Description = "Dungeon and structure generation", IsRequired = false },

            // Atmosphere and Extras
            new() { SearchQuery = "Corpse", CurseForgeId = 316582, ModrinthId = "corpse", Description = "Dead bodies that can reanimate", IsRequired = false },
            new() { SearchQuery = "Sound Physics Remastered", CurseForgeId = 535489, ModrinthId = "sound-physics-remastered", Description = "Realistic sound in caves and buildings", IsRequired = false },

            // Required Libraries
            new() { SearchQuery = "CoroUtil", CurseForgeId = 237256, Description = "Library for Zombie Awareness and Epic Siege", IsRequired = true },
        }
    };

    // ===========================================
    // GUNS & WEAPONS PACK
    // ===========================================
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
            // Core Gun Mods - Verified IDs for 1.20.1
            new() { SearchQuery = "MrCrayfish's Gun Mod", CurseForgeId = 289479, Description = "High-quality 3D guns with attachments", IsRequired = true },
            new() { SearchQuery = "Timeless and Classics Guns", CurseForgeId = 450281, ModrinthId = "timeless-and-classics", Description = "Classic firearms with realistic mechanics", IsRequired = true },
            new() { SearchQuery = "CGM Addon", CurseForgeId = 478351, Description = "Additional guns for MrCrayfish's Gun Mod", IsRequired = false },

            // Combat Enhancement
            new() { SearchQuery = "Better Combat", CurseForgeId = 639842, ModrinthId = "better-combat", Description = "Enhanced melee combat", IsRequired = true },
            new() { SearchQuery = "Epic Fight", CurseForgeId = 405076, ModrinthId = "epic-fight", Description = "Souls-like combat animations", IsRequired = false },
            new() { SearchQuery = "Combat Roll", CurseForgeId = 853885, ModrinthId = "combat-roll", Description = "Dodge roll ability", IsRequired = false },

            // Defense & Security
            new() { SearchQuery = "SecurityCraft", CurseForgeId = 222043, ModrinthId = "security-craft", Description = "Security systems and turrets", IsRequired = true },

            // Armor
            new() { SearchQuery = "Armored Elytra", CurseForgeId = 297944, ModrinthId = "armored-elytra", Description = "Combine elytra with armor", IsRequired = false },
            new() { SearchQuery = "Cosmetic Armor Reworked", CurseForgeId = 237307, Description = "Hide armor with cosmetic slots", IsRequired = false },

            // Required Libraries
            new() { SearchQuery = "Framework", CurseForgeId = 549225, Description = "Library for MrCrayfish mods", IsRequired = true },
            new() { SearchQuery = "playerAnimator", CurseForgeId = 658587, ModrinthId = "playeranimator", Description = "Required for Better Combat", IsRequired = true },
        }
    };

    // ===========================================
    // HORROR & TERROR PACK
    // ===========================================
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
            // Horror Creatures - Verified IDs
            new() { SearchQuery = "Weeping Angels", CurseForgeId = 236293, Description = "Don't blink! Doctor Who Weeping Angels", IsRequired = true },
            new() { SearchQuery = "The Graveyard", CurseForgeId = 574057, ModrinthId = "the-graveyard", Description = "Haunted graveyards with undead", IsRequired = true },
            new() { SearchQuery = "Whisperwoods", CurseForgeId = 408758, Description = "Creepy forest creatures", IsRequired = true },

            // Atmosphere
            new() { SearchQuery = "Blood Moon", CurseForgeId = 242784, Description = "Dangerous blood moon events", IsRequired = true },
            new() { SearchQuery = "Dynamic Surroundings", CurseForgeId = 238891, Description = "Atmospheric effects and sounds", IsRequired = true },
            new() { SearchQuery = "Sound Physics Remastered", CurseForgeId = 535489, ModrinthId = "sound-physics-remastered", Description = "Realistic echo in dark caves", IsRequired = false },

            // Darkness & Light
            new() { SearchQuery = "True Darkness", CurseForgeId = 402477, ModrinthId = "true-darkness", Description = "Complete darkness without light", IsRequired = true },
            new() { SearchQuery = "Hardcore Torches", CurseForgeId = 281597, Description = "Torches burn out and need relighting", IsRequired = false },

            // More Horror Mobs
            new() { SearchQuery = "Mowzie's Mobs", CurseForgeId = 250498, ModrinthId = "mowzies-mobs", Description = "Unique and terrifying boss creatures", IsRequired = true },
            new() { SearchQuery = "Stalker Creepers", CurseForgeId = 223548, Description = "Creepers that stalk and hunt you", IsRequired = true },

            // Survival Horror
            new() { SearchQuery = "Spooky Biomes", CurseForgeId = 407990, Description = "Haunted biome generation", IsRequired = false },
            new() { SearchQuery = "The Undergarden", CurseForgeId = 379849, ModrinthId = "the-undergarden", Description = "Dark underground dimension", IsRequired = false },
        }
    };

    // ===========================================
    // ULTIMATE APOCALYPSE PACK (Combined)
    // ===========================================
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
            // === ZOMBIES & CREATURES ===
            new() { SearchQuery = "Zombie Awareness", CurseForgeId = 223237, ModrinthId = "zombie-awareness", Description = "Smart zombies that hunt by sound", IsRequired = true },
            new() { SearchQuery = "Epic Siege Mod", CurseForgeId = 240617, Description = "Base sieges from zombies", IsRequired = true },
            new() { SearchQuery = "Weeping Angels", CurseForgeId = 236293, Description = "Terrifying weeping angels", IsRequired = true },
            new() { SearchQuery = "Stalker Creepers", CurseForgeId = 223548, Description = "Stalking creepers", IsRequired = true },
            new() { SearchQuery = "Mowzie's Mobs", CurseForgeId = 250498, ModrinthId = "mowzies-mobs", Description = "Terrifying boss mobs", IsRequired = false },

            // === GUNS & COMBAT ===
            new() { SearchQuery = "MrCrayfish's Gun Mod", CurseForgeId = 289479, Description = "Modern firearms", IsRequired = true },
            new() { SearchQuery = "Timeless and Classics Guns", CurseForgeId = 450281, ModrinthId = "timeless-and-classics", Description = "Classic weapons", IsRequired = true },
            new() { SearchQuery = "Better Combat", CurseForgeId = 639842, ModrinthId = "better-combat", Description = "Enhanced melee combat", IsRequired = true },
            new() { SearchQuery = "SecurityCraft", CurseForgeId = 222043, ModrinthId = "security-craft", Description = "Defensive turrets and security", IsRequired = false },

            // === SURVIVAL ===
            new() { SearchQuery = "Tough As Nails", CurseForgeId = 238676, ModrinthId = "tough-as-nails", Description = "Thirst and temperature", IsRequired = true },
            new() { SearchQuery = "First Aid", CurseForgeId = 319818, Description = "Body part damage", IsRequired = true },
            new() { SearchQuery = "Corpse", CurseForgeId = 316582, ModrinthId = "corpse", Description = "Keep items on death", IsRequired = false },

            // === WORLD & ATMOSPHERE ===
            new() { SearchQuery = "Lost Cities", CurseForgeId = 269024, Description = "Ruined city worldgen", IsRequired = true },
            new() { SearchQuery = "Blood Moon", CurseForgeId = 242784, Description = "Dangerous blood moon events", IsRequired = true },
            new() { SearchQuery = "Dynamic Surroundings", CurseForgeId = 238891, Description = "Atmospheric sounds and effects", IsRequired = false },
            new() { SearchQuery = "True Darkness", CurseForgeId = 402477, ModrinthId = "true-darkness", Description = "Complete darkness at night", IsRequired = false },

            // === REQUIRED LIBRARIES ===
            new() { SearchQuery = "CoroUtil", CurseForgeId = 237256, Description = "Library for zombie mods", IsRequired = true },
            new() { SearchQuery = "Framework", CurseForgeId = 549225, Description = "Library for MrCrayfish mods", IsRequired = true },
            new() { SearchQuery = "playerAnimator", CurseForgeId = 658587, ModrinthId = "playeranimator", Description = "Animation library", IsRequired = true },
            new() { SearchQuery = "GeckoLib", CurseForgeId = 388172, ModrinthId = "geckolib", Description = "Animation library for mobs", IsRequired = true },
        }
    };

    // ===========================================
    // PERFORMANCE & QUALITY PACK
    // ===========================================
    public static ModPack PerformancePack => new()
    {
        Id = "performance",
        Name = "Performance & Optimization",
        Description = "Essential mods to improve FPS and reduce lag on servers",
        Category = "Utility",
        IconPath = "/Assets/Images/performance_pack.png",
        RecommendedMinecraftVersion = "1.20.1",
        Mods = new List<ModPackItem>
        {
            // Performance Mods - Verified
            new() { SearchQuery = "Embeddium", CurseForgeId = 908741, ModrinthId = "embeddium", Description = "Forge port of Sodium for better FPS", IsRequired = true },
            new() { SearchQuery = "FerriteCore", CurseForgeId = 429235, ModrinthId = "ferrite-core", Description = "Reduces memory usage", IsRequired = true },
            new() { SearchQuery = "ModernFix", CurseForgeId = 790626, ModrinthId = "modernfix", Description = "Fixes performance issues", IsRequired = true },
            new() { SearchQuery = "Entity Culling", CurseForgeId = 448233, ModrinthId = "entityculling", Description = "Skip rendering hidden entities", IsRequired = true },
            new() { SearchQuery = "Clumps", CurseForgeId = 256717, ModrinthId = "clumps", Description = "Groups XP orbs together", IsRequired = true },

            // Utility
            new() { SearchQuery = "JEI", CurseForgeId = 238222, ModrinthId = "jei", Description = "Just Enough Items - Recipe viewer", IsRequired = true },
            new() { SearchQuery = "Jade", CurseForgeId = 324717, ModrinthId = "jade", Description = "What block am I looking at", IsRequired = true },
            new() { SearchQuery = "Controlling", CurseForgeId = 250398, ModrinthId = "controlling", Description = "Search keybindings", IsRequired = false },
        }
    };

    // ===========================================
    // ALL PACKS LIST
    // ===========================================
    public static List<ModPack> AllPacks => new()
    {
        ZombieSurvival,
        GunsAndWeapons,
        HorrorCraft,
        UltimateApocalypse,
        PerformancePack
    };
}
