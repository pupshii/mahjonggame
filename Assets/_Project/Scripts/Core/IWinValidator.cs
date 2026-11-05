using System.Collections.Generic;

namespace MahjongNight.Core
{
    /// <summary>
    /// Decides whether a hand is a legal win. Plugged into <see cref="HandState"/> so the
    /// turn loop doesn't depend on scoring. M2 provides the real implementation
    /// (4 melds + pair, special hands, minimum faan).
    /// </summary>
    public interface IWinValidator
    {
        /// <param name="concealed">Concealed tiles including the winning tile (so 14 − 3 × melds).</param>
        /// <param name="melds">Declared melds.</param>
        bool IsWinningHand(IReadOnlyList<Tile> concealed, IReadOnlyList<Meld> melds);
    }

    /// <summary>Never allows a win. Placeholder until M2.</summary>
    public sealed class NeverWinValidator : IWinValidator
    {
        public static readonly NeverWinValidator Instance = new NeverWinValidator();
        public bool IsWinningHand(IReadOnlyList<Tile> concealed, IReadOnlyList<Meld> melds) => false;
    }
}
