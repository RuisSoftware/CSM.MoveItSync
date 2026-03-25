# CSM.MoveItSync

[![Mod Version](https://img.shields.io/badge/version-v0.1.0--beta-orange.svg)](https://github.com/CSM-Mods/CSM.MoveItSync)
[![CSM Version](https://img.shields.io/badge/CSM-v2603.307-blue.svg)](https://github.com/CitiesSkylinesMultiplayer/CSM)
[![License](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE)

**CSM.MoveItSync** is a synchronization bridge between the popular [Move It](https://steamcommunity.com/sharedfiles/filedetails/?id=1619685021) mod and [Cities: Skylines Multiplayer (CSM)](https://cmultiplayer.com/). It allows players in a multiplayer session to see and interact with each other's object transformations in real-time.

## 🚀 Features

- **Real-Time Transformation Sync**: Synchronize moving, rotating, and scaling of buildings, props, and trees.
- **Unified Action Bridge**: Syncs complex tools like **Align Height**, **Line Tool**, and **Mirror** using a single, efficient state-based synchronization path.
- **Bulk Operations**: Efficiently handles large selections of objects.
- **Stability Patches**: Includes custom Harmony patches for `SimulationManager` to prevent desync loops and ensure thread-safe operations on the simulation thread.
- **Professional Logging**: Categorized, daily log files for easy troubleshooting.

## 📦 Installation

1. Ensure you have **Cities: Skylines Multiplayer (CSM)** installed.
2. Download the latest `CSM.MoveItSync.dll`.
3. Place the DLL into your Cities: Skylines `Mods` directory.
4. Enable the mod in the game's Content Manager.

## 🛠️ Technical Architecture

### Unified Synchronization
Unlike traditional integrations, this mod uses a **Generic Action Bridge**. It intercepts Move It's internal `ActionQueue` and captures the final state of all modified objects. This ensures that even if Move It adds new tools, the bridge can likely synchronize them without code changes.

### Stability & Loop Prevention
The mod implements a "Hardware-level" (Simulation Thread) ignore propagation. When a synchronization command is applied, it ensures that any asynchronous game updates triggered by Move It are correctly flagged as "ignored" by CSM, preventing infinite feedback loops.

## 📝 Logging
Logs are stored in your local application data folder:
- **Linux**: `~/.local/share/Colossal Order/Cities_Skylines/CSM.MoveItSync/`
- **Windows**: `%LOCALAPPDATA%\Colossal Order\Cities_Skylines\CSM.MoveItSync\`

## 🧪 Current Limitations (Beta)
- **Road Nodes**: Currently supports basic position updates; advanced road bending and segment nodes are partially implemented.
- **Cloning/Paste**: Cloning objects is currently local-only.
- **Procedural Objects**: PO-specific transformations are not yet synchronized.

## 🤝 Contributing
Contributions are welcome! Please feel free to submit Pull Requests or open issues on the GitHub repository.

## ⚖️ License
This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.
