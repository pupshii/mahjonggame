using System.Collections.Generic;
using System.Linq;
using MahjongNight.Core;
using UnityEngine;

namespace MahjongNight.Game
{
    /// <summary>
    /// Greybox match: you vs three bots at a table with greybox bodies. Steer your head with
    /// the cursor; look down to reach into your pocket and swap tiles (3 steps); watch the
    /// others and press SPACE / click them to call out a cheat. Faan is the currency: win
    /// hands to earn it, spend it on cheating upgrades between hands; a caught cheater pays
    /// 10 faan to everyone. Highest score after the last hand wins.
    /// </summary>
    public sealed class GreyboxGame : MonoBehaviour
    {
        [Header("Rules")]
        [Tooltip("Off = Party rules. On = Classic. Both need 3 faan to win.")]
        [SerializeField] private bool classicRules = false;
        [Tooltip("Players pick which tile kind to draw (lose the turn if none is left).")]
        [SerializeField] private bool chooseDraws = false;

        [Header("Match")]
        [SerializeField] private int handsPerMatch = 8;
        [SerializeField] private int cheatPenalty = 10;
        [SerializeField] private int falseAccusationFine = 3;

        [Header("Pacing")]
        [SerializeField] private float botTurnDelay = 0.8f;
        [SerializeField] private float botClaimDelay = 0.5f;
        [Tooltip("Seconds before you auto-discard / auto-pass. 0 = no limit.")]
        [SerializeField] private float humanTimeLimit = 20f;

        [Header("Bots")]
        [Tooltip("How willing bots are to cheat when they see a good swap (0-1).")]
        [SerializeField] private float botNerve = 0.7f;
        [Tooltip("Chance a bot calls out a cheat it has spotted (0-1).")]
        [SerializeField] private float botSpotChance = 0.75f;

        [Header("Debug")]
        [SerializeField] private bool revealBotHands = false;
        [Tooltip("0 = a different match every run.")]
        [SerializeField] private int seed = 0;

        private const int HumanSeat = 0;
        private const int FullLogLines = 16;
        private const int StatusLogLines = 3;
        private const float SpotConeDegrees = 15f;
        private const float WatchedConeDegrees = 35f;
        private const float CallOutTargetDegrees = 25f;

        private static readonly Color[] SeatColors =
        {
            new Color(0.25f, 0.45f, 0.85f), new Color(0.8f, 0.3f, 0.25f),
            new Color(0.3f, 0.65f, 0.35f), new Color(0.6f, 0.35f, 0.7f),
        };

        private RuleSet rules;
        private MatchRules matchRules;
        private ScoreMatch match;
        private HandState Hand => match.Hand;
        private SeededRandom seedSource;

        private readonly IBot[] bots = new IBot[HandState.SeatCount];
        private IBot humanAutoBot;
        private readonly BotBrain[] brains = new BotBrain[HandState.SeatCount];
        private readonly PlayerAvatar[] avatars = new PlayerAvatar[HandState.SeatCount];
        private readonly CheatMotion[] cheats = new CheatMotion[HandState.SeatCount];
        private readonly float[] lookingDownFor = new float[HandState.SeatCount];

        private TableView table;
        private Camera cam;
        private HeadLook headLook;

        private float nextBotTime;
        private float humanWaitStart;
        private float callOutCooldownUntil;

        private readonly List<string> log = new List<string>();
        private int loggedHandEvents;
        private int loggedMatchEvents;
        private List<string> resultLines = new List<string>();
        private readonly List<string> paymentLines = new List<string>();
        private string popupTitle, popupText;
        private float popupUntil;

        private Vector2 mouseGui;
        private bool hasMouse;
        private TileView hoveredTile;
        private int hoveredAvatar = -1;
        private readonly List<Rect> uiRects = new List<Rect>();
        private float stylesForHeight;
        private GUIStyle boxStyle, textStyle, titleStyle, centerStyle, logStyle, buttonStyle, pickerStyle;
        private bool showFullLog;
        private Tile? pickedHandTile;     // pocket strip selections (before pressing Swap)
        private int pickedPocketIndex = -1;

        // ------------------------------------------------------------------ lifecycle

        private void Start()
        {
            rules = classicRules ? RuleSet.Classic() : RuleSet.Party();
            rules.ChooseDraws = chooseDraws;
            matchRules = new MatchRules
            {
                HandsPerMatch = Mathf.Max(1, handsPerMatch),
                CheatPenalty = cheatPenalty,
                FalseAccusationFine = falseAccusationFine,
            };
            seedSource = new SeededRandom(seed != 0 ? (ulong)seed : (ulong)System.DateTime.Now.Ticks);

            SetUpCameraAndLight();
            table = new TableView(transform, new TileFactory());
            for (int s = 0; s < HandState.SeatCount; s++)
            {
                int relative = (s - HumanSeat + HandState.SeatCount) % HandState.SeatCount;
                avatars[s] = new PlayerAvatar(transform, s, relative, SeatColors[s], firstPerson: s == HumanSeat);
            }
            headLook = new HeadLook(cam.transform, avatars[HumanSeat].EyePosition);
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
            cam.fieldOfView = 65f;
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
            match = new ScoreMatch(rules, matchRules, matchSeed);
            loggedMatchEvents = 0;
            for (int s = 0; s < HandState.SeatCount; s++)
                brains[s] = s == HumanSeat ? null : new BotBrain(s, matchSeed + (ulong)s * 31UL);
            AddLog($"New match: {matchRules.HandsPerMatch} hands (seed {matchSeed}).");
            StartHand();
        }

        private void StartHand()
        {
            match.StartHand();
            ulong botSeed = seedSource.NextUInt64();
            for (int s = 0; s < bots.Length; s++)
                bots[s] = new SimpleBot(botSeed + (ulong)s * 7919UL);
            humanAutoBot = new SimpleBot(botSeed ^ 0xA070UL);
            for (int s = 0; s < HandState.SeatCount; s++)
            {
                cheats[s] = null;
                brains[s]?.ResetForHand(Time.time);
            }
            pickedHandTile = null;
            pickedPocketIndex = -1;

            loggedHandEvents = 0;
            resultLines = new List<string>();
            paymentLines.Clear();
            FlushMatchEvents();
            AddLog($"Your pocket: {string.Join(" ", match.Players[HumanSeat].Pocket)}");
            OnHandChanged();
        }

        private bool HandLive => match.Phase == MatchPhase.PlayingHand && Hand.Phase != HandPhase.Ended;

        // ------------------------------------------------------------------ frame update

        private void Update()
        {
            if (match == null)
                return;
            float now = Time.time;
            float dt = Time.deltaTime;

            UpdateHumanHead(dt);
            for (int s = 0; s < HandState.SeatCount; s++)
            {
                float pitch = s == HumanSeat ? headLook.Pitch : avatars[s].PitchDown;
                lookingDownFor[s] = pitch >= HeadLook.LookingDownPitch ? lookingDownFor[s] + dt : 0f;
            }

            if (HandLive)
            {
                UpdateBotGaze(now);
                UpdateCheats(now);
                if (HandLive) UpdateBotCheating(now);
                if (HandLive) UpdateSpotting(now, dt);
                if (HandLive) UpdateTurns(now);
            }

            for (int s = 0; s < HandState.SeatCount; s++)
            {
                CheatMotion c = cheats[s];
                bool active = c != null && c.InProgress;
                avatars[s].SetRightHand(active ? c.Pose : HandPose.Rest, active ? c.Progress(now) : 0f, now);
                avatars[s].Tick(dt, followGaze: s != HumanSeat);
            }
        }

        private void UpdateHumanHead(float dt)
        {
            bool overUi = uiRects.Any(r => r.Contains(mouseGui));
            if (!HandLive)
                headLook.AimNeutral();
            else if (hasMouse && !overUi && !PickerOpen)
                headLook.AimFromCursor(mouseGui, Screen.width, Screen.height);
            headLook.Tick(dt);
            avatars[HumanSeat].SetHeadRotation(headLook.Rotation);
        }

        private bool PickerOpen => HandLive && Hand.Phase == HandPhase.AwaitingDraw && Hand.CurrentSeat == HumanSeat;

        // ------------------------------------------------------------------ mahjong turns

        private void UpdateTurns(float now)
        {
            if (HumanActions().Count > 0 && humanTimeLimit > 0f && now - humanWaitStart > humanTimeLimit)
            {
                AutoPlayHuman();
                return;
            }
            if (now < nextBotTime)
                return;

            if (Hand.Phase == HandPhase.AwaitingAction || Hand.Phase == HandPhase.AwaitingDraw)
            {
                int s = Hand.CurrentSeat;
                if (s != HumanSeat && !BotBusyCheating(s))
                    BotAct(s);
            }
            else
            {
                foreach (int s in Hand.SeatsAwaitingResponse())
                {
                    if (s != HumanSeat)
                    {
                        BotAct(s);
                        return;
                    }
                }
            }
        }

        /// <summary>A bot holds its turn while it's mid-swap, or waiting for a moment to cheat a win.</summary>
        private bool BotBusyCheating(int seat)
        {
            CheatMotion c = cheats[seat];
            if (c != null && c.InProgress)
                return true;
            BotBrain b = brains[seat];
            return b != null && b.Plan.HasValue && b.Plan.Value.MakesWin;
        }

        private List<PlayerAction> HumanActions() =>
            HandLive ? Hand.GetLegalActions(HumanSeat) : new List<PlayerAction>();

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
            if (Hand.Phase == HandPhase.AwaitingDraw)
            {
                action = humanAutoBot.Choose(Hand, HumanSeat, legal);
            }
            else if (Hand.Phase == HandPhase.AwaitingAction)
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
            float delay = Hand.Phase == HandPhase.AwaitingAction ? botTurnDelay
                : Hand.Phase == HandPhase.AwaitingDraw ? botTurnDelay * 0.6f
                : botClaimDelay;
            nextBotTime = Time.time + delay;
            if (Hand.Phase == HandPhase.Ended && match.Phase == MatchPhase.PlayingHand)
                OnHandEnded();
            RebuildTable();
        }

        private void OnHandEnded()
        {
            CancelCheats();
            HandResult result = Hand.Result;
            resultLines = new List<string>();
            if (result.Outcome == HandOutcome.ExhaustiveDraw)
            {
                resultLines.Add("Draw game — the wall is empty. Nobody pays.");
            }
            else
            {
                for (int i = 0; i < result.Winners.Count; i++)
                {
                    int winner = result.Winners[i];
                    FaanResult score = FaanCalculator.Calculate(result.WinContexts[i], rules);
                    resultLines.Add($"{DescribeWin(result, winner)} — {score.Total} faan" + (score.IsLimit ? " (LIMIT!)" : ""));
                    foreach (FaanItem item in score.Items)
                        resultLines.Add($"    {item}   {FaanPatterns.ChineseName(item.Pattern)}");
                }
            }
            match.ResolveHand();
            FlushMatchEvents();
            BotsShop();
        }

        private void RebuildTable()
        {
            var hidden = new Dictionary<int, Tile>();
            for (int s = 0; s < HandState.SeatCount; s++)
                if (cheats[s] != null && cheats[s].InProgress)
                    hidden[s] = cheats[s].HandTile;
            table.Rebuild(Hand, HumanSeat, revealBotHands, hidden);
        }

        // ------------------------------------------------------------------ cheating

        private float StepSeconds(int seat) => (float)Upgrades.CheatStepSeconds(match.Players[seat].Level(Upgrade.SleightOfHand));

        private void StartCheat(int seat, Tile handTile, int pocketIndex)
        {
            var motion = new CheatMotion(seat, handTile, StepSeconds(seat), Time.time) { PocketIndex = pocketIndex };
            cheats[seat] = motion;
            RebuildTable();
        }

        /// <summary>Advances the timed steps of every cheat; applies the swap at the end of step 3.</summary>
        private void UpdateCheats(float now)
        {
            for (int s = 0; s < HandState.SeatCount; s++)
            {
                CheatMotion c = cheats[s];
                if (c == null || !c.InProgress)
                    continue;
                switch (c.Step)
                {
                    case CheatStep.Stashing when c.StepTimeUp(now):
                        c.Begin(CheatStep.PullingOut, now);
                        break;
                    case CheatStep.PullingOut when c.StepTimeUp(now):
                        c.Begin(CheatStep.Returning, now);
                        break;
                    case CheatStep.Returning when c.StepTimeUp(now):
                        CompleteCheat(c, now);
                        break;
                }
            }
        }

        private void CompleteCheat(CheatMotion c, float now)
        {
            int s = c.Seat;
            if (!Hand.CanPocketSwap(s))
                return; // e.g. a claim is pending — keep holding the tile and try next frame

            Tile pulled = match.Players[s].Pocket[c.PocketIndex];
            bool ok = match.TryPocketSwap(s, c.HandTile, c.PocketIndex, out string error);
            c.Finish(now, ok);
            if (s == HumanSeat)
                AddLog(ok ? $"You slipped {c.HandTile} into your pocket and {pulled} into your hand." : $"The swap fell through: {error}");

            BotBrain brain = brains[s];
            if (brain != null)
            {
                CheatPlan? plan = brain.Plan;
                brain.Plan = null;
                brain.ReleaseGaze(now);
                if (ok && plan.HasValue && plan.Value.MakesWin && Hand.GetLegalActions(s).Contains(PlayerAction.SelfDrawnWin(s)))
                {
                    Apply(PlayerAction.SelfDrawnWin(s));
                    return;
                }
            }
            FlushHandEvents();
            RebuildTable();
        }

        private void CancelCheats()
        {
            for (int s = 0; s < HandState.SeatCount; s++)
                if (cheats[s] != null && cheats[s].InProgress)
                    cheats[s].Finish(Time.time, completed: false);
        }

        /// <summary>Is anyone (whose head <paramref name="seat"/> can see) looking at <paramref name="seat"/>'s hands?</summary>
        private bool WatchedBySomeone(int seat)
        {
            for (int w = 0; w < HandState.SeatCount; w++)
                if (w != seat && LookingAt(w, avatars[seat].CheatAnchor, WatchedConeDegrees))
                    return true;
            return false;
        }

        private bool LookingAt(int watcher, Vector3 point, float coneDegrees)
        {
            Vector3 eye = watcher == HumanSeat ? cam.transform.position : avatars[watcher].EyePosition;
            Vector3 gaze = watcher == HumanSeat ? cam.transform.forward : avatars[watcher].Gaze;
            return Vector3.Angle(gaze, point - eye) < coneDegrees;
        }

        private void UpdateBotGaze(float now)
        {
            int onTurn = Hand.Phase == HandPhase.AwaitingAction || Hand.Phase == HandPhase.AwaitingDraw ? Hand.CurrentSeat : -1;
            for (int s = 0; s < HandState.SeatCount; s++)
            {
                BotBrain b = brains[s];
                if (b == null || (cheats[s] != null && cheats[s].InProgress) || b.CallOutTarget >= 0)
                    continue;
                b.UpdateGaze(now, avatars[s], avatars, onTurn, lookingDownFor);
            }
        }

        /// <summary>Bots look for worthwhile swaps and start one only when nobody is watching.</summary>
        private void UpdateBotCheating(float now)
        {
            for (int s = 0; s < HandState.SeatCount; s++)
            {
                BotBrain b = brains[s];
                if (b == null || b.CheatedThisHand || (cheats[s] != null && cheats[s].InProgress))
                    continue;

                bool myTurn = Hand.Phase == HandPhase.AwaitingAction && Hand.CurrentSeat == s;
                if (!myTurn)
                    b.CheckedThisTurn = false;

                if (!b.Plan.HasValue)
                {
                    CheatPlan? plan = null;
                    if (myTurn && !b.CheckedThisTurn)
                    {
                        b.CheckedThisTurn = true;
                        plan = CheatAdvisor.FindWorthwhileSwap(Hand, s, match.Players[s].Pocket);
                        if (!(plan.HasValue && plan.Value.MakesWin && b.Roll() < botNerve))
                            plan = null;
                    }
                    else if (!myTurn && now >= b.NextCheatCheck)
                    {
                        b.NextCheatCheck = now + 4f + (float)b.Roll() * 4f;
                        plan = CheatAdvisor.FindWorthwhileSwap(Hand, s, match.Players[s].Pocket);
                        if (!(plan.HasValue && b.Roll() < botNerve * 0.35))
                            plan = null;
                    }
                    if (plan.HasValue)
                    {
                        b.Plan = plan;
                        b.WantCheatSince = now;
                    }
                }

                if (!b.Plan.HasValue)
                    continue;

                CheatPlan p = b.Plan.Value;
                bool stillValid = Hand.CanPocketSwap(s) && Hand.Seats[s].CountConcealed(p.FromHand) > 0
                    && (!p.MakesWin || myTurn);
                if (!stillValid || now - b.WantCheatSince > 4f)
                {
                    b.Plan = null; // lost the moment (or too many eyes) — give up this time
                    b.WantCheatSince = -1f;
                    continue;
                }
                if (WatchedBySomeone(s))
                    continue;

                b.CheatedThisHand = true;
                b.WantCheatSince = -1f;
                b.LookAtPocket(avatars[s]);
                StartCheat(s, p.FromHand, p.PocketIndex);
            }
        }

        /// <summary>Bots who watch a cheater's hands long enough call them out.</summary>
        private void UpdateSpotting(float now, float dt)
        {
            for (int c = 0; c < HandState.SeatCount; c++)
            {
                CheatMotion motion = cheats[c];
                bool moving = motion != null && motion.InProgress; // only the motion itself can be seen
                for (int w = 0; w < HandState.SeatCount; w++)
                {
                    BotBrain b = brains[w];
                    if (b == null || w == c)
                        continue;
                    if (!moving)
                    {
                        b.WatchSeconds[c] = 0f;
                        continue;
                    }
                    if (b.CallOutTarget >= 0 || b.Shrugged[c])
                        continue;

                    b.WatchSeconds[c] = LookingAt(w, avatars[c].CheatAnchor, SpotConeDegrees) ? b.WatchSeconds[c] + dt : 0f;
                    double need = Upgrades.SpotSeconds(match.Players[c].Level(Upgrade.PokerFace), match.Players[w].Level(Upgrade.EagleEye));
                    if (b.WatchSeconds[c] < need)
                        continue;

                    if (b.Roll() < botSpotChance)
                    {
                        b.CallOutTarget = c;
                        b.CallOutAt = now + 0.3f + (float)b.Roll() * 0.6f;
                        avatars[w].LookAt(avatars[c].BodyPoint);
                    }
                    else
                    {
                        b.Shrugged[c] = true;
                    }
                }
            }

            for (int w = 0; w < HandState.SeatCount; w++)
            {
                BotBrain b = brains[w];
                if (b == null || b.CallOutTarget < 0 || now < b.CallOutAt)
                    continue;
                int target = b.CallOutTarget;
                b.CallOutTarget = -1;
                b.ReleaseGaze(now);
                if (cheats[target] != null && cheats[target].Exposed(now))
                {
                    Caught(target, w);
                    return;
                }
            }
        }

        private void Caught(int cheater, int caller)
        {
            CancelCheats();
            match.CatchCheater(cheater, caller);
            resultLines = new List<string>
            {
                cheater == HumanSeat
                    ? $"{SeatName(caller)} caught YOU cheating! 出千!"
                    : $"{SeatName(caller)} caught {SeatName(cheater)} cheating! 出千!",
                $"    {SeatName(cheater)} pays {matchRules.CheatPenalty} faan to each player. The hand is over.",
            };
            FlushMatchEvents();
            FlushHandEvents();
            BotsShop();
            ShowPopup("出千!  Caught cheating!", resultLines[0].Replace(" 出千!", ""));
            RebuildTable();
        }

        /// <summary>The bot you're looking at most directly, or -1.</summary>
        private int LookTarget()
        {
            int best = -1;
            float bestAngle = CallOutTargetDegrees;
            for (int s = 0; s < HandState.SeatCount; s++)
            {
                if (s == HumanSeat)
                    continue;
                float a = Vector3.Angle(cam.transform.forward, avatars[s].BodyPoint - cam.transform.position);
                if (a < bestAngle)
                {
                    bestAngle = a;
                    best = s;
                }
            }
            return best;
        }

        private void HumanCallOut(int target)
        {
            if (target < 0 || !HandLive || Time.time < callOutCooldownUntil)
                return;
            if (cheats[target] != null && cheats[target].Exposed(Time.time))
            {
                Caught(target, HumanSeat);
                return;
            }
            match.FalseAccusation(HumanSeat, target);
            FlushMatchEvents();
            callOutCooldownUntil = Time.time + 4f;
            ShowPopup("Wrong call!", $"{SeatName(target)} wasn't cheating.\nYou pay {matchRules.FalseAccusationFine} faan to {SeatName(target)}.");
        }

        /// <summary>Bots spend spare faan on upgrades between hands.</summary>
        private void BotsShop()
        {
            if (match.Phase != MatchPhase.BetweenHands)
                return;
            for (int s = 0; s < HandState.SeatCount; s++)
            {
                BotBrain b = brains[s];
                if (b == null)
                    continue;
                var affordable = Upgrades.All
                    .Where(u => match.CanBuy(s, u, out int cost) && match.Players[s].Score >= cost + 4)
                    .ToList();
                if (affordable.Count > 0)
                    match.TryBuy(s, affordable[(int)(b.Roll() * affordable.Count) % affordable.Count], out _);
            }
            FlushMatchEvents();
        }

        // ------------------------------------------------------------------ names & log

        private string SeatName(int seat)
        {
            string who = seat == HumanSeat ? "You" : "Bot " + seat;
            return Hand == null ? who : $"{who} ({Hand.Seats[seat].SeatWind})";
        }

        private string ShortName(int seat) => seat == HumanSeat ? "You" : "Bot " + seat;

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
                switch (Hand.Events[loggedHandEvents])
                {
                    case TileDrawnEvent d when d.Seat == HumanSeat:
                        AddLog($"You draw {d.Tile}{(d.FromBack ? " (replacement)" : "")}");
                        break;
                    case BonusTileRevealedEvent b:
                        AddLog($"{SeatName(b.Seat)} reveals {b.Tile}");
                        break;
                    case DrawFailedEvent f when f.Seat == HumanSeat:
                        AddLog($"You tried to draw {f.Tile} — none left. Turn lost.");
                        ShowPopup("Invalid draw!",
                            $"There are no {LongName(f.Tile)} left in the wall —\nthey're all in other hands or on the table.\nYou lose your turn.");
                        break;
                    case DrawFailedEvent f:
                        AddLog($"{SeatName(f.Seat)} reached for a tile that wasn't there — turn lost.");
                        break;
                    case TileDiscardedEvent t:
                        AddLog($"{SeatName(t.Seat)} discards {t.Tile}");
                        break;
                    case MeldDeclaredEvent m:
                        AddLog($"{SeatName(m.Seat)}: {m.Meld}");
                        break;
                    case HandEndedEvent h when h.Result.Outcome == HandOutcome.Win:
                        foreach (int w in h.Result.Winners)
                            AddLog(DescribeWin(h.Result, w));
                        break;
                    case HandEndedEvent h when h.Result.Outcome == HandOutcome.ExhaustiveDraw:
                        AddLog("Draw game — the wall is empty.");
                        break;
                }
            }
        }

        private void FlushMatchEvents()
        {
            for (; loggedMatchEvents < match.Events.Count; loggedMatchEvents++)
            {
                switch (match.Events[loggedMatchEvents])
                {
                    case MatchHandStartedEvent h:
                        AddLog($"— Hand {h.HandNumber}/{matchRules.HandsPerMatch}: {SeatName(h.Dealer)} deals, {h.PrevailingWind} round —");
                        break;
                    case PaymentEvent p:
                        string line = $"{SeatName(p.From)} {(p.From == HumanSeat ? "pay" : "pays")} {(p.To == HumanSeat ? "you" : SeatName(p.To))} {p.Amount} faan";
                        paymentLines.Add(line);
                        AddLog(line);
                        break;
                    case CheaterCaughtEvent c:
                        AddLog($"{SeatName(c.Caller)} called out {SeatName(c.Seat)} — caught cheating!");
                        break;
                    case FalseAccusationEvent f:
                        AddLog($"{SeatName(f.Seat)} wrongly accused {SeatName(f.Accused)}.");
                        break;
                    case UpgradeBoughtEvent u:
                        AddLog($"{SeatName(u.Seat)} bought {Upgrades.Name(u.Upgrade)} {u.Level} ({u.Cost} faan).");
                        break;
                    case MatchOverEvent o:
                        AddLog("Match over! Winner: " + string.Join(", ", o.Winners.Select(SeatName)));
                        break;
                }
            }
        }

        // ------------------------------------------------------------------ input & UI

        private void OnGUI()
        {
            if (match == null || Hand == null)
                return;
            EnsureStyles();

            Event e = Event.current;
            if (e.type == EventType.Layout)
                uiRects.Clear();

            if (e.type == EventType.MouseMove || e.type == EventType.MouseDown || e.type == EventType.MouseDrag || e.type == EventType.Repaint)
            {
                mouseGui = e.mousePosition;
                hasMouse = true;
                UpdateHover(e.mousePosition);
            }

            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Space && HandLive)
            {
                e.Use();
                HumanCallOut(LookTarget());
                GUIUtility.ExitGUI();
            }

            if (e.type == EventType.MouseDown && e.button == 0 && !uiRects.Any(r => r.Contains(e.mousePosition)))
            {
                if (hoveredTile != null)
                {
                    Tile tile = hoveredTile.Tile;
                    e.Use();
                    Apply(PlayerAction.Discard(HumanSeat, tile));
                    GUIUtility.ExitGUI();
                }
                else if (hoveredAvatar >= 0 && HandLive)
                {
                    int target = hoveredAvatar;
                    e.Use();
                    HumanCallOut(target);
                    GUIUtility.ExitGUI();
                }
            }

            DrawSeatTags();
            float statusBottom = DrawStatus();
            if (showFullLog)
                DrawFullLog(statusBottom + 8);

            if (HandLive)
            {
                bool cheatUi = headLook.LookingDown || (cheats[HumanSeat] != null && cheats[HumanSeat].InProgress);
                if (cheatUi)
                    DrawCheatStrip();
                else
                    DrawActionBar();
                if (PickerOpen)
                    DrawPicker();
                if (!headLook.LookingDown)
                    DrawLookHint();
            }
            else
            {
                DrawBetweenHands();
            }
            DrawPopup();
        }

        private void UpdateHover(Vector2 guiPosition)
        {
            TileView nextTile = null;
            int nextAvatar = -1;
            bool overUi = uiRects.Any(r => r.Contains(guiPosition));
            bool canDiscard = HandLive && Hand.Phase == HandPhase.AwaitingAction && Hand.CurrentSeat == HumanSeat
                && !headLook.LookingDown;

            if (cam != null && !overUi && HandLive)
            {
                var screen = new Vector3(guiPosition.x, Screen.height - guiPosition.y, 0f);
                if (Physics.Raycast(cam.ScreenPointToRay(screen), out RaycastHit hit, 10f))
                {
                    TileView view = hit.collider.GetComponentInParent<TileView>();
                    AvatarView avatar = hit.collider.GetComponent<AvatarView>();
                    if (canDiscard && view != null && view.InHand && view.Seat == HumanSeat
                        && Hand.GetLegalActions(HumanSeat).Contains(PlayerAction.Discard(HumanSeat, view.Tile)))
                        nextTile = view;
                    else if (avatar != null && avatar.Seat != HumanSeat)
                        nextAvatar = avatar.Seat;
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
            hoveredAvatar = nextAvatar;
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
            buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = size, richText = true, padding = new RectOffset(12, 12, 6, 6) };
            pickerStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = Mathf.RoundToInt(size * 0.8f),
                richText = true,
                wordWrap = false,
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(2, 2, 2, 2),
            };
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
            return p.z > 0f ? Screen.height - p.y : Screen.height * 0.8f;
        }

        private string ScoresLine() =>
            "Scores: " + string.Join("  ·  ", Enumerable.Range(0, HandState.SeatCount).Select(s => $"{ShortName(s)} {match.Players[s].Score}"));

        private float DrawStatus()
        {
            MatchPlayer me = match.Players[HumanSeat];
            var lines = new List<string>
            {
                $"Hand {match.HandNumber}/{matchRules.HandsPerMatch} · {(classicRules ? "Classic" : "Party")} · {match.PrevailingWind} round · Wall {Hand.Wall.DrawableCount}",
                HandLive ? $"Turn: {SeatName(Hand.CurrentSeat)}" : "Hand over",
                ScoresLine(),
                "Your upgrades: " + string.Join(" · ", Upgrades.All.Select(u => $"{Upgrades.Name(u)} {me.Level(u)}")),
            };

            float w = Unit * 24;
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
            return r.yMax;
        }

        private void DrawFullLog(float top)
        {
            int count = Mathf.Min(FullLogLines, log.Count);
            float lh = LineHeight * 0.85f;
            Rect r = Panel(new Rect(Pad, top, Unit * 26, Pad * 2 + count * lh));
            float y = r.y + Pad;
            foreach (string line in log.Skip(log.Count - count))
            {
                GUI.Label(new Rect(r.x + Pad, y, r.width - Pad * 2, lh), line, logStyle);
                y += lh;
            }
        }

        /// <summary>Name tags floating above each bot's head.</summary>
        private void DrawSeatTags()
        {
            for (int s = 0; s < HandState.SeatCount; s++)
            {
                if (s == HumanSeat)
                    continue;
                Vector3 p = cam.WorldToScreenPoint(avatars[s].TagPoint);
                if (p.z <= 0f)
                    continue;
                bool active = HandLive && Hand.CurrentSeat == s;
                bool calling = brains[s] != null && brains[s].CallOutTarget >= 0;
                string text = (calling ? "<color=#ff6060>!!</color> " : active ? "> " : "") +
                    $"{SeatName(s)} · {match.Players[s].Score} faan";
                GUIStyle style = active || hoveredAvatar == s ? titleStyle : textStyle;
                float w = TextWidth(style, text) + Pad * 2;
                float h = LineHeight + 4;
                var r = new Rect(p.x - w / 2f, Screen.height - p.y - h / 2f, w, h);
                GUI.Box(r, GUIContent.none, boxStyle);
                GUI.Label(new Rect(r.x + Pad, r.y + 2, r.width - Pad * 2, r.height - 4), text, style);
            }
        }

        /// <summary>Tells you who a SPACE / click call-out would accuse.</summary>
        private void DrawLookHint()
        {
            int target = hoveredAvatar >= 0 ? hoveredAvatar : LookTarget();
            if (target < 0)
                return;
            string text = Time.time < callOutCooldownUntil
                ? "Catch your breath before calling out again…"
                : $"Watching {SeatName(target)} — press SPACE or click them to call out cheating (出千!)";
            float w = TextWidth(centerStyle, text) + Pad * 2;
            var r = new Rect(Screen.width - w - Pad, Pad, w, LineHeight + Pad);
            GUI.Box(r, GUIContent.none, boxStyle);
            GUI.Label(r, text, centerStyle);
        }

        // ------------------------------------------------------------------ the pocket cheat UI

        /// <summary>
        /// Bottom strip shown while you look down into your lap. Choose a tile from your hand and
        /// one from your pocket (nobody can see you choosing), then press Swap: the three steps —
        /// into the pocket, out of the pocket, into your hand — play out as one visible motion.
        /// You can only act while looking down, and while you are, you can't see who's watching.
        /// </summary>
        private void DrawCheatStrip()
        {
            CheatMotion c = cheats[HumanSeat];
            IReadOnlyList<Tile> pocket = match.Players[HumanSeat].Pocket;
            bool down = headLook.LookingDown;
            float now = Time.time;
            float stepSeconds = StepSeconds(HumanSeat);
            float buttonH = Unit * 2.6f;
            bool wasEnabled = GUI.enabled;

            if (c != null && c.InProgress)
            {
                string step = c.Step == CheatStep.Stashing ? $"1/3  Slipping {c.HandTile} into your pocket…"
                    : c.Step == CheatStep.PullingOut ? $"2/3  Pulling {pocket[c.PocketIndex]} out of your pocket…"
                    : $"3/3  Slipping {pocket[c.PocketIndex]} into your hand…";
                float hMotion = Pad * 2 + LineHeight * 2;
                Rect rm = Panel(new Rect(Pad, Screen.height - hMotion - Pad, Screen.width - Pad * 2, hMotion));
                GUI.Label(new Rect(rm.x + Pad, rm.y + Pad, rm.width - Pad * 2, LineHeight),
                    $"{step}  {Mathf.RoundToInt(c.Progress(now) * 100)}%", titleStyle);
                GUI.Label(new Rect(rm.x + Pad, rm.y + Pad + LineHeight, rm.width - Pad * 2, LineHeight),
                    "<color=#ff8080>Your hands are moving — anyone watching them can call you out!</color>", textStyle);
                return;
            }

            bool canSwap = Hand.CanPocketSwap(HumanSeat);
            List<Tile> handTiles = Hand.Seats[HumanSeat].Concealed.Distinct().OrderBy(t => t).ToList();
            if (pickedHandTile.HasValue && !handTiles.Contains(pickedHandTile.Value))
                pickedHandTile = null;
            if (pickedPocketIndex >= pocket.Count)
                pickedPocketIndex = -1;

            float h = Pad * 2 + LineHeight * 3 + (buttonH + 6) * 2 + 6;
            Rect r = Panel(new Rect(Pad, Screen.height - h - Pad, Screen.width - Pad * 2, h));
            float x0 = r.x + Pad;
            float y = r.y + Pad;
            float iw = r.width - Pad * 2;

            string header = !canSwap
                ? "Your pocket — can't swap right now (you have a claim pending)"
                : !down ? "Your pocket — look down (bottom edge) to reach for it"
                : "Your pocket — pick a tile to put in, a tile to take out, then Swap";
            GUI.Label(new Rect(x0, y, iw, LineHeight), header, titleStyle);
            y += LineHeight + 2;

            GUI.enabled = wasEnabled && down && canSwap;
            Color oldBackground = GUI.backgroundColor;

            // Row 1: hand tiles → pocket
            GUI.Label(new Rect(x0, y + buttonH / 2f - LineHeight / 2f, Unit * 7, LineHeight), "Hand → pocket:", textStyle);
            float bw = Mathf.Min(Unit * 5.2f, (iw - Unit * 7) / Mathf.Max(1, handTiles.Count) - 4f);
            float x = x0 + Unit * 7;
            foreach (Tile t in handTiles)
            {
                GUI.backgroundColor = pickedHandTile == t ? new Color(1f, 0.85f, 0.3f) : oldBackground;
                if (GUI.Button(new Rect(x, y, bw, buttonH), PickerLabel(t), pickerStyle))
                    pickedHandTile = t;
                x += bw + 4;
            }
            y += buttonH + 6;

            // Row 2: pocket tiles → hand, then the Swap button
            GUI.Label(new Rect(x0, y + buttonH / 2f - LineHeight / 2f, Unit * 7, LineHeight), "Pocket → hand:", textStyle);
            x = x0 + Unit * 7;
            for (int i = 0; i < pocket.Count; i++)
            {
                GUI.backgroundColor = pickedPocketIndex == i ? new Color(1f, 0.85f, 0.3f) : oldBackground;
                if (GUI.Button(new Rect(x, y, bw, buttonH), PickerLabel(pocket[i]), pickerStyle))
                    pickedPocketIndex = i;
                x += bw + 4;
            }
            GUI.backgroundColor = oldBackground;

            bool ready = pickedHandTile.HasValue && pickedPocketIndex >= 0;
            string swapLabel = ready
                ? $"<b>Swap {pickedHandTile.Value} → {pocket[pickedPocketIndex]}</b>  ({stepSeconds * 3f:0.0}s)"
                : "Swap";
            GUI.enabled = wasEnabled && down && canSwap && ready;
            float sw = TextWidth(buttonStyle, swapLabel) + 24;
            if (GUI.Button(new Rect(x + Unit, y, sw, buttonH), swapLabel, buttonStyle))
            {
                GUI.enabled = wasEnabled;
                Tile tile = pickedHandTile.Value;
                int index = pickedPocketIndex;
                pickedHandTile = null;
                pickedPocketIndex = -1;
                StartCheat(HumanSeat, tile, index);
                GUIUtility.ExitGUI();
            }
            GUI.enabled = wasEnabled;
            y += buttonH + 6;

            GUI.Label(new Rect(x0, y, iw, LineHeight),
                $"Choosing is safe; the swap itself takes {stepSeconds * 3f:0.0}s and can be seen.  Caught = pay {matchRules.CheatPenalty} faan to everyone.  " +
                "Tip: cheat while a bot is busy with its own turn.", logStyle);
        }

        private static string PickerLabel(Tile t) =>
            $"<color={PickerColor(t)}><b>{TileFactory.LabelFor(t).Replace("\n", " ")}</b></color>";

        // ------------------------------------------------------------------ turn prompts

        /// <summary>One-line prompt plus a row of claim/turn buttons, just above the human's hand.</summary>
        private void DrawActionBar()
        {
            List<PlayerAction> legal = HumanActions();
            List<PlayerAction> buttons = legal.Where(a => a.Type != ActionType.Discard && a.Type != ActionType.Draw).ToList();

            string message;
            if (legal.Count == 0)
                message = $"Waiting for {SeatName(Hand.CurrentSeat)}…   (push the cursor to the screen edges to look around)";
            else if (Hand.Phase == HandPhase.AwaitingDraw)
                message = "Your turn — pick the tile you want to draw";
            else if (Hand.Phase == HandPhase.AwaitingAction)
                message = "Your turn — click a tile in your hand to discard it";
            else if (Hand.Phase == HandPhase.AwaitingRobKong)
                message = $"{SeatName(Hand.CurrentSeat)} is adding to a kong";
            else
                message = $"{SeatName(Hand.LastDiscardSeat)} discarded {Hand.LastDiscard}";
            if (legal.Count > 0 && humanTimeLimit > 0f)
                message += $"   ({SecondsLeft()}s)";

            string hint = ReadyHint();
            List<string> labels = buttons.Select(Describe).ToList();
            List<System.Action> actions = buttons.Select(a => (System.Action)(() => Apply(a))).ToList();
            var lines = new List<string> { message };
            if (hint != null)
                lines.Add(hint);
            DrawBottomPanel(null, lines, labels, actions, centered: true);
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

        // ------------------------------------------------------------------ chosen draws (optional rule)

        private void DrawPicker()
        {
            int[] seen = Hand.VisibleKindCounts(HumanSeat);
            var gone = new HashSet<Tile>(Hand.Events.OfType<DrawFailedEvent>()
                .Where(f => f.Seat == HumanSeat).Select(f => f.Tile));

            float cell = Unit * 3.1f;
            const float gap = 5f;
            float gridW = 9 * cell + 8 * gap;
            string title = $"Choose your draw   ({SecondsLeft()}s)";
            const string sub = "Pick a tile you think is still in the wall. If none are left, you lose your turn.";
            const string legend = "<color=#8f8>Green</color> = wins your hand   ·   Grey = all 4 accounted for (or already came up empty)";
            float w = Mathf.Max(gridW, TextWidth(textStyle, sub)) + Pad * 2;
            float h = Pad * 2 + LineHeight * 1.2f + LineHeight * 2 + 4 * (cell + gap) + 4;
            Rect r = Panel(new Rect((Screen.width - w) / 2f, Screen.height * 0.1f, w, h));

            float x0 = r.x + (r.width - gridW) / 2f;
            float y = r.y + Pad;
            GUI.Label(new Rect(r.x + Pad, y, r.width - Pad * 2, LineHeight * 1.2f), title, titleStyle);
            y += LineHeight * 1.2f;
            GUI.Label(new Rect(r.x + Pad, y, r.width - Pad * 2, LineHeight), sub, textStyle);
            y += LineHeight + 4;

            Color oldBackground = GUI.backgroundColor;
            for (int k = 0; k < TileSet.PlayableKindCount; k++)
            {
                Tile t = Tile.FromKindIndex(k);
                int row = k < 27 ? k / 9 : 3;
                int col = k < 27 ? k % 9 : k - 27;
                var b = new Rect(x0 + col * (cell + gap), y + row * (cell + gap), cell, cell);

                FaanResult win = FaanCalculator.Calculate(Hand.PreviewDraw(HumanSeat, t), rules);
                bool wins = win != null && win.Total >= rules.MinFaan;
                bool hopeless = seen[k] >= TileSet.CopiesPerPlayableKind || gone.Contains(t);

                GUI.backgroundColor = wins ? new Color(0.35f, 1f, 0.35f) : hopeless ? new Color(0.45f, 0.45f, 0.45f) : oldBackground;
                string label = PickerLabel(t) + "\n" +
                    (gone.Contains(t) ? "none left" : $"seen {seen[k]}/4") + (wins ? $"\n<color=#ff0>WIN {win.Total}</color>" : "");
                if (GUI.Button(b, label, pickerStyle))
                {
                    GUI.backgroundColor = oldBackground;
                    Apply(PlayerAction.Draw(HumanSeat, t));
                    GUIUtility.ExitGUI();
                }
            }
            GUI.backgroundColor = oldBackground;
            GUI.Label(new Rect(r.x + Pad, y + 4 * (cell + gap), r.width - Pad * 2, LineHeight), legend, textStyle);
        }

        /// <summary>Bright text colours that read on the dark default buttons.</summary>
        private static string PickerColor(Tile t)
        {
            switch (t.Suit)
            {
                case TileSuit.Dots: return "#8cb8ff";
                case TileSuit.Bamboo: return "#8ee69a";
                case TileSuit.Characters: return "#ff8c8c";
                case TileSuit.Dragon: return t.Rank == 1 ? "#ff6b6b" : t.Rank == 2 ? "#6be36b" : "#b8ccff";
                default: return "#ffffff";
            }
        }

        private static string LongName(Tile t)
        {
            switch (t.Suit)
            {
                case TileSuit.Dots: return $"{t.Rank} Dots";
                case TileSuit.Bamboo: return $"{t.Rank} Bamboo";
                case TileSuit.Characters: return $"{t.Rank} Characters";
                case TileSuit.Wind: return new[] { "East", "South", "West", "North" }[t.Rank - 1] + " Winds";
                case TileSuit.Dragon: return new[] { "Red", "Green", "White" }[t.Rank - 1] + " Dragons";
                default: return t.ToString();
            }
        }

        // ------------------------------------------------------------------ popups

        private void ShowPopup(string title, string text)
        {
            popupTitle = title;
            popupText = text;
            popupUntil = Time.time + 4f;
        }

        /// <summary>Centred notice; closes itself after a few seconds.</summary>
        private void DrawPopup()
        {
            if (popupText == null || Time.time > popupUntil)
                return;
            string[] lines = popupText.Split('\n');
            float w = Mathf.Max(TextWidth(titleStyle, popupTitle), lines.Max(l => TextWidth(centerStyle, l))) + Pad * 4;
            float buttonH = Unit * 2.1f;
            float h = Pad * 2 + LineHeight * 1.3f + lines.Length * LineHeight + buttonH + 10;
            Rect r = Panel(new Rect((Screen.width - w) / 2f, Screen.height * 0.3f, w, h));
            Panel(r); // drawn twice for a more opaque box

            float y = r.y + Pad;
            var titleCentered = new GUIStyle(titleStyle) { alignment = TextAnchor.MiddleCenter };
            GUI.Label(new Rect(r.x, y, r.width, LineHeight * 1.3f), $"<color=#ff7070>{popupTitle}</color>", titleCentered);
            y += LineHeight * 1.3f;
            foreach (string line in lines)
            {
                GUI.Label(new Rect(r.x, y, r.width, LineHeight), line, centerStyle);
                y += LineHeight;
            }
            float bw = Unit * 6;
            if (GUI.Button(new Rect(r.center.x - bw / 2f, y + 6, bw, buttonH), "OK", buttonStyle))
            {
                popupText = null;
                GUIUtility.ExitGUI();
            }
        }

        // ------------------------------------------------------------------ between hands: results + shop

        private void DrawBetweenHands()
        {
            var lines = new List<string>(resultLines.Skip(1));
            lines.AddRange(paymentLines.Select(p => "    " + p));
            lines.Add(ScoresLine());

            string title = resultLines.Count > 0 ? resultLines[0] : "";
            var labels = new List<string>();
            var actions = new List<System.Action>();

            if (match.Phase == MatchPhase.MatchOver)
            {
                bool youWon = match.Winners.Contains(HumanSeat);
                lines.Add(youWon
                    ? (match.Winners.Count > 1 ? "MATCH OVER — you share first place!" : "MATCH OVER — YOU WIN!")
                    : "MATCH OVER — winner: " + string.Join(", ", match.Winners.Select(SeatName)));
                labels.Add("New match");
                actions.Add(NewMatch);
            }
            else
            {
                MatchPlayer me = match.Players[HumanSeat];
                lines.Add($"Upgrade shop — you have {me.Score} faan:");
                foreach (Upgrade u in Upgrades.All)
                {
                    int level = me.Level(u);
                    bool canBuy = match.CanBuy(HumanSeat, u, out int cost);
                    string price = level >= Upgrades.MaxLevel ? "MAX" : $"{cost} faan";
                    lines.Add($"    {Upgrades.Name(u)} {level}/{Upgrades.MaxLevel} — {Upgrades.Description(u)}");
                    if (level < Upgrades.MaxLevel)
                    {
                        Upgrade upgrade = u;
                        labels.Add((canBuy ? "" : "<color=#888>") + $"Buy {Upgrades.Name(u)} {level + 1} ({price})" + (canBuy ? "" : "</color>"));
                        actions.Add(() =>
                        {
                            if (!match.TryBuy(HumanSeat, upgrade, out string error))
                                ShowPopup("Can't buy that", error);
                            FlushMatchEvents();
                        });
                    }
                }
                labels.Add("<b>Next hand</b>");
                actions.Add(StartHand);
            }
            DrawBottomPanel(title, lines, labels, actions, centered: false);
        }

        /// <summary>Panel just above the human's hand: optional bold title, text lines, rows of buttons.</summary>
        private void DrawBottomPanel(string title, List<string> lines, List<string> buttonLabels,
            List<System.Action> buttonActions, bool centered)
        {
            GUIStyle lineStyle = centered ? centerStyle : textStyle;
            List<float> widths = buttonLabels.Select(l => TextWidth(buttonStyle, l) + 8).ToList();
            const float gap = 8f;
            float maxRow = Screen.width * 0.85f - Pad * 2;

            // Wrap buttons into rows.
            var rows = new List<List<int>> { new List<int>() };
            float rowWidth = 0f;
            for (int i = 0; i < widths.Count; i++)
            {
                if (rows[rows.Count - 1].Count > 0 && rowWidth + gap + widths[i] > maxRow)
                {
                    rows.Add(new List<int>());
                    rowWidth = 0f;
                }
                rowWidth += (rows[rows.Count - 1].Count > 0 ? gap : 0f) + widths[i];
                rows[rows.Count - 1].Add(i);
            }
            if (widths.Count == 0)
                rows.Clear();

            float buttonH = Unit * 2.1f;
            bool hasTitle = !string.IsNullOrEmpty(title);
            float w = rows.Count > 0 ? rows.Max(row => row.Sum(i => widths[i]) + gap * (row.Count - 1)) : 0f;
            if (hasTitle)
                w = Mathf.Max(w, TextWidth(titleStyle, title));
            foreach (string line in lines)
                w = Mathf.Max(w, TextWidth(lineStyle, line));
            w = Mathf.Min(w + Pad * 2, Screen.width * 0.85f);

            float h = Pad * 2 + (hasTitle ? LineHeight * 1.2f : 0f) + lines.Count * LineHeight
                + rows.Count * (buttonH + 6) + (rows.Count > 0 ? 4 : 0);
            float bottom = Mathf.Min(HandTopY() - Unit * 0.4f, Screen.height - Pad);
            float top = Mathf.Max(Pad, bottom - h);
            Rect r = Panel(new Rect((Screen.width - w) / 2f, top, w, h));

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

            y += 4;
            foreach (List<int> row in rows)
            {
                float rw = row.Sum(i => widths[i]) + gap * (row.Count - 1);
                float bx = r.center.x - rw / 2f;
                foreach (int i in row)
                {
                    if (GUI.Button(new Rect(bx, y, widths[i], buttonH), buttonLabels[i], buttonStyle))
                    {
                        buttonActions[i]();
                        GUIUtility.ExitGUI();
                    }
                    bx += widths[i] + gap;
                }
                y += buttonH + 6;
            }
        }
    }
}
