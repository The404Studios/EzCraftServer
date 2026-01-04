# EzCraft Mod Manager

<div align="center">

![Minecraft](https://img.shields.io/badge/Minecraft-1.7.10--1.21.4-green?style=for-the-badge&logo=minecraft)
![Forge](https://img.shields.io/badge/Forge-All%20Versions-orange?style=for-the-badge)
![.NET](https://img.shields.io/badge/.NET-8.0-purple?style=for-the-badge&logo=dotnet)
![Platform](https://img.shields.io/badge/Platform-Windows-blue?style=for-the-badge)
![License](https://img.shields.io/badge/License-MIT-yellow?style=for-the-badge)

**A modern WPF application for downloading, installing, and managing Minecraft mods and servers**

*Made with love by [The404Studios](https://github.com/The404Studios)*

[Features](#features) | [Installation](#installation) | [Mod Packs](#curated-mod-packs) | [Screenshots](#screenshots)

</div>

---

## Overview

EzCraft Mod Manager is a beautiful, modern Windows application that makes it easy to:

- **Browse and download mods** directly from CurseForge and Modrinth (no sketchy websites!)
- **Install Forge** for any Minecraft version automatically
- **Create and manage servers** with optimized settings
- **Install curated mod packs** for zombie survival, guns, horror, and more
- **Handle dependencies** automatically

## Features

### Modern WPF Interface
- Dark theme with beautiful Material Design-inspired styling
- Smooth navigation between views
- Real-time download progress tracking
- Mod preview images and descriptions

### Direct API Integration
- Downloads directly from **CurseForge** and **Modrinth** APIs
- No redirects through ad-filled download sites
- Automatic version compatibility checking
- Dependency resolution and download

### Forge Installation
- Supports all Minecraft versions (1.7.10 to 1.21.4+)
- Automatic Forge version detection
- Recommended and latest version options
- One-click server installation

### Server Management
- Create multiple server profiles
- Configure RAM, players, game mode, difficulty
- Optimized JVM flags for performance
- Automatic startup script generation

### Mod Management
- Enable/disable mods without deleting
- Track installed mods per server
- Update detection (coming soon)
- Conflict detection (coming soon)

---

## Curated Mod Packs

### Zombie Apocalypse
Survive waves of intelligent zombies with enhanced AI, new zombie types, and survival mechanics.

**Included Mods:**
- Zombie Awareness - Smarter zombies that track you by sound and light
- Epic Siege Mod - Zombies break blocks and siege your base
- Tough As Nails - Thirst, temperature, and survival mechanics
- First Aid - Realistic body part damage system
- And more...

### Arsenal & Firearms
Modern and futuristic weapons including guns, explosives, and tactical gear.

**Included Mods:**
- MrCrayfish's Gun Mod - High-quality 3D guns with attachments
- Timeless and Classics Guns - Classic firearms with realistic mechanics
- Tech Guns - Sci-fi weapons and power armor
- Better Combat - Enhanced melee combat
- And more...

### Horror & Terror
Terrifying creatures, dark atmospheres, and jump scares for horror fans.

**Included Mods:**
- The Midnight - Dark dimension with terrifying creatures
- Weeping Angels - Don't blink! Doctor Who Weeping Angels
- Grue - Something lurks in the darkness
- Horror Elements - Jump scares and horror ambiance
- Stalker Creepers - Creepers that stalk and hunt you
- And more...

### Ultimate Apocalypse
The complete post-apocalyptic experience combining zombies, guns, horror, and survival.

---

## Installation

### Requirements
- Windows 10/11
- .NET 8.0 Runtime
- Java 17+ (for running Minecraft servers)

### Download
1. Go to the [Releases](https://github.com/The404Studios/EzCraftServer/releases) page
2. Download the latest EzCraftModManager.exe
3. Run the application (no installation required)

### Build from Source
\`\`\`bash
# Clone the repository
git clone https://github.com/The404Studios/EzCraftServer.git

# Navigate to the WPF project
cd EzCraftServer/EzCraftModManager

# Build the project
dotnet build -c Release

# Run the application
dotnet run
\`\`\`

---

## Project Structure

\`\`\`
EzCraftModManager/
├── Models/              # Data models (ModInfo, ServerProfile, etc.)
├── ViewModels/          # MVVM ViewModels
├── Views/               # XAML views and code-behind
├── Services/            # API services (CurseForge, Modrinth, Forge)
├── Converters/          # Value converters for XAML
├── Assets/              # Images and icons
└── App.xaml             # Application resources and styles
\`\`\`

---

## Technical Details

### API Integration

**CurseForge API**
- Uses official CurseForge API v1
- Searches mods filtered by game version and mod loader
- Retrieves mod files with dependency information
- Direct download URLs (bypasses web interface)

**Modrinth API**
- Uses Modrinth API v2
- Faceted search for mods
- Project versions with loader compatibility
- Direct file downloads

**Forge Files**
- Downloads from official Maven repository
- Parses version metadata for available versions
- Installer-based server setup

### MVVM Architecture
- Uses CommunityToolkit.Mvvm for INotifyPropertyChanged
- Clean separation of concerns
- Dependency injection ready
- Async/await throughout

---

## Contributing

Contributions are welcome! Please:

1. Fork the repository
2. Create a feature branch
3. Commit your changes
4. Push to the branch
5. Open a Pull Request

---

## License

This project is licensed under the MIT License.

---

## Acknowledgments

- **Minecraft** - The amazing game
- **MinecraftForge** - The modding framework
- **CurseForge** - Mod hosting and API
- **Modrinth** - Open-source mod hosting
- **The Minecraft Community** - For endless creativity

---

<div align="center">

### Star this repository if you find it helpful!

Made with love by **[The404Studios](https://github.com/The404Studios)**

Support Discord: https://discord.gg/CrtPtMwcDA

[Report Bug](https://github.com/The404Studios/EzCraftServer/issues) | [Request Feature](https://github.com/The404Studios/EzCraftServer/issues)

</div>
