# 🎮 Easy Minecraft Server Setup 2025

<div align="center">

![Minecraft](https://img.shields.io/badge/Minecraft-1.21.8-green?style=for-the-badge&logo=minecraft)
![Forge](https://img.shields.io/badge/Forge-Compatible-orange?style=for-the-badge)
![.NET](https://img.shields.io/badge/.NET-6.0+-purple?style=for-the-badge&logo=dotnet)
![Platform](https://img.shields.io/badge/Platform-Windows%20%7C%20Linux%20%7C%20Mac-blue?style=for-the-badge)
![License](https://img.shields.io/badge/License-MIT-yellow?style=for-the-badge)

**An advanced, all-in-one Minecraft Forge server setup utility with automatic mod installation and interactive configuration**

*Made with ❤️ by [FourZeroFour](https://github.com/fourzerofour)*

[Features](#✨-features) • [Installation](#📦-installation) • [Usage](#🚀-usage) • [Configuration](#⚙️-configuration) • [Requirements](#📋-requirements) • [Contributing](#🤝-contributing)

</div>

---

## 📖 Overview

This powerful utility automates the entire process of setting up a Minecraft Forge server, from downloading the correct version to installing mods and configuring server properties. With an intuitive interactive menu, you can customize every aspect of your server before installation begins.

### 🎯 What it does:
- ✅ **Automatically downloads and installs Minecraft Forge**
- ✅ **Interactive configuration menu for all server settings**
- ✅ **Downloads and installs mods from CurseForge**
- ✅ **Creates optimized startup scripts for Windows/Linux/Mac**
- ✅ **Sets up operator permissions automatically**
- ✅ **Generates backup and maintenance scripts**
- ✅ **Optimizes JVM flags based on player count**
- ✅ **Handles all EULA acceptance and configuration files**

---

## ✨ Features

### 🎛️ Interactive Configuration Menu
Configure your server exactly how you want it before installation:
- Server name and IP address
- Port configuration
- Max players and RAM allocation
- Game mode and difficulty
- PvP and whitelist settings
- View distance optimization
- Automatic operator setup

### 🔧 Automatic Setup
- **Smart Version Detection**: Automatically finds and downloads the correct Minecraft and Forge versions
- **Dependency Resolution**: Handles mod dependencies automatically
- **Performance Optimization**: Configures JVM flags based on your server size
- **Multi-Platform Support**: Creates startup scripts for Windows, Linux, and Mac

### 📦 Mod Management
- **CurseForge Integration**: Searches and downloads mods directly from CurseForge
- **Compatibility Checking**: Ensures mods work with your Minecraft version
- **Dependency Handling**: Automatically downloads required mod dependencies
- **Horror Mod Pack**: Pre-configured with popular horror-themed mods

### 🛡️ Server Management Tools
- **Startup Scripts**: Optimized launch scripts with proper JVM flags
- **Backup System**: Easy backup scripts for world and configuration
- **Restart Scripts**: Quick server restart utilities
- **Maintenance Guide**: Comprehensive server management documentation

---

## 📋 Requirements

### System Requirements
- **OS**: Windows 10/11, Linux, or macOS
- **RAM**: Minimum 4GB (8GB+ recommended)
- **Storage**: 10GB+ free space
- **Network**: Broadband internet connection

### Software Requirements
- **Java 21** or newer ([Download here](https://adoptium.net/temurin/releases/?version=21))
- **.NET 6.0** or newer (for running the utility)
- **Administrator/Root access** (for some features)

---

## 📦 Installation

### Option 1: Download Release (Recommended)
1. Go to the [Releases](https://github.com/fourzerofour/minecraft-server-setup/releases) page
2. Download the latest `MinecraftServerSetup.exe`
3. Run the executable (no installation needed)

### Option 2: Build from Source
```bash
# Clone the repository
git clone https://github.com/fourzerofour/minecraft-server-setup.git

# Navigate to the project directory
cd minecraft-server-setup

# Build the project
dotnet build -c Release

# Run the utility
dotnet run
```

---

## 🚀 Usage

### Quick Start
1. **Run the utility** - Double-click `MinecraftServerSetup.exe`
2. **Configure your server** - Use the interactive menu to set your preferences
3. **Wait for installation** - The utility will download and set up everything
4. **Start your server** - Navigate to the server folder and run `start_server.bat`

### Configuration Menu Options

```
═══════════════════════════════════════════════════════════════
                    SERVER CONFIGURATION MENU                  
═══════════════════════════════════════════════════════════════

  1. Server Name:        My Minecraft Server
  2. Server IP:          [Auto-detect]
  3. Server Port:        25565
  4. Max Players:        100
  5. Server RAM:         8 GB
  6. Operator Username:  YourUsername
  7. Game Mode:          survival
  8. Difficulty:         normal
  9. Enable PvP:         Yes
 10. Enable Whitelist:   No
 11. View Distance:      10 chunks
 12. Install Location:   C:\Desktop\MinecraftServer
 13. Minecraft Version:  1.21.8

 S. Save & Start Setup
 D. Use Default Settings
 Q. Quit Setup
```

### First Time Setup
1. **Java Check**: The utility will verify Java 21 is installed
2. **Server Configuration**: Customize all settings through the menu
3. **Minecraft Version**: Confirms the target version exists
4. **Forge Installation**: Downloads and installs Forge automatically
5. **Mod Downloads**: Fetches all requested mods from CurseForge
6. **Script Generation**: Creates platform-specific startup scripts
7. **Final Configuration**: Sets up ops, whitelist, and server properties

---

## ⚙️ Configuration

### Server Properties
The utility automatically configures `server.properties` with optimized settings based on your choices:

| Setting | Description | Default |
|---------|-------------|---------|
| `max-players` | Maximum concurrent players | 100 |
| `server-port` | Port for server connections | 25565 |
| `gamemode` | Default game mode | survival |
| `difficulty` | World difficulty | normal |
| `pvp` | Enable player combat | true |
| `view-distance` | Chunk render distance | 10 |

### JVM Optimization
RAM allocation and JVM flags are automatically optimized:

| Players | Recommended RAM | JVM Flags |
|---------|----------------|-----------|
| 1-10 | 2GB | Basic G1GC |
| 10-20 | 4GB | Optimized G1GC |
| 20-50 | 6GB | Performance G1GC |
| 50-100 | 8GB | Advanced G1GC |
| 100+ | 12GB+ | Maximum Performance |

### Included Mods
The utility comes pre-configured to download these horror-themed mods:
- 🧟 Sons Of Sins
- 👻 Stalker Creepers
- 💀 Scary Mobs And Bosses
- 👼 Doctor Who - Weeping Angels
- 🌑 Grue
- 👁️ EnhancedVisuals
- 🎃 Horror Elements
- 🧬 Mutationcraft
- ⛏️ The Spelunker's Charm
- 🌌 IntoTheVoid
- 🔫 Guns Mod
- 🚀 Galacticraft

---

## 📁 Project Structure

```
MinecraftServer/
├── minecraft_1.21.8/          # Server files
│   ├── mods/                  # Downloaded mods
│   ├── config/                # Configuration files
│   ├── world/                 # World data
│   ├── backups/               # Backup storage
│   ├── logs/                  # Server logs
│   ├── libraries/             # Forge libraries
│   ├── scripts/               # Utility scripts
│   ├── server.properties      # Server configuration
│   ├── eula.txt              # EULA acceptance
│   ├── ops.json              # Operator list
│   ├── whitelist.json        # Whitelist
│   ├── start_server.bat      # Windows startup
│   ├── start_server.sh       # Linux/Mac startup
│   ├── backup_server.bat     # Backup script
│   └── restart_server.bat    # Restart script
```

---

## 🛠️ Troubleshooting

### Common Issues

| Issue | Solution |
|-------|----------|
| **Java not found** | Install Java 21 from [Adoptium](https://adoptium.net/) |
| **Forge won't install** | Check firewall settings and internet connection |
| **Mods not downloading** | Verify CurseForge is accessible in your region |
| **Server won't start** | Check Java version with `java -version` |
| **Out of memory** | Increase RAM in configuration menu |

### Getting Help
1. Check the `logs/` folder for detailed error messages
2. Review the `SERVER_MAINTENANCE_GUIDE.txt` in your server folder
3. Open an [issue](https://github.com/fourzerofour/minecraft-server-setup/issues) on GitHub
4. Join our [Discord community](https://discord.gg/minecraft) for support

---

## 🤝 Contributing

Contributions are welcome! Here's how you can help:

1. **Fork the repository**
2. **Create a feature branch** (`git checkout -b feature/AmazingFeature`)
3. **Commit your changes** (`git commit -m 'Add some AmazingFeature'`)
4. **Push to the branch** (`git push origin feature/AmazingFeature`)
5. **Open a Pull Request**

### Development Setup
```bash
# Clone your fork
git clone https://github.com/yourusername/minecraft-server-setup.git

# Install dependencies
dotnet restore

# Run tests
dotnet test

# Build the project
dotnet build
```

---

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

```
MIT License

Copyright (c) 2024 FourZeroFour

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction...
```

---

## 🙏 Acknowledgments

- **[FourZeroFour](https://github.com/fourzerofour)** - Creator and maintainer
- **Minecraft** - The amazing game that started it all
- **MinecraftForge** - For the modding framework
- **CurseForge** - For hosting the mods
- **The Minecraft Community** - For inspiration and support

---

## 📊 Stats

![GitHub Stars](https://img.shields.io/github/stars/fourzerofour/minecraft-server-setup?style=social)
![GitHub Forks](https://img.shields.io/github/forks/fourzerofour/minecraft-server-setup?style=social)
![GitHub Issues](https://img.shields.io/github/issues/fourzerofour/minecraft-server-setup)
![GitHub Pull Requests](https://img.shields.io/github/issues-pr/fourzerofour/minecraft-server-setup)

---

<div align="center">

### ⭐ Star this repository if you find it helpful!

Made with ❤️ by **[FourZeroFour](https://github.com/fourzerofour)**

Support Discord : https://discord.gg/CrtPtMwcDA

[Report Bug](https://github.com/fourzerofour/minecraft-server-setup/issues) • [Request Feature](https://github.com/fourzerofour/minecraft-server-setup/issues) • [Discord](https://discord.gg/minecraft)

</div>
