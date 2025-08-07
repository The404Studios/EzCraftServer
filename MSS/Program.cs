using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

// created by fourzerofour

namespace MSS // Fixed namespace to match folder structure
{
    class Program
    {
        #region Configuration Variables
        // Core server configuration
        private static string serverIP = ""; // Will be set by user
        private static string targetVersion = "1.21.8"; // Can be changed by user
        private static readonly string fallbackVersion = "1.20.6"; // Fallback if target version doesn't exist
        private static int maxPlayers = 100; // Default to 100, can be changed by user
        private static string serverRootFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "MinecraftServer"); // Can be changed by user
        private static string serverName = "My Minecraft Server"; // Server name for MOTD
        private static string operatorUsername = ""; // Minecraft username to make operator
        private static int serverPort = 25565; // Default Minecraft port
        private static string gameMode = "survival"; // Default game mode
        private static string difficulty = "normal"; // Default difficulty
        private static bool enablePvP = true; // PvP enabled by default
        private static bool enableWhitelist = false; // Whitelist disabled by default
        private static int viewDistance = 10; // Default view distance

        // Subfolder structure
        private static string serverFolder = string.Empty;
        private static string modsFolder = string.Empty;
        private static string configFolder = string.Empty;
        private static string worldFolder = string.Empty;
        private static string backupsFolder = string.Empty;
        private static string librariesFolder = string.Empty;
        private static string scriptsFolder = string.Empty;
        private static string logsFolder = string.Empty;
        private static string cachesFolder = string.Empty;

        // Server resources
        private static int serverRamGB = 4; // Default RAM, will be calculated based on player count

        // File paths
        private static string? logFilePath;
        private static string serverPropertiesPath = string.Empty;
        private static string eulaPath = string.Empty;
        private static string forgeInstallerPath = string.Empty;
        private static string serverJarPath = string.Empty;

        // State variables
        private static string actualMinecraftVersion = string.Empty;
        private static string forgeVersion = string.Empty;
        private static int javaVersion = 0;
        private static readonly HttpClientHandler handler = new() { AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate };
        private static readonly HttpClient httpClient = new(handler) { Timeout = TimeSpan.FromMinutes(10) };
        private static readonly CancellationTokenSource globalCts = new();
        private static readonly int maxConcurrentDownloads = 5;
        private static readonly SemaphoreSlim downloadSemaphore = new(maxConcurrentDownloads);
        private static readonly object consoleLock = new();
        private static readonly Dictionary<string, ModInfo> modsList = [];
        private static readonly List<Dependency> modDependencies = [];

        // Cached JsonSerializerOptions
        private static readonly JsonSerializerOptions jsonOptions = new() { PropertyNameCaseInsensitive = true };

        // Compiled regex patterns
        private static readonly Regex JavaVersionRegex = new(@"version ""(\d+)\.(\d+)", RegexOptions.Compiled);
        private static readonly Regex ModVersionRegex = new(@"(?:\d+\.\d+(?:\.\d+)?)-(?:\d+\.\d+(?:\.\d+)?)", RegexOptions.Compiled);
        private static readonly Regex SimpleVersionRegex = new(@"\d+\.\d+\.\d+", RegexOptions.Compiled);

        // Requested mods
        private static readonly List<string> requestedMods =
        [
            "Sons Of Sins",
            "Stalker Creepers",
            "Scary Mobs And Bosses",
            "Doctor Who - Weeping Angels",
            "Grue",
            "EnhancedVisuals",
            "Horror Elements",
            "Mutationcraft",
            "The Spelunker's Charm",
            "IntoTheVoid",
            "Guns",
            "Galacticraft"
        ];

        // Cursed Forge API
        private static readonly string CF_API_BASE = "https://api.curseforge.com/v1";
        private static readonly string CF_API_KEY = "$2a$10$6Y15zxWaENIAeUySYwtK0uM5STstUVkvvqmwZl1xIBxdWxVd3YJ4W"; // Public API key
        private static readonly int MINECRAFT_GAME_ID = 432;

        // Logging levels
        private enum LogLevel { INFO, WARNING, ERROR, SUCCESS, DEBUG }
        #endregion

        #region Models
        private class ModInfo
        {
            public required string Name { get; set; }
            public required string SlugName { get; set; }
            public required string Description { get; set; }
            public required string DownloadUrl { get; set; }
            public required string Version { get; set; }
            public long FileId { get; set; }
            public long ModId { get; set; }
            public long FileSize { get; set; }
            public DateTime ReleaseDate { get; set; }
            public required string FileName { get; set; }
            public List<Dependency> Dependencies { get; set; } = [];
            public required string LocalFilePath { get; set; }
            public bool IsDownloaded { get; set; } = false;
        }

        private class Dependency
        {
            public long ModId { get; set; }
            public required string RelationType { get; set; } // REQUIRED, OPTIONAL, etc.
            public required string ModName { get; set; }
            public string? DownloadUrl { get; set; }
            public string? FileName { get; set; }
        }

        private class CFSearchResult
        {
            public required CFPagination Pagination { get; set; }
            public required List<CFMod> Data { get; set; }
        }

        private class CFPagination
        {
            public int Index { get; set; }
            public int PageSize { get; set; }
            public int ResultCount { get; set; }
            public int TotalCount { get; set; }
        }

        private class CFMod
        {
            public long Id { get; set; }
            public required string Name { get; set; }
            public required string Slug { get; set; }
            public required string Summary { get; set; }
            public long MainFileId { get; set; }
            public required List<CFFile> LatestFiles { get; set; }
            public required CFLinks Links { get; set; }
        }

        private class CFLinks
        {
            public required string WebsiteUrl { get; set; }
            public string? WikiUrl { get; set; }
            public string? IssuesUrl { get; set; }
            public string? SourceUrl { get; set; }
        }

        private class CFFile
        {
            public long Id { get; set; }
            public required string FileName { get; set; }
            public required string DisplayName { get; set; }
            public DateTime FileDate { get; set; }
            public long FileLength { get; set; }
            public required string DownloadUrl { get; set; }
            public required List<CFFileDependency> Dependencies { get; set; }
            public required List<CFSortableGameVersion> SortableGameVersions { get; set; }
        }

        private class CFFileDependency
        {
            public long ModId { get; set; }
            public int RelationType { get; set; }
        }

        private class CFSortableGameVersion
        {
            public required string GameVersion { get; set; }
            public string? GameVersionName { get; set; }
            public string? GameVersionPadded { get; set; }
        }

        private class ForgeVersion
        {
            public required string MinecraftVersion { get; set; }
            public required string LatestVersion { get; set; }
            public string? RecommendedVersion { get; set; }
            public required string DownloadUrl { get; set; }
        }

        private class MinecraftVersionInfo
        {
            public required string Id { get; set; }
            public required string Type { get; set; }
            public required string Url { get; set; }
            public DateTime Time { get; set; }
            public DateTime ReleaseTime { get; set; }
        }

        private class MinecraftVersionManifest
        {
            public required MinecraftLatest Latest { get; set; }
            public required List<MinecraftVersionInfo> Versions { get; set; }
        }

        private class MinecraftLatest
        {
            public required string Release { get; set; }
            public required string Snapshot { get; set; }
        }
        #endregion

        static async Task Main()
        {
            Console.Title = "Minecraft Server Setup Utility - Advanced Edition";
            Console.OutputEncoding = Encoding.UTF8; // Ensure proper character display

            // Initialize HttpClient with appropriate headers
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            httpClient.DefaultRequestHeaders.Add("User-Agent", "MinecraftServerSetup/1.0");
            httpClient.DefaultRequestHeaders.Add("x-api-key", CF_API_KEY);

            try
            {
                // Display welcome and configuration menu
                DisplayInitialWelcome();

                // Run configuration menu
                bool continueSetup = await RunConfigurationMenu();

                if (!continueSetup)
                {
                    Log("Setup cancelled by user.", LogLevel.INFO);
                    return;
                }

                // Display configured settings
                DisplayWelcomeBanner();

                // Initialize logging
                InitializeLogging();
                Log("Starting server setup process...", LogLevel.INFO);

                // Create server directory structure
                CreateServerDirectoryStructure();

                // Check prerequisites
                await CheckPrerequisites();

                // Check Minecraft version
                await CheckAndResolveMinecraftVersion();

                // Download and install Forge
                await DownloadAndInstallForge();

                // Configure server
                ConfigureServer();

                // Search for and download requested mods
                await DownloadAllRequestedMods();

                // Create startup scripts
                CreateStartupScripts();

                // Perform post-setup optimizations
                PerformServerOptimizations();

                // Display completion message
                DisplayCompletionBanner();
            }
            catch (OperationCanceledException)
            {
                Log("Operation was canceled.", LogLevel.ERROR);
            }
            catch (Exception ex)
            {
                Log($"A critical error occurred: {ex.Message}", LogLevel.ERROR);
                Log(ex.StackTrace ?? "No stack trace available", LogLevel.DEBUG);

                // Create error log file
                string errorLogPath = Path.Combine(serverRootFolder, "SETUP_ERROR.log");
                File.WriteAllText(errorLogPath, $"Error: {ex.Message}\n\nStack Trace:\n{ex.StackTrace}");
                Log($"Error details have been saved to: {errorLogPath}", LogLevel.INFO);
            }
            finally
            {
                Log("Press any key to exit...", LogLevel.INFO);
                Console.ReadKey(true);
            }
        }

        #region UI Methods
        private static void DisplayInitialWelcome()
        {
            Console.Clear();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(@"
╔══════════════════════════════════════════════════════════════════════════╗
║                                                                          ║
║   █▀▄▀█ █ █▄░█ █▀▀ █▀▀ █▀█ ▄▀█ █▀▀ ▀█▀   █▀ █▀▀ █▀█ █░█ █▀▀ █▀█        ║
║   █░▀░█ █ █░▀█ ██▄ █▄▄ █▀▄ █▀█ █▀░ ░█░   ▄█ ██▄ █▀▄ ▀▄▀ ██▄ █▀▄        ║
║                                                                          ║
║                    SERVER CONFIGURATION WIZARD                           ║
║                                                                          ║
╚══════════════════════════════════════════════════════════════════════════╝");
            Console.ResetColor();
            Console.WriteLine();
            Console.WriteLine("Welcome to the Minecraft Server Setup Utility!");
            Console.WriteLine("Let's configure your server settings before we begin.");
            Console.WriteLine();
        }

        private static async Task<bool> RunConfigurationMenu()
        {
            bool configuring = true;

            // Load default or saved configuration
            LoadConfiguration();

            while (configuring)
            {
                Console.Clear();
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("═══════════════════════════════════════════════════════════════");
                Console.WriteLine("                    SERVER CONFIGURATION MENU                  ");
                Console.WriteLine("═══════════════════════════════════════════════════════════════");
                Console.ResetColor();
                Console.WriteLine();

                // Display current settings
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("Current Settings:");
                Console.ResetColor();
                Console.WriteLine($"  1. Server Name:        {(string.IsNullOrEmpty(serverName) ? "[Not Set]" : serverName)}");
                Console.WriteLine($"  2. Server IP:          {(string.IsNullOrEmpty(serverIP) ? "[Auto-detect]" : serverIP)}");
                Console.WriteLine($"  3. Server Port:        {serverPort}");
                Console.WriteLine($"  4. Max Players:        {maxPlayers}");
                Console.WriteLine($"  5. Server RAM:         {serverRamGB} GB");
                Console.WriteLine($"  6. Operator Username:  {(string.IsNullOrEmpty(operatorUsername) ? "[Not Set]" : operatorUsername)}");
                Console.WriteLine($"  7. Game Mode:          {gameMode}");
                Console.WriteLine($"  8. Difficulty:         {difficulty}");
                Console.WriteLine($"  9. Enable PvP:         {(enablePvP ? "Yes" : "No")}");
                Console.WriteLine($" 10. Enable Whitelist:   {(enableWhitelist ? "Yes" : "No")}");
                Console.WriteLine($" 11. View Distance:      {viewDistance} chunks");
                Console.WriteLine($" 12. Install Location:   {serverRootFolder}");
                Console.WriteLine($" 13. Minecraft Version:  {targetVersion}");
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine(" S. Save & Start Setup");
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine(" D. Use Default Settings");
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(" Q. Quit Setup");
                Console.ResetColor();
                Console.WriteLine();
                Console.Write("Enter your choice (1-13, S, D, or Q): ");

                string? choice = Console.ReadLine()?.Trim().ToUpper();

                switch (choice)
                {
                    case "1":
                        ConfigureServerName();
                        break;
                    case "2":
                        ConfigureServerIP();
                        break;
                    case "3":
                        ConfigureServerPort();
                        break;
                    case "4":
                        ConfigureMaxPlayers();
                        break;
                    case "5":
                        ConfigureServerRAM();
                        break;
                    case "6":
                        ConfigureOperator();
                        break;
                    case "7":
                        ConfigureGameMode();
                        break;
                    case "8":
                        ConfigureDifficulty();
                        break;
                    case "9":
                        enablePvP = !enablePvP;
                        break;
                    case "10":
                        enableWhitelist = !enableWhitelist;
                        break;
                    case "11":
                        ConfigureViewDistance();
                        break;
                    case "12":
                        ConfigureInstallLocation();
                        break;
                    case "13":
                        ConfigureMinecraftVersion();
                        break;
                    case "S":
                        SaveConfiguration();
                        return true;
                    case "D":
                        UseDefaultSettings();
                        return true;
                    case "Q":
                        return false;
                    default:
                        Console.WriteLine("Invalid choice. Press any key to continue...");
                        Console.ReadKey(true);
                        break;
                }
            }

            return true;
        }

        private static void ConfigureServerName()
        {
            Console.Clear();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("SERVER NAME CONFIGURATION");
            Console.WriteLine("═════════════════════════");
            Console.ResetColor();
            Console.WriteLine();
            Console.WriteLine("Enter a name for your server (this will appear in the server list):");
            Console.WriteLine($"Current: {serverName}");
            Console.Write("New name (or press Enter to keep current): ");

            string? input = Console.ReadLine()?.Trim();
            if (!string.IsNullOrEmpty(input))
            {
                serverName = input;
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"Server name set to: {serverName}");
                Console.ResetColor();
            }

            Console.WriteLine("\nPress any key to continue...");
            Console.ReadKey(true);
        }

        private static void ConfigureServerIP()
        {
            Console.Clear();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("SERVER IP CONFIGURATION");
            Console.WriteLine("═══════════════════════");
            Console.ResetColor();
            Console.WriteLine();
            Console.WriteLine("Enter the IP address for your server:");
            Console.WriteLine("(Leave blank to auto-detect or use 0.0.0.0 for all interfaces)");
            Console.WriteLine();
            Console.WriteLine("Examples:");
            Console.WriteLine("  - Leave blank for auto-detect (recommended for most users)");
            Console.WriteLine("  - 0.0.0.0 to listen on all network interfaces");
            Console.WriteLine("  - Your public IP (e.g., 123.456.789.0) for dedicated servers");
            Console.WriteLine("  - 127.0.0.1 for localhost only");
            Console.WriteLine();
            Console.WriteLine($"Current: {(string.IsNullOrEmpty(serverIP) ? "[Auto-detect]" : serverIP)}");
            Console.Write("New IP (or press Enter for auto-detect): ");

            string? input = Console.ReadLine()?.Trim();
            if (input != null)
            {
                serverIP = input;
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"Server IP set to: {(string.IsNullOrEmpty(serverIP) ? "[Auto-detect]" : serverIP)}");
                Console.ResetColor();
            }

            Console.WriteLine("\nPress any key to continue...");
            Console.ReadKey(true);
        }

        private static void ConfigureServerPort()
        {
            Console.Clear();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("SERVER PORT CONFIGURATION");
            Console.WriteLine("═════════════════════════");
            Console.ResetColor();
            Console.WriteLine();
            Console.WriteLine("Enter the port for your server (default is 25565):");
            Console.WriteLine($"Current: {serverPort}");
            Console.Write("New port (1-65535): ");

            if (int.TryParse(Console.ReadLine(), out int port) && port > 0 && port <= 65535)
            {
                serverPort = port;
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"Server port set to: {serverPort}");
                Console.ResetColor();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Invalid port number. Keeping current setting.");
                Console.ResetColor();
            }

            Console.WriteLine("\nPress any key to continue...");
            Console.ReadKey(true);
        }

        private static void ConfigureMaxPlayers()
        {
            Console.Clear();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("MAX PLAYERS CONFIGURATION");
            Console.WriteLine("═════════════════════════");
            Console.ResetColor();
            Console.WriteLine();
            Console.WriteLine("Enter the maximum number of players:");
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("Recommended settings:");
            Console.WriteLine("  1-10 players:   2GB RAM");
            Console.WriteLine("  10-20 players:  4GB RAM");
            Console.WriteLine("  20-50 players:  6GB RAM");
            Console.WriteLine("  50-100 players: 8GB RAM");
            Console.WriteLine("  100+ players:   12GB+ RAM");
            Console.ResetColor();
            Console.WriteLine();
            Console.WriteLine($"Current: {maxPlayers}");
            Console.Write("New max players (1-1000): ");

            if (int.TryParse(Console.ReadLine(), out int players) && players > 0 && players <= 1000)
            {
                maxPlayers = players;

                // Auto-adjust RAM based on player count
                if (players <= 10)
                    serverRamGB = 2;
                else if (players <= 20)
                    serverRamGB = 4;
                else if (players <= 50)
                    serverRamGB = 6;
                else if (players <= 100)
                    serverRamGB = 8;
                else if (players <= 200)
                    serverRamGB = 12;
                else
                    serverRamGB = 16;

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"Max players set to: {maxPlayers}");
                Console.WriteLine($"RAM automatically adjusted to: {serverRamGB}GB");
                Console.ResetColor();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Invalid number. Keeping current setting.");
                Console.ResetColor();
            }

            Console.WriteLine("\nPress any key to continue...");
            Console.ReadKey(true);
        }

        private static void ConfigureServerRAM()
        {
            Console.Clear();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("SERVER RAM CONFIGURATION");
            Console.WriteLine("════════════════════════");
            Console.ResetColor();
            Console.WriteLine();
            Console.WriteLine("Enter the amount of RAM to allocate (in GB):");
            Console.WriteLine($"Current: {serverRamGB}GB");
            Console.WriteLine($"Recommended for {maxPlayers} players: {CalculateRecommendedRAM()}GB");
            Console.Write("New RAM allocation (1-64 GB): ");

            if (int.TryParse(Console.ReadLine(), out int ram) && ram > 0 && ram <= 64)
            {
                serverRamGB = ram;
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"Server RAM set to: {serverRamGB}GB");
                Console.ResetColor();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Invalid amount. Keeping current setting.");
                Console.ResetColor();
            }

            Console.WriteLine("\nPress any key to continue...");
            Console.ReadKey(true);
        }

        private static int CalculateRecommendedRAM()
        {
            if (maxPlayers <= 10) return 2;
            if (maxPlayers <= 20) return 4;
            if (maxPlayers <= 50) return 6;
            if (maxPlayers <= 100) return 8;
            if (maxPlayers <= 200) return 12;
            return 16;
        }

        private static void ConfigureOperator()
        {
            Console.Clear();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("OPERATOR CONFIGURATION");
            Console.WriteLine("══════════════════════");
            Console.ResetColor();
            Console.WriteLine();
            Console.WriteLine("Enter your Minecraft username to make yourself a server operator:");
            Console.WriteLine("(Operators have full administrative privileges)");
            Console.WriteLine();
            Console.WriteLine($"Current: {(string.IsNullOrEmpty(operatorUsername) ? "[Not Set]" : operatorUsername)}");
            Console.Write("Minecraft username (or press Enter to skip): ");

            string? input = Console.ReadLine()?.Trim();
            if (!string.IsNullOrEmpty(input))
            {
                operatorUsername = input;
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"Operator set to: {operatorUsername}");
                Console.WriteLine("You will have full admin privileges when you join the server!");
                Console.ResetColor();
            }
            else if (input == "")
            {
                operatorUsername = "";
                Console.WriteLine("No operator will be set.");
            }

            Console.WriteLine("\nPress any key to continue...");
            Console.ReadKey(true);
        }

        private static void ConfigureGameMode()
        {
            Console.Clear();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("GAME MODE CONFIGURATION");
            Console.WriteLine("═══════════════════════");
            Console.ResetColor();
            Console.WriteLine();
            Console.WriteLine("Select the default game mode:");
            Console.WriteLine("  1. Survival");
            Console.WriteLine("  2. Creative");
            Console.WriteLine("  3. Adventure");
            Console.WriteLine("  4. Spectator");
            Console.WriteLine();
            Console.WriteLine($"Current: {gameMode}");
            Console.Write("Choose (1-4): ");

            string? choice = Console.ReadLine()?.Trim();
            switch (choice)
            {
                case "1":
                    gameMode = "survival";
                    break;
                case "2":
                    gameMode = "creative";
                    break;
                case "3":
                    gameMode = "adventure";
                    break;
                case "4":
                    gameMode = "spectator";
                    break;
                default:
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Invalid choice. Keeping current setting.");
                    Console.ResetColor();
                    Console.WriteLine("\nPress any key to continue...");
                    Console.ReadKey(true);
                    return;
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"Game mode set to: {gameMode}");
            Console.ResetColor();
            Console.WriteLine("\nPress any key to continue...");
            Console.ReadKey(true);
        }

        private static void ConfigureDifficulty()
        {
            Console.Clear();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("DIFFICULTY CONFIGURATION");
            Console.WriteLine("════════════════════════");
            Console.ResetColor();
            Console.WriteLine();
            Console.WriteLine("Select the difficulty level:");
            Console.WriteLine("  1. Peaceful (no hostile mobs)");
            Console.WriteLine("  2. Easy");
            Console.WriteLine("  3. Normal");
            Console.WriteLine("  4. Hard");
            Console.WriteLine();
            Console.WriteLine($"Current: {difficulty}");
            Console.Write("Choose (1-4): ");

            string? choice = Console.ReadLine()?.Trim();
            switch (choice)
            {
                case "1":
                    difficulty = "peaceful";
                    break;
                case "2":
                    difficulty = "easy";
                    break;
                case "3":
                    difficulty = "normal";
                    break;
                case "4":
                    difficulty = "hard";
                    break;
                default:
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Invalid choice. Keeping current setting.");
                    Console.ResetColor();
                    Console.WriteLine("\nPress any key to continue...");
                    Console.ReadKey(true);
                    return;
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"Difficulty set to: {difficulty}");
            Console.ResetColor();
            Console.WriteLine("\nPress any key to continue...");
            Console.ReadKey(true);
        }

        private static void ConfigureViewDistance()
        {
            Console.Clear();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("VIEW DISTANCE CONFIGURATION");
            Console.WriteLine("═══════════════════════════");
            Console.ResetColor();
            Console.WriteLine();
            Console.WriteLine("Enter the view distance in chunks (2-32):");
            Console.WriteLine("Lower values improve performance but reduce visible distance.");
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("Recommendations:");
            Console.WriteLine("  2-4:  Very low (best performance)");
            Console.WriteLine("  6-8:  Low (good for many players)");
            Console.WriteLine("  10:   Default");
            Console.WriteLine("  12-16: High (needs good hardware)");
            Console.WriteLine("  20+:  Very high (single player or powerful servers)");
            Console.ResetColor();
            Console.WriteLine();
            Console.WriteLine($"Current: {viewDistance}");
            Console.Write("New view distance (2-32): ");

            if (int.TryParse(Console.ReadLine(), out int distance) && distance >= 2 && distance <= 32)
            {
                viewDistance = distance;
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"View distance set to: {viewDistance} chunks");
                Console.ResetColor();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Invalid distance. Keeping current setting.");
                Console.ResetColor();
            }

            Console.WriteLine("\nPress any key to continue...");
            Console.ReadKey(true);
        }

        private static void ConfigureInstallLocation()
        {
            Console.Clear();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("INSTALL LOCATION CONFIGURATION");
            Console.WriteLine("═══════════════════════════════");
            Console.ResetColor();
            Console.WriteLine();
            Console.WriteLine("Enter the folder path where the server should be installed:");
            Console.WriteLine($"Current: {serverRootFolder}");
            Console.WriteLine();
            Console.WriteLine("Examples:");
            Console.WriteLine(@"  C:\MinecraftServer");
            Console.WriteLine(@"  D:\Games\Minecraft");
            Console.WriteLine(@"  C:\Users\YourName\Desktop\MinecraftServer");
            Console.WriteLine();
            Console.Write("New path (or press Enter to keep current): ");

            string? input = Console.ReadLine()?.Trim();
            if (!string.IsNullOrEmpty(input))
            {
                if (Directory.Exists(Path.GetDirectoryName(input)) || Path.GetDirectoryName(input) == null)
                {
                    serverRootFolder = input;
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"Install location set to: {serverRootFolder}");
                    Console.ResetColor();
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Invalid path. Keeping current setting.");
                    Console.ResetColor();
                }
            }

            Console.WriteLine("\nPress any key to continue...");
            Console.ReadKey(true);
        }

        private static void ConfigureMinecraftVersion()
        {
            Console.Clear();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("MINECRAFT VERSION CONFIGURATION");
            Console.WriteLine("════════════════════════════════");
            Console.ResetColor();
            Console.WriteLine();
            Console.WriteLine("Enter the Minecraft version you want to use:");
            Console.WriteLine($"Current: {targetVersion}");
            Console.WriteLine();
            Console.WriteLine("Popular versions:");
            Console.WriteLine("  1.21.4 - Latest release");
            Console.WriteLine("  1.20.6 - Stable with many mods");
            Console.WriteLine("  1.19.4 - Good mod compatibility");
            Console.WriteLine("  1.18.2 - Extensive mod support");
            Console.WriteLine("  1.16.5 - Classic modded version");
            Console.WriteLine();
            Console.Write("Version (e.g., 1.20.6): ");

            string? input = Console.ReadLine()?.Trim();
            if (!string.IsNullOrEmpty(input) && System.Text.RegularExpressions.Regex.IsMatch(input, @"^\d+\.\d+(\.\d+)?$"))
            {
                targetVersion = input;
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"Minecraft version set to: {targetVersion}");
                Console.ResetColor();
            }
            else if (!string.IsNullOrEmpty(input))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Invalid version format. Keeping current setting.");
                Console.ResetColor();
            }

            Console.WriteLine("\nPress any key to continue...");
            Console.ReadKey(true);
        }

        private static void LoadConfiguration()
        {
            // Try to load saved configuration from a config file if it exists
            string configPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MinecraftServerSetup", "config.json");

            if (File.Exists(configPath))
            {
                try
                {
                    string json = File.ReadAllText(configPath);
                    var config = JsonSerializer.Deserialize<ServerConfig>(json, jsonOptions);
                    if (config != null)
                    {
                        serverName = config.ServerName ?? serverName;
                        serverIP = config.ServerIP ?? serverIP;
                        serverPort = config.ServerPort;
                        maxPlayers = config.MaxPlayers;
                        serverRamGB = config.ServerRamGB;
                        operatorUsername = config.OperatorUsername ?? operatorUsername;
                        gameMode = config.GameMode ?? gameMode;
                        difficulty = config.Difficulty ?? difficulty;
                        enablePvP = config.EnablePvP;
                        enableWhitelist = config.EnableWhitelist;
                        viewDistance = config.ViewDistance;
                        serverRootFolder = config.ServerRootFolder ?? serverRootFolder;
                        targetVersion = config.TargetVersion ?? targetVersion;
                    }
                }
                catch
                {
                    // Ignore errors loading config
                }
            }
        }

        private static void SaveConfiguration()
        {
            // Save configuration for future use
            try
            {
                string configDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MinecraftServerSetup");
                Directory.CreateDirectory(configDir);
                string configPath = Path.Combine(configDir, "config.json");

                var config = new ServerConfig
                {
                    ServerName = serverName,
                    ServerIP = serverIP,
                    ServerPort = serverPort,
                    MaxPlayers = maxPlayers,
                    ServerRamGB = serverRamGB,
                    OperatorUsername = operatorUsername,
                    GameMode = gameMode,
                    Difficulty = difficulty,
                    EnablePvP = enablePvP,
                    EnableWhitelist = enableWhitelist,
                    ViewDistance = viewDistance,
                    ServerRootFolder = serverRootFolder,
                    TargetVersion = targetVersion
                };

                string json = JsonSerializer.Serialize(config, jsonOptions);
                File.WriteAllText(configPath, json);
            }
            catch
            {
                // Ignore errors saving config
            }
        }

        private static void UseDefaultSettings()
        {
            serverName = "My Minecraft Server";
            serverIP = "";
            serverPort = 25565;
            maxPlayers = 20;
            serverRamGB = 4;
            operatorUsername = "";
            gameMode = "survival";
            difficulty = "normal";
            enablePvP = true;
            enableWhitelist = false;
            viewDistance = 10;
            targetVersion = "1.21.8";
        }

        private class ServerConfig
        {
            public string? ServerName { get; set; }
            public string? ServerIP { get; set; }
            public int ServerPort { get; set; }
            public int MaxPlayers { get; set; }
            public int ServerRamGB { get; set; }
            public string? OperatorUsername { get; set; }
            public string? GameMode { get; set; }
            public string? Difficulty { get; set; }
            public bool EnablePvP { get; set; }
            public bool EnableWhitelist { get; set; }
            public int ViewDistance { get; set; }
            public string? ServerRootFolder { get; set; }
            public string? TargetVersion { get; set; }
        }
        private static void DisplayWelcomeBanner()
        {
            Console.Clear();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(@"
╔══════════════════════════════════════════════════════════════════════════╗
║                                                                          ║
║   █▀▄▀█ █ █▄░█ █▀▀ █▀▀ █▀█ ▄▀█ █▀▀ ▀█▀   █▀ █▀▀ █▀█ █░█ █▀▀ █▀█        ║
║   █░▀░█ █ █░▀█ ██▄ █▄▄ █▀▄ █▀█ █▀░ ░█░   ▄█ ██▄ █▀▄ ▀▄▀ ██▄ █▀▄        ║
║                                                                          ║
║                       ADVANCED SETUP UTILITY                             ║
║                                                                          ║
╚══════════════════════════════════════════════════════════════════════════╝");
            Console.ResetColor();
            Console.WriteLine();
            Console.WriteLine($"Setting up '{serverName}' with the following configuration:");
            Console.WriteLine($"• Server IP:       {(string.IsNullOrEmpty(serverIP) ? "Auto-detect" : serverIP)}");
            Console.WriteLine($"• Server Port:     {serverPort}");
            Console.WriteLine($"• Target Version:  {targetVersion}");
            Console.WriteLine($"• Max Players:     {maxPlayers}");
            Console.WriteLine($"• RAM Allocation:  {serverRamGB}GB");
            Console.WriteLine($"• Game Mode:       {gameMode}");
            Console.WriteLine($"• Difficulty:      {difficulty}");
            Console.WriteLine($"• PvP Enabled:     {(enablePvP ? "Yes" : "No")}");
            Console.WriteLine($"• View Distance:   {viewDistance} chunks");
            Console.WriteLine($"• Installation:    {serverRootFolder}");
            if (!string.IsNullOrEmpty(operatorUsername))
            {
                Console.WriteLine($"• Operator:        {operatorUsername}");
            }
            Console.WriteLine();
            Console.WriteLine("Requested Mods:");
            foreach (var mod in requestedMods)
            {
                Console.WriteLine($"• {mod}");
            }
            Console.WriteLine();
            DrawProgressBar(0, "Initializing...");
        }

        private static void DisplayCompletionBanner()
        {
            Console.Clear();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(@"
╔══════════════════════════════════════════════════════════════════════════╗
║                                                                          ║
║    █▀ █▀▀ █▀█ █░█ █▀▀ █▀█   █▀ █▀▀ ▀█▀ █░█ █▀█   █▀▀ █▀█ █▀▄▀█ █▀█     ║
║    ▄█ ██▄ █▀▄ ▀▄▀ ██▄ █▀▄   ▄█ ██▄ ░█░ █▄█ █▀▀   █▄▄ █▄█ █░▀░█ █▀▀     ║
║                                                                          ║
║                              COMPLETE!                                   ║
║                                                                          ║
╚══════════════════════════════════════════════════════════════════════════╝");
            Console.ResetColor();
            Console.WriteLine();
            Console.WriteLine($"'{serverName}' has been successfully set up!");
            Console.WriteLine();
            Console.WriteLine($"Server Details:");
            Console.WriteLine($"• Server Location: {serverFolder}");
            Console.WriteLine($"• Minecraft Version: {actualMinecraftVersion}");
            Console.WriteLine($"• Forge Version: {forgeVersion}");
            Console.WriteLine($"• IP Address: {(string.IsNullOrEmpty(serverIP) ? "All interfaces (0.0.0.0)" : serverIP)}");
            Console.WriteLine($"• Port: {serverPort}");
            Console.WriteLine($"• Max Players: {maxPlayers}");
            Console.WriteLine($"• RAM Allocation: {serverRamGB}GB");
            Console.WriteLine($"• Game Mode: {gameMode}");
            Console.WriteLine($"• Difficulty: {difficulty}");
            Console.WriteLine($"• PvP: {(enablePvP ? "Enabled" : "Disabled")}");
            Console.WriteLine($"• Whitelist: {(enableWhitelist ? "Enabled" : "Disabled")}");
            if (!string.IsNullOrEmpty(operatorUsername))
            {
                Console.WriteLine($"• Server Operator: {operatorUsername}");
            }
            Console.WriteLine();
            Console.WriteLine($"Installed Mods: {modsList.Count} primary mods + {modDependencies.Count} dependencies");
            foreach (var mod in modsList.Values.Take(5))
            {
                Console.WriteLine($"• {mod.Name} ({mod.Version})");
            }
            if (modsList.Count > 5)
            {
                Console.WriteLine($"  ...and {modsList.Count - 5} more");
            }
            Console.WriteLine();
            Console.WriteLine("To start the server:");
            Console.WriteLine($"1. Navigate to: {serverFolder}");
            Console.WriteLine("2. Run 'start_server.bat' (Windows) or 'start_server.sh' (Linux/Mac)");
            Console.WriteLine();

            if (!string.IsNullOrEmpty(operatorUsername))
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"When you join the server as '{operatorUsername}', you'll have full admin privileges!");
                Console.ResetColor();
            }

            Console.WriteLine("First startup may take several minutes as the server generates the world.");
            Console.WriteLine();

            if (maxPlayers > 50)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"Note: A {maxPlayers}-player server requires significant hardware resources!");
                Console.WriteLine($"- Minimum {serverRamGB}GB RAM dedicated to the server");
                Console.WriteLine("- High-performance CPU");
                Console.WriteLine("- SSD storage recommended");
                Console.WriteLine("- Good network bandwidth");
                Console.ResetColor();
            }
        }

        private static void DrawProgressBar(int percent, string status)
        {
            lock (consoleLock)
            {
                Console.CursorVisible = false;
                int width = Console.WindowWidth - 10;
                int filledWidth = (int)Math.Round(width * percent / 100.0);

                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write("[");
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write(new string('■', filledWidth));
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write(new string('□', width - filledWidth));
                Console.Write($"] {percent,3}%");
                Console.WriteLine();
                Console.Write($"Status: ");
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write(status);
                Console.ResetColor();
                Console.WriteLine(new string(' ', Console.WindowWidth - status.Length - 9));
                Console.CursorTop -= 2;
                Console.CursorVisible = true;
            }
        }

        private static void Log(string message, LogLevel level = LogLevel.INFO)
        {
            lock (consoleLock)
            {
                // Save cursor position to return after logging
                int currentTop = Console.CursorTop;
                int currentLeft = Console.CursorLeft;

                // Move to bottom of console for logging
                Console.CursorTop = Console.WindowHeight - 3;
                Console.CursorLeft = 0;

                // Clear the current line
                Console.Write(new string(' ', Console.WindowWidth));
                Console.CursorLeft = 0;

                // Set color based on log level
                switch (level)
                {
                    case LogLevel.INFO:
                        Console.ForegroundColor = ConsoleColor.White;
                        break;
                    case LogLevel.WARNING:
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        break;
                    case LogLevel.ERROR:
                        Console.ForegroundColor = ConsoleColor.Red;
                        break;
                    case LogLevel.SUCCESS:
                        Console.ForegroundColor = ConsoleColor.Green;
                        break;
                    case LogLevel.DEBUG:
                        Console.ForegroundColor = ConsoleColor.Gray;
                        break;
                }

                // Write log message
                string timestamp = DateTime.Now.ToString("HH:mm:ss");
                string logPrefix = level == LogLevel.DEBUG ? "[DEBUG]" : level == LogLevel.ERROR ? "[ERROR]" :
                                   level == LogLevel.WARNING ? "[WARN] " : level == LogLevel.SUCCESS ? "[OK]   " : "[INFO] ";

                Console.Write($"[{timestamp}] {logPrefix} {message}");
                Console.ResetColor();

                // Write to log file
                if (logFilePath != null && level != LogLevel.DEBUG)
                {
                    try
                    {
                        File.AppendAllText(logFilePath, $"[{timestamp}] {logPrefix} {message}\n");
                    }
                    catch { /* Ignore log file write failures */ }
                }

                // Restore cursor position
                Console.CursorTop = currentTop;
                Console.CursorLeft = currentLeft;
            }
        }

        private static void InitializeLogging()
        {
            logsFolder = Path.Combine(serverRootFolder, "logs");
            Directory.CreateDirectory(logsFolder);

            string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            logFilePath = Path.Combine(logsFolder, $"setup_{timestamp}.log");

            File.WriteAllText(logFilePath, $"=== Minecraft Server Setup Log - {DateTime.Now} ===\n\n");
        }
        #endregion

        #region Server Setup Methods
        private static void CreateServerDirectoryStructure()
        {
            Log("Creating server directory structure...", LogLevel.INFO);
            DrawProgressBar(5, "Creating directory structure...");

            // Create server version-specific folder
            serverFolder = Path.Combine(serverRootFolder, $"minecraft_{targetVersion}");
            Directory.CreateDirectory(serverFolder);

            // Create all subdirectories
            modsFolder = Path.Combine(serverFolder, "mods");
            Directory.CreateDirectory(modsFolder);

            configFolder = Path.Combine(serverFolder, "config");
            Directory.CreateDirectory(configFolder);

            worldFolder = Path.Combine(serverFolder, "world");
            Directory.CreateDirectory(worldFolder);

            backupsFolder = Path.Combine(serverFolder, "backups");
            Directory.CreateDirectory(backupsFolder);

            librariesFolder = Path.Combine(serverFolder, "libraries");
            Directory.CreateDirectory(librariesFolder);

            scriptsFolder = Path.Combine(serverFolder, "scripts");
            Directory.CreateDirectory(scriptsFolder);

            cachesFolder = Path.Combine(serverFolder, "caches");
            Directory.CreateDirectory(cachesFolder);

            // Set file paths
            serverPropertiesPath = Path.Combine(serverFolder, "server.properties");
            eulaPath = Path.Combine(serverFolder, "eula.txt");
            forgeInstallerPath = Path.Combine(serverFolder, "forge-installer.jar");

            Log("Server directory structure created successfully", LogLevel.SUCCESS);
        }

        private static async Task CheckPrerequisites()
        {
            Log("Checking prerequisites...", LogLevel.INFO);
            DrawProgressBar(10, "Checking prerequisites...");

            // Check Java installation
            await CheckJavaInstallation();

            // Check internet connectivity
            await CheckInternetConnectivity();

            Log("Prerequisite checks completed", LogLevel.SUCCESS);
        }

        private static async Task CheckJavaInstallation()
        {
            Log("Checking Java installation...", LogLevel.INFO);

            try
            {
                // Create process to run 'java -version'
                ProcessStartInfo psi = new()
                {
                    FileName = "java",
                    Arguments = "-version",
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using Process process = new() { StartInfo = psi };
                process.Start();
                string output = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();

                if (process.ExitCode == 0)
                {
                    // Parse Java version
                    Match match = JavaVersionRegex.Match(output);
                    if (match.Success)
                    {
                        javaVersion = int.Parse(match.Groups[1].Value);
                        Log($"Java {javaVersion} detected", LogLevel.SUCCESS);

                        if (javaVersion < 17)
                        {
                            Log("Warning: Minecraft 1.18+ requires Java 17 or newer. Consider upgrading.", LogLevel.WARNING);
                        }
                    }
                    else
                    {
                        Log("Java detected, but version could not be determined", LogLevel.WARNING);
                    }
                }
            }
            catch
            {
                Log("Java not found. Please install Java 17 or newer.", LogLevel.ERROR);

                // Create instructions file
                string javaInstructionsPath = Path.Combine(serverRootFolder, "JAVA_INSTALLATION_INSTRUCTIONS.txt");
                File.WriteAllText(javaInstructionsPath,
                    "Java Installation Instructions\n" +
                    "============================\n\n" +
                    "Minecraft servers require Java to run. Please follow these steps:\n\n" +
                    "1. Download Java 17 (or newer) from:\n" +
                    "   https://adoptium.net/temurin/releases/\n\n" +
                    "2. Install Java following the installation wizard\n\n" +
                    "3. After installation, run this setup utility again\n"
                );

                Log($"Java installation instructions saved to: {javaInstructionsPath}", LogLevel.INFO);
            }
        }

        private static async Task CheckInternetConnectivity()
        {
            Log("Checking internet connectivity...", LogLevel.INFO);

            try
            {
                HttpResponseMessage response = await httpClient.GetAsync("https://api.minecraftforge.net/");
                if (response.IsSuccessStatusCode)
                {
                    Log("Internet connectivity confirmed", LogLevel.SUCCESS);
                }
                else
                {
                    Log($"Warning: Limited internet connectivity (Status: {response.StatusCode})", LogLevel.WARNING);
                }
            }
            catch (Exception ex)
            {
                Log($"Warning: Internet connectivity issues detected: {ex.Message}", LogLevel.WARNING);
                Log("The setup will continue, but some downloads may fail", LogLevel.INFO);
            }
        }

        private static async Task CheckAndResolveMinecraftVersion()
        {
            Log($"Verifying Minecraft version {targetVersion}...", LogLevel.INFO);
            DrawProgressBar(15, "Checking Minecraft version...");

            try
            {
                // Get Minecraft version manifest
                string manifestUrl = "https://launchermeta.mojang.com/mc/game/version_manifest.json";
                string manifestJson = await httpClient.GetStringAsync(manifestUrl);
                var manifest = JsonSerializer.Deserialize<MinecraftVersionManifest>(manifestJson, jsonOptions);

                // Find target version
                var versionInfo = manifest?.Versions.FirstOrDefault(v => v.Id == targetVersion);

                if (versionInfo != null)
                {
                    actualMinecraftVersion = targetVersion;
                    Log($"Target Minecraft version {targetVersion} found", LogLevel.SUCCESS);
                }
                else
                {
                    // Version not found, use fallback
                    Log($"Minecraft version {targetVersion} not found. Checking fallback version {fallbackVersion}...", LogLevel.WARNING);

                    versionInfo = manifest?.Versions.FirstOrDefault(v => v.Id == fallbackVersion);

                    if (versionInfo != null)
                    {
                        actualMinecraftVersion = fallbackVersion;
                        Log($"Using fallback Minecraft version {fallbackVersion}", LogLevel.SUCCESS);
                    }
                    else
                    {
                        // Neither target nor fallback found, use latest release
                        actualMinecraftVersion = manifest?.Latest.Release ?? targetVersion;
                        Log($"Fallback version not found either. Using latest release: {actualMinecraftVersion}", LogLevel.WARNING);
                    }
                }

                // Update server folder name to reflect actual version
                string newServerFolder = Path.Combine(serverRootFolder, $"minecraft_{actualMinecraftVersion}");
                if (serverFolder != newServerFolder)
                {
                    Directory.Move(serverFolder, newServerFolder);
                    serverFolder = newServerFolder;

                    // Update paths
                    modsFolder = Path.Combine(serverFolder, "mods");
                    configFolder = Path.Combine(serverFolder, "config");
                    worldFolder = Path.Combine(serverFolder, "world");
                    backupsFolder = Path.Combine(serverFolder, "backups");
                    librariesFolder = Path.Combine(serverFolder, "libraries");
                    scriptsFolder = Path.Combine(serverFolder, "scripts");

                    serverPropertiesPath = Path.Combine(serverFolder, "server.properties");
                    eulaPath = Path.Combine(serverFolder, "eula.txt");
                    forgeInstallerPath = Path.Combine(serverFolder, "forge-installer.jar");
                }

                Log($"Using Minecraft version: {actualMinecraftVersion}", LogLevel.INFO);
            }
            catch (Exception ex)
            {
                Log($"Error checking Minecraft version: {ex.Message}", LogLevel.ERROR);
                Log("Proceeding with target version without verification", LogLevel.WARNING);
                actualMinecraftVersion = targetVersion;
            }
        }

        private static async Task DownloadAndInstallForge()
        {
            Log("Finding appropriate Forge version...", LogLevel.INFO);
            DrawProgressBar(25, "Locating Forge version...");

            // Get Forge version information
            await FindForgeVersion();

            DrawProgressBar(30, "Downloading Forge installer...");

            // Download Forge installer
            bool forgeDownloaded = await DownloadForgeInstaller();

            if (forgeDownloaded)
            {
                DrawProgressBar(40, "Installing Forge server...");

                // Install Forge
                await InstallForgeServer();
            }
            else
            {
                Log("Forge installation skipped due to download failure", LogLevel.WARNING);
            }
        }

        private static async Task FindForgeVersion()
        {
            try
            {
                Log($"Finding latest Forge version for Minecraft {actualMinecraftVersion}...", LogLevel.INFO);

                string forgeApiUrl = $"https://maven.minecraftforge.net/net/minecraftforge/forge/maven-metadata.xml";

                // Download Forge version metadata
                string metadataXml = await httpClient.GetStringAsync(forgeApiUrl);

                XmlDocument xmlDoc = new();
                xmlDoc.LoadXml(metadataXml);

                // Parse available versions
                var versionNodes = xmlDoc.SelectNodes("//version");
                List<string> availableVersions = [];

                if (versionNodes != null)
                {
                    foreach (XmlNode node in versionNodes)
                    {
                        string version = node.InnerText;
                        if (version.StartsWith($"{actualMinecraftVersion}-"))
                        {
                            availableVersions.Add(version);
                        }
                    }
                }

                if (availableVersions.Count > 0)
                {
                    // Sort versions to find the latest
                    availableVersions.Sort((a, b) => {
                        // Extract the build numbers after the Minecraft version
                        string buildA = a[(actualMinecraftVersion.Length + 1)..];
                        string buildB = b[(actualMinecraftVersion.Length + 1)..];

                        // Compare by version segments
                        string[] segmentsA = buildA.Split('.');
                        string[] segmentsB = buildB.Split('.');

                        for (int i = 0; i < Math.Min(segmentsA.Length, segmentsB.Length); i++)
                        {
                            if (int.TryParse(segmentsA[i], out int numA) && int.TryParse(segmentsB[i], out int numB))
                            {
                                int comparison = numB.CompareTo(numA); // Descending order
                                if (comparison != 0)
                                    return comparison;
                            }
                            else
                            {
                                int comparison = string.Compare(segmentsB[i], segmentsA[i]); // Descending order
                                if (comparison != 0)
                                    return comparison;
                            }
                        }

                        return segmentsB.Length.CompareTo(segmentsA.Length); // Longer version is newer
                    });

                    // Use the latest version
                    forgeVersion = availableVersions.First();
                    Log($"Latest Forge version found: {forgeVersion}", LogLevel.SUCCESS);
                }
                else
                {
                    // If no specific version is found, use a placeholder
                    forgeVersion = $"{actualMinecraftVersion}-latest";
                    Log($"No specific Forge version found. Using generic identifier: {forgeVersion}", LogLevel.WARNING);
                }
            }
            catch (Exception ex)
            {
                Log($"Error finding Forge version: {ex.Message}", LogLevel.ERROR);
                forgeVersion = $"{actualMinecraftVersion}-latest";
                Log($"Using generic Forge identifier: {forgeVersion}", LogLevel.WARNING);
            }
        }

        private static async Task<bool> DownloadForgeInstaller()
        {
            try
            {
                Log($"Downloading Forge installer for version {forgeVersion}...", LogLevel.INFO);

                // Construct installer URL
                string installerUrl = $"https://maven.minecraftforge.net/net/minecraftforge/forge/{forgeVersion}/forge-{forgeVersion}-installer.jar";

                // Download the installer using HttpClient
                using var response = await httpClient.GetAsync(installerUrl, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                await using var contentStream = await response.Content.ReadAsStreamAsync();
                await using var fileStream = new FileStream(forgeInstallerPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

                var buffer = new byte[8192];
                var totalRead = 0L;
                int read;

                while ((read = await contentStream.ReadAsync(buffer.AsMemory(0, buffer.Length))) != 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, read));
                    totalRead += read;

                    if (totalBytes != -1)
                    {
                        var progressPercentage = (int)((totalRead * 100) / totalBytes);
                        Log($"Downloading Forge: {progressPercentage}% ({FormatFileSize(totalRead)}/{FormatFileSize(totalBytes)})", LogLevel.INFO);
                        DrawProgressBar(30 + (progressPercentage / 10), $"Downloading Forge: {progressPercentage}%");
                    }
                }

                Log("Forge installer downloaded successfully", LogLevel.SUCCESS);
                return true;
            }
            catch (Exception ex)
            {
                Log($"Error downloading Forge installer: {ex.Message}", LogLevel.ERROR);

                // Create instructions for manual download
                string instructionsPath = Path.Combine(serverFolder, "FORGE_INSTALL_INSTRUCTIONS.txt");
                File.WriteAllText(instructionsPath,
                    "Forge Installation Instructions\n" +
                    "============================\n\n" +
                    $"1. Download the Forge installer for Minecraft {actualMinecraftVersion} from:\n" +
                    $"   https://files.minecraftforge.net/net/minecraftforge/forge/index_{actualMinecraftVersion}.html\n\n" +
                    "2. Select 'Installer' to download the .jar file\n\n" +
                    $"3. Copy the downloaded .jar file to: {serverFolder}\n\n" +
                    "4. Run the installer with this command:\n" +
                    $"   java -jar forge-{actualMinecraftVersion}-XX.XX.X-installer.jar --installServer\n\n" +
                    "   (Replace XX.XX.X with the actual version number from the file you downloaded)\n\n" +
                    "5. Accept the Minecraft EULA by creating a file named 'eula.txt' with the content:\n" +
                    "   eula=true\n"
                );

                Log($"Manual installation instructions saved to: {instructionsPath}", LogLevel.INFO);
                return false;
            }
        }

        private static async Task InstallForgeServer()
        {
            if (!File.Exists(forgeInstallerPath))
            {
                Log("Forge installer not found. Skipping installation.", LogLevel.WARNING);
                return;
            }

            Log("Installing Forge server...", LogLevel.INFO);

            try
            {
                // Create process to run the Forge installer
                ProcessStartInfo psi = new()
                {
                    FileName = "java",
                    Arguments = $"-jar \"{forgeInstallerPath}\" --installServer",
                    WorkingDirectory = serverFolder,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using Process process = new() { StartInfo = psi };
                process.OutputDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        Log($"Forge: {e.Data}", LogLevel.DEBUG);

                        // Update progress for specific milestones
                        if (e.Data.Contains("Extracting"))
                            DrawProgressBar(32, "Extracting Forge files...");
                        else if (e.Data.Contains("Downloading"))
                            DrawProgressBar(35, "Downloading Forge dependencies...");
                        else if (e.Data.Contains("Installing"))
                            DrawProgressBar(38, "Installing Forge...");
                    }
                };

                process.ErrorDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        Log($"Forge Error: {e.Data}", LogLevel.WARNING);
                    }
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                // Wait up to 10 minutes for installation to complete
                bool completed = await WaitForProcessWithTimeout(process, TimeSpan.FromMinutes(10));

                if (!completed)
                {
                    Log("Forge installation is taking too long. Process will continue in the background.", LogLevel.WARNING);
                }
                else if (process.ExitCode == 0)
                {
                    Log("Forge server installed successfully", LogLevel.SUCCESS);
                }
                else
                {
                    Log($"Forge installation exited with code {process.ExitCode}", LogLevel.WARNING);
                }

                // Find the server jar
                FindServerJar();
            }
            catch (Exception ex)
            {
                Log($"Error installing Forge server: {ex.Message}", LogLevel.ERROR);
                Log("You may need to install Forge manually using the instructions file", LogLevel.INFO);
            }
        }

        private static void FindServerJar()
        {
            Log("Locating server JAR file...", LogLevel.INFO);

            // Common patterns for Forge server jars
            string[] patterns =
            [
                $"forge-{actualMinecraftVersion}-*.jar",
                $"forge-{actualMinecraftVersion}*universal.jar",
                "forge-*.jar"
            ];

            foreach (var pattern in patterns)
            {
                string[] files = Directory.GetFiles(serverFolder, pattern);

                var serverJar = files.FirstOrDefault(f => !f.Contains("installer") &&
                                                         !f.Contains("sources") &&
                                                         !f.Contains("javadoc"));

                if (!string.IsNullOrEmpty(serverJar))
                {
                    serverJarPath = serverJar;
                    Log($"Server JAR found: {Path.GetFileName(serverJarPath)}", LogLevel.SUCCESS);
                    return;
                }
            }

            Log("Server JAR not found. It may still be downloading or needs manual installation.", LogLevel.WARNING);
        }

        private static async Task<bool> WaitForProcessWithTimeout(Process process, TimeSpan timeout)
        {
            var completionSource = new TaskCompletionSource<bool>();

            // Register exit event
            process.EnableRaisingEvents = true;
            process.Exited += (sender, args) => completionSource.TrySetResult(true);

            // If process already exited, return immediately
            if (process.HasExited)
                return true;

            // Wait for process to exit or timeout
            using var timeoutCts = new CancellationTokenSource(timeout);
            var completedTask = await Task.WhenAny(completionSource.Task, Task.Delay(Timeout.Infinite, timeoutCts.Token));

            return completedTask == completionSource.Task;
        }

        private static void ConfigureServer()
        {
            Log("Configuring server...", LogLevel.INFO);
            DrawProgressBar(45, "Configuring server properties...");

            // Configure server.properties
            ConfigureServerProperties();

            // Accept EULA
            AcceptEula();

            // Create additional configuration
            ConfigureOptionalFiles();

            Log("Server configuration completed", LogLevel.SUCCESS);
        }

        private static void ConfigureServerProperties()
        {
            Log("Setting up server.properties...", LogLevel.INFO);

            // Create custom MOTD with colors
            string motd = $"\\u00A7d\\u00A7l{serverName} \\u00A78| \\u00A7cForge \\u00A78| \\u00A76{maxPlayers} Players";

            // Advanced server properties optimized for the configured player count
            Dictionary<string, string> properties = new()
            {
                // Server basics
                { "server-ip", serverIP },
                { "server-port", serverPort.ToString() },
                { "max-players", maxPlayers.ToString() },
                { "motd", motd },
                { "enable-status", "true" },
                
                // Game rules
                { "gamemode", gameMode },
                { "difficulty", difficulty },
                { "hardcore", "false" },
                { "pvp", enablePvP.ToString().ToLower() },
                { "force-gamemode", "false" },
                { "spawn-protection", "16" },
                
                // World settings
                { "level-name", "world" },
                { "level-seed", "" },
                { "level-type", "default" },
                { "generate-structures", "true" },
                { "max-world-size", "29999984" },
                { "max-build-height", "256" },
                
                // Resource usage (adjusted based on player count)
                { "view-distance", viewDistance.ToString() },
                { "simulation-distance", Math.Min(viewDistance - 2, 10).ToString() }, // Lower than view distance
                { "entity-broadcast-range-percentage", maxPlayers > 50 ? "50" : "100" }, // Lower for many players
                { "max-tick-time", "60000" }, // Higher to prevent server from stalling
                
                // Performance
                { "network-compression-threshold", "256" },
                { "prevent-proxy-connections", "false" },
                { "use-native-transport", "true" },
                { "sync-chunk-writes", maxPlayers > 50 ? "false" : "true" }, // Async for many players
                
                // Spawning
                { "spawn-npcs", "true" },
                { "spawn-animals", "true" },
                { "spawn-monsters", "true" },
                
                // Misc
                { "allow-nether", "true" },
                { "allow-flight", "true" },
                { "enable-command-block", "true" },
                { "function-permission-level", "2" },
                { "op-permission-level", "4" },
                { "enable-query", "false" },
                { "enable-rcon", "false" },
                { "query.port", serverPort.ToString() },
                { "rcon.port", "25575" },
                { "broadcast-rcon-to-ops", "true" },
                { "broadcast-console-to-ops", "true" },
                { "white-list", enableWhitelist.ToString().ToLower() },
                { "enforce-whitelist", enableWhitelist.ToString().ToLower() },
                { "resource-pack", "" },
                { "resource-pack-sha1", "" },
                { "require-resource-pack", "false" },
                { "snooper-enabled", "true" },
                { "player-idle-timeout", "0" }
            };

            try
            {
                // Check if file already exists (might be created by Forge installer)
                if (File.Exists(serverPropertiesPath))
                {
                    var existingProperties = File.ReadAllLines(serverPropertiesPath)
                        .Where(line => !string.IsNullOrWhiteSpace(line) && !line.StartsWith('#'))
                        .Select(line => line.Split('=', 2))
                        .Where(parts => parts.Length == 2)
                        .ToDictionary(parts => parts[0].Trim(), parts => parts[1].Trim());

                    // Merge existing with our config, prioritizing our values
                    foreach (var prop in properties)
                    {
                        existingProperties[prop.Key] = prop.Value;
                    }

                    properties = existingProperties;
                }

                // Write the properties to file
                using StreamWriter writer = new(serverPropertiesPath, false);
                writer.WriteLine("# Minecraft server properties");
                writer.WriteLine($"# Generated by Minecraft Server Setup Utility on {DateTime.Now}");
                writer.WriteLine($"# Optimized for {maxPlayers} players with horror-themed mods");

                foreach (var prop in properties.OrderBy(p => p.Key))
                {
                    writer.WriteLine($"{prop.Key}={prop.Value}");
                }

                Log("Server properties configured successfully", LogLevel.SUCCESS);
            }
            catch (Exception ex)
            {
                Log($"Error configuring server properties: {ex.Message}", LogLevel.ERROR);
                Log("You may need to configure server.properties manually", LogLevel.INFO);
            }
        }

        private static void AcceptEula()
        {
            Log("Accepting Minecraft EULA...", LogLevel.INFO);

            try
            {
                File.WriteAllText(eulaPath,
                    "# By changing the setting below to TRUE you are indicating your agreement to Mojang's EULA (https://account.mojang.com/documents/minecraft_eula).\n" +
                    "# Generated by Minecraft Server Setup Utility\n" +
                    "eula=true"
                );

                Log("EULA accepted", LogLevel.SUCCESS);
            }
            catch (Exception ex)
            {
                Log($"Error accepting EULA: {ex.Message}", LogLevel.ERROR);
                Log("You will need to accept the EULA manually by creating eula.txt", LogLevel.INFO);
            }
        }

        private static void ConfigureOptionalFiles()
        {
            Log("Creating additional configuration files...", LogLevel.INFO);

            try
            {
                // Create ops.json (server administrators)
                string opsPath = Path.Combine(serverFolder, "ops.json");
                if (!string.IsNullOrEmpty(operatorUsername))
                {
                    // Create ops.json with the configured operator
                    var opsData = new[]
                    {
                        new
                        {
                            uuid = Guid.NewGuid().ToString(), // Placeholder UUID
                            name = operatorUsername,
                            level = 4,
                            bypassesPlayerLimit = false
                        }
                    };

                    string opsJson = JsonSerializer.Serialize(opsData, new JsonSerializerOptions
                    {
                        WriteIndented = true,
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    });
                    File.WriteAllText(opsPath, opsJson);

                    Log($"Added {operatorUsername} as server operator", LogLevel.SUCCESS);
                }
                else
                {
                    File.WriteAllText(opsPath, "[]");
                }

                // Create banned-players.json
                string bannedPlayersPath = Path.Combine(serverFolder, "banned-players.json");
                File.WriteAllText(bannedPlayersPath, "[]");

                // Create banned-ips.json
                string bannedIpsPath = Path.Combine(serverFolder, "banned-ips.json");
                File.WriteAllText(bannedIpsPath, "[]");

                // Create whitelist.json
                string whitelistPath = Path.Combine(serverFolder, "whitelist.json");
                if (enableWhitelist && !string.IsNullOrEmpty(operatorUsername))
                {
                    // Add operator to whitelist
                    var whitelistData = new[]
                    {
                        new
                        {
                            uuid = Guid.NewGuid().ToString(), // Placeholder UUID
                            name = operatorUsername
                        }
                    };

                    string whitelistJson = JsonSerializer.Serialize(whitelistData, new JsonSerializerOptions
                    {
                        WriteIndented = true,
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    });
                    File.WriteAllText(whitelistPath, whitelistJson);

                    Log($"Added {operatorUsername} to whitelist", LogLevel.SUCCESS);
                }
                else
                {
                    File.WriteAllText(whitelistPath, "[]");
                }

                Log("Optional configuration files created", LogLevel.SUCCESS);
            }
            catch (Exception ex)
            {
                Log($"Error creating optional configuration files: {ex.Message}", LogLevel.WARNING);
            }
        }

        private static async Task DownloadAllRequestedMods()
        {
            Log("Preparing to download requested mods...", LogLevel.INFO);
            DrawProgressBar(50, "Searching for mods...");

            // Search for each requested mod
            foreach (var modName in requestedMods)
            {
                await SearchAndAddMod(modName);
            }

            // Count total downloads
            int totalDownloads = modsList.Count;
            int completedDownloads = 0;

            // Download all found mods
            if (modsList.Count > 0)
            {
                Log($"Found {modsList.Count} mods to download", LogLevel.SUCCESS);
                DrawProgressBar(60, $"Downloading mods (0/{totalDownloads})...");

                // Create a list of download tasks
                List<Task> downloadTasks = [];

                foreach (var mod in modsList.Values)
                {
                    var task = Task.Run(async () =>
                    {
                        await downloadSemaphore.WaitAsync(); // Limit concurrent downloads

                        try
                        {
                            bool success = await DownloadMod(mod);

                            if (success)
                            {
                                Interlocked.Increment(ref completedDownloads);
                                int percent = 60 + (completedDownloads * 20 / totalDownloads);
                                DrawProgressBar(percent, $"Downloading mods ({completedDownloads}/{totalDownloads})...");
                            }
                        }
                        finally
                        {
                            downloadSemaphore.Release();
                        }
                    });

                    downloadTasks.Add(task);
                }

                // Wait for all downloads to complete
                await Task.WhenAll(downloadTasks);

                // Process dependencies for downloaded mods
                await ProcessModDependencies();

                Log($"Mod downloads completed: {modsList.Count(m => m.Value.IsDownloaded)} successful, {modsList.Count - modsList.Count(m => m.Value.IsDownloaded)} failed",
                    modsList.All(m => m.Value.IsDownloaded) ? LogLevel.SUCCESS : LogLevel.WARNING);

                // Create README for mods
                CreateModsReadme();
            }
            else
            {
                Log("No matching mods found for the requested version", LogLevel.WARNING);

                // Create a file with instructions for manual mod installation
                string instructionsPath = Path.Combine(modsFolder, "MODS_INSTALLATION_INSTRUCTIONS.txt");
                StringBuilder sb = new();
                sb.AppendLine("Mod Installation Instructions");
                sb.AppendLine("===========================");
                sb.AppendLine();
                sb.AppendLine($"No mods were automatically found for Minecraft {actualMinecraftVersion}.");
                sb.AppendLine("You will need to download and install the mods manually:");
                sb.AppendLine();

                foreach (var modName in requestedMods)
                {
                    sb.AppendLine($"• {modName}");
                    sb.AppendLine($"  Search for this mod on CurseForge or similar sites for Minecraft {actualMinecraftVersion}");
                    sb.AppendLine();
                }

                sb.AppendLine("To install mods:");
                sb.AppendLine("1. Download the .jar files for each mod");
                sb.AppendLine($"2. Place the .jar files in this folder: {modsFolder}");
                sb.AppendLine("3. Ensure the mods are compatible with your Minecraft and Forge versions");

                File.WriteAllText(instructionsPath, sb.ToString());

                Log($"Manual mod installation instructions saved to: {instructionsPath}", LogLevel.INFO);
            }
        }

        private static async Task SearchAndAddMod(string modName)
        {
            Log($"Searching for mod: {modName}...", LogLevel.INFO);

            try
            {
                // Prepare search URL
                string searchUrl = $"{CF_API_BASE}/mods/search?gameId={MINECRAFT_GAME_ID}&searchFilter={Uri.EscapeDataString(modName)}&classId=6"; // Class 6 = Mods

                // Make the request
                HttpResponseMessage response = await httpClient.GetAsync(searchUrl);

                if (response.IsSuccessStatusCode)
                {
                    string responseJson = await response.Content.ReadAsStringAsync();
                    var searchResult = JsonSerializer.Deserialize<CFSearchResult>(responseJson, jsonOptions);

                    if (searchResult != null && searchResult.Data != null && searchResult.Data.Count > 0)
                    {
                        // Find best match
                        var bestMatch = FindBestModMatch(searchResult.Data, modName);

                        if (bestMatch != null)
                        {
                            // Get mod files
                            await GetModFiles(bestMatch);
                        }
                        else
                        {
                            Log($"No suitable match found for mod: {modName}", LogLevel.WARNING);
                        }
                    }
                    else
                    {
                        Log($"No results found for mod: {modName}", LogLevel.WARNING);
                    }
                }
                else
                {
                    Log($"Search failed for mod {modName}: {response.StatusCode}", LogLevel.ERROR);
                }
            }
            catch (Exception ex)
            {
                Log($"Error searching for mod {modName}: {ex.Message}", LogLevel.ERROR);
            }
        }

        private static CFMod? FindBestModMatch(List<CFMod> mods, string searchTerm)
        {
            // First try exact match
            var exactMatch = mods.FirstOrDefault(m =>
                string.Equals(m.Name, searchTerm, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(m.Slug, searchTerm.Replace(" ", "-"), StringComparison.OrdinalIgnoreCase));

            if (exactMatch != null)
                return exactMatch;

            // Try contains match
            var containsMatch = mods.FirstOrDefault(m =>
                m.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                searchTerm.Contains(m.Name, StringComparison.OrdinalIgnoreCase));

            if (containsMatch != null)
                return containsMatch;

            // Try word match
            var words = searchTerm.Split([' ', '-', '_'], StringSplitOptions.RemoveEmptyEntries);

            foreach (var mod in mods)
            {
                int matchCount = 0;

                foreach (var word in words)
                {
                    if (mod.Name.Contains(word, StringComparison.OrdinalIgnoreCase))
                        matchCount++;
                }

                if (matchCount >= words.Length / 2) // At least half the words match
                    return mod;
            }

            // If all else fails, return the first result
            return mods.Count > 0 ? mods[0] : null;
        }

        private static async Task GetModFiles(CFMod mod)
        {
            Log($"Getting files for mod: {mod.Name}...", LogLevel.INFO);

            try
            {
                // Get mod files endpoint
                string filesUrl = $"{CF_API_BASE}/mods/{mod.Id}/files";

                HttpResponseMessage response = await httpClient.GetAsync(filesUrl);

                if (response.IsSuccessStatusCode)
                {
                    string responseJson = await response.Content.ReadAsStringAsync();
                    var filesResult = JsonSerializer.Deserialize<CFSearchResult>(responseJson, jsonOptions);

                    if (filesResult != null && filesResult.Data != null && filesResult.Data.Count > 0)
                    {
                        // Extract files from the response (it's nested differently but works with our model)
                        List<CFFile> files = [];
                        foreach (var item in filesResult.Data)
                        {
                            var fileData = JsonSerializer.Deserialize<CFFile>(item.ToString() ?? "{}", jsonOptions);
                            if (fileData != null)
                                files.Add(fileData);
                        }

                        // Find compatible files
                        var compatibleFiles = files.Where(f =>
                            f.SortableGameVersions != null &&
                            f.SortableGameVersions.Any(v =>
                                v.GameVersion == actualMinecraftVersion ||
                                v.GameVersion.StartsWith(actualMinecraftVersion[..4]))).ToList();

                        if (compatibleFiles.Count > 0)
                        {
                            // Sort by release date (newest first)
                            compatibleFiles = compatibleFiles.OrderByDescending(f => f.FileDate).ToList();

                            // Get the newest file
                            var newestFile = compatibleFiles.First();

                            // Create mod info
                            var modInfo = new ModInfo
                            {
                                Name = mod.Name,
                                SlugName = mod.Slug,
                                Description = mod.Summary,
                                DownloadUrl = newestFile.DownloadUrl,
                                Version = GetVersionFromFileName(newestFile.FileName, actualMinecraftVersion),
                                FileId = newestFile.Id,
                                ModId = mod.Id,
                                FileSize = newestFile.FileLength,
                                ReleaseDate = newestFile.FileDate,
                                FileName = newestFile.FileName,
                                LocalFilePath = Path.Combine(modsFolder, newestFile.FileName),
                                Dependencies = []
                            };

                            // Extract dependencies
                            if (newestFile.Dependencies != null)
                            {
                                foreach (var dep in newestFile.Dependencies)
                                {
                                    if (dep.RelationType == 3) // 3 = Required dependency
                                    {
                                        modDependencies.Add(new Dependency
                                        {
                                            ModId = dep.ModId,
                                            RelationType = "REQUIRED",
                                            ModName = $"Dependency for {mod.Name}"
                                        });
                                    }
                                }
                            }

                            // Add to mods list using TryAdd
                            modsList.TryAdd(mod.Slug, modInfo);
                            if (modsList.ContainsKey(mod.Slug))
                            {
                                Log($"Found compatible version of {mod.Name} for Minecraft {actualMinecraftVersion}", LogLevel.SUCCESS);
                            }
                        }
                        else
                        {
                            Log($"No compatible version found for {mod.Name} for Minecraft {actualMinecraftVersion}", LogLevel.WARNING);

                            // Try to find close version match
                            var anyVersionFile = files.OrderByDescending(f => f.FileDate).FirstOrDefault();

                            if (anyVersionFile != null)
                            {
                                // Extract version from file name
                                string fileVersion = GetVersionFromFileName(anyVersionFile.FileName, "Unknown");

                                Log($"Found version for {fileVersion} instead - this may not be compatible", LogLevel.WARNING);

                                // Create mod info with warning
                                var modInfo = new ModInfo
                                {
                                    Name = mod.Name + " (COMPATIBILITY WARNING)",
                                    SlugName = mod.Slug,
                                    Description = mod.Summary,
                                    DownloadUrl = anyVersionFile.DownloadUrl,
                                    Version = fileVersion,
                                    FileId = anyVersionFile.Id,
                                    ModId = mod.Id,
                                    FileSize = anyVersionFile.FileLength,
                                    ReleaseDate = anyVersionFile.FileDate,
                                    FileName = anyVersionFile.FileName,
                                    LocalFilePath = Path.Combine(modsFolder, anyVersionFile.FileName)
                                };

                                // Add to mods list using TryAdd
                                modsList.TryAdd(mod.Slug, modInfo);
                            }
                        }
                    }
                    else
                    {
                        Log($"No files found for mod: {mod.Name}", LogLevel.WARNING);
                    }
                }
                else
                {
                    Log($"Failed to get files for {mod.Name}: {response.StatusCode}", LogLevel.ERROR);
                }
            }
            catch (Exception ex)
            {
                Log($"Error getting files for mod {mod.Name}: {ex.Message}", LogLevel.ERROR);
            }
        }

        private static string GetVersionFromFileName(string fileName, string defaultVersion)
        {
            // Try to extract version from file name
            // Examples: mod-1.19.4-2.0.1.jar, mod-forge-1.19-1.2.3.jar

            // Pattern for Minecraft version + mod version
            var match = ModVersionRegex.Match(fileName);

            if (match.Success)
                return match.Value;

            // Just try to find any version number
            match = SimpleVersionRegex.Match(fileName);

            if (match.Success)
                return match.Value;

            // Return the default if no version found
            return defaultVersion;
        }

        private static async Task<bool> DownloadMod(ModInfo mod)
        {
            if (string.IsNullOrEmpty(mod.DownloadUrl))
            {
                Log($"No download URL for mod: {mod.Name}", LogLevel.ERROR);
                return false;
            }

            Log($"Downloading mod: {mod.Name}...", LogLevel.INFO);

            try
            {
                // Check if already exists
                if (File.Exists(mod.LocalFilePath))
                {
                    Log($"Mod {mod.Name} already exists, skipping download", LogLevel.SUCCESS);
                    mod.IsDownloaded = true;
                    return true;
                }

                // Download the file using HttpClient
                using var response = await httpClient.GetAsync(mod.DownloadUrl);
                response.EnsureSuccessStatusCode();

                await using var fileStream = File.Create(mod.LocalFilePath);
                await response.Content.CopyToAsync(fileStream);

                // Verify file exists and has size
                if (File.Exists(mod.LocalFilePath) && new FileInfo(mod.LocalFilePath).Length > 0)
                {
                    Log($"Downloaded mod: {mod.Name} ({FormatFileSize(new FileInfo(mod.LocalFilePath).Length)})", LogLevel.SUCCESS);
                    mod.IsDownloaded = true;
                    return true;
                }
                else
                {
                    Log($"Download completed but file is missing or empty: {mod.Name}", LogLevel.ERROR);
                    return false;
                }
            }
            catch (Exception ex)
            {
                Log($"Error downloading mod {mod.Name}: {ex.Message}", LogLevel.ERROR);

                // Create placeholder file with instructions
                string instructionsPath = Path.Combine(modsFolder, $"{mod.SlugName}_MANUAL_DOWNLOAD.txt");
                File.WriteAllText(instructionsPath,
                    $"Manual Download Instructions for {mod.Name}\n" +
                    "=======================================\n\n" +
                    $"This mod could not be downloaded automatically. Please download it manually:\n\n" +
                    $"1. Visit: {mod.DownloadUrl}\n" +
                    $"2. Download the file: {mod.FileName}\n" +
                    $"3. Save it to this folder: {modsFolder}\n\n" +
                    $"Mod details:\n" +
                    $"- Name: {mod.Name}\n" +
                    $"- Version: {mod.Version}\n" +
                    $"- Compatible with Minecraft: {actualMinecraftVersion}\n"
                );

                return false;
            }
        }

        private static async Task ProcessModDependencies()
        {
            if (modDependencies.Count == 0)
                return;

            Log($"Processing {modDependencies.Count} mod dependencies...", LogLevel.INFO);

            foreach (var dependency in modDependencies)
            {
                // Skip if we already have this mod
                if (modsList.Values.Any(m => m.ModId == dependency.ModId))
                    continue;

                try
                {
                    // Get mod info
                    string modUrl = $"{CF_API_BASE}/mods/{dependency.ModId}";
                    HttpResponseMessage response = await httpClient.GetAsync(modUrl);

                    if (response.IsSuccessStatusCode)
                    {
                        string responseJson = await response.Content.ReadAsStringAsync();

                        // Extract the data field which contains the mod
                        using JsonDocument doc = JsonDocument.Parse(responseJson);
                        if (doc.RootElement.TryGetProperty("data", out JsonElement dataElement))
                        {
                            // Convert to our model
                            var modJson = dataElement.GetRawText();
                            var mod = JsonSerializer.Deserialize<CFMod>(modJson, jsonOptions);

                            if (mod != null)
                            {
                                // Store name in dependency for later reference
                                dependency.ModName = mod.Name;

                                // Get files for this dependency
                                await GetModFiles(mod);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log($"Error processing dependency {dependency.ModId}: {ex.Message}", LogLevel.ERROR);
                }
            }

            // Download all dependencies
            var dependencyMods = modsList.Values.Where(m => !m.IsDownloaded).ToList();

            if (dependencyMods.Count > 0)
            {
                Log($"Downloading {dependencyMods.Count} dependencies...", LogLevel.INFO);

                foreach (var mod in dependencyMods)
                {
                    await DownloadMod(mod);
                }
            }
        }

        private static void CreateModsReadme()
        {
            string readmePath = Path.Combine(modsFolder, "INSTALLED_MODS.txt");

            StringBuilder sb = new();
            sb.AppendLine("Installed Mods");
            sb.AppendLine("==============");
            sb.AppendLine();
            sb.AppendLine($"Minecraft Version: {actualMinecraftVersion}");
            sb.AppendLine($"Forge Version: {forgeVersion}");
            sb.AppendLine($"Total Mods: {modsList.Count}");
            sb.AppendLine();

            sb.AppendLine("Primary Mods:");
            sb.AppendLine("------------");
            foreach (var mod in modsList.Values.OrderBy(m => m.Name))
            {
                sb.AppendLine($"• {mod.Name}");
                sb.AppendLine($"  Version: {mod.Version}");
                sb.AppendLine($"  File: {mod.FileName}");
                sb.AppendLine($"  Status: {(mod.IsDownloaded ? "Downloaded" : "Failed - Manual download required")}");
                sb.AppendLine();
            }

            sb.AppendLine();
            sb.AppendLine("Server Setup Information:");
            sb.AppendLine("-----------------------");
            sb.AppendLine($"• Server Name: {serverName}");
            sb.AppendLine($"• Server IP: {(string.IsNullOrEmpty(serverIP) ? "Auto-detect" : serverIP)}");
            sb.AppendLine($"• Server Port: {serverPort}");
            sb.AppendLine($"• Max Players: {maxPlayers}");
            sb.AppendLine($"• RAM Allocation: {serverRamGB}GB");
            sb.AppendLine($"• Game Mode: {gameMode}");
            sb.AppendLine($"• Difficulty: {difficulty}");
            if (!string.IsNullOrEmpty(operatorUsername))
            {
                sb.AppendLine($"• Server Operator: {operatorUsername}");
            }
            sb.AppendLine();
            sb.AppendLine("To add more mods, place the .jar files in this folder.");

            File.WriteAllText(readmePath, sb.ToString());

            Log($"Created mods documentation: {readmePath}", LogLevel.SUCCESS);
        }

        private static void CreateStartupScripts()
        {
            Log("Creating server startup scripts...", LogLevel.INFO);
            DrawProgressBar(85, "Creating startup scripts...");

            // Find the server jar
            string serverJar = FindForgeServerJar();

            // Create Windows batch file
            CreateWindowsStartScript(serverJar);

            // Create Unix/Linux shell script
            CreateUnixStartScript(serverJar);

            // Create restart script
            CreateRestartScript();

            // Create backup script
            CreateBackupScript();

            Log("Startup scripts created successfully", LogLevel.SUCCESS);
        }

        private static string FindForgeServerJar()
        {
            // Find the forge server jar
            string[] forgeJars = Directory.GetFiles(serverFolder, "forge-*.jar");

            // Filter out installer jars
            var serverJar = forgeJars.FirstOrDefault(j => !j.Contains("installer") &&
                                                         !j.Contains("sources") &&
                                                         !j.Contains("javadoc"));

            if (string.IsNullOrEmpty(serverJar))
            {
                // If we can't find the exact forge jar, use a generic name
                Log("Could not find specific Forge server JAR. Using generic name in scripts.", LogLevel.WARNING);
                return "forge-universal.jar";
            }

            return Path.GetFileName(serverJar);
        }

        private static void CreateWindowsStartScript(string jarName)
        {
            string batchPath = Path.Combine(serverFolder, "start_server.bat");

            StringBuilder sb = new();
            sb.AppendLine("@echo off");
            sb.AppendLine("cls");
            sb.AppendLine("color 0A");
            sb.AppendLine();
            sb.AppendLine("echo ======================================");
            sb.AppendLine("echo   MINECRAFT FORGE SERVER - STARTUP");
            sb.AppendLine("echo ======================================");
            sb.AppendLine("echo.");
            sb.AppendLine($"echo Server Name: {serverName}");
            sb.AppendLine($"echo Server Version: Minecraft {actualMinecraftVersion} with Forge");
            sb.AppendLine($"echo Max Players: {maxPlayers}");
            sb.AppendLine($"echo Server Port: {serverPort}");
            if (!string.IsNullOrEmpty(serverIP))
            {
                sb.AppendLine($"echo Server IP: {serverIP}");
            }
            sb.AppendLine("echo.");

            if (maxPlayers > 50)
            {
                sb.AppendLine($"echo WARNING: A {maxPlayers}-player server requires significant hardware resources!");
                sb.AppendLine($"echo - Minimum {serverRamGB}GB RAM dedicated to the server");
                sb.AppendLine("echo - High-performance CPU required");
                sb.AppendLine("echo - SSD storage with at least 20GB free space");
                sb.AppendLine("echo - Good network bandwidth required");
                sb.AppendLine("echo.");
            }

            sb.AppendLine("echo Starting server...");
            sb.AppendLine("echo.");
            sb.AppendLine();
            sb.AppendLine("rem Java startup parameters optimized for performance");

            // Adjust JVM parameters based on RAM allocation
            string jvmParams;
            if (serverRamGB <= 4)
            {
                jvmParams = $"-Xmx{serverRamGB}G -Xms1G";
            }
            else if (serverRamGB <= 8)
            {
                jvmParams = $"-Xmx{serverRamGB}G -Xms2G";
            }
            else
            {
                jvmParams = $"-Xmx{serverRamGB}G -Xms4G";
            }

            sb.AppendLine($"set JAVA_OPTS={jvmParams} -XX:+UseG1GC -XX:+ParallelRefProcEnabled -XX:MaxGCPauseMillis=200 -XX:+UnlockExperimentalVMOptions -XX:+DisableExplicitGC -XX:+AlwaysPreTouch -XX:G1NewSizePercent=30 -XX:G1MaxNewSizePercent=40 -XX:G1HeapRegionSize=8M -XX:G1ReservePercent=20 -XX:G1HeapWastePercent=5 -XX:G1MixedGCCountTarget=4 -XX:InitiatingHeapOccupancyPercent=15 -XX:G1MixedGCLiveThresholdPercent=90 -XX:G1RSetUpdatingPauseTimePercent=5 -XX:SurvivorRatio=32 -XX:+PerfDisableSharedMem -XX:MaxTenuringThreshold=1");
            sb.AppendLine();
            sb.AppendLine("rem Run the server");
            sb.AppendLine($"java %JAVA_OPTS% -jar \"{jarName}\" nogui");
            sb.AppendLine();
            sb.AppendLine("echo.");
            sb.AppendLine("echo Server stopped. Press any key to exit.");
            sb.AppendLine("pause > nul");

            File.WriteAllText(batchPath, sb.ToString());

            Log("Windows startup script created", LogLevel.SUCCESS);
        }

        private static void CreateUnixStartScript(string jarName)
        {
            string shellPath = Path.Combine(serverFolder, "start_server.sh");

            StringBuilder sb = new();
            sb.AppendLine("#!/bin/bash");
            sb.AppendLine("clear");
            sb.AppendLine();
            sb.AppendLine("echo \"======================================\"");
            sb.AppendLine("echo \"  MINECRAFT FORGE SERVER - STARTUP\"");
            sb.AppendLine("echo \"======================================\"");
            sb.AppendLine("echo");
            sb.AppendLine($"echo \"Server Name: {serverName}\"");
            sb.AppendLine($"echo \"Server Version: Minecraft {actualMinecraftVersion} with Forge\"");
            sb.AppendLine($"echo \"Max Players: {maxPlayers}\"");
            sb.AppendLine($"echo \"Server Port: {serverPort}\"");
            if (!string.IsNullOrEmpty(serverIP))
            {
                sb.AppendLine($"echo \"Server IP: {serverIP}\"");
            }
            sb.AppendLine("echo");

            if (maxPlayers > 50)
            {
                sb.AppendLine($"echo \"WARNING: A {maxPlayers}-player server requires significant hardware resources!\"");
                sb.AppendLine($"echo \"- Minimum {serverRamGB}GB RAM dedicated to the server\"");
                sb.AppendLine("echo \"- High-performance CPU required\"");
                sb.AppendLine("echo \"- SSD storage with at least 20GB free space\"");
                sb.AppendLine("echo \"- Good network bandwidth required\"");
                sb.AppendLine("echo");
            }

            sb.AppendLine("echo \"Starting server...\"");
            sb.AppendLine("echo");
            sb.AppendLine();
            sb.AppendLine("# Java startup parameters optimized for performance");

            // Adjust JVM parameters based on RAM allocation
            string jvmParams;
            if (serverRamGB <= 4)
            {
                jvmParams = $"-Xmx{serverRamGB}G -Xms1G";
            }
            else if (serverRamGB <= 8)
            {
                jvmParams = $"-Xmx{serverRamGB}G -Xms2G";
            }
            else
            {
                jvmParams = $"-Xmx{serverRamGB}G -Xms4G";
            }

            sb.AppendLine($"JAVA_OPTS=\"{jvmParams} -XX:+UseG1GC -XX:+ParallelRefProcEnabled -XX:MaxGCPauseMillis=200 -XX:+UnlockExperimentalVMOptions -XX:+DisableExplicitGC -XX:+AlwaysPreTouch -XX:G1NewSizePercent=30 -XX:G1MaxNewSizePercent=40 -XX:G1HeapRegionSize=8M -XX:G1ReservePercent=20 -XX:G1HeapWastePercent=5 -XX:G1MixedGCCountTarget=4 -XX:InitiatingHeapOccupancyPercent=15 -XX:G1MixedGCLiveThresholdPercent=90 -XX:G1RSetUpdatingPauseTimePercent=5 -XX:SurvivorRatio=32 -XX:+PerfDisableSharedMem -XX:MaxTenuringThreshold=1\"");
            sb.AppendLine();
            sb.AppendLine("# Run the server");
            sb.AppendLine($"java $JAVA_OPTS -jar \"{jarName}\" nogui");
            sb.AppendLine();
            sb.AppendLine("echo");
            sb.AppendLine("echo \"Server stopped. Press Enter to exit.\"");
            sb.AppendLine("read");

            File.WriteAllText(shellPath, sb.ToString());

            // Make the shell script executable
            try
            {
                ProcessStartInfo psi = new()
                {
                    FileName = "chmod",
                    Arguments = $"+x \"{shellPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                Process.Start(psi);
            }
            catch
            {
                // Probably on Windows, which is fine
            }

            Log("Unix/Linux startup script created", LogLevel.SUCCESS);
        }

        private static void CreateRestartScript()
        {
            string restartPath = Path.Combine(serverFolder, "restart_server.bat");

            StringBuilder sb = new();
            sb.AppendLine("@echo off");
            sb.AppendLine("echo Restarting server in 10 seconds...");
            sb.AppendLine("timeout /t 10");
            sb.AppendLine("taskkill /f /im java.exe");
            sb.AppendLine("timeout /t 5");
            sb.AppendLine("start start_server.bat");
            sb.AppendLine("exit");

            File.WriteAllText(restartPath, sb.ToString());

            // Unix version
            string unixRestartPath = Path.Combine(serverFolder, "restart_server.sh");

            StringBuilder unixSb = new();
            unixSb.AppendLine("#!/bin/bash");
            unixSb.AppendLine("echo \"Restarting server in 10 seconds...\"");
            unixSb.AppendLine("sleep 10");
            unixSb.AppendLine("pkill -f \"java.*forge\"");
            unixSb.AppendLine("sleep 5");
            unixSb.AppendLine("./start_server.sh &");
            unixSb.AppendLine("disown");
            unixSb.AppendLine("exit");

            File.WriteAllText(unixRestartPath, unixSb.ToString());

            // Make executable
            try
            {
                ProcessStartInfo psi = new()
                {
                    FileName = "chmod",
                    Arguments = $"+x \"{unixRestartPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                Process.Start(psi);
            }
            catch
            {
                // Probably on Windows, which is fine
            }

            Log("Restart scripts created", LogLevel.SUCCESS);
        }

        private static void CreateBackupScript()
        {
            string backupPath = Path.Combine(serverFolder, "backup_server.bat");

            StringBuilder sb = new();
            sb.AppendLine("@echo off");
            sb.AppendLine("setlocal");
            sb.AppendLine();
            sb.AppendLine("set TIMESTAMP=%date:~-4,4%-%date:~-7,2%-%date:~-10,2%_%time:~0,2%-%time:~3,2%-%time:~6,2%");
            sb.AppendLine("set TIMESTAMP=%TIMESTAMP: =0%");
            sb.AppendLine();
            sb.AppendLine("echo Creating backup...");
            sb.AppendLine();
            sb.AppendLine("mkdir backups 2>nul");
            sb.AppendLine();
            sb.AppendLine("rem Compress world folder");
            sb.AppendLine("powershell -command \"Compress-Archive -Path world -DestinationPath 'backups\\world_%TIMESTAMP%.zip' -Force\"");
            sb.AppendLine();
            sb.AppendLine("rem Copy config files");
            sb.AppendLine("copy server.properties backups\\server.properties.%TIMESTAMP% >nul");
            sb.AppendLine("copy eula.txt backups\\eula.txt.%TIMESTAMP% >nul");
            sb.AppendLine();
            sb.AppendLine("echo Backup completed: backups\\world_%TIMESTAMP%.zip");
            sb.AppendLine();
            sb.AppendLine("pause");

            File.WriteAllText(backupPath, sb.ToString());

            // Unix version
            string unixBackupPath = Path.Combine(serverFolder, "backup_server.sh");

            StringBuilder unixSb = new();
            unixSb.AppendLine("#!/bin/bash");
            unixSb.AppendLine();
            unixSb.AppendLine("TIMESTAMP=$(date +\"%Y-%m-%d_%H-%M-%S\")");
            unixSb.AppendLine();
            unixSb.AppendLine("echo \"Creating backup...\"");
            unixSb.AppendLine();
            unixSb.AppendLine("mkdir -p backups");
            unixSb.AppendLine();
            unixSb.AppendLine("# Compress world folder");
            unixSb.AppendLine("tar -czf \"backups/world_${TIMESTAMP}.tar.gz\" world");
            unixSb.AppendLine();
            unixSb.AppendLine("# Copy config files");
            unixSb.AppendLine("cp server.properties \"backups/server.properties.${TIMESTAMP}\"");
            unixSb.AppendLine("cp eula.txt \"backups/eula.txt.${TIMESTAMP}\"");
            unixSb.AppendLine();
            unixSb.AppendLine("echo \"Backup completed: backups/world_${TIMESTAMP}.tar.gz\"");
            unixSb.AppendLine();
            unixSb.AppendLine("read -p \"Press Enter to continue...\"");

            File.WriteAllText(unixBackupPath, unixSb.ToString());

            // Make executable
            try
            {
                ProcessStartInfo psi = new()
                {
                    FileName = "chmod",
                    Arguments = $"+x \"{unixBackupPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                Process.Start(psi);
            }
            catch
            {
                // Probably on Windows, which is fine
            }

            Log("Backup scripts created", LogLevel.SUCCESS);
        }

        private static void PerformServerOptimizations()
        {
            Log("Performing server optimizations...", LogLevel.INFO);
            DrawProgressBar(90, "Optimizing server configuration...");

            // Create additional configuration files for performance
            CreateServerOptimizationFiles();

            Log("Server optimization completed", LogLevel.SUCCESS);
        }

        private static void CreateServerOptimizationFiles()
        {
            try
            {
                // Create JVM flags file
                string jvmFlagsPath = Path.Combine(serverFolder, "jvm_flags.txt");
                string minRamJvm = serverRamGB <= 4 ? "1G" : serverRamGB <= 8 ? "2G" : "4G";
                File.WriteAllText(jvmFlagsPath,
                    "# Optimized JVM flags for Minecraft server\n" +
                    "# Copy these into your startup script if needed\n\n" +
                    $"-Xmx{serverRamGB}G -Xms{minRamJvm}\n" +
                    "-XX:+UseG1GC\n" +
                    "-XX:+ParallelRefProcEnabled\n" +
                    "-XX:MaxGCPauseMillis=200\n" +
                    "-XX:+UnlockExperimentalVMOptions\n" +
                    "-XX:+DisableExplicitGC\n" +
                    "-XX:+AlwaysPreTouch\n" +
                    "-XX:G1NewSizePercent=30\n" +
                    "-XX:G1MaxNewSizePercent=40\n" +
                    "-XX:G1HeapRegionSize=8M\n" +
                    "-XX:G1ReservePercent=20\n" +
                    "-XX:G1HeapWastePercent=5\n" +
                    "-XX:G1MixedGCCountTarget=4\n" +
                    "-XX:InitiatingHeapOccupancyPercent=15\n" +
                    "-XX:G1MixedGCLiveThresholdPercent=90\n" +
                    "-XX:G1RSetUpdatingPauseTimePercent=5\n" +
                    "-XX:SurvivorRatio=32\n" +
                    "-XX:+PerfDisableSharedMem\n" +
                    "-XX:MaxTenuringThreshold=1\n"
                );

                // Create aikar flags file (alternative optimization)
                string aikarFlagsPath = Path.Combine(serverFolder, "aikar_flags.txt");
                string minRam = serverRamGB <= 4 ? "1G" : serverRamGB <= 8 ? "2G" : "4G";
                File.WriteAllText(aikarFlagsPath,
                    "# Aikar's JVM flags for Minecraft server (alternative optimization)\n" +
                    "# Copy these into your startup script if needed\n\n" +
                    $"-Xms{minRam} -Xmx{serverRamGB}G\n" +
                    "-XX:+UseG1GC -XX:+ParallelRefProcEnabled -XX:MaxGCPauseMillis=200\n" +
                    "-XX:+UnlockExperimentalVMOptions -XX:+DisableExplicitGC -XX:+AlwaysPreTouch\n" +
                    "-XX:G1NewSizePercent=30 -XX:G1MaxNewSizePercent=40 -XX:G1HeapRegionSize=8M\n" +
                    "-XX:G1ReservePercent=20 -XX:G1HeapWastePercent=5 -XX:G1MixedGCCountTarget=4\n" +
                    "-XX:InitiatingHeapOccupancyPercent=15 -XX:G1MixedGCLiveThresholdPercent=90\n" +
                    "-XX:G1RSetUpdatingPauseTimePercent=5 -XX:SurvivorRatio=32 -XX:+PerfDisableSharedMem\n" +
                    "-XX:MaxTenuringThreshold=1 -Dusing.aikars.flags=https://mcflags.emc.gs\n" +
                    "-Daikars.new.flags=true\n"
                );

                // Create server maintenance guide
                string maintenancePath = Path.Combine(serverFolder, "SERVER_MAINTENANCE_GUIDE.txt");
                string maintenanceContent = "Minecraft Server Maintenance Guide\n" +
                    "===============================\n\n" +
                    $"Server: {serverName}\n" +
                    $"Version: Minecraft {actualMinecraftVersion} with Forge\n" +
                    $"Max Players: {maxPlayers}\n" +
                    $"RAM Allocation: {serverRamGB}GB\n";

                if (!string.IsNullOrEmpty(operatorUsername))
                {
                    maintenanceContent += $"Server Operator: {operatorUsername}\n";
                }

                maintenanceContent += "\n" +
                    "Regular Maintenance Tasks:\n" +
                    "-------------------------\n" +
                    "1. Backups: Run backup_server.bat/sh regularly to create world backups\n" +
                    "2. Updates: Check for Forge and mod updates periodically\n" +
                    "3. Logs: Check the logs folder for errors and warnings\n" +
                    "4. Performance: Monitor server performance with /forge tps command\n\n" +

                    "Performance Troubleshooting:\n" +
                    "--------------------------\n" +
                    "1. If TPS drops below 20, reduce view-distance in server.properties\n" +
                    "2. Consider removing some of the more intensive mods\n" +
                    $"3. For a {maxPlayers}-player server, ensure hardware meets these requirements:\n" +
                    $"   - {serverRamGB}+ GB RAM dedicated to the server\n" +
                    $"   - {(maxPlayers > 50 ? "8+" : "4+")} CPU cores (high single-thread performance)\n" +
                    "   - SSD storage with 20+ GB free space\n" +
                    $"   - {(maxPlayers > 50 ? "100+" : "50+")} Mbps network connection\n\n" +

                    "Common Commands:\n" +
                    "--------------\n" +
                    "- /op <player>: Make a player an operator\n" +
                    "- /deop <player>: Remove operator status\n" +
                    "- /ban <player>: Ban a player\n" +
                    "- /pardon <player>: Unban a player\n" +
                    "- /whitelist add <player>: Add player to whitelist\n" +
                    "- /whitelist remove <player>: Remove from whitelist\n" +
                    "- /save-all: Force a world save\n" +
                    "- /stop: Safely stop the server\n" +
                    "- /forge tps: Show server TPS (ticks per second)\n\n" +

                    "Updating the Server:\n" +
                    "------------------\n" +
                    "1. Backup your world and configuration\n" +
                    "2. Download new Forge installer for your Minecraft version\n" +
                    "3. Run the installer with --installServer flag\n" +
                    "4. Update mods as needed\n\n";

                if (maxPlayers > 100)
                {
                    maintenanceContent += $"\nNote on {maxPlayers} Players:\n" +
                        "------------------\n" +
                        $"Supporting {maxPlayers} concurrent players is extremely demanding. If you experience\n" +
                        "performance issues, consider reducing max-players in server.properties to a\n" +
                        $"more manageable number like {maxPlayers / 2} players.\n";
                }

                File.WriteAllText(maintenancePath, maintenanceContent);

                Log("Created server optimization and maintenance guides", LogLevel.SUCCESS);
            }
            catch (Exception ex)
            {
                Log($"Error creating optimization files: {ex.Message}", LogLevel.ERROR);
            }
        }
        #endregion

        #region Utility Methods
        private static string FormatFileSize(long bytes)
        {
            string[] suffixes = ["B", "KB", "MB", "GB", "TB"];
            int i = 0;
            double dblBytes = bytes;

            while (dblBytes >= 1024 && i < suffixes.Length - 1)
            {
                dblBytes /= 1024;
                i++;
            }

            return $"{dblBytes:0.##} {suffixes[i]}";
        }
        #endregion
    }
}