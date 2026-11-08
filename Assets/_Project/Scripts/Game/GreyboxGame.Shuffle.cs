using System.Linq;
using MahjongNight.Core;
using UnityEngine;

namespace MahjongNight.Game
{
    /// <summary>
    /// Before every hand, each table washes its tiles by hand (see <see cref="ShuffleView"/>):
    /// the bots swirl them around, and at the table you're sitting at you can join in — hold the
    /// left mouse button and sweep your hand through the tiles. The more you help, the sooner
    /// the walls get built and the hand starts. Nobody plays (and no clock runs) until it's done.
    /// </summary>
    public sealed partial class GreyboxGame
    {
        private TileFactory tileFactory;
        private Vector3? shufflePalm;          // your hand on the felt, while you help shuffle
        private Vector3? lastShufflePalm;
        private AudioClip[] clackClips;

        private bool Shuffling => ctx.Shuffle != null;

        private void BeginShuffle()
        {
            EndShuffle();
            ctx.Shuffle = new ShuffleView(transform, tileFactory, ctx.Origin, (int)(seedSource.NextUInt64() & 0x7fffffff));
            table.SetTilesVisible(false);
        }

        private void EndShuffle()
        {
            if (ctx.Shuffle == null)
                return;
            ctx.Shuffle.Destroy();
            ctx.Shuffle = null;
            table.SetTilesVisible(true);
        }

        private void UpdateShuffle(float now, float dt)
        {
            ShuffleView shuffle = ctx.Shuffle;
            bool helping = AtThisTable && humanAlive && !OnPhone(HumanSeat) && !InMiniGame && Input.GetMouseButton(0);
            Vector3? palm = null;
            if (helping)
            {
                Vector3 m = Input.mousePosition;
                bool overUi = uiRects.Any(r => r.Contains(new Vector2(m.x, Screen.height - m.y)));
                Ray ray = cam.ScreenPointToRay(m);
                if (!overUi && ray.direction.y < -0.01f)
                {
                    float t = (ctx.Origin.y + 0.03f - ray.origin.y) / ray.direction.y;
                    Vector3 hit = ray.origin + ray.direction * t;
                    if (Mathf.Abs(hit.x - ctx.Origin.x) < 0.8f && Mathf.Abs(hit.z - ctx.Origin.z) < 0.8f)
                        palm = hit;
                }
            }
            Vector3 velocity = palm.HasValue && lastShufflePalm.HasValue && dt > 0f ? (palm.Value - lastShufflePalm.Value) / dt : Vector3.zero;
            if (AtThisTable)
            {
                lastShufflePalm = palm;
                shufflePalm = palm;
            }

            shuffle.Tick(dt, palm, velocity);

            // Clack clack clack — only at your table.
            if (AtThisTable && clackClips != null && Random.value < dt * (3f + 9f * shuffle.Activity))
                PlaySound(clackClips[Random.Range(0, clackClips.Length)], Random.Range(0.12f, 0.35f));

            if (shuffle.Done)
            {
                EndShuffle();
                ctx.ClockUsed = 0f;                 // your turn clock starts now, not when it was dealt
                ctx.WaitingSince = now;
                nextBotTime = now + botTurnDelay * ctx.Pace;
                if (AtThisTable)
                    AddLog("The walls are built and the tiles are dealt — play!");
            }
        }

        /// <summary>The bottom panel while your table shuffles: how to help, and how far along it is.</summary>
        private void DrawShuffleHint()
        {
            ShuffleView shuffle = ctx.Shuffle;
            string stage = shuffle.CurrentStage == ShuffleView.Stage.Build ? "Building the walls…"
                : shuffle.CurrentStage == ShuffleView.Stage.Deal ? "Dealing…"
                : "Shuffling the tiles — <b>hold the left mouse button and swirl them around!</b>";
            float w = Mathf.Max(TextWidth(textStyle, stage), Unit * 24f) + Pad * 4;
            float h = LineHeight * 2.4f + Pad * 2;
            Rect r = Panel(new Rect((Screen.width - w) / 2f, Screen.height - h - Pad * 2, w, h));
            GUI.Label(new Rect(r.x, r.y + Pad, r.width, LineHeight), stage, centerStyle);

            var bar = new Rect(r.x + Pad * 2, r.y + Pad + LineHeight * 1.3f, r.width - Pad * 4, LineHeight * 0.7f);
            Color keep = GUI.color;
            GUI.color = new Color(0.2f, 0.2f, 0.22f);
            GUI.DrawTexture(bar, Texture2D.whiteTexture);
            GUI.color = new Color(0.45f, 0.9f, 0.55f);
            float p = shuffle.CurrentStage == ShuffleView.Stage.Spill ? 0f : shuffle.CurrentStage == ShuffleView.Stage.Wash ? shuffle.WashProgress : 1f;
            GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * p, bar.height), Texture2D.whiteTexture);
            GUI.color = keep;
        }

        private void SetUpShuffleSounds()
        {
            clackClips = new[]
            {
                MakeClip("Clack1", 44100, 0.05f, (t, n) => (n * 0.5f + Mathf.Sin(2f * Mathf.PI * 2900f * t) * 0.5f) * Mathf.Exp(-t * 110f)),
                MakeClip("Clack2", 44100, 0.05f, (t, n) => (n * 0.5f + Mathf.Sin(2f * Mathf.PI * 3600f * t) * 0.5f) * Mathf.Exp(-t * 130f)),
                MakeClip("Clack3", 44100, 0.06f, (t, n) => (n * 0.6f + Mathf.Sin(2f * Mathf.PI * 2400f * t) * 0.4f) * Mathf.Exp(-t * 90f)),
            };
        }
    }
}
