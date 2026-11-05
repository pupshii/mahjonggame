using System.Collections.Generic;
using System.Linq;
using MahjongNight.Core;
using UnityEngine;

namespace MahjongNight.Game
{
    /// <summary>
    /// Draws the table and every tile from a <see cref="HandState"/>. Greybox approach:
    /// <see cref="Rebuild"/> throws away all tiles and recreates them after each action.
    /// </summary>
    /// <remarks>
    /// Each seat has a local frame: local -Z points out toward that player, +X is the
    /// player's right. The human sits at the bottom of the screen; the next seat in turn
    /// order sits to their right.
    /// </remarks>
    public sealed class TableView
    {
        private const float HandZ = -0.74f;
        private const float MeldZ = -0.6f;
        private const float MeldStartX = 0.8f;
        private const float DiscardZ = -0.49f;
        private const int DiscardsPerRow = 6;
        private const float HumanTiltDegrees = 50f;
        private const float WallDistance = 0.18f;
        private const int WallStacksPerSide = 12;
        private const float WallScale = 0.6f;
        private const float Spacing = TileFactory.Width + 0.002f;

        private readonly TileFactory factory;
        private readonly Transform tilesRoot;
        private readonly List<TileView> humanHand = new List<TileView>();

        public TableView(Transform parent, TileFactory factory)
        {
            this.factory = factory;

            GameObject top = GameObject.CreatePrimitive(PrimitiveType.Cube);
            top.name = "Table";
            top.transform.SetParent(parent, false);
            top.transform.localPosition = new Vector3(0f, -0.025f, 0f);
            top.transform.localScale = new Vector3(1.9f, 0.05f, 1.9f);
            top.GetComponent<Renderer>().sharedMaterial = TileFactory.CreateMaterial(new Color(0.16f, 0.36f, 0.24f));

            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.SetParent(parent, false);
            floor.transform.localPosition = new Vector3(0f, -0.75f, 0f);
            floor.GetComponent<Renderer>().sharedMaterial = TileFactory.CreateMaterial(new Color(0.18f, 0.14f, 0.12f));

            tilesRoot = new GameObject("Tiles").transform;
            tilesRoot.SetParent(parent, false);

        }

        /// <summary>The human's clickable hand tiles from the last rebuild.</summary>
        public IReadOnlyList<TileView> HumanHand => humanHand;

        /// <summary>World point at the top of the human's hand, for placing the action bar above it.</summary>
        public static Vector3 HumanHandTop => new Vector3(0f, TileFactory.Height + 0.01f, HandZ + 0.02f);

        /// <summary>World point above a seat's hand, for placing name tags.</summary>
        public static Vector3 SeatAnchor(int seat, int humanSeat)
        {
            int relative = (seat - humanSeat + HandState.SeatCount) % HandState.SeatCount;
            return Quaternion.Euler(0f, -90f * relative, 0f) * new Vector3(0f, 0.13f, HandZ);
        }

        public void Rebuild(HandState hand, int humanSeat, bool revealBots)
        {
            for (int i = tilesRoot.childCount - 1; i >= 0; i--)
            {
                GameObject old = tilesRoot.GetChild(i).gameObject;
                old.SetActive(false); // Destroy is deferred; hide now so raycasts can't hit it
                Object.Destroy(old);
            }
            humanHand.Clear();

            bool ended = hand.Phase == HandPhase.Ended;
            for (int seat = 0; seat < HandState.SeatCount; seat++)
            {
                int relative = (seat - humanSeat + HandState.SeatCount) % HandState.SeatCount;
                Quaternion frame = Quaternion.Euler(0f, -90f * relative, 0f);
                SeatState s = hand.Seats[seat];
                bool isHuman = seat == humanSeat;

                BuildConcealed(hand, s, frame, isHuman, isHuman || revealBots || ended);
                BuildMeldsAndBonus(s, frame);
                BuildDiscards(hand, s, frame);
            }
            BuildWall(hand.Wall.Remaining);
        }

        // ------------------------------------------------------------------ seats

        private void BuildConcealed(HandState hand, SeatState s, Quaternion frame, bool isHuman, bool showFaces)
        {
            List<Tile> tiles = s.Concealed.OrderBy(t => t).ToList();

            // Keep the human's freshly drawn tile apart on the right, like at a real table.
            Tile? drawn = null;
            if (isHuman && hand.Phase == HandPhase.AwaitingAction && hand.CurrentSeat == s.Seat && hand.LastDrawnTile.HasValue)
            {
                drawn = hand.LastDrawnTile.Value;
                tiles.Remove(drawn.Value);
                tiles.Add(drawn.Value);
            }

            float gap = drawn.HasValue ? 0.02f : 0f;
            float width = tiles.Count * Spacing + gap;
            float x = -width / 2f + Spacing / 2f;
            bool layFlat = showFaces && !isHuman; // revealed bot hands lie face up

            for (int i = 0; i < tiles.Count; i++)
            {
                if (drawn.HasValue && i == tiles.Count - 1)
                    x += gap;

                Quaternion local;
                float y;
                if (layFlat)
                {
                    local = Quaternion.Euler(90f, 0f, 0f);
                    y = TileFactory.Depth / 2f;
                }
                else
                {
                    local = Quaternion.Euler(isHuman ? HumanTiltDegrees : 0f, 0f, 0f);
                    y = TileFactory.Height / 2f;
                }

                TileView view = Place(tiles[i], showFaces, frame, new Vector3(x, y, HandZ), local);
                view.Seat = s.Seat;
                view.InHand = true;
                if (isHuman)
                    humanHand.Add(view);
                x += Spacing;
            }
        }

        private void BuildMeldsAndBonus(SeatState s, Quaternion frame)
        {
            float x = MeldStartX;
            Quaternion flat = Quaternion.Euler(90f, 0f, 0f);
            float y = TileFactory.Depth / 2f;

            // Melds stack leftwards from the player's right, but each meld reads left to right.
            foreach (Meld meld in s.Melds)
            {
                int n = meld.Tiles.Count;
                for (int i = 0; i < n; i++)
                    Place(meld.Tiles[i], true, frame, new Vector3(x - (n - i) * Spacing, y, MeldZ), flat);
                x -= n * Spacing + 0.012f;
            }
            foreach (Tile t in s.Bonus)
            {
                x -= Spacing;
                Place(t, true, frame, new Vector3(x, y, MeldZ), flat);
            }
        }

        private void BuildDiscards(HandState hand, SeatState s, Quaternion frame)
        {
            Quaternion flat = Quaternion.Euler(90f, 0f, 0f);
            for (int i = 0; i < s.Discards.Count; i++)
            {
                int row = i / DiscardsPerRow;
                int col = i % DiscardsPerRow;
                bool claimable = hand.Phase == HandPhase.AwaitingClaims && hand.LastDiscardSeat == s.Seat
                    && i == s.Discards.Count - 1;
                var pos = new Vector3((col - (DiscardsPerRow - 1) / 2f) * Spacing,
                    TileFactory.Depth / 2f,
                    DiscardZ + row * (TileFactory.Height + 0.004f));
                Place(s.Discards[i], true, frame, pos, flat, claimable ? TileLook.Highlight : TileLook.Normal);
            }
        }

        // ------------------------------------------------------------------ wall

        private void BuildWall(int remaining)
        {
            int shown = Mathf.Min(remaining, WallStacksPerSide * 4 * 2);
            float w = TileFactory.Width * WallScale;
            float thickness = TileFactory.Depth * WallScale;
            Quaternion faceDown = Quaternion.Euler(-90f, 0f, 0f);

            for (int i = 0; i < shown; i++)
            {
                int stack = i / 2;
                int level = i % 2;
                int side = stack / WallStacksPerSide;
                int pos = stack % WallStacksPerSide;
                Quaternion frame = Quaternion.Euler(0f, -90f * side, 0f);
                var local = new Vector3((pos - (WallStacksPerSide - 1) / 2f) * (w + 0.001f),
                    thickness / 2f + level * thickness, -WallDistance);
                Place(default, false, frame, local, faceDown, TileLook.Normal, WallScale);
            }
        }

        // ------------------------------------------------------------------ helpers

        private TileView Place(Tile tile, bool showFace, Quaternion frame, Vector3 localPos, Quaternion localRot,
            TileLook look = TileLook.Normal, float scale = 1f)
        {
            TileView view = factory.Create(tile, showFace, tilesRoot, look, scale);
            view.transform.position = frame * localPos;
            view.transform.rotation = frame * localRot;
            view.RestPosition = view.transform.position;
            return view;
        }
    }
}
