# Mahjong Night (working title)

3D online party game built on Hong Kong mahjong — Unreal Engine 5, C++ + Blueprints.
Design and roadmap: [OUTLINE.md](OUTLINE.md).

## Requirements
- **Unreal Engine 5.6** (Epic Games Launcher). A newer 5.x works too: right-click
  `MahjongNight.uproject` → *Switch Unreal Engine version…*
- **Windows:** Visual Studio 2022 with the *Game development with C++* workload
  (or JetBrains Rider). **macOS:** Xcode.
- **Git LFS** — run `git lfs install` once before cloning.

## First-time setup
```sh
git lfs install
git clone https://github.com/pupshii/mahjonggame.git
cd mahjonggame
```
1. Right-click `MahjongNight.uproject` → **Generate Visual Studio project files**.
2. Open `MahjongNight.sln`, set configuration to **Development Editor**, and build
   (or just double-click the `.uproject` and let Unreal compile the modules).
3. The editor opens with an empty level — the parlour map arrives in M3.

## Running tests
In the editor: **Tools → Session Frontend → Automation**, filter `MahjongCore`, run.

Command line (Windows):
```sh
"<UE>/Engine/Binaries/Win64/UnrealEditor-Cmd.exe" "%CD%/MahjongNight.uproject" ^
  -ExecCmds="Automation RunTests MahjongCore; Quit" -unattended -nopause -nullrhi -log
```

## Code layout
| Path | What |
|---|---|
| `Source/MahjongCore/` | Pure C++ rules (depends on `Core` only): tiles, wall, turns, scoring, party rules |
| `Source/MahjongCore/Private/Tests/` | Automation tests for the rules |
| `Source/MahjongNight/` | Game module: GameMode, GameState, pawns, table, networking |
| `Content/` | Blueprints, meshes, materials, audio (Git LFS) |
| `Config/` | Engine and project settings |

## Conventions
- Rules never go in `MahjongNight` or Blueprints — add them to `MahjongCore` with tests.
- `.uasset`/`.umap` files are binary and **lockable**: lock before editing shared
  assets (`git lfs lock Content/...`) to avoid conflicts that can't be merged.
