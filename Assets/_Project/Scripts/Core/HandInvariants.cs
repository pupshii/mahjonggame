using System.Collections.Generic;

namespace MahjongNight.Core
{
    /// <summary>
    /// Consistency checks for a <see cref="HandState"/>: every one of the 144 tiles is in
    /// exactly one place and hand sizes are legal. Used by tests and bot soak runs; cheap
    /// enough to call after every action in development builds.
    /// </summary>
    public static class HandInvariants
    {
        /// <summary>Returns a list of problems; empty means consistent.</summary>
        public static List<string> Check(HandState hand)
        {
            var problems = new List<string>();
            var counts = new int[TileSet.KindCount];
            int total = 0;

            void Count(Tile t, string where)
            {
                if (!t.IsValid)
                {
                    problems.Add($"Invalid tile in {where}.");
                    return;
                }
                counts[t.KindIndex]++;
                total++;
            }

            foreach (Tile t in hand.Wall.RemainingTiles())
                Count(t, "wall");

            foreach (SeatState s in hand.Seats)
            {
                foreach (Tile t in s.Concealed) Count(t, $"seat {s.Seat} hand");
                foreach (Tile t in s.Bonus) Count(t, $"seat {s.Seat} bonus");
                foreach (Tile t in s.Discards) Count(t, $"seat {s.Seat} discards");
                foreach (Meld m in s.Melds)
                    foreach (Tile t in m.Tiles)
                        Count(t, $"seat {s.Seat} meld");

                foreach (Tile t in s.Concealed)
                    if (t.IsBonus)
                        problems.Add($"Seat {s.Seat} has bonus tile {t} in hand.");

                int size = s.Concealed.Count + 3 * s.Melds.Count;
                int expected = ExpectedHandSize(hand, s.Seat);
                if (expected >= 0 && size != expected)
                    problems.Add($"Seat {s.Seat} hand size {size}, expected {expected}.");
            }

            if (total != TileSet.TotalTiles)
                problems.Add($"Found {total} tiles, expected {TileSet.TotalTiles}.");
            for (int k = 0; k < TileSet.KindCount; k++)
            {
                int expected = k < TileSet.PlayableKindCount ? TileSet.CopiesPerPlayableKind : 1;
                if (counts[k] != expected)
                    problems.Add($"Found {counts[k]} × {Tile.FromKindIndex(k)}, expected {expected}.");
            }
            return problems;
        }

        /// <summary>Concealed + 3 × melds: 14 for whoever holds an extra tile, else 13. -1 = skip check.</summary>
        private static int ExpectedHandSize(HandState hand, int seat)
        {
            switch (hand.Phase)
            {
                case HandPhase.AwaitingAction:
                case HandPhase.AwaitingRobKong:
                    return seat == hand.CurrentSeat ? 14 : 13;
                case HandPhase.AwaitingClaims:
                case HandPhase.AwaitingDraw:
                    return 13;
                case HandPhase.Ended:
                    if (hand.Result.Outcome == HandOutcome.ExhaustiveDraw)
                        return -1; // the wall may run out mid-turn
                    return hand.Result.Winners[0] == seat ? 14 : 13;
                default:
                    return -1;
            }
        }
    }
}
