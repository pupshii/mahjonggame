using System.Collections.Generic;
using MahjongNight.Core;
using UnityEngine;

namespace MahjongNight.Game
{
    /// <summary>
    /// Draws into a mini-game's panel in normalised coordinates (0..1 across, 0..1 down).
    /// Plain IMGUI: coloured rectangles and labels.
    /// </summary>
    public sealed class MiniCanvas
    {
        public Rect Area;
        public GUIStyle Text, Big;

        public Rect ToScreen(float x, float y, float w, float h) =>
            new Rect(Area.x + x * Area.width, Area.y + y * Area.height, w * Area.width, h * Area.height);

        public void Fill(float x, float y, float w, float h, Color c)
        {
            Color keep = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(ToScreen(x, y, w, h), Texture2D.whiteTexture);
            GUI.color = keep;
        }

        /// <summary>A filled circle (radius in panel heights, so it stays round), drawn as horizontal strips.</summary>
        public void Circle(float cx, float cy, float r, Color c)
        {
            const int strips = 28;
            float aspect = Area.height / Mathf.Max(1f, Area.width);
            for (int i = 0; i < strips; i++)
            {
                float y0 = -r + 2f * r * i / strips, y1 = -r + 2f * r * (i + 1) / strips;
                float ym = (y0 + y1) / 2f;
                float half = Mathf.Sqrt(Mathf.Max(0f, r * r - ym * ym)) * aspect;
                Fill(cx - half, cy + y0, half * 2f, y1 - y0 + 0.002f, c);
            }
        }

        public void Label(float x, float y, float w, float h, string text, GUIStyle style = null) =>
            GUI.Label(ToScreen(x, y, w, h), text, style ?? Text);

        /// <summary>A dashed vertical line.</summary>
        public void DashedV(float x, float y0, float y1, Color c, float thickness = 0.004f)
        {
            for (float y = y0; y < y1; y += 0.04f)
                Fill(x - thickness / 2f, y, thickness, Mathf.Min(0.022f, y1 - y), c);
        }

        /// <summary>A dashed horizontal line.</summary>
        public void DashedH(float x0, float x1, float y, Color c, float thickness = 0.008f)
        {
            for (float x = x0; x < x1; x += 0.03f)
                Fill(x, y - thickness / 2f, Mathf.Min(0.016f, x1 - x), thickness, c);
        }
    }

    /// <summary>
    /// A short cooking mini-game (a few seconds, played with the mouse). The game feeds it the
    /// mouse each frame in panel coordinates and reads <see cref="Score"/> (0–100) when it's done.
    /// </summary>
    public abstract class CookingMiniGame
    {
        protected float Elapsed;

        public abstract string Title { get; }
        public abstract string Instruction { get; }
        /// <summary>Gives up (and scores what you've got) after this long.</summary>
        public virtual float TimeLimit => 10f;
        /// <summary>Played sitting at the table (true) rather than standing in the kitchen.</summary>
        public virtual bool PlayedSeated => false;
        public bool Finished { get; protected set; }
        public double Score { get; protected set; }
        /// <summary>Optional extra word on the result ("SPILL!").</summary>
        public string Remark { get; protected set; }

        public void Tick(float dt, Vector2 mouse, bool held, bool pressed, bool released)
        {
            if (Finished)
                return;
            Elapsed += dt;
            Step(dt, mouse, held, pressed, released);
            if (!Finished && Elapsed >= TimeLimit)
            {
                OnTimeout();
                Finished = true;
            }
        }

        public float TimeLeft01 => Mathf.Clamp01(1f - Elapsed / TimeLimit);

        protected abstract void Step(float dt, Vector2 mouse, bool held, bool pressed, bool released);
        protected abstract void OnTimeout();
        public abstract void Draw(MiniCanvas c);

        protected void Finish(double score, string remark = null)
        {
            Score = score;
            Remark = remark;
            Finished = true;
        }

        protected static readonly Color Ink = new Color(0.15f, 0.12f, 0.1f);
        protected static readonly Color Good = new Color(0.35f, 0.85f, 0.35f);
    }

    /// <summary>Light the stove: a needle sweeps back and forth — click when it's in the flame.</summary>
    public sealed class IgniteGame : CookingMiniGame
    {
        private readonly float zone;
        private float needle;
        private const float ZoneHalf = 0.06f;

        public IgniteGame(float zoneCentre) => zone = zoneCentre;

        public override string Title => "LIGHT THE STOVE";
        public override string Instruction => "Click when the needle is in the flame!";
        public override float TimeLimit => 6f;

        protected override void Step(float dt, Vector2 mouse, bool held, bool pressed, bool released)
        {
            needle = Mathf.PingPong(Elapsed * 1.25f, 1f);
            if (pressed)
                Finish(CookingScore.Timing(needle, zone, 0.025, 0.14), needle > zone - ZoneHalf && needle < zone + ZoneHalf ? "FWOOMP!" : "click… click…");
        }

        protected override void OnTimeout() => Finish(10, "You fumble it on");

        public override void Draw(MiniCanvas c)
        {
            float x0 = 0.1f, w = 0.8f, y = 0.5f;
            c.Fill(x0, y, w, 0.12f, new Color(0.25f, 0.25f, 0.28f));
            c.Fill(x0 + (zone - ZoneHalf) * w, y, ZoneHalf * 2f * w, 0.12f, new Color(1f, 0.45f, 0.1f));
            c.Fill(x0 + (zone - 0.02f) * w, y, 0.04f * w, 0.12f, new Color(1f, 0.85f, 0.2f));
            c.Label(x0 + (zone - 0.1f) * w, y - 0.1f, 0.2f * w, 0.09f, "<b>FLAME</b>", c.Big);
            c.Fill(x0 + needle * w - 0.006f, y - 0.04f, 0.012f, 0.2f, Color.white);
            c.Fill(x0 + needle * w - 0.006f, y + 0.16f, 0.012f, 0.04f, Ink);
        }
    }

    /// <summary>Pour water (or soup into a bowl): hold the mouse button to pour, let go at the line. Don't spill!</summary>
    public sealed class PourGame : CookingMiniGame
    {
        private readonly string title, what;
        private readonly float line;
        private readonly Color liquid, vessel;
        private readonly float rate;
        private float level;
        private bool started;

        public PourGame(string title, string what, float line, Color liquid, Color vessel, float rate)
        {
            this.title = title;
            this.what = what;
            this.line = line;
            this.liquid = liquid;
            this.vessel = vessel;
            this.rate = rate;
        }

        public override string Title => title;
        public override string Instruction => $"Hold the mouse button to pour {what} — let go at the line!";
        public override float TimeLimit => 9f;

        protected override void Step(float dt, Vector2 mouse, bool held, bool pressed, bool released)
        {
            if (held)
            {
                started = true;
                level += rate * dt * (0.6f + level);       // speeds up as it fills: careful near the top
                if (level >= 1f)
                {
                    level = 1f;
                    Finish(5, "SPILL!");
                }
            }
            else if (started && released)
            {
                Finish(CookingScore.Timing(level, line, 0.02, 0.22));
            }
        }

        protected override void OnTimeout() => Finish(started ? CookingScore.Timing(level, line, 0.02, 0.22) : 0, started ? null : "Nothing poured");

        public override void Draw(MiniCanvas c)
        {
            float x = 0.38f, w = 0.24f, top = 0.25f, h = 0.62f;
            c.Fill(x - 0.015f, top, w + 0.03f, h + 0.02f, vessel);
            c.Fill(x, top, w, h, new Color(0.1f, 0.1f, 0.12f));
            c.Fill(x, top + h * (1f - level), w, h * level, liquid);
            float ly = top + h * (1f - line);
            c.DashedH(x - 0.06f, x + w + 0.06f, ly, Color.white);
            c.Label(x + w + 0.07f, ly - 0.04f, 0.2f, 0.08f, "<b>LINE</b>", c.Text);
            if (level > 0f && level < 1f && !Finished)
                c.Label(0.1f, 0.45f, 0.22f, 0.1f, $"{Mathf.RoundToInt(level * 100)}%", c.Big);
        }
    }

    /// <summary>Slice an ingredient: swipe down through each dashed guide.</summary>
    public sealed class SliceGame : CookingMiniGame
    {
        private const float BandTop = 0.42f, BandBottom = 0.66f, Left = 0.14f, Right = 0.86f;
        private readonly Ingredient ingredient;
        private readonly Color colour;
        private readonly List<double> guides = new List<double>();
        private readonly List<double> cuts = new List<double>();
        private bool armed;          // the mouse went down above the ingredient
        private float entryX;

        public SliceGame(Ingredient ingredient, Color colour, int pieces, System.Func<float> random)
        {
            this.ingredient = ingredient;
            this.colour = colour;
            for (int i = 1; i <= pieces; i++)
            {
                float t = i / (pieces + 1f) + (random() - 0.5f) * 0.06f;
                guides.Add(Left + t * (Right - Left));
            }
        }

        public override string Title => $"SLICE THE {ingredient.ToString().ToUpperInvariant()}";
        public override string Instruction => "Hold the button above it and swipe DOWN through each dashed line!";
        public override float TimeLimit => 8f;

        protected override void Step(float dt, Vector2 mouse, bool held, bool pressed, bool released)
        {
            if (!held)
            {
                armed = false;
                return;
            }
            if (mouse.y < BandTop)
            {
                armed = true;
                entryX = mouse.x;
            }
            else if (armed && mouse.y > BandBottom)
            {
                armed = false;
                float x = (entryX + mouse.x) / 2f;
                if (x > Left && x < Right)
                {
                    cuts.Add(x);
                    if (cuts.Count >= guides.Count)
                        Finish(CookingScore.Slice(cuts, guides));
                }
            }
        }

        protected override void OnTimeout() => Finish(CookingScore.Slice(cuts, guides), cuts.Count < guides.Count ? "Too slow" : null);

        public override void Draw(MiniCanvas c)
        {
            c.Fill(Left - 0.04f, BandTop - 0.04f, Right - Left + 0.08f, BandBottom - BandTop + 0.08f, new Color(0.85f, 0.7f, 0.45f)); // board
            c.Fill(Left, BandTop, Right - Left, BandBottom - BandTop, colour);
            foreach (double g in guides)
                c.DashedV((float)g, BandTop - 0.08f, BandBottom + 0.08f, Ink);
            foreach (double x in cuts)
                c.Fill((float)x - 0.004f, BandTop - 0.02f, 0.008f, BandBottom - BandTop + 0.04f, Color.white);
            c.Label(0.1f, 0.78f, 0.8f, 0.08f, $"Cuts: {cuts.Count} / {guides.Count}", c.Text);
        }
    }

    /// <summary>Into the pot: drag every sliced ingredient into the boiling pot, fast.</summary>
    public sealed class AddToPotGame : CookingMiniGame
    {
        private const float PotX = 0.7f, PotY = 0.55f, PotR = 0.24f, ItemR = 0.07f;
        private readonly List<(Ingredient Item, Color Colour, Vector2 Home, bool InPot)> items = new List<(Ingredient, Color, Vector2, bool)>();
        private int dragging = -1;
        private Vector2 dragAt;

        public AddToPotGame(IReadOnlyList<(Ingredient, Color)> ingredients)
        {
            for (int i = 0; i < ingredients.Count; i++)
                items.Add((ingredients[i].Item1, ingredients[i].Item2, new Vector2(0.14f + (i % 2) * 0.17f, 0.36f + (i / 2) * 0.3f), false));
        }

        public override string Title => "INTO THE POT!";
        public override string Instruction => "Drag every ingredient into the pot — quick!";
        public override float TimeLimit => 14f;

        private bool InPot(Vector2 p, float aspect)
        {
            float dx = (p.x - PotX) / aspect, dy = p.y - PotY;
            return dx * dx + dy * dy < PotR * PotR;
        }

        // The game only knows the panel's aspect while drawing; 0.55 is the panel's height/width.
        private const float Aspect = 0.55f;

        protected override void Step(float dt, Vector2 mouse, bool held, bool pressed, bool released)
        {
            if (pressed)
            {
                for (int i = 0; i < items.Count; i++)
                {
                    if (items[i].InPot) continue;
                    Vector2 d = mouse - items[i].Home;
                    if (Mathf.Abs(d.x) < ItemR * Aspect * 1.4f && Mathf.Abs(d.y) < ItemR * 1.4f)
                    {
                        dragging = i;
                        break;
                    }
                }
            }
            if (dragging >= 0)
            {
                dragAt = mouse;
                if (!held)
                {
                    if (InPot(mouse, Aspect))
                    {
                        var it = items[dragging];
                        items[dragging] = (it.Item, it.Colour, it.Home, true);
                    }
                    dragging = -1;
                    if (items.TrueForAll(i => i.InPot))
                        Finish(CookingScore.Speed(Elapsed, 5, 12));
                }
            }
        }

        protected override void OnTimeout() => Finish(25, "Dumped the rest in");

        public override void Draw(MiniCanvas c)
        {
            c.Circle(PotX, PotY, PotR + 0.02f, new Color(0.55f, 0.57f, 0.6f));
            c.Circle(PotX, PotY, PotR, new Color(0.8f, 0.9f, 1f));
            float t = Elapsed * 6f;
            for (int i = 0; i < 6; i++)   // bubbles
                c.Circle(PotX + Mathf.Sin(t + i * 1.7f) * 0.08f, PotY + Mathf.Cos(t * 0.7f + i) * 0.12f, 0.02f, Color.white);
            int added = 0;
            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i];
                if (it.InPot)
                {
                    c.Circle(PotX - 0.06f + added * 0.04f, PotY + 0.05f * (added % 2 == 0 ? 1 : -1), 0.035f, it.Colour);
                    added++;
                    continue;
                }
                Vector2 p = i == dragging ? dragAt : it.Home;
                c.Circle(p.x, p.y, ItemR, it.Colour);
                c.Label(p.x - 0.08f, p.y + ItemR + 0.01f, 0.16f, 0.07f, it.Item.ToString().ToLowerInvariant(), c.Text);
            }
        }
    }

    /// <summary>Stir: circle the mouse around the pot as fast as you can.</summary>
    public sealed class StirGame : CookingMiniGame
    {
        private const float PotX = 0.5f, PotY = 0.55f, PotR = 0.3f, TargetLaps = 6f;
        private float angle, laps, turned;
        private bool hasAngle;
        private Vector2 spoon = new Vector2(PotX, PotY);

        public override string Title => "STIR!";
        public override string Instruction => "Circle the mouse around the pot — 6 laps!";
        public override float TimeLimit => 6f;

        protected override void Step(float dt, Vector2 mouse, bool held, bool pressed, bool released)
        {
            spoon = mouse;
            float a = Mathf.Atan2(mouse.y - PotY, (mouse.x - PotX) / 0.55f);
            if (hasAngle)
            {
                // Signed, so wiggling back and forth doesn't count — only going round does.
                turned += Mathf.DeltaAngle(angle * Mathf.Rad2Deg, a * Mathf.Rad2Deg);
                laps = Mathf.Abs(turned) / 360f;
            }
            angle = a;
            hasAngle = true;
            if (laps >= TargetLaps)
                Finish(100);
        }

        protected override void OnTimeout() => Finish(CookingScore.Stir(laps, TargetLaps));

        public override void Draw(MiniCanvas c)
        {
            c.Circle(PotX, PotY, PotR + 0.02f, new Color(0.55f, 0.57f, 0.6f));
            c.Circle(PotX, PotY, PotR, new Color(0.95f, 0.78f, 0.35f));
            c.Circle(spoon.x, spoon.y, 0.035f, new Color(0.6f, 0.4f, 0.2f));
            c.Label(0.05f, 0.2f, 0.3f, 0.1f, $"<b>{Mathf.FloorToInt(laps)}</b> / {TargetLaps:0} laps", c.Big);
        }
    }

    /// <summary>
    /// Trickshot: stack your ready hand into a leaning diagonal tower. A tile slides back and
    /// forth above the stack — click to drop it onto the next step of the diagonal. Too far off
    /// and the whole tower comes down. Every drop lands near-perfect: ×3. Otherwise: ×2.
    /// </summary>
    public sealed class TrickshotGame : CookingMiniGame
    {
        private const int Drops = 6;
        private const float TileW = 0.13f, TileH = 0.075f, Lean = 0.06f, BaseX = 0.28f, FloorY = 0.88f;
        private readonly List<float> placed = new List<float>();
        private float slider = 0.1f, crashAt = -1f;
        private int perfects;

        public override string Title => "TRICKSHOT!";
        public override string Instruction => "Click to drop each tile onto the next step of the diagonal — don't let it topple!";
        public override float TimeLimit => 12f;
        public override bool PlayedSeated => true;

        private float Target(int level) => BaseX + level * Lean;

        protected override void Step(float dt, Vector2 mouse, bool held, bool pressed, bool released)
        {
            if (crashAt >= 0f)
            {
                if (Elapsed - crashAt > 0.7f)
                    Finish(0, "CRASH!");
                return;
            }
            float speed = 0.5f + 0.13f * placed.Count;                 // faster as it gets taller
            slider = 0.1f + Mathf.PingPong(Elapsed * speed, 0.8f);
            if (!pressed)
                return;
            float error = Mathf.Abs(slider - Target(placed.Count));
            placed.Add(slider);
            if (error > TileW * 0.5f)
            {
                crashAt = Elapsed;                                      // watch it fall, then fail
                return;
            }
            if (error < TileW * 0.13f)
                perfects++;
            if (placed.Count >= Drops)
                Finish(perfects == Drops ? 100 : 75, perfects == Drops ? "×3 — flawless!" : "×2 — it stands!");
        }

        protected override void OnTimeout() => Finish(0, "Too slow — it slid apart");

        public override void Draw(MiniCanvas c)
        {
            c.Fill(0.05f, FloorY, 0.9f, 0.02f, new Color(0.16f, 0.36f, 0.24f));      // the felt
            c.Label(0.62f, 0.14f, 0.34f, 0.08f, $"{placed.Count} / {Drops}", c.Big);
            c.Label(0.62f, 0.22f, 0.34f, 0.08f, perfects > 0 ? $"perfect: {perfects}" : "", c.Text);

            // The ideal diagonal, as faint outlines.
            for (int i = placed.Count; i < Drops; i++)
                c.Fill(Target(i) - TileW / 2f, FloorY - (i + 1) * TileH, TileW, TileH - 0.006f, new Color(1f, 1f, 1f, 0.07f));

            bool crashing = crashAt >= 0f;
            float fall = crashing ? (Elapsed - crashAt) : 0f;
            for (int i = 0; i < placed.Count; i++)
            {
                float x = placed[i] - TileW / 2f, y = FloorY - (i + 1) * TileH;
                if (crashing)
                {
                    // Everything slides off and tumbles to the right.
                    x += fall * fall * (0.4f + i * 0.12f);
                    y = Mathf.Min(FloorY - TileH, y + fall * fall * 1.6f * (i + 1) * 0.3f);
                }
                c.Fill(x, y, TileW, TileH - 0.006f, new Color(0.96f, 0.94f, 0.86f));
                c.Fill(x, y + TileH * 0.65f, TileW, TileH * 0.3f, new Color(0.1f, 0.45f, 0.3f));  // the coloured back
            }

            if (!crashing && !Finished)
            {
                float y = FloorY - (placed.Count + 1) * TileH - 0.1f;
                c.Fill(slider - TileW / 2f, y, TileW, TileH - 0.006f, new Color(1f, 0.97f, 0.88f));
                c.Fill(slider - TileW / 2f, y + TileH * 0.65f, TileW, TileH * 0.3f, new Color(0.1f, 0.45f, 0.3f));
                c.DashedV(Target(placed.Count), y + TileH, FloorY - placed.Count * TileH, new Color(1f, 0.85f, 0.3f, 0.6f));
            }
        }
    }
}
