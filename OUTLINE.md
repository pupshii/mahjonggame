# Mahjong Night (working title) — Project Outline

A **3D online party game** built on **Hong Kong mahjong**. 2–4 friends sit around a
table in a smoky back-alley Hong Kong mahjong parlour (麻雀館), talk trash over
voice chat, cheat when nobody is looking — and whoever loses a hand has to eat a
dumpling from the steamer… one of which is a ghost-pepper bomb.

Genre: cheap, chaotic, streamer-friendly "party slop" — short sessions, physical
comedy, social tension, easy to pick up even if you've never played mahjong.

> All characters, setting, art, and the penalty mechanic are our own. We take
> inspiration from the genre (online table game + voice chat + high-stakes penalty),
> not from any specific game's content.

---

## 1. Core Concept

### 1.1 Pitch pillars
1. **Real mahjong, fast.** Genuine HK rules underneath, but tuned for 3–5 minute hands.
2. **Social chaos.** Voice chat, cheating, accusations, emotes, slamming tiles.
3. **Stakes you can laugh at.** Dumpling Roulette — lose a hand, risk a spicy knockout.
4. **Readable for newbies.** Claim buttons light up, hand-value hints, auto-sort.

### 1.2 Match loop
```
Lobby (Steam invite) → pick character → sit at table
   └─► Play a hand of mahjong (party rules)
         └─► Losers face Dumpling Roulette
               ├─ safe dumpling   → keep playing
               └─ ghost pepper    → KO'd (ragdoll, fire breath) → become a ghost spectator
   └─► Repeat until one player is left → winner gets the golden mahjong tile
```
Target match length: **15–25 minutes**.

### 1.3 Dumpling Roulette (penalty)
- A bamboo steamer sits in the middle of the table with **6 dumplings**; one is the ghost pepper.
- After each hand, every *loser* (see 3.2) must pick a dumpling and eat it on camera.
- Eaten safe dumplings are **not** replaced, so the odds get worse for everyone as the match goes on.
- Eat the pepper → knocked out. The steamer is refilled for the remaining players.
- Optional variant: each player has their own steamer (personal risk instead of shared).

### 1.4 Cheating & Accusing (the "liar" layer)
Mahjong parlours are famous for cheats, so cheating is a mechanic:
- Once per hand, each player may **cheat**: swap a hand tile with the wall,
  peek at the next draw, or palm a discard back.
- Cheating plays a visible hand animation. **Other players' look direction is
  networked**, so you can only cheat safely when nobody is looking at your hands.
- Anyone can hit **Accuse (出千!)** on a player:
  - Correct → the cheater eats a dumpling immediately.
  - Wrong → the accuser eats one.
- This creates staring contests, distraction tactics over voice chat, and bluffing.

### 1.5 Modes
| Mode | Description |
|---|---|
| **Party** (default) | Fast rules + Dumpling Roulette + cheating |
| **Party (no cheats)** | Roulette only — for groups who just want mahjong + stakes |
| **Classic** | Full HK rules, full 4-round game, points scoring, no penalties |
| **Practice** | Solo vs bots with tutorial hints |

---

## 2. Characters & Setting

### 2.1 Setting
- A cramped, neon-lit 1980s–90s HK mahjong parlour: flickering fluorescent tube,
  ceiling fan, cigarette haze, a TV playing static, milk tea cups on the table,
  a window view of a rainy street with neon signs.
- Low-poly / slightly crunchy art style: cheap to produce, reads well on stream.
- Later maps: rooftop under the neon, a cha chaan teng after closing, a junk boat in the harbour.

### 2.2 Character concepts (original cast, placeholders)
| Character | Gimmick |
|---|---|
| **Auntie Perm** | Hair curlers, gold bangles, shouts every claim |
| **Roast Goose** | A literal roast goose in a vest; drips when nervous |
| **Pineapple Bun** | Crumbly bun guy, flakes on the table when he slams tiles |
| **Koi Uncle** | Lucky koi fish in a singlet and flip-flops, always "lucky" |

Each character: idle/fidget animations, claim voice lines (碰! 上! 槓! 食糊!),
reaction to eating a pepper, unique tile-slam style. Cosmetics are a later goal.

---

## 3. Game Rules

### 3.1 Hong Kong mahjong basics (the engine models all of this)

**Tiles (144):** Dots, Bamboo, Characters 1–9 (×4 each), Winds E/S/W/N (×4),
Dragons Red/Green/White (×4), Flowers 1–4 and Seasons 1–4 (×1 each). Flowers and
seasons are bonus tiles, set aside and replaced from the back of the wall.

**Turn:** draw (or claim the last discard) → optional kong → self-draw win or discard.

**Claims & priority:** Win (食糊) > Kong (槓) / Pung (碰) > Chow (上, next player only).
Robbing the kong (搶槓) allowed.

**Winning hand:** 4 melds + 1 pair, or special hands (Thirteen Orphans; Seven Pairs optional).

**Faan (番) scoring — defaults, all configurable:**
| Pattern | Faan |
|---|---|
| Self-drawn (自摸), Concealed (門前清), No flowers (無花) | 1 each |
| Own flower / season | 1 each |
| Pung of dragon / seat wind / prevailing wind | 1 each |
| All chows (平糊), Last tile (海底撈月), Kong replacement (槓上開花), Robbing kong (搶槓) | 1 each |
| Full flower or season set (一台花) | 2 |
| All pungs (對對糊), Mixed one suit (混一色) | 3 each |
| Seven pairs (七對) *(optional)* | 4 |
| Small three dragons (小三元) | 5 |
| Pure one suit (清一色) | 7 |
| Limit hands: Big three dragons, Small/Big four winds, All honours, All terminals, Thirteen orphans, Nine gates, Four concealed pungs, Four kongs, Heavenly/Earthly hand | Max |

Note: a hand can be split into melds in several ways (111222333 = three pungs *or*
three chows); the scorer must try every split and take the highest faan.

### 3.2 Party rule set (tuned for speed and fun)
| Rule | Party default | Classic |
|---|---|---|
| Minimum faan to win | 0 (any valid hand) | 3 |
| Turn timer | 10 s (auto-discard on timeout) | 20 s |
| Claim window | 3 s | 5 s |
| Match structure | Hands until 1 player left | 4 rounds (E/S/W/N) |
| Who "loses" a hand | Win on discard → **discarder**; self-drawn → **everyone else** | Points payout |
| Draw game (wall empty) | Everyone with no ready hand eats | Dealer stays |
| Big hands (≥ 5 faan) | Losers eat **two** dumplings | Bigger payout |
| Players | 2–4 (empty seats filled by bots) | 4 |

Big hands carrying extra risk keeps the mahjong strategy meaningful: going for
a pure one suit is a gamble that can wipe the table.

---

## 4. Tech Plan

### 4.1 Stack (recommended)
| Area | Choice | Notes |
|---|---|---|
| Engine | **Unity 6 (C#)** | Most common choice for indie 3D party games; huge asset/tutorial base |
| Netcode | Netcode for GameObjects *or* FishNet | Host-authoritative (one player hosts) |
| Lobbies / invites | Steamworks (Facepunch.Steamworks) | Steam friends invite, relay = no port forwarding |
| Voice chat | Vivox or Steam Voice | Proximity not needed — everyone's at one table |
| Physics / ragdoll | Unity physics | Tiles are physical objects for "juice" |

Alternative: **Godot 4** (free, open source) if you'd rather avoid Unity.

### 4.2 Architecture
- **Pure C# game engine** (separate assembly, no `UnityEngine` reference): tiles,
  wall, turns, claims, win detection, faan, party rules. Fully unit-tested, deterministic,
  seeded RNG.
- **Host-authoritative:** only the host runs the engine. Clients send *intents*
  ("discard 5 Bamboo", "accuse player 3"); the host validates and broadcasts results.
- **Hidden information:** clients only receive their own hand + public info, so a
  modded client can't see other hands or the wall.
- **Presentation layer** listens to engine events and plays animations/sound
  (tile slides, slams, roulette, ragdolls). The engine never waits on animation.

### 4.3 Project layout
```
Assets/_Project/
  Scripts/
    Engine/        Pure C#: Tiles, Wall, Hand, Actions, GameState, RuleSet
    Scoring/       Decomposition, Faan detectors, Payout
    Party/         Lives, Dumpling Roulette, Cheats, Accusations
    Bots/          Bot players (fill empty seats, practice mode)
    Net/           Lobby, Steam, message types, host/client sync
    Table/         3D table, tile objects, seat cameras, look-direction sync
    Characters/    Animation controllers, voice lines, ragdoll
    UI/            Claim buttons, hand hints, menus, settings
  Art/  Audio/  Prefabs/  Scenes/
Tests/EditMode/    NUnit tests for Engine, Scoring, Party
```

---

## 5. Juice & Feel Checklist
- Heavy, clacky tile sounds; screen shake and a shout on Pung/Kong
- First-person seat camera, free head look (and your head turns for others to see)
- Push-to-talk + text chat bubbles; emotes (slam table, point, laugh, sweat)
- Dumpling Roulette moment: slow zoom, heartbeat audio, everyone watches
- Pepper KO: face turns red, fire breath, chair tips over, ragdoll
- Dead players become floating ghosts who can heckle (voice) but not see hands
- Winning hand reveal: tiles flip with a big faan counter rolling up

---

## 6. Milestones

| # | Milestone | Done when |
|---|---|---|
| M0 | Setup | Unity project, Git LFS, engine assembly, test runner green |
| M1 | Engine core | Tiles, wall, dealing, flowers, turn loop, claims — headless tests pass |
| M2 | Win & faan | Win detection + faan calculator with tests for every pattern |
| M3 | Greybox table | Single player vs 3 bots in a 3D greybox room, playable start to finish |
| M4 | Multiplayer | Steam lobby, 2–4 players online, host-authoritative, bots fill seats |
| M5 | Party layer | Lives, Dumpling Roulette, eliminations, ghost spectators |
| M6 | Cheat & accuse | Cheats, look-direction sync, accusation flow |
| M7 | Characters & voice | 4 characters, animations, voice lines, voice chat, emotes |
| M8 | Art & polish | Parlour environment, lighting, juice pass, menus, settings |
| M9 | Ship | Playtests, Steam page, trailer, release (~US$5 price point) |

Get M3 fun before investing in M4+: if the greybox party loop isn't funny with
bots and placeholder cubes, art won't save it.

---

## 7. Testing
- Unit tests for every faan pattern and tricky decompositions.
- Invariant checks: 144 tiles always accounted for; no duplicates across zones.
- Bot-vs-bot soak tests: thousands of simulated hands to catch illegal states.
- Network tests: two local instances via ParrelSync; latency/disconnect simulation.
- Weekly friend playtests from M5 onward — measure laughs, not just bugs.

---

## 8. Open Questions
- Unity or Godot?
- Cheating in v1, or ship roulette-only first and add cheats as an update?
- Shared steamer vs personal steamers?
- Art direction: low-poly clean, or crunchy PSX-style?
- Do we keep Classic mode in v1 or cut it to ship faster?
