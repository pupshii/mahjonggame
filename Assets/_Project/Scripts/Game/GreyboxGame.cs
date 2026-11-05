using System.Collections.Generic;
using System.Linq;
using MahjongNight.Core;
using UnityEngine;

namespace MahjongNight.Game
{
    /// <summary>
    /// M3 greybox: one human vs three <see cref="SimpleBot"/>s on a primitive 3D table,
    /// with an IMGUI overlay for prompts and claim buttons. Builds everything it needs at
    /// runtime — add this component to an empty GameObject (or use the
    /// "Mahjong Night → Create Greybox Scene" menu) and press Play.
    /// </summary>
    public sealed class GreyboxGame : MonoBehaviour
    {
        [Header("Rules")]
        [Tooltip("Off = Party rules (any hand wins). On = Classic (3 faan minimum).")]
        [SerializeField] private bool classicRules = false;

        [Header("Pacing")]
        [SerializeField] private float botTurnDelay = 0.8f;
        [SerializeField] private float botClaimDelay = 0.5f;
        [Tooltip("Seconds before you auto-discard / auto-pass. 0 = no limit.")]
        [SerializeField] private float humanTimeLimit = 20f;

        [Header("Debug")]
        [SerializeField] private bool revealBotHands = false;
        [Tooltip("0 = a different deal every run.")]
        [SerializeField] private int seed = 0;

        private const int HumanSeat = 0;
        private const int LogLines = 9;

        private RuleSet rules;
        private HandState hand;
        private readonly IBot[] bots = new IBot[HandState.SeatCount];
        private TableView table;
        private Camera cam;
        private SeededRandom seedSource;

        private int dealer;
        private int nextDealer;
        private int handNumber;
        private float nextBotTime;
        private float humanWaitStart;
        private readonly int[] wins = new int[HandState.SeatCount];

        private readonly List<string> log = new List<string>();
        private int loggedEvents;
        private List<string> resultLines;

        private TileView hovered;
        private readonly List<Rect> uiRects = new List<Rect>();
        private float stylesForHeight;
        private GUIStyle boxStyle, textStyle, titleStyle, buttonStyle;

        // ------------------------------------------------------------------ lifecycle

        private void Start()
        {
            rules = classicRules ? RuleSet.Classic() : RuleSet.Party();
            seedSource = new SeededRandom(seed != 0 ? (ulong)seed : (ulong)System.DateTime.Now.Ticks);
            SetUpCameraAndLight();
            table = new TableView(transform, new TileFactory());
            dealer = HumanSeat;
            StartHand();
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

        private void StartHand()
        {
            handNumber++;
            ulong handSeed = seedSource.NextUInt64();
            hand = HandState.Deal(rules, dealer, Wind.East, handSeed);
            for (int s = 0; s < bots.Length; s++)
                bots[s] = s == HumanSeat ? null : new SimpleBot(handSeed + (ulong)s * 7919UL);

            log.Clear();
            loggedEvents = 0;
            resultLines = null;
            AddLog($"Hand {handNumber} (seed {handSeed}). {SeatName(dealer)} deals.");
            OnStateChanged();
        }

        private void Update()
        {
            if (hand == null || hand.Phase == HandPhase.Ended)
                return;

            if (HumanActions().Count > 0 && humanTimeLimit > 0f && Time.time - humanWaitStart > humanTimeLimit)
            {
                AutoPlayHuman();
                return;
            }

            if (Time.time < nextBotTime)
                return;

            if (hand.Phase == HandPhase.AwaitingAction)
            {
                if (hand.CurrentSeat != HumanSeat)
                    BotAct(hand.CurrentSeat);
            }
            else
            {
                foreach (int s in hand.SeatsAwaitingResponse())
                {
                    if (s != HumanSeat)
                    {
                        BotAct(s);
                        return;
                    }
                }
            }
        }

        // ------------------------------------------------------------------ actions

        private List<PlayerAction> HumanActions() => hand.GetLegalActions(HumanSeat);

        private void BotAct(int seat)
        {
            List<PlayerAction> legal = hand.GetLegalActions(seat);
            if (legal.Count > 0)
                Apply(bots[seat].Choose(hand, seat, legal));
        }

        private void AutoPlayHuman()
        {
            List<PlayerAction> legal = HumanActions();
            PlayerAction action;
            if (hand.Phase == HandPhase.AwaitingAction)
            {
                PlayerAction drawnDiscard = hand.LastDrawnTile.HasValue
                    ? PlayerAction.Discard(HumanSeat, hand.LastDrawnTile.Value)
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
            if (!hand.TryApply(action, out string error))
            {
                Debug.LogWarning(error);
                return;
            }
            OnStateChanged();
        }

        private void OnStateChanged()
        {
            FlushEventsToLog();
            hovered = null;
            humanWaitStart = Time.time;
            nextBotTime = Time.time + (hand.Phase == HandPhase.AwaitingAction ? botTurnDelay : botClaimDelay);
            if (hand.Phase == HandPhase.Ended)
                OnHandEnded();
            table.Rebuild(hand, HumanSeat, revealBotHands);
        }

        private void OnHandEnded()
        {
            HandResult result = hand.Result;
            resultLines = new List<string>();

            if (result.Outcome == HandOutcome.ExhaustiveDraw)
            {
                resultLines.Add("Draw game — the wall is empty.");
                nextDealer = dealer;
                return;
            }

            for (int i = 0; i < result.Winners.Count; i++)
            {
                int winner = result.Winners[i];
                wins[winner]++;
                FaanResult score = FaanCalculator.Calculate(result.WinContexts[i], rules);
                string how = result.SelfDrawn ? "self-drawn"
                    : result.RobbedKong ? $"robbing {SeatName(result.Discarder)}'s kong"
                    : $"on {SeatName(result.Discarder)}'s discard";
                resultLines.Add($"{SeatName(winner)} wins {how} with {result.WinningTile} — {score.Total} faan" +
                    (score.IsLimit ? " (LIMIT!)" : ""));
                foreach (FaanItem item in score.Items)
                    resultLines.Add($"    {item}   {FaanPatterns.ChineseName(item.Pattern)}");
                if (score.Decomposition != null)
                    resultLines.Add($"    Sets: {score.Decomposition}");
            }

            // Dealer keeps the deal when they win (or on a draw); otherwise it passes on.
            nextDealer = result.Winners.Contains(dealer) ? dealer : HandState.NextSeat(dealer);
        }

        private void NextHand()
        {
            dealer = nextDealer;
            StartHand();
        }

        // ------------------------------------------------------------------ names & log

        private string SeatName(int seat)
        {
            string who = seat == HumanSeat ? "You" : "Bot " + seat;
            return hand == null ? who : $"{who} ({hand.Seats[seat].SeatWind})";
        }

        private void AddLog(string line)
        {
            log.Add(line);
            if (log.Count > 200)
                log.RemoveAt(0);
        }

        private void FlushEventsToLog()
        {
            for (; loggedEvents < hand.Events.Count; loggedEvents++)
            {
                HandEvent e = hand.Events[loggedEvents];
                switch (e)
                {
                    case TileDrawnEvent d when d.Seat == HumanSeat:
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
                        AddLog(h.ToString());
                        break;
                }
            }
        }

        // ------------------------------------------------------------------ input & UI

        private void OnGUI()
        {
            if (hand == null)
                return;
            EnsureStyles();

            Event e = Event.current;
            if (e.type == EventType.MouseMove || e.type == EventType.MouseDown || e.type == EventType.Repaint)
                UpdateHover(e.mousePosition);

            if (e.type == EventType.MouseDown && e.button == 0 && hovered != null && !uiRects.Any(r => r.Contains(e.mousePosition)))
            {
                Tile tile = hovered.Tile;
                e.Use();
                Apply(PlayerAction.Discard(HumanSeat, tile));
                GUIUtility.ExitGUI(); // layout changed; abort this GUI pass cleanly
            }

            if (e.type == EventType.Layout)
                uiRects.Clear();

            DrawSeatTags();
            DrawStatus();
            DrawLog();
            if (hand.Phase == HandPhase.Ended)
                DrawResult();
            else
                DrawPrompt();
        }

        private void UpdateHover(Vector2 guiPosition)
        {
            TileView next = null;
            if (cam != null && hand.Phase == HandPhase.AwaitingAction && hand.CurrentSeat == HumanSeat)
            {
                var screen = new Vector3(guiPosition.x, Screen.height - guiPosition.y, 0f);
                if (Physics.Raycast(cam.ScreenPointToRay(screen), out RaycastHit hit, 10f))
                {
                    TileView view = hit.collider.GetComponentInParent<TileView>();
                    if (view != null && view.InHand && view.Seat == HumanSeat
                        && hand.GetLegalActions(HumanSeat).Contains(PlayerAction.Discard(HumanSeat, view.Tile)))
                        next = view;
                }
            }

            if (next == hovered)
                return;
            if (hovered != null)
                hovered.transform.position = hovered.RestPosition;
            hovered = next;
            if (hovered != null)
                hovered.transform.position = hovered.RestPosition + Vector3.up * 0.015f;
        }

        private void EnsureStyles()
        {
            if (boxStyle != null && Mathf.Approximately(stylesForHeight, Screen.height))
                return;
            stylesForHeight = Screen.height;
            int size = Mathf.Max(12, Mathf.RoundToInt(Screen.height / 55f));

            boxStyle = new GUIStyle(GUI.skin.box) { padding = new RectOffset(12, 12, 10, 10) };
            textStyle = new GUIStyle(GUI.skin.label) { fontSize = size, wordWrap = true, richText = true };
            titleStyle = new GUIStyle(textStyle) { fontSize = Mathf.RoundToInt(size * 1.25f), fontStyle = FontStyle.Bold };
            buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = size, padding = new RectOffset(10, 10, 6, 6) };
        }

        private Rect Panel(float x, float y, float w, float h)
        {
            var r = new Rect(x, y, w, h);
            uiRects.Add(r);
            GUI.Box(r, GUIContent.none, boxStyle);
            return new Rect(r.x + 12, r.y + 10, r.width - 24, r.height - 20);
        }

        private float Unit => Screen.height / 55f;

        private void DrawStatus()
        {
            Rect inner = Panel(10, 10, Unit * 20, Unit * 6);
            GUILayout.BeginArea(inner);
            GUILayout.Label($"Hand {handNumber} · {(classicRules ? "Classic" : "Party")} rules · Wall {hand.Wall.DrawableCount}", textStyle);
            string turn = hand.Phase == HandPhase.Ended ? "Hand over" : $"Turn: {SeatName(hand.CurrentSeat)}";
            GUILayout.Label(turn, textStyle);
            GUILayout.Label($"{SeatName(HumanSeat)} · wins: {wins[HumanSeat]}", textStyle);
            GUILayout.EndArea();
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
                bool active = hand.Phase != HandPhase.Ended && hand.CurrentSeat == s;
                string text = $"{(active ? "> " : "")}{SeatName(s)} · wins {wins[s]}";
                float w = Unit * 11;
                float h = Unit * 1.9f;
                var r = new Rect(p.x - w / 2f, Screen.height - p.y - h / 2f, w, h);
                GUI.Box(r, GUIContent.none, boxStyle);
                GUI.Label(new Rect(r.x + 8, r.y + 2, r.width - 16, r.height - 4), text, active ? titleStyle : textStyle);
            }
        }

        private void DrawLog()
        {
            float h = Unit * (LogLines * 1.45f + 1);
            Rect inner = Panel(10, Screen.height - h - 10, Unit * 20, h);
            GUILayout.BeginArea(inner);
            foreach (string line in log.Skip(Mathf.Max(0, log.Count - LogLines)))
                GUILayout.Label(line, textStyle);
            GUILayout.EndArea();
        }

        private void DrawPrompt()
        {
            List<PlayerAction> legal = HumanActions();
            float w = Unit * 19;
            float x = Screen.width - w - 10;

            if (legal.Count == 0)
            {
                Rect waitInner = Panel(x, 10, w, Unit * 2.6f);
                GUILayout.BeginArea(waitInner);
                GUILayout.Label($"Waiting for {SeatName(hand.CurrentSeat)}…", textStyle);
                GUILayout.EndArea();
                DrawWaitsHint(x, 10 + Unit * 3, w);
                return;
            }

            var buttons = legal.Where(a => a.Type != ActionType.Discard).ToList();
            float h = Unit * (5.5f + buttons.Count * 2.4f);
            Rect inner = Panel(x, 10, w, h);
            GUILayout.BeginArea(inner);

            if (hand.Phase == HandPhase.AwaitingAction)
                GUILayout.Label("Your turn", titleStyle);
            else if (hand.Phase == HandPhase.AwaitingRobKong)
                GUILayout.Label($"{SeatName(hand.CurrentSeat)} is adding to a kong", titleStyle);
            else
                GUILayout.Label($"{SeatName(hand.LastDiscardSeat)} discarded {hand.LastDiscard}", titleStyle);

            if (hand.Phase == HandPhase.AwaitingAction)
                GUILayout.Label("Click a tile in your hand to discard it.", textStyle);

            if (humanTimeLimit > 0f)
                GUILayout.Label($"{Mathf.CeilToInt(humanTimeLimit - (Time.time - humanWaitStart))}s", textStyle);

            foreach (PlayerAction a in buttons)
            {
                if (GUILayout.Button(Describe(a), buttonStyle))
                {
                    GUILayout.EndArea();
                    Apply(a);
                    GUIUtility.ExitGUI();
                    return;
                }
            }
            GUILayout.EndArea();
        }

        /// <summary>Newbie hint: show which tiles would complete the hand.</summary>
        private void DrawWaitsHint(float x, float y, float w)
        {
            SeatState me = hand.Seats[HumanSeat];
            if (me.Concealed.Count + 3 * me.Melds.Count != 13)
                return;
            List<Tile> waits = FaanCalculator.GetWaits(me.Concealed, me.Melds, rules);
            if (waits.Count == 0)
                return;
            Rect inner = Panel(x, y, w, Unit * 2.6f);
            GUILayout.BeginArea(inner);
            GUILayout.Label("Ready! Waiting on: " + string.Join(" ", waits), textStyle);
            GUILayout.EndArea();
        }

        private string Describe(PlayerAction a)
        {
            switch (a.Type)
            {
                case ActionType.Win:
                case ActionType.SelfDrawnWin:
                    WinContext ctx = hand.PreviewWin(HumanSeat);
                    FaanResult score = ctx != null ? FaanCalculator.Calculate(ctx, rules) : null;
                    string name = a.Type == ActionType.SelfDrawnWin ? "WIN — self-drawn (自摸)" : "WIN (食糊)";
                    return score != null ? $"{name} · {score.Total} faan" : name;
                case ActionType.Pung: return $"Pung (碰) {hand.LastDiscard}";
                case ActionType.Kong: return $"Kong (槓) {hand.LastDiscard}";
                case ActionType.Chow:
                    return $"Chow (上) {a.Tile} {new Tile(a.Tile.Suit, a.Tile.Rank + 1)} {new Tile(a.Tile.Suit, a.Tile.Rank + 2)}";
                case ActionType.ConcealedKong: return $"Concealed kong {a.Tile}";
                case ActionType.PromoteKong: return $"Add to kong {a.Tile}";
                case ActionType.Pass: return "Pass";
                default: return a.ToString();
            }
        }

        private void DrawResult()
        {
            float w = Unit * 30;
            float h = Unit * (4.5f + resultLines.Count * 1.45f);
            Rect inner = Panel((Screen.width - w) / 2f, Screen.height * 0.12f, w, h);
            GUILayout.BeginArea(inner);
            GUILayout.Label(resultLines.Count > 0 ? resultLines[0] : "", titleStyle);
            foreach (string line in resultLines.Skip(1))
                GUILayout.Label(line, textStyle);
            GUILayout.Space(Unit * 0.5f);
            if (GUILayout.Button("Next hand", buttonStyle))
            {
                GUILayout.EndArea();
                NextHand();
                GUIUtility.ExitGUI();
                return;
            }
            GUILayout.EndArea();
        }
    }
}
