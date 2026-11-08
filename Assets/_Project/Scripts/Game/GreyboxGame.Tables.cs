using System.Collections.Generic;
using System.Linq;
using MahjongNight.Core;
using UnityEngine;

namespace MahjongNight.Game
{
    /// <summary>
    /// Several tables at once: you hold a seat at every table and have to scramble between them,
    /// sitting down to play your hand at each one. A countdown floats above every table while
    /// it's waiting on you; when it runs out, your move there is played for you.
    /// </summary>
    /// <remarks>
    /// Everything that belongs to one table (its match, bots, avatars, cheats, tiles, timers,
    /// the bots' phones) lives in a <see cref="TableContext"/>. The rest of the controller works
    /// on <see cref="ctx"/> — the table being updated or drawn right now — through the forwarding
    /// properties below, so the single-table code runs unchanged once per table.
    /// </remarks>
    public sealed partial class GreyboxGame
    {
        [Header("Tables")]
        [Tooltip("How many tables you play at simultaneously (1-5).")]
        [SerializeField] private int tableCount = 5;
        [Tooltip("Seconds between hands before a table deals the next hand by itself.")]
        [SerializeField] private float autoNextHandSeconds = 8f;

        private const float TableSpacing = 4.6f;

        private sealed class TableContext
        {
            public int Index;
            public Vector3 Origin;
            public ScoreMatch Match;
            public readonly SimpleBot[] Bots = new SimpleBot[HandState.SeatCount];
            public IBot HumanAutoBot;
            public readonly BotBrain[] Brains = new BotBrain[HandState.SeatCount];
            public readonly PlayerAvatar[] Avatars = new PlayerAvatar[HandState.SeatCount];
            public readonly CheatMotion[] Cheats = new CheatMotion[HandState.SeatCount];
            public readonly float[] LookingDownFor = new float[HandState.SeatCount];
            public TableView View;
            public float NextBotTime;
            public float HumanWaitStart;
            public int LoggedHandEvents;
            public int LoggedMatchEvents;
            public List<string> ResultLines = new List<string>();
            public readonly List<string> PaymentLines = new List<string>();
            public float AutoNextHandAt = -1f;
            public TextMesh Label;

            // Bots' phones at this table (yours is global — you only have one phone).
            public readonly PhoneState[] Phones = new PhoneState[HandState.SeatCount];
            public readonly float[] NextDogText = new float[HandState.SeatCount];
            public readonly float[] CallCooldownUntil = new float[HandState.SeatCount];
        }

        private readonly List<TableContext> tables = new List<TableContext>();
        private TableContext ctx;     // the table being updated / drawn right now
        private int seatedTable;      // the table whose chair you last sat in

        private TableContext Seated => tables[seatedTable];

        /// <summary>Is the human physically sitting at the table being processed?</summary>
        private bool AtThisTable => ctx == Seated && !standing;

        // --- per-table state, forwarded to the current context
        private ScoreMatch match { get => ctx.Match; set => ctx.Match = value; }
        private HandState Hand => match.Hand;
        private SimpleBot[] bots => ctx.Bots;
        private IBot humanAutoBot { get => ctx.HumanAutoBot; set => ctx.HumanAutoBot = value; }
        private BotBrain[] brains => ctx.Brains;
        private PlayerAvatar[] avatars => ctx.Avatars;
        private CheatMotion[] cheats => ctx.Cheats;
        private float[] lookingDownFor => ctx.LookingDownFor;
        private TableView table => ctx.View;
        private float nextBotTime { get => ctx.NextBotTime; set => ctx.NextBotTime = value; }
        private float humanWaitStart { get => ctx.HumanWaitStart; set => ctx.HumanWaitStart = value; }
        private int loggedHandEvents { get => ctx.LoggedHandEvents; set => ctx.LoggedHandEvents = value; }
        private int loggedMatchEvents { get => ctx.LoggedMatchEvents; set => ctx.LoggedMatchEvents = value; }
        private List<string> resultLines { get => ctx.ResultLines; set => ctx.ResultLines = value; }
        private List<string> paymentLines => ctx.PaymentLines;

        /// <summary>Centre table first, then the four around it.</summary>
        private static List<Vector3> TableOrigins(int count)
        {
            var all = new List<Vector3>
            {
                Vector3.zero,
                new Vector3(TableSpacing, 0f, 0f), new Vector3(-TableSpacing, 0f, 0f),
                new Vector3(0f, 0f, TableSpacing), new Vector3(0f, 0f, -TableSpacing),
            };
            return all.Take(Mathf.Clamp(count, 1, all.Count)).ToList();
        }

        private void BuildTables()
        {
            var factory = new TileFactory();
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            List<Vector3> origins = TableOrigins(tableCount);
            for (int i = 0; i < origins.Count; i++)
            {
                var t = new TableContext { Index = i, Origin = origins[i] };
                t.View = new TableView(transform, factory, t.Origin);
                for (int s = 0; s < HandState.SeatCount; s++)
                {
                    int relative = (s - HumanSeat + HandState.SeatCount) % HandState.SeatCount;
                    t.Avatars[s] = new PlayerAvatar(transform, s, relative, SeatColors[s], firstPerson: s == HumanSeat, origin: t.Origin);
                }

                var labelGo = new GameObject($"Table{i + 1}Label");
                labelGo.transform.SetParent(transform, false);
                t.Label = labelGo.AddComponent<TextMesh>();
                t.Label.font = font;
                t.Label.fontSize = 64;
                t.Label.characterSize = 0.035f;
                t.Label.anchor = TextAnchor.LowerCenter;
                t.Label.alignment = TextAlignment.Center;
                labelGo.GetComponent<MeshRenderer>().sharedMaterial = font.material;
                tables.Add(t);
            }
            ctx = tables[0];
            seatedTable = 0;
        }

        /// <summary>Starts a fresh match at every table (and resets you).</summary>
        private void NewMatchAll()
        {
            foreach (TableContext t in tables)
            {
                ctx = t;
                NewMatch();
            }
            ctx = Seated;
            ResetLife();
        }

        /// <summary>Everything one table does in a frame (bots, cheats, spotting, turns, labels).</summary>
        private void UpdateTable(float now, float dt)
        {
            UpdateBotPhones(now);

            for (int s = 0; s < HandState.SeatCount; s++)
            {
                bool present = s != HumanSeat || AtThisTable;
                float pitch = s == HumanSeat ? headLook.Pitch : avatars[s].PitchDown;
                bool innocent = !present || OnPhone(s); // everyone can see a phone
                lookingDownFor[s] = !innocent && pitch >= HeadLook.LookingDownPitch ? lookingDownFor[s] + dt : 0f;
            }

            if (HandLive)
            {
                UpdateBotGaze(now);
                UpdateCheats(now);
                if (HandLive) UpdateBotCheating(now);
                if (HandLive) UpdateSpotting(now, dt);
                if (HandLive) UpdateTurns(now);
            }
            else if (match.Phase == MatchPhase.BetweenHands && ctx.AutoNextHandAt > 0f && now >= ctx.AutoNextHandAt)
            {
                StartHand();
            }

            avatars[HumanSeat].SetVisible(AtThisTable || (ctx == Seated && !humanAlive));
            if (AtThisTable)
                avatars[HumanSeat].SetHeadRotation(headLook.HeadRotation);
            for (int s = 0; s < HandState.SeatCount; s++)
            {
                CheatMotion c = cheats[s];
                bool active = c != null && c.InProgress;
                avatars[s].SetRightHand(active ? c.Pose : HandPose.Rest, active ? c.Progress(now) : 0f, now);
                avatars[s].Tick(dt, followGaze: s != HumanSeat);
            }

            UpdateTableLabel(now);
        }

        /// <summary>Is this table waiting on you right now?</summary>
        private bool WaitingOnHuman => HandLive && humanAlive && Hand.GetLegalActions(HumanSeat).Count > 0;

        /// <summary>The sign floating above the table: its number and, when it's waiting on you, a countdown.</summary>
        private void UpdateTableLabel(float now)
        {
            TextMesh label = ctx.Label;
            Vector3 pos = ctx.Origin + new Vector3(0f, 1.35f, 0f);
            label.transform.position = pos;
            Vector3 away = pos - cam.transform.position;
            if (away.sqrMagnitude > 0.0001f)
                label.transform.rotation = Quaternion.LookRotation(away); // always faces you

            string title = $"TABLE {ctx.Index + 1}" + (AtThisTable ? "  (you)" : "");
            if (WaitingOnHuman)
            {
                int secs = SecondsLeft();
                bool claim = Hand.Phase == HandPhase.AwaitingClaims || Hand.Phase == HandPhase.AwaitingRobKong;
                label.text = $"{title}\n{(claim ? "CLAIM?" : "YOUR TURN")}  {secs}s";
                label.color = secs <= 5 ? new Color(1f, 0.25f, 0.2f) : secs <= 10 ? new Color(1f, 0.75f, 0.2f) : new Color(1f, 1f, 0.6f);
            }
            else if (match.Phase == MatchPhase.BetweenHands)
            {
                label.text = $"{title}\nnext hand in {Mathf.Max(0, Mathf.CeilToInt(ctx.AutoNextHandAt - now))}s";
                label.color = new Color(0.8f, 0.8f, 0.8f);
            }
            else if (match.Phase == MatchPhase.MatchOver)
            {
                label.text = $"{title}\nmatch over";
                label.color = new Color(0.7f, 0.7f, 0.7f);
            }
            else
            {
                label.text = title;
                label.color = new Color(0.9f, 0.9f, 0.9f);
            }
        }

        /// <summary>Right-hand list of every table: your score there and whether it needs you.</summary>
        private void DrawTablesOverview()
        {
            if (tables.Count < 2)
                return;
            TableContext keep = ctx;
            var lines = new List<string>();
            foreach (TableContext t in tables)
            {
                ctx = t;
                bool claim = Hand.Phase == HandPhase.AwaitingClaims || Hand.Phase == HandPhase.AwaitingRobKong;
                string status = WaitingOnHuman
                    ? $"<color={(SecondsLeft() <= 5 ? "#ff5050" : "#ffcc40")}>{(claim ? "CLAIM?" : "YOUR TURN")} {SecondsLeft()}s</color>"
                    : match.Phase == MatchPhase.MatchOver ? "match over"
                    : match.Phase == MatchPhase.BetweenHands ? "between hands"
                    : $"hand {match.HandNumber}/{matchRules.HandsPerMatch}";
                string you = t == Seated && !standing ? "  (you)" : "";
                lines.Add($"Table {t.Index + 1}: {match.Players[HumanSeat].Score} faan · {status}{you}");
            }
            ctx = keep;
            lines.Add($"Total: {tables.Sum(t => t.Match.Players[HumanSeat].Score)} faan");

            float w = lines.Max(l => TextWidth(textStyle, l)) + Pad * 2;
            float h = Pad * 2 + LineHeight * lines.Count;
            Rect r = Panel(new Rect(Screen.width - w - Pad, Pad * 2 + LineHeight * 1.5f, w, h));
            float y = r.y + Pad;
            foreach (string line in lines)
            {
                GUI.Label(new Rect(r.x + Pad, y, r.width - Pad * 2, LineHeight), line, textStyle);
                y += LineHeight;
            }
        }
    }
}
