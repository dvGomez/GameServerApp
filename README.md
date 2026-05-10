<div align="center">

# 🎮 GameServerApp

**Manage your game servers simply, centrally, and hassle-free.**

Create, configure, monitor, and control multiple servers in a single interface —<br>
with real-time console, file editing, and configuration, all without touching the terminal.

[![Release](https://img.shields.io/github/v/release/dvGomez/GameServerApp?style=for-the-badge&color=0EA5E9)](https://github.com/dvGomez/GameServerApp/releases)
[![License](https://img.shields.io/github/license/dvGomez/GameServerApp?style=for-the-badge&color=22C55E)](LICENSE)
[![Platform](https://img.shields.io/badge/platform-Windows%20%7C%20Linux%20%7C%20macOS-888888?style=for-the-badge)](https://github.com/dvGomez/GameServerApp/releases)

</div>

---

## 📸 Screenshots

<p align="center">
  <img src="https://i.imgur.com/ZScA75g.png" alt="Server Console" width="720" />
</p>

<p align="center">
  <img src="https://i.imgur.com/spQJu2p.png" alt="Server Settings" width="720" />
</p>

---

## ✨ Features

- 🖥️ **Modern desktop interface** — Manage everything visually, no terminal needed
- ⚡ **Real-time console** — View logs, send commands, and monitor server status live
- 📝 **Configuration editing** — Change port, game mode, difficulty, and more with intuitive forms
- 📁 **File browser** — Explore and edit server files directly from the interface
- 🔄 **Reactive updates** — Any change is automatically reflected across the entire UI
- 📋 **Persistent logging** — Switch between servers without losing console logs
- 🧩 **Plugin system** — Extensible for any game (Minecraft, PaperMC, FiveM, and Project Zomboid included)
- 🚀 **Automatic setup** — Server downloads and dependencies (Java, SteamCMD, FXServer) managed automatically
- 🌍 **Cross-platform** — Works on Windows, Linux, and macOS

---

## 📦 Installation

### Download (recommended)

1. Go to the [releases page](https://github.com/dvGomez/GameServerApp/releases)
2. Download the `.zip` file for your operating system:
   | System  | File |
   |---------|------|
   | Windows | `GameServerApp-windows-x64.zip` |
   | Linux   | `GameServerApp-linux-x64.zip` |
   | macOS   | `GameServerApp-macos-x64.zip` |
3. Extract the `.zip` contents
4. Run the application:
   - **Windows:** run `GameServerApp.UI.exe`
   - **Linux/macOS:** run `./GameServerApp.UI` (you may need to grant permission with `chmod +x GameServerApp.UI`)

> **Note:** The executable is **self-contained** — no need to install .NET or Java. Java is downloaded automatically when creating the first server.

### Build from source

If you prefer to compile manually:

```bash
# Prerequisite: .NET 9 SDK
# https://dotnet.microsoft.com/download/dotnet/9.0

# Clone the repository
git clone https://github.com/dvGomez/GameServerApp.git
cd GameServerApp

# Run in development mode
dotnet run --project src/GameServerApp.UI

# Or generate a production build
dotnet publish src/GameServerApp.UI/GameServerApp.UI.csproj \
  -c Release \
  -r win-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -o publish
```

---

## 🚀 Getting started

1. Open GameServerApp
2. Click **"+ New Server"** in the sidebar
3. Select the game (e.g., Minecraft Java Edition)
4. Name your server and wait for the automatic download
5. Click **"Start"** and you're done — your server is running!

From there you can:
- Send commands through the integrated console
- Change settings in **Settings**
- Edit server files in the **Files** tab
- Monitor online players in real time

---

## 🧩 Supported games

| Game | Plugin | Status |
|------|--------|--------|
| Minecraft Java Edition | `GameServerApp.Plugins.Minecraft` | ✅ Available |
| Minecraft PaperMC | `GameServerApp.Plugins.PaperMC` | ✅ Available |
| FiveM (GTA V) | `GameServerApp.Plugins.FiveM` | ✅ Available |
| Project Zomboid | `GameServerApp.Plugins.Zomboid` | ✅ Available |
| *Your favorite game* | Create a plugin! | 🔧 Extensible |

### Creating a plugin

To add support for a new game, implement the `IGameServerPlugin` interface:

```csharp
public interface IGameServerPlugin
{
    string GameId { get; }
    string DisplayName { get; }

    // Configuration schema (defines the fields shown in the UI)
    IReadOnlyList<ConfigField> GetConfigSchema();

    // How to start and stop the server
    ProcessStartInfo BuildStartInfo(ServerConfig config);
    string? GetGracefulStopCommand();

    // Server download and versioning
    Task<string> GetLatestVersionAsync(CancellationToken ct = default);
    Task DownloadServerAsync(string version, string targetDirectory, ...);

    // Game configuration read/write
    Task WriteGameConfigAsync(string serverDirectory, Dictionary<string, object> configValues, ...);
    Task<Dictionary<string, object>> ReadGameConfigAsync(string serverDirectory, ...);
}
```

Simply create a new project, implement the interface, and register it in the DI container in `App.axaml.cs`.

---

## 🏗️ Architecture

```
GameServerApp/
├── src/
│   ├── GameServerApp.Core/          # Business logic, interfaces, and models
│   │   ├── Interfaces/              # IServerManager, IGameServerPlugin, etc.
│   │   ├── Models/                  # ServerConfig, ServerInstance, etc.
│   │   ├── Services/                # ServerManager, ProcessManager
│   │   └── Events/                  # State, config, and console events
│   │
│   ├── GameServerApp.UI/            # Desktop interface (Avalonia UI + MVVM)
│   │   ├── ViewModels/              # MainWindow, Console, Config, etc.
│   │   ├── Views/                   # Screen XAML files
│   │   └── Converters/              # Data converters for UI
│   │
│   ├── GameServerApp.Plugins.Minecraft/  # Minecraft Java plugin
│   │   ├── MinecraftPlugin.cs        # IGameServerPlugin implementation
│   │   ├── JavaManager.cs            # Java download and management
│   │   └── MinecraftServerProperties.cs  # server.properties parser
│   │
│   ├── GameServerApp.Plugins.PaperMC/    # PaperMC plugin
│   │   └── PaperPlugin.cs            # PaperMC with Bukkit/Spigot plugin support
│   │
│   ├── GameServerApp.Plugins.FiveM/      # FiveM (GTA V) plugin
│   │   ├── FiveMPlugin.cs             # Download via cfx.re, server.cfg config
│   │   ├── FiveMConsoleParser.cs       # FXServer log parser
│   │   └── FiveMServerConfig.cs        # server.cfg read/write
│   │
│   └── GameServerApp.Plugins.Zomboid/    # Project Zomboid plugin
│       ├── ZomboidPlugin.cs            # Download via SteamCMD, Steam/Non-Steam support
│       ├── ZomboidConsoleParser.cs      # PZ server log parser
│       └── ZomboidIniConfig.cs          # .ini read/write
```

The project follows the **MVVM** pattern with clear separation between Core (logic), UI (interface), and Plugins (games).

---

## 🤝 Contributing

Contributions are welcome! Feel free to:

1. **Fork** the repository
2. Create a **branch** for your feature (`git checkout -b feature/my-feature`)
3. **Commit** your changes (`git commit -m 'feat: my feature'`)
4. **Push** to the branch (`git push origin feature/my-feature`)
5. Open a **Pull Request**

### Ideas for contributing

- 🎮 Plugins for new games (Terraria, Factorio, Valheim, CS2, Rust, etc.)
- 🌐 i18n support (internationalization)
- 📊 Dashboard with usage metrics (CPU, RAM, uptime)
- 🔔 Notification system
- 🎨 Customizable themes

---

## 📄 License

This project is distributed under the MIT License. See the [LICENSE](LICENSE) file for details.

---

<div align="center">

Made with ❤️ by [dvGomez](https://github.com/dvGomez)

</div>
