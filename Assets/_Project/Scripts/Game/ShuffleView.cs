using System.Collections.Generic;
using UnityEngine;

namespace MahjongNight.Game
{
    /// <summary>
    /// Washing the tiles before a hand, the way it's done by hand at a real table: all 144 tiles
    /// tip face-down into the middle, everyone swirls them around (you can join in with the
    /// mouse), they get pushed into four walls of 18 stacks, and the hands are dealt out — then
    /// the table's real tiles take over.
    /// </summary>
    public sealed class ShuffleView
    {
        public enum Stage { Spill, Wash, Build, Deal, Done }

        private const int TileCount = 144;
        private const float Scale = 0.85f;           // loose tiles on the felt
        private const float WallTileScale = 0.6f;    // the walls (as the table draws them)
        private const float SpillSeconds = 0.5f;
        private const float WashSeconds = 6f;        // with nobody helping (the bots alone)
        private const float BuildSeconds = 1.3f;
        private const float DealSeconds = 0.9f;
        private const float WashRadius = 0.42f;
        private const float WallDistance = 0.3f;
        private const int StacksPerSide = 18;
        private const int DealtTiles = 53;           // 13 each and one more for the dealer

        private readonly Vector3 origin;
        private readonly Transform root;
        private readonly Tile[] tiles = new Tile[TileCount];
        private readonly System.Random rng;
        private float stageTime;

        private struct Tile
        {
            public Transform T;
            public float Angle, Radius, Spin, Wobble, Phase, Yaw;
            public Vector2 Offset;           // pushed around by your hand
            public Vector3 From;             // where it started the current stage
            public float FromScale;
        }

        public ShuffleView(Transform parent, TileFactory factory, Vector3 origin, int seed)
        {
            this.origin = origin;
            rng = new System.Random(seed);
            root = new GameObject("Shuffle").transform;
            root.SetParent(parent, false);
            for (int i = 0; i < TileCount; i++)
            {
                TileView view = factory.Create(default, false, root, TileLook.Normal, Scale);
                foreach (Collider c in view.GetComponentsInChildren<Collider>())
                    Object.Destroy(c);       // not clickable
                var t = new Tile
                {
                    T = view.transform,
                    Angle = Rand(0f, Mathf.PI * 2f),
                    Radius = Mathf.Sqrt(Rand(0.02f, 1f)) * WashRadius,
                    Spin = Rand(-1f, 1f) * 90f,
                    Wobble = Rand(0.02f, 0.06f),
                    Phase = Rand(0f, 10f),
                    Yaw = Rand(0f, 360f),
                };
                // Start where the last hand's walls were, then spill into the middle.
                t.From = WallSlot(i, out _);
                t.FromScale = WallTileScale;
                tiles[i] = t;
                Place(i, t.From, t.Yaw, WallTileScale);
            }
            CurrentStage = Stage.Spill;
        }

        public Stage CurrentStage { get; private set; }
        public bool Done => CurrentStage == Stage.Done;
        /// <summary>0..1 through the washing (fills faster when you help).</summary>
        public float WashProgress { get; private set; }
        /// <summary>How hard the tiles are moving right now (0..~2), for the clacking sounds.</summary>
        public float Activity { get; private set; }

        private float Rand(float min, float max) => min + (float)rng.NextDouble() * (max - min);

        /// <summary>Runs the animation. <paramref name="palm"/> is where your hand is on the table (null if you're not helping).</summary>
        public void Tick(float dt, Vector3? palm, Vector3 palmVelocity)
        {
            stageTime += dt;
            switch (CurrentStage)
            {
                case Stage.Spill: TickSpill(); break;
                case Stage.Wash: TickWash(dt, palm, palmVelocity); break;
                case Stage.Build: TickBuild(); break;
                case Stage.Deal: TickDeal(); break;
            }
        }

        private void Next(Stage stage)
        {
            for (int i = 0; i < TileCount; i++)
            {
                tiles[i].From = tiles[i].T.position;
                tiles[i].FromScale = tiles[i].T.localScale.x;
            }
            CurrentStage = stage;
            stageTime = 0f;
        }

        private void TickSpill()
        {
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(stageTime / SpillSeconds));
            for (int i = 0; i < TileCount; i++)
            {
                Tile tile = tiles[i];
                Vector3 to = WashPosition(tile, 0f);
                Vector3 p = Vector3.Lerp(tile.From, to, t);
                p.y += Mathf.Sin(t * Mathf.PI) * 0.03f;
                Place(i, p, tile.Yaw, Mathf.Lerp(tile.FromScale, Scale, t));
            }
            Activity = 1.5f * (1f - t);
            if (stageTime >= SpillSeconds)
                Next(Stage.Wash);
        }

        private Vector3 WashPosition(Tile t, float time)
        {
            float r = Mathf.Clamp(t.Radius + Mathf.Sin(time * 1.7f + t.Phase) * t.Wobble, 0f, WashRadius + 0.05f);
            var p = new Vector2(Mathf.Cos(t.Angle) * r, Mathf.Sin(t.Angle) * r) + t.Offset;
            if (p.magnitude > WashRadius + 0.12f)
                p = p.normalized * (WashRadius + 0.12f);
            return origin + new Vector3(p.x, TileFactory.Depth * Scale / 2f + (t.Phase % 3f) * 0.0015f, p.y);
        }

        private void TickWash(float dt, Vector3? palm, Vector3 palmVelocity)
        {
            // Your effort: how fast your hand moves across the tiles.
            Vector2 pv = new Vector2(palmVelocity.x, palmVelocity.z);
            float speed = palm.HasValue ? Mathf.Min(pv.magnitude, 2.5f) : 0f;
            float effort = Mathf.Clamp(speed / 0.8f, 0f, 1.5f);
            WashProgress = Mathf.Min(1f, WashProgress + dt * (1f / WashSeconds + 0.25f * effort));
            Activity = 0.6f + effort;
            float swirl = 1f + effort * 1.5f;

            for (int i = 0; i < TileCount; i++)
            {
                Tile tile = tiles[i];
                // Everyone's hands keep the pile turning — some tiles one way, some the other.
                tile.Angle += (tile.Spin / 90f) * 0.9f * swirl * dt;
                tile.Yaw += tile.Spin * swirl * dt;
                tile.Offset *= Mathf.Max(0f, 1f - 1.5f * dt);
                if (palm.HasValue)
                {
                    var d = new Vector2(tiles[i].T.position.x - palm.Value.x, tiles[i].T.position.z - palm.Value.z);
                    if (d.magnitude < 0.12f)
                    {
                        tile.Offset += pv * dt * 0.9f;                 // pushed along by your hand
                        tile.Yaw += Rand(-1f, 1f) * 200f * dt;
                    }
                }
                tiles[i] = tile;
                Place(i, WashPosition(tile, stageTime), tile.Yaw, Scale);
            }
            if (WashProgress >= 1f)
                Next(Stage.Build);
        }

        /// <summary>Wall slot <paramref name="i"/>: 4 walls of 18 stacks, two high (as at a real table).</summary>
        private Vector3 WallSlot(int i, out Quaternion frame)
        {
            int stack = i / 2, level = i % 2;
            int side = stack / StacksPerSide, pos = stack % StacksPerSide;
            frame = Quaternion.Euler(0f, -90f * side, 0f);
            float w = TileFactory.Width * WallTileScale, thick = TileFactory.Depth * WallTileScale;
            var local = new Vector3((pos - (StacksPerSide - 1) / 2f) * (w + 0.001f), thick / 2f + level * thick, -WallDistance);
            return origin + frame * local;
        }

        private void TickBuild()
        {
            for (int i = 0; i < TileCount; i++)
            {
                // Each wall builds up stack by stack.
                float delay = (i % (StacksPerSide * 2)) / (float)(StacksPerSide * 2) * 0.5f;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((stageTime - delay) / (BuildSeconds - 0.5f)));
                Vector3 to = WallSlot(i, out Quaternion frame);
                Vector3 p = Vector3.Lerp(tiles[i].From, to, t);
                p.y += Mathf.Sin(t * Mathf.PI) * 0.04f;
                float yaw = Mathf.LerpAngle(tiles[i].Yaw, frame.eulerAngles.y, t);
                Place(i, p, yaw, Mathf.Lerp(tiles[i].FromScale, WallTileScale, t));
            }
            Activity = 1.2f;
            if (stageTime >= BuildSeconds)
            {
                for (int i = 0; i < TileCount; i++)
                    tiles[i].Yaw = tiles[i].T.eulerAngles.y;
                Next(Stage.Deal);
            }
        }

        private void TickDeal()
        {
            // The first 53 tiles fly off the walls to the four players (four at a time, like a real deal) and vanish into the hands.
            for (int i = 0; i < DealtTiles; i++)
            {
                int seat = (i / 4) % 4;
                float delay = i / (float)DealtTiles * (DealSeconds - 0.3f);
                float t = Mathf.Clamp01((stageTime - delay) / 0.3f);
                Quaternion frame = Quaternion.Euler(0f, -90f * seat, 0f);
                Vector3 to = origin + frame * new Vector3(((i % 13) - 6) * 0.045f, 0.03f, -0.74f);
                Vector3 p = Vector3.Lerp(tiles[i].From, to, Mathf.SmoothStep(0f, 1f, t));
                p.y += Mathf.Sin(t * Mathf.PI) * 0.05f;
                Place(i, p, tiles[i].Yaw, Mathf.Lerp(tiles[i].FromScale, 0f, t * t));
            }
            Activity = 1f;
            if (stageTime >= DealSeconds)
                CurrentStage = Stage.Done;
        }

        private void Place(int i, Vector3 position, float yaw, float scale)
        {
            Transform t = tiles[i].T;
            t.position = position;
            t.rotation = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(-90f, 0f, 0f);   // face down
            t.localScale = Vector3.one * scale;
        }

        public void Destroy()
        {
            if (root != null)
                Object.Destroy(root.gameObject);
        }
    }
}
