# Mahjong Night (working title)

3D online party game built on Hong Kong mahjong — **Unity 6**, C#.
Design and roadmap: [OUTLINE.md](OUTLINE.md).

## Requirements
- **Unity Hub** + a **Unity 6** editor (latest LTS recommended).
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

## Running tests
**Window → General → Test Runner → EditMode → Run All.**
If the Test Runner window is missing, install **Test Framework** from
Window → Package Manager → Unity Registry.

## Code layout
| Path | Assembly | What |
|---|---|---|
| `Assets/_Project/Scripts/Core/` | `MahjongCore` | Pure C# rules (no UnityEngine): tiles, wall, turns, scoring, party rules |
| `Assets/_Project/Tests/EditMode/` | `MahjongCore.Tests` | NUnit tests for the rules |

More assemblies (Net, Table, Characters, UI) arrive with their milestones — see OUTLINE.md §4.

## Conventions
- Rules live only in `MahjongCore` (its asmdef has *No Engine References* on), with tests.
  Gameplay MonoBehaviours call into it; they never re-implement rules.
- Core code targets **C# 9** (Unity's language version): no file-scoped namespaces,
  global usings, or record structs.
- Only one person edits a given scene or prefab at a time — say so in chat before
  you start. Prefer small prefabs over big shared scenes.
