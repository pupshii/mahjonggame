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
        private const int FullLogLines = 16;
        private const int StatusLogLines = 3;

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
        private GUIStyle boxStyle, textStyle, titleStyle, centerStyle, logStyle, buttonStyle;
        private bool showFullLog;

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
        //
        // Layout: status + recent log in the top-left corner (floor, not table); bot name tags
        // float above their hands; the action bar and the result panel sit just above your
        // hand. Corners near the table edges are left clear because melds and flowers land there.

        private void OnGUI()
        {
            if (hand == null)
                return;
            EnsureStyles();

            Event e = Event.current;
            if (e.type == EventType.Layout)
                uiRects.Clear();

            if (e.type == EventType.MouseMove || e.type == EventType.MouseDown || e.type == EventType.Repaint)
                UpdateHover(e.mousePosition);

            if (e.type == EventType.MouseDown && e.button == 0 && hovered != null && !uiRects.Any(r => r.Contains(e.mousePosition)))
            {
                Tile tile = hovered.Tile;
                e.Use();
                Apply(PlayerAction.Discard(HumanSeat, tile));
                GUIUtility.ExitGUI(); // state changed; abort this GUI pass cleanly
            }

            DrawSeatTags();
            float statusBottom = DrawStatus();
            if (showFullLog)
                DrawFullLog(statusBottom + 8);
            if (hand.Phase == HandPhase.Ended)
                DrawResult();
            else
                DrawActionBar();
        }

        private void UpdateHover(Vector2 guiPosition)
        {
            TileView next = null;
            if (cam != null && hand.Phase == HandPhase.AwaitingAction && hand.CurrentSeat == HumanSeat
                && !uiRects.Any(r => r.Contains(guiPosition)))
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

        /// <summary>Draws a background box, registers it as UI (so clicks don't fall through), returns it.</summary>
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
            float w = Unit * 18;
            int logCount = Mathf.Min(StatusLogLines, log.Count);
            float buttonH = Unit * 1.8f;
            float h = Pad * 2 + LineHeight * 3 + logCount * LineHeight * 0.85f + buttonH + 6;
            Rect r = Panel(new Rect(Pad, Pad, w, h));

            float x = r.x + Pad;
            float y = r.y + Pad;
            float iw = r.width - Pad * 2;
            GUI.Label(new Rect(x, y, iw, LineHeight), $"Hand {handNumber} · {(classicRules ? "Classic" : "Party")} · Wall {hand.Wall.DrawableCount}", textStyle);
            y += LineHeight;
            string turn = hand.Phase == HandPhase.Ended ? "Hand over" : $"Turn: {SeatName(hand.CurrentSeat)}";
            GUI.Label(new Rect(x, y, iw, LineHeight), turn, textStyle);
            y += LineHeight;
            GUI.Label(new Rect(x, y, iw, LineHeight), $"{SeatName(HumanSeat)} · wins {wins[HumanSeat]}", textStyle);
            y += LineHeight;

            foreach (string line in log.Skip(log.Count - logCount))
            {
                GUI.Label(new Rect(x, y, iw, LineHeight * 0.85f), line, logStyle);
                y += LineHeight * 0.85f;
            }

            if (GUI.Button(new Rect(x, y + 6, Unit * 7, buttonH), showFullLog ? "Hide log" : "Show log", buttonStyle))
                showFullLog = !showFullLog;
            return r.yMax;
        }

        private void DrawFullLog(float top)
        {
            int count = Mathf.Min(FullLogLines, log.Count);
            float lh = LineHeight * 0.85f;
            Rect r = Panel(new Rect(Pad, top, Unit * 22, Pad * 2 + count * lh));
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
                bool active = hand.Phase != HandPhase.Ended && hand.CurrentSeat == s;
                string text = $"{(active ? "> " : "")}{SeatName(s)} · wins {wins[s]}";
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
                message = $"Waiting for {SeatName(hand.CurrentSeat)}…";
            else if (hand.Phase == HandPhase.AwaitingAction)
                message = "Your turn — click a tile in your hand to discard it";
            else if (hand.Phase == HandPhase.AwaitingRobKong)
                message = $"{SeatName(hand.CurrentSeat)} is adding to a kong";
            else
                message = $"{SeatName(hand.LastDiscardSeat)} discarded {hand.LastDiscard}";
            if (legal.Count > 0 && humanTimeLimit > 0f)
                message += $"   ({Mathf.CeilToInt(humanTimeLimit - (Time.time - humanWaitStart))}s)";

            string hint = ReadyHint();

            var labels = buttons.Select(Describe).ToList();
            var widths = labels.Select(l => TextWidth(buttonStyle, l) + 8).ToList();
            float gap = 8f;
            float rowW = widths.Sum() + gap * Mathf.Max(0, widths.Count - 1);
            float buttonH = Unit * 2.1f;

            int lines = 1 + (hint != null ? 1 : 0);
            float w = Mathf.Max(rowW, Mathf.Max(TextWidth(centerStyle, message), hint != null ? TextWidth(centerStyle, hint) : 0f)) + Pad * 2;
            float h = Pad * 2 + lines * LineHeight + (buttons.Count > 0 ? buttonH + 6 : 0);
            float bottom = HandTopY() - Unit * 0.4f;
            Rect r = Panel(new Rect((Screen.width - w) / 2f, bottom - h, w, h));

            float y = r.y + Pad;
            GUI.Label(new Rect(r.x, y, r.width, LineHeight), message, centerStyle);
            y += LineHeight;
            if (hint != null)
            {
                GUI.Label(new Rect(r.x, y, r.width, LineHeight), hint, centerStyle);
                y += LineHeight;
            }

            float x = r.center.x - rowW / 2f;
            for (int i = 0; i < buttons.Count; i++)
            {
                if (GUI.Button(new Rect(x, y + 6, widths[i], buttonH), labels[i], buttonStyle))
                {
                    Apply(buttons[i]);
                    GUIUtility.ExitGUI();
                }
                x += widths[i] + gap;
            }
        }

        /// <summary>Newbie hint: which tiles would complete the hand (only between turns).</summary>
        private string ReadyHint()
        {
            SeatState me = hand.Seats[HumanSeat];
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

        /// <summary>Hand result above the human's hand, leaving the revealed hands visible.</summary>
        private void DrawResult()
        {
            string title = resultLines.Count > 0 ? resultLines[0] : "";
            List<string> details = resultLines.Skip(1).ToList();
            const string next = "Next hand";

            float w = Mathf.Max(TextWidth(titleStyle, title), details.Count > 0 ? details.Max(l => TextWidth(textStyle, l)) : 0f);
            w = Mathf.Min(w + Pad * 2, Screen.width * 0.8f);
            float buttonH = Unit * 2.1f;
            float h = Pad * 2 + LineHeight * 1.2f + details.Count * LineHeight + buttonH + 8;
            float bottom = HandTopY() - Unit * 0.4f;
            Rect r = Panel(new Rect((Screen.width - w) / 2f, bottom - h, w, h));

            float x = r.x + Pad;
            float y = r.y + Pad;
            GUI.Label(new Rect(x, y, r.width - Pad * 2, LineHeight * 1.2f), title, titleStyle);
            y += LineHeight * 1.2f;
            foreach (string line in details)
            {
                GUI.Label(new Rect(x, y, r.width - Pad * 2, LineHeight), line, textStyle);
                y += LineHeight;
            }

            float bw = TextWidth(buttonStyle, next) + 24;
            if (GUI.Button(new Rect(r.center.x - bw / 2f, y + 8, bw, buttonH), next, buttonStyle))
            {
                NextHand();
                GUIUtility.ExitGUI();
            }
        }
    }
}
