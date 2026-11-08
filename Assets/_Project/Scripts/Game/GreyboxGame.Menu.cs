using UnityEngine;

namespace MahjongNight.Game
{
    public enum Difficulty
    {
        Easy,    // 1 table
        Medium,  // 3 tables
        Hard,    // 5 tables
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

        private static readonly Difficulty[] Difficulties = { Difficulty.Easy, Difficulty.Medium, Difficulty.Hard };

        public static int TablesFor(Difficulty d) => d == Difficulty.Easy ? 1 : d == Difficulty.Medium ? 3 : 5;

        private static string DifficultyName(Difficulty d) => d.ToString();

        private static string DifficultyBlurb(Difficulty d)
        {
            switch (d)
            {
                case Difficulty.Easy: return "1 table, 3 customers. Learn the ropes.";
                case Difficulty.Medium: return "3 tables, 9 customers. Start running.";
                default: return "5 tables, 15 customers. Pure chaos.";
            }
        }

        /// <summary>Builds the club for a difficulty (or the inspector's Table Count) and starts every table.</summary>
        private void StartStage(Difficulty? d)
        {
            if (d.HasValue)
                tableCount = TablesFor(d.Value);
            difficulty = d;
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
                StartStage(d);
                return true;
            }
            if (Input.GetKeyDown(KeyCode.Escape) && tables.Count > 0)
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
                int pick = e.keyCode == KeyCode.Alpha1 ? 0 : e.keyCode == KeyCode.Alpha2 ? 1 : e.keyCode == KeyCode.Alpha3 ? 2 : -1;
                if (pick >= 0)
                {
                    pendingDifficulty = Difficulties[pick];
                    e.Use();
                }
            }

            float w = Mathf.Min(Screen.width - Pad * 4, Unit * 34);
            float x = (Screen.width - w) / 2f;
            float y = Screen.height * (menu == MenuMode.Start ? 0.16f : 0.2f);
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

            for (int i = 0; i < Difficulties.Length; i++)
            {
                Difficulty d = Difficulties[i];
                bool current = menu == MenuMode.Pause && difficulty == d;
                string text = $"<b>{i + 1}.  {DifficultyName(d)} — {TablesFor(d)} table{(TablesFor(d) == 1 ? "" : "s")}</b>{(current ? "   (current)" : "")}\n" +
                              $"<size={Mathf.RoundToInt(textStyle.fontSize * 0.85f)}>{DifficultyBlurb(d)}</size>";
                if (GUI.Button(new Rect(x, y, w, Unit * 4f), text, buttonStyle))
                {
                    pendingDifficulty = d;
                    GUIUtility.ExitGUI();
                }
                y += Unit * 4f + 8;
            }

            if (menu == MenuMode.Start)
            {
                y += LineHeight * 0.5f;
                GUI.Label(new Rect(x, y, w, LineHeight), "<color=#aaa>Press 1, 2 or 3 · Esc pauses during the game</color>", centerStyle);
            }
        }
    }
}
