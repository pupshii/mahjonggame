using System.Collections.Generic;

namespace MahjongNight.Core
{
    public enum HandOutcome : byte
    {
        Win,
        /// <summary>流局 — the wall ran out with no winner.</summary>
        ExhaustiveDraw
    }

    /// <summary>How a hand ended. Flags feed faan scoring in M2.</summary>
    public sealed class HandResult
    {
        internal HandResult(HandOutcome outcome, IReadOnlyList<int> winners, IReadOnlyList<WinContext> winContexts,
            int discarder, Tile? winningTile, bool selfDrawn, bool robbedKong, bool kongReplacementWin, bool lastTileWin)
        {
            Outcome = outcome;
            Winners = winners;
            WinContexts = winContexts;
            Discarder = discarder;
            WinningTile = winningTile;
            SelfDrawn = selfDrawn;
            RobbedKong = robbedKong;
            KongReplacementWin = kongReplacementWin;
            LastTileWin = lastTileWin;
        }

        public HandOutcome Outcome { get; }
        /// <summary>
        /// Winning seats, nearest to the discarder first; more than one only with
        /// <see cref="RuleSet.AllowMultipleWinners"/>. The winning tile goes into Winners[0]'s
        /// hand; any other winner's winning hand is their concealed tiles + <see cref="WinningTile"/>.
        /// </summary>
        public IReadOnlyList<int> Winners { get; }
        /// <summary>
        /// The winning hand of each winner, aligned with <see cref="Winners"/>. Score with
        /// <see cref="FaanCalculator.Calculate"/>.
        /// </summary>
        public IReadOnlyList<WinContext> WinContexts { get; }
        /// <summary>Seat that dealt in (discard or robbed kong), or -1 for self-drawn / draw game.</summary>
        public int Discarder { get; }
        public Tile? WinningTile { get; }
        /// <summary>自摸</summary>
        public bool SelfDrawn { get; }
        /// <summary>搶槓</summary>
        public bool RobbedKong { get; }
        /// <summary>槓上開花 — self-drawn win on a kong replacement tile.</summary>
        public bool KongReplacementWin { get; }
        /// <summary>海底撈月 — won when no drawable tiles were left.</summary>
        public bool LastTileWin { get; }

        internal static HandResult Draw() =>
            new HandResult(HandOutcome.ExhaustiveDraw, new int[0], new WinContext[0], -1, null, false, false, false, false);

        public override string ToString() =>
            Outcome == HandOutcome.ExhaustiveDraw
                ? "Draw game"
                : $"Win by {string.Join(",", Winners)} on {WinningTile}" + (SelfDrawn ? " (self-drawn)" : $" from seat {Discarder}");
    }
}
