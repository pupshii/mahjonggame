using System.Collections.Generic;

namespace MahjongNight.Core
{
    /// <summary>A pocket swap worth doing: which hand tile goes in, which pocket slot comes out.</summary>
    public readonly struct CheatPlan
    {
        public CheatPlan(Tile fromHand, int pocketIndex, Tile fromPocket, bool makesWin)
        {
            FromHand = fromHand;
            PocketIndex = pocketIndex;
            FromPocket = fromPocket;
            MakesWin = makesWin;
        }

        public Tile FromHand { get; }
        public int PocketIndex { get; }
        public Tile FromPocket { get; }
        /// <summary>True if the swap completes a winning hand right now (then declare self-drawn).</summary>
        public bool MakesWin { get; }
    }

    /// <summary>
    /// Decides whether a pocket swap is worth the risk for a bot. Only big gains qualify:
    /// completing a winning hand on your own turn, or turning a non-ready hand into a ready one.
    /// Uses only the bot's own hand and pocket.
    /// </summary>
    public static class CheatAdvisor
    {
        /// <param name="minWinFaan">Smallest win worth cheating for (defaults to the table minimum).</param>
        public static CheatPlan? FindWorthwhileSwap(HandState hand, int seat, IReadOnlyList<Tile> pocket, int minWinFaan = 0)
        {
            int winTarget = System.Math.Max(minWinFaan, hand.Rules.MinFaan);
            if (!hand.CanPocketSwap(seat) || pocket.Count == 0)
                return null;

            SeatState me = hand.Seats[seat];
            int size = me.Concealed.Count + 3 * me.Melds.Count;
            var distinct = new HashSet<Tile>(me.Concealed);

            // On our own turn holding the extra tile: look for a swap that wins outright.
            if (size == hand.Rules.HandSize + 1 && hand.Phase == HandPhase.AwaitingAction && hand.CurrentSeat == seat)
            {
                WinContext baseCtx = hand.PreviewWin(seat);
                if (baseCtx == null)
                    return null;
                foreach (Tile h in distinct)
                {
                    for (int p = 0; p < pocket.Count; p++)
                    {
                        if (pocket[p] == h)
                            continue;
                        var swapped = new List<Tile>(me.Concealed);
                        swapped.Remove(h);
                        swapped.Add(pocket[p]);
                        baseCtx.Concealed = swapped;
                        baseCtx.WinningTile = pocket[p];
                        FaanResult score = FaanCalculator.Calculate(baseCtx, hand.Rules);
                        if (score != null && (score.IsLimit || score.Total >= winTarget))
                            return new CheatPlan(h, p, pocket[p], makesWin: true);
                    }
                }
                return null;
            }

            // Between turns: turn a non-ready hand into a ready one.
            if (size == hand.Rules.HandSize && FaanCalculator.GetWaits(me.Concealed, me.Melds, hand.Rules).Count == 0)
            {
                CheatPlan? best = null;
                int bestWaits = 0;
                foreach (Tile h in distinct)
                {
                    for (int p = 0; p < pocket.Count; p++)
                    {
                        if (pocket[p] == h)
                            continue;
                        var swapped = new List<Tile>(me.Concealed);
                        swapped.Remove(h);
                        swapped.Add(pocket[p]);
                        int waits = FaanCalculator.GetWaits(swapped, me.Melds, hand.Rules).Count;
                        if (waits > bestWaits)
                        {
                            bestWaits = waits;
                            best = new CheatPlan(h, p, pocket[p], makesWin: false);
                        }
                    }
                }
                return best;
            }
            return null;
        }
    }
}
