using System.Collections.Generic;
using System.Linq;
using MahjongNight.Core;
using UnityEngine;

namespace MahjongNight.Game
{
    /// <summary>
    /// Your phone (P) has apps:
    /// <list type="bullet">
    /// <item><b>Phone</b> — call another player at your table to distract them (or call Latte).</item>
    /// <item><b>Drum Rush</b> — a side-scrolling drum rhythm game. Red notes: hit the drum's face
    /// (J or left click); blue notes: hit the rim (K or right click). The phone turns sideways.</item>
    /// <item><b>Latte Cam</b> — see what Latte's up to at home, and how hungry they are.</item>
    /// </list>
    /// Playing Drum Rush means staring at your phone: you can't play your hand meanwhile and every
    /// table's clock keeps running. A text or call pauses the song until you deal with it.
    /// </summary>
    public sealed partial class GreyboxGame
    {
        private enum PhoneApp { Home, Calls, DrumSelect, DrumPlay, DrumResult, LatteCam }

        private PhoneApp phoneApp = PhoneApp.Home;
        private IReadOnlyList<DrumSong> setlist;
        private DrumGame drum;                         // a song in progress (you're on your phone)
        private DrumGame lastDrum;                     // the song just finished (results screen)
        private readonly Dictionary<string, int> bestDrumScores = new Dictionary<string, int>();
        private bool lastWasBest;
        private string drumJudgeText;
        private Color drumJudgeColor;
        private float drumJudgeAt = -10f, boomFlashAt = -10f, takFlashAt = -10f;
        private int drumBeat;
        private readonly MiniCanvas phoneCanvas = new MiniCanvas();

        private bool Drumming => drum != null;

        private void TogglePhone()
        {
            if (phonePanelOpen)
                ClosePhone();
            else
            {
                phonePanelOpen = true;
                phoneApp = PhoneApp.Home;
            }
        }

        private void ClosePhone()
        {
            StopDrums();
            phonePanelOpen = false;
            phoneApp = PhoneApp.Home;
        }

        private void OpenApp(PhoneApp app)
        {
            phoneApp = app;
            if (app == PhoneApp.DrumSelect && setlist == null)
                setlist = DrumSong.Setlist();
        }

        // ------------------------------------------------------------------ Drum Rush

        private void StartSong(DrumSong song)
        {
            drum = new DrumGame(song);
            drumBeat = int.MinValue;
            drumJudgeText = null;
            phoneApp = PhoneApp.DrumPlay;
            Seated.Avatars[HumanSeat].SetPhone(true);
            if (!standing)
                hoveredTile = null;
        }

        /// <summary>Stops a song in progress (no results).</summary>
        private void StopDrums()
        {
            if (drum == null)
                return;
            drum = null;
            if (humanPhone == null && tables.Count > 0)
                Seated.Avatars[HumanSeat].SetPhone(false);
            if (phoneApp == PhoneApp.DrumPlay)
                phoneApp = PhoneApp.DrumSelect;
        }

        /// <summary>Number keys inside the apps (outside the Phone app, 1-3 pick things instead of calling).</summary>
        private void HandlePhoneKeys()
        {
            if (Input.GetKeyDown(KeyCode.Backspace) && phoneApp != PhoneApp.Home)
            {
                StopDrums();
                phoneApp = PhoneApp.Home;
                return;
            }
            int pick = Input.GetKeyDown(KeyCode.Alpha1) ? 0 : Input.GetKeyDown(KeyCode.Alpha2) ? 1 : Input.GetKeyDown(KeyCode.Alpha3) ? 2 : -1;
            if (pick < 0)
                return;
            switch (phoneApp)
            {
                case PhoneApp.Home:
                    OpenApp(pick == 0 ? PhoneApp.Calls : pick == 1 ? PhoneApp.DrumSelect : PhoneApp.LatteCam);
                    break;
                case PhoneApp.DrumSelect:
                    if (pick < setlist.Count)
                        StartSong(setlist[pick]);
                    break;
                case PhoneApp.DrumResult:
                    if (pick == 0) StartSong(lastDrum.Song);
                    else if (pick == 1) phoneApp = PhoneApp.DrumSelect;
                    else phoneApp = PhoneApp.Home;
                    break;
            }
        }

        /// <summary>Runs the song: the clock (paused while a text or call is up), your hits, the beat.</summary>
        private void UpdateDrums(float dt)
        {
            if (drum == null)
                return;
            if (!humanAlive)
            {
                StopDrums();
                return;
            }
            if (humanPhone != null)
                return;                             // paused: answer your phone first

            drum.Advance(dt);

            // The beat: a tick on every beat (a stronger one on each bar), with a count-in.
            double beatLen = drum.Song.BeatSeconds;
            int beat = Mathf.FloorToInt((float)((drum.Time - DrumSong.LeadIn) / beatLen));
            if (beat != drumBeat && beat >= -4 && drum.Time < drum.Song.Length - 1.0)
            {
                drumBeat = beat;
                PlaySound(beat % 4 == 0 ? accentClip : tickClip, 0.35f);
            }

            bool boom = Input.GetKeyDown(KeyCode.J) || Input.GetMouseButtonDown(0);
            bool tak = Input.GetKeyDown(KeyCode.K) || Input.GetMouseButtonDown(1);
            if (boom) DrumHitPressed(DrumHit.Boom);
            if (tak) DrumHitPressed(DrumHit.Tak);

            if (drum.Finished)
            {
                lastDrum = drum;
                string key = drum.Song.Name;
                lastWasBest = !bestDrumScores.TryGetValue(key, out int best) || drum.Score > best;
                if (lastWasBest)
                    bestDrumScores[key] = drum.Score;
                AddLog($"Drum Rush — {key}: {drum.Score:N0} pts, rank {drum.Rank}{(drum.FullCombo ? ", FULL COMBO!" : "")}");
                StopDrums();
                phoneApp = PhoneApp.DrumResult;
            }
        }

        private void DrumHitPressed(DrumHit kind)
        {
            PlaySound(kind == DrumHit.Boom ? boomClip : takClip, 0.8f);
            if (kind == DrumHit.Boom) boomFlashAt = Time.time; else takFlashAt = Time.time;
            DrumJudgement j = drum.Hit(kind);
            if (j == DrumJudgement.None)
                return;
            drumJudgeAt = Time.time;
            drumJudgeText = j == DrumJudgement.Great ? "GREAT" : j == DrumJudgement.Good ? "GOOD" : "MISS";
            drumJudgeColor = j == DrumJudgement.Great ? new Color(1f, 0.85f, 0.2f) : j == DrumJudgement.Good ? Color.white : new Color(0.6f, 0.6f, 0.65f);
        }

        // ------------------------------------------------------------------ drawing

        private static readonly Color BoomRed = new Color(0.92f, 0.27f, 0.2f);
        private static readonly Color TakBlue = new Color(0.25f, 0.6f, 0.98f);

        /// <summary>Draws the phone, if it's out: upright for the apps, sideways for Drum Rush.</summary>
        private void DrawPhone()
        {
            if (!phonePanelOpen || !humanAlive)
                return;
            bool sideways = phoneApp == PhoneApp.DrumPlay;
            float w = sideways ? Mathf.Min(Screen.width * 0.7f, Unit * 46f) : Unit * 16f;
            float h = sideways ? w * 0.5f : Unit * 28f;
            float x = sideways ? (Screen.width - w) / 2f : Mathf.Min(Screen.width - w - Pad, Screen.width / 2f + Unit * 12f);
            float y = sideways ? (Screen.height - h) / 2f + Unit * 3f : (Screen.height - h) / 2f + Unit * 2f;

            // The phone: a dark body with a screen inset.
            var body = new Rect(x - Unit * 0.8f, y - Unit * 0.8f, w + Unit * 1.6f, h + Unit * 1.6f);
            uiRects.Add(body);
            phoneCanvas.Area = body;
            phoneCanvas.Fill(0f, 0f, 1f, 1f, new Color(0.06f, 0.06f, 0.08f));
            phoneCanvas.Area = new Rect(x, y, w, h);
            phoneCanvas.Fill(0f, 0f, 1f, 1f, new Color(0.13f, 0.14f, 0.18f));
            phoneCanvas.Text = new GUIStyle(textStyle) { alignment = TextAnchor.MiddleCenter, wordWrap = true };
            phoneCanvas.Big = new GUIStyle(titleStyle) { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.RoundToInt(Unit * 1.7f) };

            switch (phoneApp)
            {
                case PhoneApp.Home: DrawPhoneHome(); break;
                case PhoneApp.Calls: DrawCallsApp(); break;
                case PhoneApp.DrumSelect: DrawDrumSelect(); break;
                case PhoneApp.DrumPlay: DrawDrumPlay(); break;
                case PhoneApp.DrumResult: DrawDrumResult(); break;
                case PhoneApp.LatteCam: DrawLatteCam(); break;
            }
        }

        private bool PhoneButton(float x, float y, float w, float h, string label, Color tint)
        {
            Color keep = GUI.backgroundColor;
            GUI.backgroundColor = tint;
            bool clicked = GUI.Button(phoneCanvas.ToScreen(x, y, w, h), label, buttonStyle);
            GUI.backgroundColor = keep;
            return clicked;
        }

        private void PhoneHeader(string title, bool back = true)
        {
            var c = phoneCanvas;
            c.Label(0.04f, 0.01f, 0.4f, 0.05f, $"<size={Mathf.RoundToInt(Unit * 0.9f)}>{System.DateTime.Now:HH:mm}</size>", logStyle);
            c.Label(0.6f, 0.01f, 0.38f, 0.05f, $"<size={Mathf.RoundToInt(Unit * 0.9f)}>5G  87%</size>", new GUIStyle(logStyle) { alignment = TextAnchor.MiddleRight });
            c.Label(0f, 0.06f, 1f, 0.07f, $"<b>{title}</b>", c.Big);
            if (back && PhoneButton(0.03f, 0.07f, 0.2f, 0.055f, "◀", new Color(0.6f, 0.6f, 0.7f)))
            {
                StopDrums();
                phoneApp = PhoneApp.Home;
                GUIUtility.ExitGUI();
            }
        }

        private void DrawPhoneHome()
        {
            PhoneHeader("Home", back: false);
            var apps = new (string Label, string Sub, Color Tint, PhoneApp App)[]
            {
                ("1  Phone", "call someone", new Color(0.4f, 0.9f, 0.45f), PhoneApp.Calls),
                ("2  Drum Rush", "rhythm game", new Color(1f, 0.45f, 0.35f), PhoneApp.DrumSelect),
                ("3  Latte Cam", "live from home", new Color(0.95f, 0.8f, 0.4f), PhoneApp.LatteCam),
            };
            float y = 0.17f;
            foreach (var a in apps)
            {
                if (PhoneButton(0.08f, y, 0.84f, 0.16f, $"<b>{a.Label}</b>\n<size={Mathf.RoundToInt(textStyle.fontSize * 0.8f)}>{a.Sub}</size>", a.Tint))
                {
                    OpenApp(a.App);
                    GUIUtility.ExitGUI();
                }
                y += 0.19f;
            }
            phoneCanvas.Label(0.05f, 0.9f, 0.9f, 0.06f, "<color=#aaa>P: put it away · Backspace: home</color>", logStyle);
        }

        private void DrawCallsApp()
        {
            PhoneHeader("Phone");
            float now = Time.time;
            bool ready = now >= humanCallReadyAt;
            phoneCanvas.Label(0.05f, 0.14f, 0.9f, 0.08f,
                ready ? "Call someone at your table — they'll stare at their phone for ~5 s."
                      : $"<color=#ffcc40>You can call again in {Mathf.CeilToInt(humanCallReadyAt - now)}s</color>", phoneCanvas.Text);
            float y = 0.25f;
            for (int s = 1; s < HandState.SeatCount; s++)
            {
                bool can = ready && !standing && !OnPhone(s);
                bool wasEnabled = GUI.enabled;
                GUI.enabled = can;
                string who = standing ? $"{s}  {ShortName(s)} (sit down first)" : $"{s}  {SeatName(s)}{(OnPhone(s) ? " — busy" : "")}";
                if (PhoneButton(0.08f, y, 0.84f, 0.11f, who, new Color(0.4f, 0.9f, 0.45f)))
                {
                    GUI.enabled = wasEnabled;
                    TryHumanCall(s);
                    GUIUtility.ExitGUI();
                }
                GUI.enabled = wasEnabled;
                y += 0.13f;
            }
            if (PhoneButton(0.08f, y + 0.02f, 0.84f, 0.11f, "Latte (home)", new Color(0.95f, 0.8f, 0.4f)))
            {
                CallLatte();
                GUIUtility.ExitGUI();
            }
        }

        private void CallLatte()
        {
            string reply = dog.Mood == DogMood.Content ? "*happy little woof* … *snoring*"
                : dog.Mood == DogMood.Peckish ? "WOOF? … woof. (food later?)"
                : dog.Mood == DogMood.Hungry ? "WOOF WOOF WOOF (where is DINNER)"
                : "*AWOOOOO* (come HOME. FOOD.)";
            ShowToast($"Latte picks up: {reply}");
            AddLog($"You call Latte. {reply}");
            home.Dog.Hop();
        }

        private void DrawLatteCam()
        {
            PhoneHeader("Latte Cam");
            var c = phoneCanvas;
            c.Fill(0.06f, 0.15f, 0.88f, 0.36f, new Color(0.3f, 0.25f, 0.2f));      // the "video"
            c.Circle(0.5f, 0.35f, 0.08f, new Color(0.06f, 0.06f, 0.07f));           // a very low-res Latte
            c.Fill(0.475f, 0.3f, 0.05f, 0.1f, new Color(0.95f, 0.95f, 0.93f));
            c.Label(0.08f, 0.16f, 0.4f, 0.05f, "<color=#ff4040>●</color> LIVE", logStyle);
            c.Label(0.06f, 0.43f, 0.88f, 0.07f, $"<i>{home.Dog.Doing}</i>", c.Text);
            string color = dog.Mood == DogMood.Content ? "#8f8" : dog.Mood == DogMood.Peckish ? "#ffe880" : dog.Mood == DogMood.Hungry ? "#ffb040" : "#ff5050";
            c.Label(0.05f, 0.55f, 0.9f, 0.07f, $"Latte is <color={color}><b>{Dog.Describe(dog.Mood)}</b></color>", c.Text);
            c.Fill(0.12f, 0.64f, 0.76f, 0.035f, new Color(0.25f, 0.25f, 0.28f));
            c.Fill(0.12f, 0.64f, 0.76f * (float)(dog.Fullness / Dog.Max), 0.035f, new Color(0.45f, 0.85f, 0.45f));
            c.Label(0.05f, 0.7f, 0.9f, 0.14f, dog.Mood >= DogMood.Hungry
                ? "Go home (club front door) and fill their bowl."
                : dog.TreatLeft > 0 ? "Still full of chicken soup." : "All good for now.", c.Text);
            if (PhoneButton(0.15f, 0.85f, 0.7f, 0.08f, "Wave at the camera", new Color(0.95f, 0.8f, 0.4f)))
            {
                home.Dog.Hop();
                ShowToast("Latte hears you and does a little hop.");
                GUIUtility.ExitGUI();
            }
        }

        private void DrawDrumSelect()
        {
            PhoneHeader("Drum Rush");
            var c = phoneCanvas;
            c.Label(0.05f, 0.14f, 0.9f, 0.1f, "<color=#f66>Red</color>: J / left click   <color=#6af>Blue</color>: K / right click", c.Text);
            float y = 0.27f;
            for (int i = 0; i < setlist.Count; i++)
            {
                DrumSong s = setlist[i];
                string stars = new string('*', s.Difficulty + 1);
                string best = bestDrumScores.TryGetValue(s.Name, out int b) ? $"best {b:N0}" : "not played";
                if (PhoneButton(0.06f, y, 0.88f, 0.17f,
                        $"<b>{i + 1}  {s.Name}</b>\n<size={Mathf.RoundToInt(textStyle.fontSize * 0.8f)}>{stars}  {s.Bpm:0} BPM · {best}</size>",
                        s.Difficulty == 0 ? new Color(0.5f, 0.9f, 0.5f) : s.Difficulty == 1 ? new Color(1f, 0.8f, 0.4f) : new Color(1f, 0.45f, 0.4f)))
                {
                    StartSong(s);
                    GUIUtility.ExitGUI();
                }
                y += 0.2f;
            }
            c.Label(0.05f, 0.88f, 0.9f, 0.08f, "<color=#aaa>You can't play your hand while you drum!</color>", logStyle);
        }

        private void DrawDrumPlay()
        {
            var c = phoneCanvas;
            DrumGame g = drum;
            if (g == null)
                return;
            float now = Time.time;
            const float HitX = 0.2f, LaneY = 0.5f, NoteR = 0.075f;
            float speed = 0.36f * (float)g.Song.Bpm / 120f;   // screen widths per second

            c.Label(0.03f, 0.03f, 0.6f, 0.1f, $"<b>{g.Song.Name}</b>", new GUIStyle(c.Big) { alignment = TextAnchor.MiddleLeft });
            c.Label(0.5f, 0.03f, 0.47f, 0.1f, $"<b>{g.Score:N0}</b>", new GUIStyle(c.Big) { alignment = TextAnchor.MiddleRight });

            // The lane, the beat lines, the target.
            c.Fill(0f, LaneY - 0.17f, 1f, 0.34f, new Color(0.22f, 0.2f, 0.24f));
            double beatLen = g.Song.BeatSeconds;
            double firstBeat = DrumSong.LeadIn + System.Math.Ceiling((g.Time - DrumSong.LeadIn - 0.6) / beatLen) * beatLen;
            for (double t = firstBeat; ; t += beatLen)
            {
                float bx = HitX + (float)(t - g.Time) * speed;
                if (bx > 1f) break;
                if (bx > 0.1f && t >= DrumSong.LeadIn && t < g.Song.Length - 1.0)
                    c.Fill(bx - 0.0015f, LaneY - 0.17f, 0.003f, 0.34f, new Color(0.35f, 0.33f, 0.38f));
            }
            c.Circle(HitX, LaneY, NoteR + 0.03f, new Color(0.85f, 0.85f, 0.85f));
            c.Circle(HitX, LaneY, NoteR + 0.012f, new Color(0.22f, 0.2f, 0.24f));
            c.Circle(HitX, LaneY, NoteR - 0.005f, new Color(0.32f, 0.3f, 0.34f));

            // Notes, furthest first so nearer ones draw on top.
            IReadOnlyList<DrumNote> notes = g.Song.Notes;
            for (int i = notes.Count - 1; i >= 0; i--)
            {
                DrumJudgement j = g.Judgement(i);
                if (j == DrumJudgement.Great || j == DrumJudgement.Good)
                    continue;                                   // hit: gone
                float nx = HitX + (float)(notes[i].Time - g.Time) * speed;
                if (nx < -0.05f || nx > 1.05f)
                    continue;
                Color col = j == DrumJudgement.Miss ? new Color(0.4f, 0.4f, 0.42f) : notes[i].Kind == DrumHit.Boom ? BoomRed : TakBlue;
                c.Circle(nx, LaneY, NoteR + 0.012f, Color.white);
                c.Circle(nx, LaneY, NoteR, col);
                c.Fill(nx - 0.012f, LaneY - 0.02f, 0.008f, 0.03f, new Color(0.1f, 0.1f, 0.1f));   // little eyes
                c.Fill(nx + 0.004f, LaneY - 0.02f, 0.008f, 0.03f, new Color(0.1f, 0.1f, 0.1f));
            }

            // The drum on the left: face (red) and rim (blue) flash when you hit them; the combo sits on it.
            float boomGlow = Mathf.Clamp01(1f - (now - boomFlashAt) / 0.15f);
            float takGlow = Mathf.Clamp01(1f - (now - takFlashAt) / 0.15f);
            c.Fill(0f, LaneY - 0.17f, 0.1f, 0.34f, new Color(0.12f, 0.1f, 0.12f));
            c.Circle(0.05f, LaneY, 0.13f, Color.Lerp(new Color(0.15f, 0.3f, 0.55f), TakBlue * 1.3f, takGlow));
            c.Circle(0.05f, LaneY, 0.1f, Color.Lerp(new Color(0.85f, 0.75f, 0.6f), BoomRed, boomGlow));
            if (g.Combo >= 3)
                c.Label(0f, LaneY - 0.06f, 0.1f, 0.12f, $"<b>{g.Combo}</b>", c.Big);

            // Judgement pop-up over the target.
            if (drumJudgeText != null && now - drumJudgeAt < 0.45f)
            {
                var js = new GUIStyle(c.Big);
                js.normal.textColor = drumJudgeColor;
                float rise = (now - drumJudgeAt) * 0.12f;
                c.Label(HitX - 0.12f, LaneY - 0.32f - rise, 0.24f, 0.12f, $"<b>{drumJudgeText}</b>", js);
            }

            // Count-in, progress, controls.
            if (g.Time < DrumSong.LeadIn)
                c.Label(0.3f, LaneY - 0.08f, 0.6f, 0.16f, $"<b>{Mathf.CeilToInt((float)(DrumSong.LeadIn - g.Time))}…</b>", c.Big);
            c.Fill(0.03f, 0.9f, 0.94f, 0.025f, new Color(0.25f, 0.25f, 0.28f));
            c.Fill(0.03f, 0.9f, 0.94f * Mathf.Clamp01((float)(g.Time / g.Song.Length)), 0.025f, new Color(1f, 0.6f, 0.3f));
            c.Label(0.03f, 0.74f, 0.94f, 0.12f,
                $"<color=#f66>RED</color> J / left click     <color=#6af>BLUE</color> K / right click     combo {g.Combo} · max {g.MaxCombo}", c.Text);
            if (humanPhone != null)
            {
                c.Fill(0f, 0f, 1f, 1f, new Color(0f, 0f, 0f, 0.55f));
                c.Label(0f, 0.4f, 1f, 0.2f, "<b>PAUSED</b> — answer your phone", c.Big);
            }
        }

        private void DrawDrumResult()
        {
            PhoneHeader("Drum Rush");
            DrumGame g = lastDrum;
            var c = phoneCanvas;
            if (g == null)
            {
                phoneApp = PhoneApp.DrumSelect;
                return;
            }
            var rank = new GUIStyle(c.Big) { fontSize = Mathf.RoundToInt(Unit * 5f) };
            rank.normal.textColor = g.Rank == "S" ? new Color(1f, 0.85f, 0.2f) : g.Rank == "A" ? new Color(0.5f, 1f, 0.5f) : Color.white;
            c.Label(0f, 0.13f, 1f, 0.06f, g.Song.Name, c.Text);
            c.Label(0f, 0.19f, 1f, 0.16f, $"<b>{g.Rank}</b>", rank);
            string fc = g.FullCombo ? "\n<color=#ffd040><b>FULL COMBO!</b></color>" : "";
            string best = lastWasBest ? "  <color=#8f8>NEW BEST</color>" : "";
            c.Label(0.05f, 0.36f, 0.9f, 0.26f,
                $"<b>{g.Score:N0}</b> pts{best}\nGREAT {g.Greats} · GOOD {g.Goods} · MISS {g.Misses}\nmax combo {g.MaxCombo} · {Mathf.RoundToInt((float)g.Accuracy * 100)}%{fc}", c.Text);
            if (PhoneButton(0.08f, 0.64f, 0.84f, 0.09f, "1  Play again", new Color(1f, 0.45f, 0.35f)))
            {
                StartSong(g.Song);
                GUIUtility.ExitGUI();
            }
            if (PhoneButton(0.08f, 0.75f, 0.84f, 0.09f, "2  Songs", new Color(1f, 0.8f, 0.4f)))
            {
                phoneApp = PhoneApp.DrumSelect;
                GUIUtility.ExitGUI();
            }
            if (PhoneButton(0.08f, 0.86f, 0.84f, 0.09f, "3  Home", new Color(0.6f, 0.6f, 0.7f)))
            {
                phoneApp = PhoneApp.Home;
                GUIUtility.ExitGUI();
            }
        }

        // ------------------------------------------------------------------ sounds (made in code)

        private AudioSource sfx;
        private AudioClip boomClip, takClip, tickClip, accentClip;

        private void SetUpSounds()
        {
            if (Object.FindAnyObjectByType<AudioListener>() == null && cam != null)
                cam.gameObject.AddComponent<AudioListener>();
            sfx = gameObject.AddComponent<AudioSource>();
            sfx.playOnAwake = false;
            const int rate = 44100;
            boomClip = MakeClip("Boom", rate, 0.32f, (t, n) =>
            {
                float f = Mathf.Lerp(120f, 50f, Mathf.Clamp01(t / 0.18f));
                return Mathf.Sin(2f * Mathf.PI * f * t) * Mathf.Exp(-t * 11f) + n * 0.15f * Mathf.Exp(-t * 80f);
            });
            takClip = MakeClip("Tak", rate, 0.09f, (t, n) =>
                (Mathf.Sin(2f * Mathf.PI * 1250f * t) * 0.6f + n * 0.6f) * Mathf.Exp(-t * 45f));
            tickClip = MakeClip("Tick", rate, 0.04f, (t, n) => n * 0.35f * Mathf.Exp(-t * 120f));
            accentClip = MakeClip("Accent", rate, 0.06f, (t, n) =>
                (n * 0.4f + Mathf.Sin(2f * Mathf.PI * 2200f * t) * 0.3f) * Mathf.Exp(-t * 70f));
        }

        private static AudioClip MakeClip(string name, int rate, float seconds, System.Func<float, float, float> wave)
        {
            int count = Mathf.CeilToInt(rate * seconds);
            var data = new float[count];
            var noise = new System.Random(name.Length * 7919);
            for (int i = 0; i < count; i++)
            {
                float n = (float)(noise.NextDouble() * 2.0 - 1.0);
                data[i] = Mathf.Clamp(wave(i / (float)rate, n), -1f, 1f);
            }
            AudioClip clip = AudioClip.Create(name, count, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private void PlaySound(AudioClip clip, float volume)
        {
            if (sfx != null && clip != null)
                sfx.PlayOneShot(clip, volume);
        }
    }
}
