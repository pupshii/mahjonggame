using MahjongNight.Core;
using UnityEngine;

namespace MahjongNight.Game
{
    public enum Difficulty
    {
        Easy,    // 1 table
        Medium,  // 3 tables
        Hard,    // 5 tables
        Mahjong, // 1 table, just mahjong: no cheating, phones, standing up, customers or dog
    }

    /// <summary>
    /// The start menu (pick Easy / Medium / Hard — how many tables you run at once) and the
    /// pause menu (Esc: resume, or restart on another difficulty).
    /// </summary>
    public sealed partial class GreyboxGame
    {
        [Header("Difficulty")]
        [Tooltip("Show the Easy / Medium / Hard menu when you press Play. Off: start straight away with Table Count.")]
        [SerializeField] private bool showDifficultyMenu = true;

        private enum MenuMode { None, Start, Pause }

        private MenuMode menu = MenuMode.None;
        private Difficulty? difficulty;          // null: started from the inspector's Table Count
        private Difficulty? pendingDifficulty;   // chosen in the menu; applied on the next Update
        private string startError;               // why the last start failed, shown on the menu

        private const string SixteenTilesPref = "SixteenTiles";
        /// <summary>16-tile mahjong (16 in hand, five sets + a pair to win) rather than 13. Remembered between runs.</summary>
        private bool sixteenTiles = true;

        private void LoadMenuPrefs()
        {
            try { sixteenTiles = PlayerPrefs.GetInt(SixteenTilesPref, 1) == 1; }
            catch (System.Exception) { sixteenTiles = true; }
        }

        private void ToggleSixteenTiles()
        {
            sixteenTiles = !sixteenTiles;
            try { PlayerPrefs.SetInt(SixteenTilesPref, sixteenTiles ? 1 : 0); PlayerPrefs.Save(); }
            catch (System.Exception) { }
        }

        private static readonly Difficulty[] Difficulties = { Difficulty.Easy, Difficulty.Medium, Difficulty.Hard, Difficulty.Mahjong };

        public static int TablesFor(Difficulty d) => d == Difficulty.Easy || d == Difficulty.Mahjong ? 1 : d == Difficulty.Medium ? 3 : 5;

        /// <summary>
        /// Mahjong mode: one table of plain Hong Kong mahjong (classic rules) — no cheating or
        /// call-outs, no phones or texts, no standing up, no hunger/thirst/bladder, no customers,
        /// no trickshots, no upgrade shop and no turn clock.
        /// </summary>
        private bool PureMahjong => difficulty == Difficulty.Mahjong;

        private static string DifficultyName(Difficulty d) => d.ToString();

        private static string DifficultyBlurb(Difficulty d)
        {
            switch (d)
            {
                case Difficulty.Easy: return "1 table, 3 customers. Learn the ropes.";
                case Difficulty.Medium: return "3 tables, 9 customers. Start running.";
                case Difficulty.Hard: return "5 tables, 15 customers. Pure chaos.";
                default: return "Just mahjong: one table, classic rules. No cheating, phones, standing up or customers.";
            }
        }

        /// <summary>Builds the club for a difficulty (or the inspector's Table Count) and starts every table.</summary>
        private void StartStage(Difficulty? d)
        {
            if (d.HasValue)
                tableCount = TablesFor(d.Value);
            difficulty = d;
            rules = PureMahjong || classicRules ? RuleSet.Classic() : RuleSet.Party();
            rules.ChooseDraws = chooseDraws && !PureMahjong;
            rules.HandSets = sixteenTiles ? 5 : 4;   // 16-tile hands (five sets + a pair) or 13 (four sets)
            if (sixteenTiles)
            {
                // HK-style Taiwanese: any complete hand wins, scored off the Taiwanese sheet
                // (add up every hand that applies, no cap; a 1-fan win off a discard is a Chicken Hand, 10).
                rules.Scoring = ScoringStyle.Taiwanese;
                rules.MinFaan = 0;
                rules.WinBase = 0;
            }
            TearDownStage();
            BuildTables();
            headLook = new HeadLook(cam, avatars[HumanSeat].EyePosition, ctx.Origin);
            SetUpLife();
            NewMatchAll();
            menu = MenuMode.None;
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            if (d.HasValue)
                AddLog($"{DifficultyName(d.Value)}: {tableCount} table{(tableCount == 1 ? "" : "s")}. Good luck, manager.");
        }

        /// <summary>Removes everything the previous stage built (tables, avatars, room, home) and its state.</summary>
        private void TearDownStage()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
                Destroy(transform.GetChild(i).gameObject);
            tables.Clear();
            turnQueue.Clear();
            standing = false;
            stageStopped = false;
            using_ = null;
            humanPhone = null;
            phonePanelOpen = false;
            drum = null;
            popupText = null;
            hoveredTile = null;
            hoveredAvatar = -1;
            log.Clear();
        }

        /// <summary>Runs first every frame. Returns true while a menu is up (the game is paused).</summary>
        private bool UpdateMenu()
        {
            if (pendingDifficulty.HasValue)
            {
                Difficulty d = pendingDifficulty.Value;
                pendingDifficulty = null;
                try
                {
                    startError = null;
                    StartStage(d);
                }
                catch (System.Exception e)
                {
                    // Show it on the menu (on a phone there's no Console to look at).
                    Debug.LogException(e);
                    startError = e.GetType().Name + ": " + e.Message;
                    menu = MenuMode.Start;
                    Time.timeScale = 1f;
                }
                return true;
            }
            if (KeyDown(KeyCode.Escape) && tables.Count > 0)
            {
                if (menu == MenuMode.None)
                {
                    menu = MenuMode.Pause;
                    Time.timeScale = 0f;            // Time.time stops: every clock and bar freezes
                }
                else if (menu == MenuMode.Pause)
                {
                    menu = MenuMode.None;
                    Time.timeScale = 1f;
                }
            }
            if (menu == MenuMode.None)
                return false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            return true;
        }

        private void DrawMenu()
        {
            var full = new Rect(0, 0, Screen.width, Screen.height);
            uiRects.Add(full);
            GUI.Box(full, GUIContent.none, boxStyle);
            if (menu == MenuMode.Start)
                GUI.Box(full, GUIContent.none, boxStyle);   // darker on the title screen

            Event e = Event.current;
            if (e.type == EventType.KeyDown)
            {
                int pick = e.keyCode == KeyCode.Alpha1 ? 0 : e.keyCode == KeyCode.Alpha2 ? 1 : e.keyCode == KeyCode.Alpha3 ? 2
                    : e.keyCode == KeyCode.Alpha4 ? 3 : -1;
                if (pick >= 0)
                {
                    pendingDifficulty = Difficulties[pick];
                    e.Use();
                }
            }

            float w = Mathf.Min(Screen.width - Pad * 4, Unit * 34);
            float x = (Screen.width - w) / 2f;
            float y = Screen.height * (TouchUI ? 0.05f : menu == MenuMode.Start ? 0.12f : 0.16f);
            var big = new GUIStyle(titleStyle) { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.RoundToInt(Unit * 3.2f) };
            var mid = new GUIStyle(textStyle) { alignment = TextAnchor.MiddleCenter, wordWrap = true };

            if (menu == MenuMode.Start)
            {
                GUI.Label(new Rect(x, y, w, Unit * 4.5f), "MAHJONG NIGHT  麻雀夜", big);
                y += Unit * 5f;
                GUI.Label(new Rect(x, y, w, LineHeight * 2.2f),
                    "You run the club. Play your hand at every table, cook for your customers, don't forget to eat, drink and pee — and keep Latte fed at home.", mid);
                y += LineHeight * 2.8f;
                GUI.Label(new Rect(x, y, w, LineHeight), "<b>Choose a difficulty</b>", centerStyle);
                y += LineHeight * 1.4f;
            }
            else
            {
                GUI.Label(new Rect(x, y, w, Unit * 4.5f), "PAUSED", big);
                y += Unit * 5f;
                if (GUI.Button(new Rect(x, y, w, Unit * 3f), "<b>Resume</b>   (Esc)", buttonStyle))
                {
                    menu = MenuMode.None;
                    Time.timeScale = 1f;
                    GUIUtility.ExitGUI();
                }
                y += Unit * 3f + LineHeight;
                GUI.Label(new Rect(x, y, w, LineHeight), "Restart on a difficulty (every table and your bars start over):", centerStyle);
                y += LineHeight * 1.4f;
            }

            // Hand size: 16 tiles (five sets + a pair) or 13 (four sets + a pair).
            string handText = sixteenTiles
                ? "<b>16 tiles · HK-style Taiwanese scoring</b> (five sets + a pair)   ·   tap for 13"
                : "<b>13 tiles · Hong Kong faan</b> (four sets + a pair)   ·   tap for 16";
            if (menu == MenuMode.Pause && tables.Count > 0 && rules.HandSize != (sixteenTiles ? 16 : 13))
                handText += "\n<size=" + Mathf.RoundToInt(textStyle.fontSize * 0.85f) + ">(starts with your next restart below)</size>";
            float hh = menu == MenuMode.Pause && tables.Count > 0 && rules.HandSize != (sixteenTiles ? 16 : 13) ? Unit * 3.6f : Unit * 2.4f;
            if (GUI.Button(new Rect(x, y, w, hh), handText, buttonStyle))
            {
                ToggleSixteenTiles();
                GUIUtility.ExitGUI();
            }
            y += hh + LineHeight * 0.6f;

            for (int i = 0; i < Difficulties.Length; i++)
            {
                Difficulty d = Difficulties[i];
                bool current = menu == MenuMode.Pause && difficulty == d;
                string heading = d == Difficulty.Mahjong ? "Mahjong — no distractions" : $"{DifficultyName(d)} — {TablesFor(d)} table{(TablesFor(d) == 1 ? "" : "s")}";
                string text = $"<b>{i + 1}.  {heading}</b>{(current ? "   (current)" : "")}\n" +
                              $"<size={Mathf.RoundToInt(textStyle.fontSize * 0.85f)}>{DifficultyBlurb(d)}</size>";
                if (GUI.Button(new Rect(x, y, w, Unit * 4f), text, buttonStyle))
                {
                    pendingDifficulty = d;
                    GUIUtility.ExitGUI();
                }
                y += Unit * 4f + 8;
            }

            if (startError != null)
            {
                var err = new GUIStyle(textStyle) { wordWrap = true, alignment = TextAnchor.UpperCenter };
                err.normal.textColor = new Color(1f, 0.45f, 0.4f);
                GUI.Label(new Rect(x, y, w, LineHeight * 3f), "Couldn't start: " + startError, err);
                y += LineHeight * 3f;
            }
            if (menu == MenuMode.Start)
            {
                y += LineHeight * 0.5f;
                GUI.Label(new Rect(x, y, w, LineHeight), "<color=#aaa>Press 1, 2, 3 or 4 · Esc pauses during the game</color>", centerStyle);
            }
        }
    }
}
