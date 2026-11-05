# Mahjong Night (working title)

3D online party game built on Hong Kong mahjong — **Unity 6**, C#.
Design and roadmap: [OUTLINE.md](OUTLINE.md).

## Requirements
- **Unity Hub** + **Unity 6.3 LTS** (project uses `6000.3.11f1`; any newer 6000.3.x patch is fine).
- **Git LFS** — run `git lfs install` once before cloning.
- An IDE: JetBrains Rider or Visual Studio Code (with the Unity extension).

## First-time setup
```sh
git lfs install
git clone https://github.com/pupshii/mahjonggame.git
```
1. Unity Hub → **Add → Add project from disk** → select the `mahjonggame` folder.
2. When asked, pick your Unity 6 editor version. Unity generates `ProjectSettings/`,
   `Packages/` and `.meta` files on first open.
3. **Commit those generated files** (`ProjectSettings/`, `Packages/manifest.json`,
   `Packages/packages-lock.json`, and every `.meta` file) so everyone shares the
   same settings and asset IDs. Never commit `Library/`.
4. Check **Edit → Project Settings → Editor**: *Asset Serialization* = **Force Text**
   (the default) so scenes and prefabs can be merged.

## Playing the greybox (M3 + M5 party layer)
1. Menu bar → **Mahjong Night → Create Greybox Scene** (first time only — creates
   `Assets/_Project/Scenes/Greybox.unity`; commit it). Later: **Mahjong Night → Open Greybox Scene**.
2. Press **Play**. You sit at the bottom against three bots.
3. On your turn, **click a tile in your hand** to discard it (it lifts when hovered).
   Claim buttons (Win / Pung / Kong / Chow / Pass) appear top-right when you can act.
4. **Dumpling Roulette:** after each hand the losers eat from the steamer in the middle
   (discarder on a discard win, everyone else on a self-draw, two each for 5+ faan, non-ready
   hands on a draw game). On your turn, click a dumpling. One is the ghost pepper — eat it and
   you're out (your seat is taken over by a neutral house bot). Last player standing wins.
5. Options on the **Greybox Game** object in the Inspector: party mode on/off, lives, shared or
   personal steamers, Classic rules, bot/roulette speed, your time limit, reveal bot hands
   (debug), fixed seed (replay the same match).

## Running tests
**Window → General → Test Runner → EditMode → Run All.**
If the Test Runner window is missing, install **Test Framework** from
Window → Package Manager → Unity Registry.

## Code layout
| Path | Assembly | What |
|---|---|---|
| `Assets/_Project/Scripts/Core/` | `MahjongCore` | Pure C# rules (no UnityEngine): tiles, wall, turns, scoring, party rules |
| `Assets/_Project/Scripts/Core/Bots/` | `MahjongCore` | Bot players (pure C#, tested) |
| `Assets/_Project/Scripts/Core/Party/` | `MahjongCore` | Party match: penalties, steamer, knockouts |
| `Assets/_Project/Scripts/Game/` | `MahjongNight.Game` | Unity side: table view, tiles, greybox game loop & UI |
| `Assets/_Project/Scripts/Editor/` | `MahjongNight.Editor` | Editor menu items |
| `Assets/_Project/Tests/EditMode/` | `MahjongCore.Tests` | NUnit tests for the rules |

More assemblies (Net, Table, Characters, UI) arrive with their milestones — see OUTLINE.md §4.

## Conventions
- Rules live only in `MahjongCore` (its asmdef has *No Engine References* on), with tests.
  Gameplay MonoBehaviours call into it; they never re-implement rules.
- Core code targets **C# 9** (Unity's language version): no file-scoped namespaces,
  global usings, or record structs.
- Only one person edits a given scene or prefab at a time — say so in chat before
  you start. Prefer small prefabs over big shared scenes.
