# Hong Kong Mahjong — Project Outline

A digital version of Hong Kong (Cantonese) mahjong: 4 players, 144 tiles,
scoring in *faan* (番), with a configurable minimum faan to win.

---

## 1. Game Rules (what the engine must model)

### 1.1 Tiles (144 total)
| Group | Tiles | Copies | Count |
|---|---|---|---|
| Dots / Circles (筒) | 1–9 | ×4 | 36 |
| Bamboo (索) | 1–9 | ×4 | 36 |
| Characters (萬) | 1–9 | ×4 | 36 |
| Winds (風) | East, South, West, North | ×4 | 16 |
| Dragons (三元) | Red 中, Green 發, White 白 | ×4 | 12 |
| Flowers (花) | Plum, Orchid, Chrysanthemum, Bamboo (1–4) | ×1 | 4 |
| Seasons (季) | Spring, Summer, Autumn, Winter (1–4) | ×1 | 4 |

Flowers/seasons are bonus tiles: set aside when drawn, replaced from the back of the wall.

### 1.2 Setup
- 4 seats: East (dealer/莊), South, West, North.
- Prevailing wind (圈風) starts at East; a full game = 4 rounds (East, South, West, North), each with ≥4 hands.
- Shuffle, build wall, roll dice to pick the break point.
- Deal 13 tiles each; dealer takes a 14th and discards first.
- Replace any flowers/seasons in the starting hands.

### 1.3 Turn flow
1. Draw a tile (or claim the previous discard).
2. Optionally declare a concealed kong / promote a pung to a kong (draw replacement tile).
3. Declare self-drawn win (自摸) **or** discard one tile.
4. Other players get a window to claim the discard.

### 1.4 Claims & priority
| Claim | Who | Meld |
|---|---|---|
| Win (食糊) | Anyone | Completes hand |
| Kong (槓) | Anyone | 3 in hand + discard |
| Pung (碰) | Anyone | 2 in hand + discard |
| Chow (上) | **Next player only** | Sequence of 3 |

Priority: **Win > Kong/Pung > Chow**. Multiple winners on one discard → configurable
(nearest player in turn order wins, or allow multiple).
Robbing the kong (搶槓): others may win off a tile used to promote a pung to a kong.

### 1.5 Winning hand
- Standard: 4 melds (pung/kong/chow) + 1 pair.
- Special hands: Thirteen Orphans (十三么); Seven Pairs (七對) as an optional house rule.
- Must meet the **minimum faan** (commonly 3; configurable 0–3).

### 1.6 End of hand & dealer rotation
- Dealer wins or hand is a draw (流局) → dealer stays (連莊).
- Otherwise → dealer passes to the next player (South becomes East, etc.).
- After every seat has dealt once, prevailing wind advances.
- Draw game: wall runs out (keep a configurable dead wall of tiles).

---

## 2. Scoring (Faan 番)

Faan values vary by table; ship a sensible default and make every value configurable.

### 2.1 Common faan (default values)
| Pattern | Faan |
|---|---|
| Self-drawn (自摸) | 1 |
| Concealed hand, won on discard (門前清) | 1 |
| No flowers (無花) | 1 |
| Own flower / own season (正花) | 1 each |
| Full set of 4 flowers or 4 seasons (一台花) | 2 |
| Pung of dragons | 1 each |
| Pung of seat wind (門風) | 1 |
| Pung of prevailing wind (圈風) | 1 |
| All chows (平糊) | 1 |
| Win on last tile (海底撈月) | 1 |
| Win on kong replacement (槓上開花) | 1 |
| Robbing the kong (搶槓) | 1 |
| All pungs (對對糊) | 3 |
| Mixed one suit (混一色) | 3 |
| Seven pairs (七對) *(optional)* | 4 |
| Small three dragons (小三元) | 5 |
| Pure one suit (清一色) | 7 |

### 2.2 Limit hands (爆棚 / max faan)
Big three dragons (大三元), Small four winds (小四喜), Big four winds (大四喜),
All honours (字一色), All terminals (清么九), Thirteen orphans (十三么),
Nine gates (九子連環), Four concealed pungs (坑坑糊), Four kongs (十八羅漢),
Heavenly hand (天糊), Earthly hand (地糊), all 8 flowers (八仙過海).

### 2.3 Payout
- Faan → points via a lookup table (roughly doubling per faan, capped at the limit,
  e.g. 10 or 13 faan). Support named tables like "half-spicy" (半辣) / "full-spicy" (全辣).
- **Win on discard:** discarder pays (optionally: discarder pays double, others pay single).
- **Self-drawn:** all three others pay.
- Optional rules: responsibility payment (包) for feeding a limit hand, etc.

### 2.4 Scoring algorithm note
A hand can decompose into melds multiple ways (e.g. 111222333 = three pungs or three chows).
The calculator must **enumerate all valid decompositions and pick the highest faan**.

---

## 3. Architecture

Recommended stack (change if you prefer): **TypeScript**, web front end, engine as a
pure library so it runs in the browser, a Node server, and tests alike.

```
/src
  /engine        Pure game logic — no UI, no I/O, fully deterministic
    tiles.ts       Tile types, full set, encoding, sorting
    wall.ts        Shuffle (seeded RNG), deal, draw, replacement draws, dead wall
    hand.ts        Concealed tiles, exposed melds, bonus tiles
    actions.ts     Legal-action generator (discard, chow, pung, kong, win)
    state.ts       GameState + reducer: apply(state, action) -> state
    rules.ts       RuleSet config (min faan, limit, house rules)
  /scoring
    decompose.ts   Find all 4-melds-+-pair decompositions; special hands
    faan.ts        Pattern detectors -> faan breakdown
    payout.ts      Faan -> points, who pays whom
  /ai
    bot.ts         Bot interface: chooseAction(view) -> action
    simple.ts      Heuristic bot (shanten / tile efficiency)
  /ui            Rendering, input, animations, sound
  /net           (later) Multiplayer client/server sync
/tests           Unit tests per module + scripted full-hand scenarios
```

Key design decisions:
- **Reducer-style state machine**: every move is an action; makes replays, undo,
  networking, and testing easy.
- **Player views**: each player/bot only sees what they're allowed to see (no peeking at walls).
- **Seeded RNG**: reproducible deals for debugging and replays.
- **RuleSet object**: all house-rule variation lives in config, not in `if` statements scattered around.

---

## 4. Features

### MVP
- Single player vs 3 bots
- Full rules: claims, kongs, flowers, dealer rotation, a full 4-round game
- Faan breakdown shown on win; running score table
- Tile sorting, highlighting of claimable actions, claim timer

### Later
- Online multiplayer (rooms, reconnect)
- Stronger AI (defensive play, reading discards)
- Rule presets / custom house rules screen
- Replays and hand history
- Tutorial mode with hints ("you can pung this", "this hand is worth N faan")
- Cantonese / English / Chinese localisation
- Mobile-friendly layout, sound effects

---

## 5. Milestones

| # | Milestone | Done when |
|---|---|---|
| M0 | Project setup | Repo, build, lint, test runner working |
| M1 | Tiles & wall | Can shuffle, deal 13/14, replace flowers; tests pass |
| M2 | Turn loop | Text/CLI game: draw, discard, chow/pung/kong with priority |
| M3 | Win detection | Correctly detects standard + special winning hands |
| M4 | Faan & payout | Full faan breakdown + points; scenario tests for every pattern |
| M5 | Full game flow | Dealer rotation, prevailing wind, draw games, final standings |
| M6 | Bots | Heuristic bots can play complete games against each other |
| M7 | UI | Playable graphical table vs 3 bots |
| M8 | Polish | Animations, sound, settings, rule presets |
| M9 | Multiplayer | (optional) Online rooms |

---

## 6. Testing strategy
- Unit tests for decomposition and every faan pattern (use known example hands).
- Property tests: tile counts always total 144; no tile duplicated across zones.
- Bot-vs-bot simulation of thousands of games to catch illegal states.
- Seeded scenario tests for edge cases: robbing the kong, kong replacement win,
  last-tile win, multiple players claiming the same discard.

---

## 7. Open questions
- Platform: web, mobile, or desktop?
- Single-player only first, or multiplayer from the start?
- Default minimum faan (0 / 1 / 3) and limit (10 / 13)?
- Include Seven Pairs? Which payout table?
