using System;
using MahjongNight.Core;
using UnityEngine;

namespace MahjongNight.Game
{
    /// <summary>
    /// Hosts the cooking mini-games: one at a time, in a panel in the middle of the screen,
    /// played with the mouse while you stand at the kitchen counter. You can't walk while one
    /// is open; a phone call or text knocks you out of it (try again). Each one ends with a big
    /// "PERFECT!" / "GREAT!" / "OK!" / "OOPS…".
    /// </summary>
    public sealed partial class GreyboxGame
    {
        private CookingMiniGame miniGame;
        private Action<double> miniGameDone;
        private readonly MiniCanvas miniCanvas = new MiniCanvas();
        private string gradeText;
        private Color gradeColor;
        private float gradeUntil;

        private bool InMiniGame => miniGame != null;

        private void StartMiniGame(CookingMiniGame game, Action<double> done)
        {
            miniGame = game;
            miniGameDone = done;
        }

        private void CancelMiniGame()
        {
            miniGame = null;
            miniGameDone = null;
        }

        /// <summary>The mini-game panel: centred, a fixed shape (height = 0.55 x width) so circles stay round.</summary>
        private Rect MiniGameRect()
        {
            float w = Mathf.Min(Screen.width * 0.62f, Screen.height * 1.2f);
            float h = w * 0.55f;
            return new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f + LineHeight, w, h);
        }

        private void UpdateMiniGame(float dt)
        {
            if (miniGame == null)
                return;
            bool wrongPlace = miniGame.PlayedSeated ? standing : !standing;
            if (!humanAlive || wrongPlace || OnPhone(HumanSeat))
            {
                bool phone = OnPhone(HumanSeat);
                CancelMiniGame();
                if (humanAlive)
                    ShowToast(phone ? "Your phone! You lost your place — try that again." : "You got up — that's over.");
                return;
            }
            Rect r = MiniGameRect();
            Vector3 m = Input.mousePosition;
            var gui = new Vector2(m.x, Screen.height - m.y);
            var local = new Vector2((gui.x - r.x) / r.width, (gui.y - r.y) / r.height);
            miniGame.Tick(dt, local, Input.GetMouseButton(0), Input.GetMouseButtonDown(0), Input.GetMouseButtonUp(0));
            if (!miniGame.Finished)
                return;

            double score = miniGame.Score;
            string remark = miniGame.Remark;
            Action<double> done = miniGameDone;
            CancelMiniGame();
            DishGrade grade = CookingScore.Grade(score);
            ShowGrade(CookingScore.Shout(grade) + (remark != null ? $"\n<size={Mathf.RoundToInt(Unit * 1.6f)}>{remark}</size>" : ""), GradeColor(grade));
            done?.Invoke(score);
        }

        private static Color GradeColor(DishGrade g) =>
            g == DishGrade.Perfect ? new Color(1f, 0.85f, 0.2f)
            : g == DishGrade.Great ? new Color(0.45f, 1f, 0.45f)
            : g == DishGrade.Ok ? new Color(0.6f, 0.85f, 1f)
            : new Color(1f, 0.45f, 0.4f);

        private void ShowGrade(string text, Color color)
        {
            gradeText = text;
            gradeColor = color;
            gradeUntil = Time.time + 1.4f;
        }

        private void DrawMiniGame()
        {
            if (miniGame != null)
            {
                Rect r = MiniGameRect();
                var header = new Rect(r.x, r.y - LineHeight * 2.6f, r.width, LineHeight * 2.6f);
                Panel(header);
                Panel(new Rect(r.x - 6, r.y - 6, r.width + 12, r.height + 12));
                GUI.Box(r, GUIContent.none, boxStyle);
                var title = new GUIStyle(titleStyle) { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.RoundToInt(Unit * 1.8f) };
                GUI.Label(new Rect(header.x, header.y + 4, header.width, LineHeight * 1.4f), miniGame.Title, title);
                GUI.Label(new Rect(header.x, header.y + LineHeight * 1.4f, header.width, LineHeight), miniGame.Instruction, centerStyle);

                miniCanvas.Area = r;
                miniCanvas.Text = new GUIStyle(textStyle) { alignment = TextAnchor.MiddleCenter };
                miniCanvas.Big = new GUIStyle(titleStyle) { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.RoundToInt(Unit * 1.6f) };
                miniGame.Draw(miniCanvas);

                // Time bar along the bottom.
                miniCanvas.Fill(0f, 0.97f, 1f, 0.03f, new Color(0.2f, 0.2f, 0.2f));
                float left = miniGame.TimeLeft01;
                miniCanvas.Fill(0f, 0.97f, left, 0.03f, left < 0.25f ? new Color(1f, 0.35f, 0.3f) : new Color(1f, 0.85f, 0.3f));
            }

            if (gradeText != null && Time.time < gradeUntil)
            {
                float t = 1f - (gradeUntil - Time.time) / 1.4f;
                float scale = t < 0.12f ? Mathf.Lerp(2.2f, 1f, t / 0.12f) : 1f;   // pops in big
                var style = new GUIStyle(titleStyle)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = Mathf.RoundToInt(Unit * 4.5f * scale),
                    richText = true,
                };
                style.normal.textColor = gradeColor;
                GUI.Label(new Rect(0, Screen.height * 0.3f, Screen.width, Unit * 10f), $"<b>{gradeText}</b>", style);
            }
        }
    }
}
