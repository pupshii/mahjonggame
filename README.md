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

## Playing the greybox
1. Menu bar → **Mahjong Night → Create Greybox Scene** (first time only; commit the scene).
   Later: **Mahjong Night → Open Greybox Scene**. Press **Play** and pick a difficulty:
   **Easy** (1 table), **Medium** (3 tables) or **Hard** (5 tables) — click, or press 1/2/3.
   **Esc** pauses (everything freezes) and lets you restart on another difficulty. (Untick
   *Show Difficulty Menu* on the GreyboxGame component to skip the menu and use *Table Count*.)
   **Five tables:** you hold a seat at 5 tables at once, each with its own 3 bots and its own
   match. A sign above every table shows a 30 s turn clock whenever it's waiting on you ("YOUR TURN
   14s"); when it hits 0 your move there is played for you. Only **one clock runs at a time**: the
   table you're sitting at, otherwise the one that has waited longest; the others say "on hold #2"
   and keep their time. Tables also start with different dealers and their bots play at different
   speeds, so your turns are spread out. Claim chances at tables you're away from only wait 6 s
   (and always count down). Press **F** to get up, walk to another table, stand behind your chair and press **F** (or
   **E** on the chair) to sit down and play there. The right-hand panel lists every table's status.
   When a table finishes its match it starts a new one by itself after a short break (your faan
   there is banked into your running total); the other tables never stop.
2. **Look around with the cursor:** the middle of the screen is calm; push to the left/right
   edges to watch the players beside you, the top edge to look across, the **bottom edge to look
   down** into your lap.
   **Table view:** hold **right-click** (or **Tab**) to lean over the table and see every discard
   pile and exposed set from above — but while you do, you can't watch anyone's hands.
   **Scroll** to zoom (in the table view, the cursor pans while zoomed); **middle-click** resets zoom.
3. **Your turn:** click a tile in your hand to discard it. Claim buttons appear above your hand.
   3 faan minimum to win.
4. **Cheat:** look down — a pocket strip appears. 1) pick a hand tile to slip into your pocket,
   2) pick one of your 5 pocket tiles (outside the game), 3) slip it into your hand. Anyone
   watching your hands during the motion can call you out: you pay 10 faan to each player and
   the hand ends.
5. **Catch cheaters:** if you see a bot reach under the table, press **SPACE** (calls out whoever
   you're looking at) or click them. A wrong call costs you 3 faan.
6. **Phone:** your dog texts you now and then ("when r u coming home??") — your view is forced
   onto your phone until you reply, and you can't act at the table meanwhile. **1-3** quick-call
   another player at your table: they stare at their phone for ~5 s (45 s cooldown). Bots text
   too, and may call *you* when you're watching them too closely.
   **P opens your phone's home screen** (1-3 or click to open an app, Backspace for home):
   - **Phone** — call a player at your table (same as 1-3), or call Latte to hear how they are.
   - **Drum Rush** — a side-scrolling drum rhythm game: the phone turns sideways and notes slide
     into the target. **Red = J or left click** (the drum's face), **blue = K or right click**
     (the rim). GREAT / GOOD / MISS, combos, a rank (S–D) and your best score per song. Three
     songs: *Mahjong Shuffle* (easy, 110 BPM), *Dim Sum Rush* (135) and *Latte Zoomies* (hard,
     160). While you drum you're on your phone — you can't play your hand and the turn clocks keep
     running — and a text or call pauses the song.
   - **Latte Cam** — what Latte is doing at home and how hungry they are.
   **Latte (your black-and-white border collie)** texts more the hungrier they get: *content* → no texts, *peckish* →
   every 70–120 s, *hungry* → every 35–60 s, *starving* → they **phone you howling** every
   15–30 s. The status panel shows Latte's bar. To feed them: walk to the club's **front door**
   (middle of the south wall, "HOME 回家") and press **E** (6 s walk) → at home, **E** the dog
   food bag to take a scoop (2 s) → **E** the red bowl → **E** your front door to walk back (6 s).
   Give them a bowl of your **chicken soup** instead and they stay full for longer; **E** on
   Latte to pet them (a little less hungry, 30 s cooldown). The tables play on while you're out.
   When you walk in, Latte runs to the door, **stands up on their hind legs and claps** ("FINALLY!!
   NOW. FOOD."), and keeps standing up to clap by the rug every few seconds until you feed them.
   Latte **walks like a person** — up on their hind legs, arms swinging, a little waddle — and
   drops to all fours when they get there. **E** their **blue ball** to throw it: Latte fetches it
   back to your feet (a little less hungry, like petting). Left alone they boop it around the rug.
   (A starving Latte won't play: food first.)
7. **Survival:** Food, Water and Pee bars drain over time (walking/sprinting drains food and
   water faster; drinking fills your bladder). Press **F** to stand up, **WASD** to walk,
   **Shift** to sprint, **E** to use the toilet / water jar / food counter, **F** behind any of
   your chairs to sit down at that table. Your turn clocks keep running while you're up (one at a time). If any bar hits 0 you die: every table stops, a death screen
   shows for 5 s, then the whole stage restarts (new matches everywhere, full bars, banked faan
   cleared). (Bots don't have bars yet.)
8. **Cooking (you're the club manager):** now and then a customer orders **chicken soup** (bubble
   above their head with a 4-minute timer; at most 2 orders open). In the **kitchen** (south-west
   corner) every step is a quick **mouse mini-game**, scored PERFECT / GREAT / OK / OOPS:
   - **E the stove knob — Light the stove:** a needle sweeps back and forth; click in the flame.
   - **E the pot — Fill it:** hold the mouse button to pour, let go at the dashed line (it pours
     faster near the top — overfill and you spill). Then 30 s to boil.
   - **E the chicken / onion / garlic / herbs — Slice:** hold the button above the ingredient
     and swipe DOWN through each dashed guide (do it while the water heats).
   - **E the boiling pot — Into the pot!:** drag every ingredient into the pot, fast.
   - **E the simmering pot — Stir!:** circle the mouse around the pot, 6 laps (skip it and the
     soup counts as half-stirred). Simmers 60 s.
   - **E the pot — Ladle a bowl:** pour up to the line again.
   - Walk to the customer and **E** them to serve. The soup's grade (shown on the recipe card as
     you go) sets the tip: 8 faan base, **+5 PERFECT, +2 GREAT, 0 OK, -4 OOPS**.
   A phone call or text knocks you out of a mini-game (just try again). Late orders cost you a
   5 faan refund; a finished pot left on a lit stove burns after 60 s.
9. **Shuffling (every hand):** before each hand the table washes its tiles by hand — all 144
   tip face-down into the middle and everyone's hands swirl them around (clack clack clack).
   At your table, **hold the left mouse button and sweep through the tiles** to help: they get
   pushed around under your hand and the shuffle bar fills much faster (about 6 s with the bots
   alone, ~2–3 s if you really go for it). Then the tiles fly into four walls of 18 stacks and
   the hands are dealt. Nobody plays, and no turn clock runs, until that's done.
10. **Between hands:** winners collect faan (discarder pays, or everyone on a self-draw); spend
   faan on upgrades (Sleight of Hand, Deep Pockets, Poker Face, Eagle Eye). Highest faan after
   8 hands wins.
11. Inspector options on **Greybox Game**: Classic rules, choose-your-draw, match length,
   penalties, bot nerve / spot chance, pacing, reveal bot hands, fixed seed.

## Running tests
**Window → General → Test Runner → EditMode → Run All.**
If the Test Runner window is missing, install **Test Framework** from
Window → Package Manager → Unity Registry.

## Code layout
| Path | Assembly | What |
|---|---|---|
| `Assets/_Project/Scripts/Core/` | `MahjongCore` | Pure C# rules (no UnityEngine): tiles, wall, turns, scoring, party rules |
| `Assets/_Project/Scripts/Core/Bots/` | `MahjongCore` | Bot players (pure C#, tested) |
| `Assets/_Project/Scripts/Core/Match/` | `MahjongCore` | Scored match: faan payouts, pockets, cheats caught, upgrades |
| `Assets/_Project/Scripts/Game/` | `MahjongNight.Game` | Unity side: table view, tiles, greybox game loop & UI |
| `Assets/_Project/Scripts/Editor/` | `MahjongNight.Editor` | Editor menu items |
| `Assets/_Project/Resources/TileFaces.png` | — | Traditional tile faces, generated by `Tools/generate_tile_faces.py` (Pillow + a CJK font) |
| `Assets/_Project/Tests/EditMode/` | `MahjongCore.Tests` | NUnit tests for the rules |

More assemblies (Net, Table, Characters, UI) arrive with their milestones — see OUTLINE.md §4.

## Conventions
- Rules live only in `MahjongCore` (its asmdef has *No Engine References* on), with tests.
  Gameplay MonoBehaviours call into it; they never re-implement rules.
- Core code targets **C# 9** (Unity's language version): no file-scoped namespaces,
  global usings, or record structs.
- Only one person edits a given scene or prefab at a time — say so in chat before
  you start. Prefer small prefabs over big shared scenes.
