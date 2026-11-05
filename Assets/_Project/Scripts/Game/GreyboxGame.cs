using System.Collections.Generic;
using System.Linq;
using MahjongNight.Core;
using UnityEngine;

namespace MahjongNight.Game
{
    /// <summary>
    /// Greybox party match: you vs three <see cref="SimpleBot"/>s on a primitive 3D table.
    /// Hands of mahjong, then Dumpling Roulette for the losers, until one player is left.
    /// Builds everything at runtime — add to an empty GameObject (or use
    /// "Mahjong Night → Create Greybox Scene") and press Play.
    /// </summary>
    public sealed class GreyboxGame : MonoBehaviour
    {
        [Header("Rules")]
        [Tooltip("Off = Party rules (any hand wins). On = Classic (3 faan minimum).")]
        [SerializeField] private bool classicRules = false;

        [Header("Party")]
        [Tooltip("Dumpling Roulette and knockouts. Off = plain mahjong, hands forever.")]
        [SerializeField] private bool partyMode = true;
        [Tooltip("Peppers you can survive before being knocked out.")]
        [SerializeField] private int lives = 1;
        [Tooltip("One steamer for the table (on) or one per player (off).")]
        [SerializeField] private bool sharedSteamer = true;

        [Header("Pacing")]
        [SerializeField] private float botTurnDelay = 0.8f;
        [SerializeField] private float botClaimDelay = 0.5f;
        [Tooltip("Pause before each bot eats a dumpling (suspense!).")]
        [SerializeField] private float rouletteDelay = 1.6f;
        [Tooltip("Seconds before you auto-discard / auto-pass / auto-eat. 0 = no limit.")]
        [SerializeField] private float humanTimeLimit = 20f;

        [Header("Debug")]
        [SerializeField] private bool revealBotHands = false;
        [Tooltip("0 = a different match every run.")]
        [SerializeField] private int seed = 0;

        private const int HumanSeat = 0;
        private const int FullLogLines = 16;
        private const int StatusLogLines = 3;
        private const float FastForwardSpeed = 0.15f;

        private RuleSet rules;
        private PartyRules partyRules;
        private PartyMatch match;
        private HandState Hand => match.Hand;
        private readonly IBot[] bots = new IBot[HandState.SeatCount];
        private SeededRandom seedSource;
        private SeededRandom botDumplingPicker;

        private TableView table;
        private SteamerView steamer;
        private Camera cam;

        private float nextBotTime;
        private float humanWaitStart;
        private float flashUntil;
        private bool fastForward;
        private readonly int[] wins = new int[HandState.SeatCount];

        private readonly List<string> log = new List<string>();
        private int loggedHandEvents;
        private int loggedPartyEvents;
        private List<string> resultLines = new List<string>();
        private string penaltyLine;
        private string rouletteLine;

        private TileView hoveredTile;
        private DumplingView hoveredDumpling;
        private readonly List<Rect> uiRects = new List<Rect>();
        private float stylesForHeight;
        private GUIStyle boxStyle, textStyle, titleStyle, centerStyle, logStyle, buttonStyle;
        private bool showFullLog;

        // ------------------------------------------------------------------ lifecycle

        private void Start()
        {
            rules = classicRules ? RuleSet.Classic() : RuleSet.Party();
            partyRules = new PartyRules
            {
                RouletteEnabled = partyMode,
                Lives = Mathf.Max(1, lives),
                SharedSteamer = sharedSteamer,
            };
            seedSource = new SeededRandom(seed != 0 ? (ulong)seed : (ulong)System.DateTime.Now.Ticks);

            SetUpCameraAndLight();
            table = new TableView(transform, new TileFactory());
            steamer = new SteamerView(transform, partyRules.DumplingsPerSteamer);
            NewMatch();
        }

        private void SetUpCameraAndLight()
        {
            cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera");
                go.tag = "MainCamera";
                cam = go.AddComponent<Camera>();
            }
            cam.transform.position = new Vector3(0f, 0.85f, -1.1f);
            cam.transform.LookAt(new Vector3(0f, 0f, -0.3f));
            cam.fieldOfView = 55f;
            cam.nearClipPlane = 0.02f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.07f, 0.06f, 0.08f);

            if (Object.FindAnyObjectByType<Light>() == null)
            {
                var go = new GameObject("Directional Light");
                var light = go.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.1f;
                light.shadows = LightShadows.Soft;
                go.transform.rotation = Quaternion.Euler(55f, -30f, 0f);
            }
            RenderSettings.ambientLight = new Color(0.45f, 0.45f, 0.5f);
        }

        private void NewMatch()
        {
            ulong matchSeed = seedSource.NextUInt64();
            match = new PartyMatch(rules, partyRules, matchSeed);
            botDumplingPicker = new SeededRandom(matchSeed ^ 0x5EEDUL);
            for (int s = 0; s < wins.Length; s++)
                wins[s] = 0;
            fastForward = false;
            loggedPartyEvents = 0;
            AddLog($"New match (seed {matchSeed}).");
            StartHand();
        }

        private void StartHand()
        {
            match.StartHand();
            ulong botSeed = seedSource.NextUInt64();
            for (int s = 0; s < bots.Length; s++)
                bots[s] = new SimpleBot(botSeed + (ulong)s * 7919UL);

            loggedHandEvents = 0;
            resultLines = new List<string>();
            penaltyLine = null;
            rouletteLine = null;
            FlushPartyEvents();
            OnHandChanged();
        }

        private float Pace => fastForward ? FastForwardSpeed : 1f;

        /// <summary>True while the human is still in the match and plays their own seat.</summary>
        private bool HumanPlays(int seat) => seat == HumanSeat && match.Players[HumanSeat].IsAlive;

        private void Update()
        {
            if (match == null)
                return;

            steamer.SetFlash(Time.time < flashUntil);

            switch (match.Phase)
            {
                case MatchPhase.PlayingHand:
                    UpdateHand();
                    break;
                case MatchPhase.Roulette:
                    UpdateRoulette();
                    break;
                case MatchPhase.BetweenHands:
                    if (fastForward && Time.time >= nextBotTime)
                        StartHand();
                    break;
            }
        }

        // ------------------------------------------------------------------ mahjong hand

        private void UpdateHand()
        {
            if (Hand.Phase == HandPhase.Ended)
                return;

            if (HumanActions().Count > 0 && humanTimeLimit > 0f && Time.time - humanWaitStart > humanTimeLimit)
            {
                AutoPlayHuman();
                return;
            }
            if (Time.time < nextBotTime)
                return;

            if (Hand.Phase == HandPhase.AwaitingAction)
            {
                if (!HumanPlays(Hand.CurrentSeat))
                    BotAct(Hand.CurrentSeat);
            }
            else
            {
                foreach (int s in Hand.SeatsAwaitingResponse())
                {
                    if (!HumanPlays(s))
                    {
                        BotAct(s);
                        return;
                    }
                }
            }
        }

        private List<PlayerAction> HumanActions() =>
            match.Phase == MatchPhase.PlayingHand && HumanPlays(HumanSeat)
                ? Hand.GetLegalActions(HumanSeat)
                : new List<PlayerAction>();

        private void BotAct(int seat)
        {
            List<PlayerAction> legal = Hand.GetLegalActions(seat);
            if (legal.Count > 0)
                Apply(bots[seat].Choose(Hand, seat, legal));
        }

        private void AutoPlayHuman()
        {
            List<PlayerAction> legal = HumanActions();
            PlayerAction action;
            if (Hand.Phase == HandPhase.AwaitingAction)
            {
                PlayerAction drawnDiscard = Hand.LastDrawnTile.HasValue
                    ? PlayerAction.Discard(HumanSeat, Hand.LastDrawnTile.Value)
                    : default;
                action = legal.Contains(drawnDiscard) ? drawnDiscard : legal.First(a => a.Type == ActionType.Discard);
            }
            else
            {
                action = PlayerAction.Pass(HumanSeat);
            }
            AddLog("(Time's up — played for you)");
            Apply(action);
        }

        private void Apply(PlayerAction action)
        {
            if (!Hand.TryApply(action, out string error))
            {
                Debug.LogWarning(error);
                return;
            }
            OnHandChanged();
        }

        private void OnHandChanged()
        {
            FlushHandEvents();
            hoveredTile = null;
            humanWaitStart = Time.time;
            nextBotTime = Time.time + Pace * (Hand.Phase == HandPhase.AwaitingAction ? botTurnDelay : botClaimDelay);
            if (Hand.Phase == HandPhase.Ended && match.Phase == MatchPhase.PlayingHand)
                OnHandEnded();
            table.Rebuild(Hand, HumanSeat, revealBotHands);
            RefreshSteamer();
        }

        private void OnHandEnded()
        {
            HandResult result = Hand.Result;
            resultLines = new List<string>();

            if (result.Outcome == HandOutcome.ExhaustiveDraw)
            {
                resultLines.Add("Draw game — the wall is empty.");
            }
            else
            {
                for (int i = 0; i < result.Winners.Count; i++)
                {
                    int winner = result.Winners[i];
                    wins[winner]++;
                    FaanResult score = FaanCalculator.Calculate(result.WinContexts[i], rules);
                    resultLines.Add($"{DescribeWin(result, winner)} — {score.Total} faan" + (score.IsLimit ? " (LIMIT!)" : ""));
                    foreach (FaanItem item in score.Items)
                        resultLines.Add($"    {item}   {FaanPatterns.ChineseName(item.Pattern)}");
                }
            }

            match.ResolveHand();
            FlushPartyEvents();
            nextBotTime = Time.time + Pace * rouletteDelay;
            humanWaitStart = Time.time;
        }

        // ------------------------------------------------------------------ roulette

        private void UpdateRoulette()
        {
            int eater = match.CurrentEater;
            if (HumanPlays(eater))
            {
                if (humanTimeLimit > 0f && Time.time - humanWaitStart > humanTimeLimit)
                {
                    AddLog("(Time's up — a dumpling was picked for you)");
                    EatRandom(eater);
                }
                return;
            }
            if (Time.time >= nextBotTime)
                EatRandom(eater);
        }

        private void EatRandom(int seat)
        {
            List<int> slots = match.SteamerFor(seat).UneatenSlots();
            Eat(seat, slots[botDumplingPicker.NextInt(slots.Count)]);
        }

        private void Eat(int seat, int slot)
        {
            if (!match.TryEat(seat, slot, out string error))
            {
                Debug.LogWarning(error);
                return;
            }
            FlushPartyEvents();
            hoveredDumpling = null;
            steamer.SetHover(null);
            humanWaitStart = Time.time;
            nextBotTime = Time.time + Pace * rouletteDelay;
            RefreshSteamer();
            table.Rebuild(Hand, HumanSeat, revealBotHands);
        }

        /// <summary>Shows the steamer of whoever is (about to be) eating, or the shared one.</summary>
        private void RefreshSteamer()
        {
            steamer.SetVisible(partyRules.RouletteEnabled);
            int seat = match.CurrentEater >= 0 ? match.CurrentEater : HumanSeat;
            steamer.Show(match.SteamerFor(seat));
        }

        // ------------------------------------------------------------------ names & log

        private string SeatName(int seat)
        {
            string who = seat == HumanSeat ? "You" : "Bot " + seat;
            string name = Hand == null ? who : $"{who} ({Hand.Seats[seat].SeatWind})";
            return match.Players[seat].IsAlive ? name : name + " [out]";
        }

        private string Possessive(int seat) => seat == HumanSeat ? "your" : SeatName(seat) + "'s";

        /// <summary>"Bot 2 (West) wins on your discard with 6C".</summary>
        private string DescribeWin(HandResult result, int winner)
        {
            string how = result.SelfDrawn ? "self-drawn"
                : result.RobbedKong ? $"by robbing {Possessive(result.Discarder)} kong"
                : $"on {Possessive(result.Discarder)} discard";
            string verb = winner == HumanSeat ? "win" : "wins";
            return $"{SeatName(winner)} {verb} {how} with {result.WinningTile}";
        }

        private void AddLog(string line)
        {
            log.Add(line);
            if (log.Count > 300)
                log.RemoveAt(0);
        }

        private void FlushHandEvents()
        {
            for (; loggedHandEvents < Hand.Events.Count; loggedHandEvents++)
            {
                HandEvent e = Hand.Events[loggedHandEvents];
                switch (e)
                {
                    case TileDrawnEvent d when d.Seat == HumanSeat && HumanPlays(HumanSeat):
                        AddLog($"You draw {d.Tile}{(d.FromBack ? " (replacement)" : "")}");
                        break;
                    case BonusTileRevealedEvent b:
                        AddLog($"{SeatName(b.Seat)} reveals {b.Tile}");
                        break;
                    case TileDiscardedEvent t:
                        AddLog($"{SeatName(t.Seat)} discards {t.Tile}");
                        break;
                    case MeldDeclaredEvent m:
                        AddLog($"{SeatName(m.Seat)}: {m.Meld}");
                        break;
                    case HandEndedEvent h:
                        if (h.Result.Outcome == HandOutcome.ExhaustiveDraw)
                            AddLog("Draw game — the wall is empty.");
                        else
                            foreach (int w in h.Result.Winners)
                                AddLog(DescribeWin(h.Result, w));
                        break;
                }
            }
        }

        private void FlushPartyEvents()
        {
            for (; loggedPartyEvents < match.Events.Count; loggedPartyEvents++)
            {
                PartyEvent e = match.Events[loggedPartyEvents];
                switch (e)
                {
                    case HandStartedEvent h:
                        AddLog($"— Hand {h.HandNumber}: {SeatName(h.Dealer)} deals, {h.PrevailingWind} round —");
                        break;
                    case PenaltiesAssignedEvent p:
                        var eaters = Enumerable.Range(0, p.DumplingsPerSeat.Count)
                            .Where(s => p.DumplingsPerSeat[s] > 0)
                            .Select(s => p.DumplingsPerSeat[s] > 1 ? $"{SeatName(s)} ×{p.DumplingsPerSeat[s]}" : SeatName(s))
                            .ToList();
                        if (!partyRules.RouletteEnabled)
                            penaltyLine = null;
                        else if (eaters.Count == 0)
                            penaltyLine = "Nobody has to eat this time.";
                        else
                            penaltyLine = (p.BigHand ? "BIG HAND! " : "") + "Must eat: " + string.Join(", ", eaters);
                        if (penaltyLine != null)
                            AddLog(penaltyLine);
                        break;
                    case DumplingEatenEvent d:
                        rouletteLine = d.Pepper
                            ? $"{SeatName(d.Seat)} bites into the GHOST PEPPER!"
                            : $"{SeatName(d.Seat)} eats a dumpling… safe. ({d.RemainingInSteamer} left)";
                        if (d.Pepper)
                            flashUntil = Time.time + 1.5f;
                        AddLog(rouletteLine);
                        break;
                    case KnockedOutEvent k:
                        rouletteLine += $"  {SeatName(k.Seat)} is knocked out!";
                        AddLog($"{SeatName(k.Seat)} is knocked out!");
                        break;
                    case SteamerRefilledEvent _:
                        AddLog("The steamer is refilled.");
                        break;
                    case MatchWonEvent w:
                        AddLog($"{SeatName(w.Seat)} {(w.Seat == HumanSeat ? "win" : "wins")} the match!");
                        break;
                }
            }
        }

        // ------------------------------------------------------------------ input & UI
        //
        // Layout: status + recent log in the top-left corner (floor, not table); bot name tags
        // float above their hands; the action bar and the result/roulette panel sit just above
        // your hand. Corners near the table edges are left clear for melds and flowers.

        private void OnGUI()
        {
            if (match == null || Hand == null)
                return;
            EnsureStyles();

            Event e = Event.current;
            if (e.type == EventType.Layout)
                uiRects.Clear();

            if (e.type == EventType.MouseMove || e.type == EventType.MouseDown || e.type == EventType.Repaint)
                UpdateHover(e.mousePosition);

            if (e.type == EventType.MouseDown && e.button == 0 && !uiRects.Any(r => r.Contains(e.mousePosition)))
            {
                if (hoveredTile != null)
                {
                    Tile tile = hoveredTile.Tile;
                    e.Use();
                    Apply(PlayerAction.Discard(HumanSeat, tile));
                    GUIUtility.ExitGUI(); // state changed; abort this GUI pass cleanly
                }
                else if (hoveredDumpling != null)
                {
                    int slot = hoveredDumpling.Slot;
                    e.Use();
                    Eat(HumanSeat, slot);
                    GUIUtility.ExitGUI();
                }
            }

            DrawSeatTags();
            float statusBottom = DrawStatus();
            if (showFullLog)
                DrawFullLog(statusBottom + 8);
            if (match.Phase == MatchPhase.PlayingHand && Hand.Phase != HandPhase.Ended)
                DrawActionBar();
            else
                DrawResult();
        }

        private void UpdateHover(Vector2 guiPosition)
        {
            TileView nextTile = null;
            DumplingView nextDumpling = null;
            bool overUi = uiRects.Any(r => r.Contains(guiPosition));
            bool canDiscard = match.Phase == MatchPhase.PlayingHand && Hand.Phase == HandPhase.AwaitingAction
                && Hand.CurrentSeat == HumanSeat && HumanPlays(HumanSeat);
            bool canEat = match.Phase == MatchPhase.Roulette && HumanPlays(match.CurrentEater);

            if (cam != null && !overUi && (canDiscard || canEat))
            {
                var screen = new Vector3(guiPosition.x, Screen.height - guiPosition.y, 0f);
                if (Physics.Raycast(cam.ScreenPointToRay(screen), out RaycastHit hit, 10f))
                {
                    if (canDiscard)
                    {
                        TileView view = hit.collider.GetComponentInParent<TileView>();
                        if (view != null && view.InHand && view.Seat == HumanSeat
                            && Hand.GetLegalActions(HumanSeat).Contains(PlayerAction.Discard(HumanSeat, view.Tile)))
                            nextTile = view;
                    }
                    else
                    {
                        nextDumpling = hit.collider.GetComponent<DumplingView>();
                    }
                }
            }

            if (nextTile != hoveredTile)
            {
                if (hoveredTile != null)
                    hoveredTile.transform.position = hoveredTile.RestPosition;
                hoveredTile = nextTile;
                if (hoveredTile != null)
                    hoveredTile.transform.position = hoveredTile.RestPosition + Vector3.up * 0.015f;
            }
            if (nextDumpling != hoveredDumpling)
            {
                hoveredDumpling = nextDumpling;
                steamer.SetHover(hoveredDumpling);
            }
        }

        private void EnsureStyles()
        {
            if (boxStyle != null && Mathf.Approximately(stylesForHeight, Screen.height))
                return;
            stylesForHeight = Screen.height;
            int size = Mathf.Max(12, Mathf.RoundToInt(Screen.height / 55f));

            boxStyle = new GUIStyle(GUI.skin.box);
            textStyle = new GUIStyle(GUI.skin.label) { fontSize = size, wordWrap = false, richText = true, clipping = TextClipping.Clip };
            titleStyle = new GUIStyle(textStyle) { fontSize = Mathf.RoundToInt(size * 1.2f), fontStyle = FontStyle.Bold };
            centerStyle = new GUIStyle(textStyle) { alignment = TextAnchor.MiddleCenter };
            logStyle = new GUIStyle(textStyle) { fontSize = Mathf.RoundToInt(size * 0.85f) };
            logStyle.normal.textColor = new Color(0.75f, 0.75f, 0.75f);
            buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = size, padding = new RectOffset(12, 12, 6, 6) };
        }

        private float Unit => Screen.height / 55f;
        private float LineHeight => Unit * 1.45f;
        private const float Pad = 10f;

        /// <summary>Draws a background box and registers it as UI (so clicks don't fall through).</summary>
        private Rect Panel(Rect r)
        {
            uiRects.Add(r);
            GUI.Box(r, GUIContent.none, boxStyle);
            return r;
        }

        private float TextWidth(GUIStyle style, string text) => style.CalcSize(new GUIContent(text)).x;

        /// <summary>Screen y (GUI coordinates) of the top of the human's hand.</summary>
        private float HandTopY()
        {
            Vector3 p = cam.WorldToScreenPoint(TableView.HumanHandTop);
            return Screen.height - p.y;
        }

        private float DrawStatus()
        {
            var lines = new List<string>
            {
                $"Hand {match.HandNumber} · {(classicRules ? "Classic" : "Party")} · {match.PrevailingWind} round · Wall {Hand.Wall.DrawableCount}",
                match.Phase == MatchPhase.PlayingHand && Hand.Phase != HandPhase.Ended
                    ? $"Turn: {SeatName(Hand.CurrentSeat)}"
                    : "Hand over",
                $"{SeatName(HumanSeat)} · wins {wins[HumanSeat]}{LivesText(HumanSeat)}",
            };
            if (partyRules.RouletteEnabled)
            {
                Steamer st = match.SteamerFor(match.CurrentEater >= 0 ? match.CurrentEater : HumanSeat);
                lines.Add($"Steamer: {st.Remaining} left · pepper chance {Mathf.RoundToInt((float)st.PepperChance * 100f)}%");
            }
            bool spectating = !match.Players[HumanSeat].IsAlive && match.Phase != MatchPhase.MatchOver;
            if (spectating)
                lines.Add("You're out — spectating.");

            float w = Unit * 19;
            int logCount = Mathf.Min(StatusLogLines, log.Count);
            float buttonH = Unit * 1.8f;
            float h = Pad * 2 + LineHeight * lines.Count + logCount * LineHeight * 0.85f + buttonH + 6;
            Rect r = Panel(new Rect(Pad, Pad, w, h));

            float x = r.x + Pad;
            float y = r.y + Pad;
            float iw = r.width - Pad * 2;
            foreach (string line in lines)
            {
                GUI.Label(new Rect(x, y, iw, LineHeight), line, textStyle);
                y += LineHeight;
            }
            foreach (string line in log.Skip(log.Count - logCount))
            {
                GUI.Label(new Rect(x, y, iw, LineHeight * 0.85f), line, logStyle);
                y += LineHeight * 0.85f;
            }

            if (GUI.Button(new Rect(x, y + 6, Unit * 7, buttonH), showFullLog ? "Hide log" : "Show log", buttonStyle))
                showFullLog = !showFullLog;
            if (spectating && GUI.Button(new Rect(x + Unit * 7 + 8, y + 6, Unit * 9, buttonH),
                    fastForward ? "Normal speed" : "Fast forward", buttonStyle))
                fastForward = !fastForward;
            return r.yMax;
        }

        private string LivesText(int seat) =>
            partyRules.RouletteEnabled && partyRules.Lives > 1 && match.Players[seat].IsAlive
                ? $" · lives {match.Players[seat].Lives}"
                : "";

        private void DrawFullLog(float top)
        {
            int count = Mathf.Min(FullLogLines, log.Count);
            float lh = LineHeight * 0.85f;
            Rect r = Panel(new Rect(Pad, top, Unit * 24, Pad * 2 + count * lh));
            float y = r.y + Pad;
            foreach (string line in log.Skip(log.Count - count))
            {
                GUI.Label(new Rect(r.x + Pad, y, r.width - Pad * 2, lh), line, logStyle);
                y += lh;
            }
        }

        /// <summary>Name tags floating above each bot's hand.</summary>
        private void DrawSeatTags()
        {
            for (int s = 0; s < HandState.SeatCount; s++)
            {
                if (s == HumanSeat)
                    continue;
                Vector3 p = cam.WorldToScreenPoint(TableView.SeatAnchor(s, HumanSeat));
                if (p.z <= 0f)
                    continue;
                bool active = (match.Phase == MatchPhase.PlayingHand && Hand.Phase != HandPhase.Ended && Hand.CurrentSeat == s)
                    || match.CurrentEater == s;
                string text = $"{(active ? "> " : "")}{SeatName(s)} · wins {wins[s]}{LivesText(s)}";
                GUIStyle style = active ? titleStyle : textStyle;
                float w = TextWidth(style, text) + Pad * 2;
                float h = LineHeight + 4;
                var r = new Rect(p.x - w / 2f, Screen.height - p.y - h / 2f, w, h);
                GUI.Box(r, GUIContent.none, boxStyle);
                GUI.Label(new Rect(r.x + Pad, r.y + 2, r.width - Pad * 2, r.height - 4), text, style);
            }
        }

        /// <summary>One-line prompt plus a row of claim/turn buttons, just above the human's hand.</summary>
        private void DrawActionBar()
        {
            List<PlayerAction> legal = HumanActions();
            List<PlayerAction> buttons = legal.Where(a => a.Type != ActionType.Discard).ToList();

            string message;
            if (legal.Count == 0)
                message = $"Waiting for {SeatName(Hand.CurrentSeat)}…";
            else if (Hand.Phase == HandPhase.AwaitingAction)
                message = "Your turn — click a tile in your hand to discard it";
            else if (Hand.Phase == HandPhase.AwaitingRobKong)
                message = $"{SeatName(Hand.CurrentSeat)} is adding to a kong";
            else
                message = $"{SeatName(Hand.LastDiscardSeat)} discarded {Hand.LastDiscard}";
            if (legal.Count > 0 && humanTimeLimit > 0f)
                message += $"   ({SecondsLeft()}s)";

            string hint = HumanPlays(HumanSeat) ? ReadyHint() : null;
            List<string> labels = buttons.Select(Describe).ToList();
            List<System.Action> actions = buttons.Select(a => (System.Action)(() => Apply(a))).ToList();
            DrawBottomPanel(null, new List<string> { message }.Concat(hint != null ? new[] { hint } : new string[0]).ToList(),
                labels, actions, centered: true);
        }

        private int SecondsLeft() => Mathf.Max(0, Mathf.CeilToInt(humanTimeLimit - (Time.time - humanWaitStart)));

        /// <summary>Newbie hint: which tiles would complete the hand (only between turns).</summary>
        private string ReadyHint()
        {
            SeatState me = Hand.Seats[HumanSeat];
            if (me.Concealed.Count + 3 * me.Melds.Count != 13)
                return null;
            List<Tile> waits = FaanCalculator.GetWaits(me.Concealed, me.Melds, rules);
            return waits.Count == 0 ? null : "Ready! Waiting on: " + string.Join(" ", waits);
        }

        private string Describe(PlayerAction a)
        {
            switch (a.Type)
            {
                case ActionType.Win:
                case ActionType.SelfDrawnWin:
                    WinContext ctx = Hand.PreviewWin(HumanSeat);
                    FaanResult score = ctx != null ? FaanCalculator.Calculate(ctx, rules) : null;
                    string name = a.Type == ActionType.SelfDrawnWin ? "WIN — self-drawn (自摸)" : "WIN (食糊)";
                    return score != null ? $"{name} · {score.Total} faan" : name;
                case ActionType.Pung: return $"Pung (碰) {Hand.LastDiscard}";
                case ActionType.Kong: return $"Kong (槓) {Hand.LastDiscard}";
                case ActionType.Chow:
                    return $"Chow (上) {a.Tile} {new Tile(a.Tile.Suit, a.Tile.Rank + 1)} {new Tile(a.Tile.Suit, a.Tile.Rank + 2)}";
                case ActionType.ConcealedKong: return $"Concealed kong {a.Tile}";
                case ActionType.PromoteKong: return $"Add to kong {a.Tile}";
                case ActionType.Pass: return "Pass";
                default: return a.ToString();
            }
        }

        /// <summary>Hand result, penalties, roulette progress, and the next-step button.</summary>
        private void DrawResult()
        {
            string title = resultLines.Count > 0 ? resultLines[0] : "";
            var lines = resultLines.Skip(1).ToList();
            if (penaltyLine != null)
                lines.Add(penaltyLine);
            if (rouletteLine != null)
                lines.Add(rouletteLine);

            var labels = new List<string>();
            var actions = new List<System.Action>();

            switch (match.Phase)
            {
                case MatchPhase.Roulette:
                    int eater = match.CurrentEater;
                    if (HumanPlays(eater))
                    {
                        Steamer st = match.SteamerFor(eater);
                        lines.Add($"YOUR TURN TO EAT — click a dumpling in the steamer ({st.Remaining} left, " +
                            $"1 is the pepper)   ({SecondsLeft()}s)");
                        labels.Add("Eat a random one");
                        actions.Add(() => EatRandom(HumanSeat));
                    }
                    else
                    {
                        lines.Add($"{SeatName(eater)} reaches for a dumpling…");
                    }
                    break;
                case MatchPhase.BetweenHands:
                    labels.Add("Next hand");
                    actions.Add(StartHand);
                    break;
                case MatchPhase.MatchOver:
                    lines.Add(match.Winner == HumanSeat
                        ? "YOU WIN THE MATCH! Last one standing."
                        : $"{SeatName(match.Winner)} wins the match — last one standing.");
                    labels.Add("New match");
                    actions.Add(NewMatch);
                    break;
            }
            DrawBottomPanel(title, lines, labels, actions, centered: false);
        }

        /// <summary>Panel just above the human's hand: optional bold title, text lines, a row of buttons.</summary>
        private void DrawBottomPanel(string title, List<string> lines, List<string> buttonLabels,
            List<System.Action> buttonActions, bool centered)
        {
            GUIStyle lineStyle = centered ? centerStyle : textStyle;
            List<float> widths = buttonLabels.Select(l => TextWidth(buttonStyle, l) + 8).ToList();
            const float gap = 8f;
            float rowW = widths.Sum() + gap * Mathf.Max(0, widths.Count - 1);
            float buttonH = Unit * 2.1f;
            bool hasTitle = !string.IsNullOrEmpty(title);

            float w = rowW;
            if (hasTitle)
                w = Mathf.Max(w, TextWidth(titleStyle, title));
            foreach (string line in lines)
                w = Mathf.Max(w, TextWidth(lineStyle, line));
            w = Mathf.Min(w + Pad * 2, Screen.width * 0.85f);

            float h = Pad * 2 + (hasTitle ? LineHeight * 1.2f : 0f) + lines.Count * LineHeight
                + (widths.Count > 0 ? buttonH + 8 : 0f);
            float bottom = HandTopY() - Unit * 0.4f;
            Rect r = Panel(new Rect((Screen.width - w) / 2f, bottom - h, w, h));

            float x = r.x + Pad;
            float y = r.y + Pad;
            float iw = r.width - Pad * 2;
            if (hasTitle)
            {
                GUI.Label(new Rect(x, y, iw, LineHeight * 1.2f), title, titleStyle);
                y += LineHeight * 1.2f;
            }
            foreach (string line in lines)
            {
                GUI.Label(new Rect(x, y, iw, LineHeight), line, lineStyle);
                y += LineHeight;
            }

            float bx = r.center.x - rowW / 2f;
            for (int i = 0; i < widths.Count; i++)
            {
                if (GUI.Button(new Rect(bx, y + 8, widths[i], buttonH), buttonLabels[i], buttonStyle))
                {
                    buttonActions[i]();
                    GUIUtility.ExitGUI();
                }
                bx += widths[i] + gap;
            }
        }
    }
}
