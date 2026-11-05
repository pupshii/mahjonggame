using System.Collections.Generic;

namespace MahjongNight.Core
{
    /// <summary>
    /// A computer player. Implementations may only read their own seat's concealed tiles plus
    /// public information (discards, melds, bonus tiles, wall count) — never other hands or
    /// the wall order — so bots play fair and can later run on clients.
    /// </summary>
    public interface IBot
    {
        /// <param name="legal">Non-empty result of <see cref="HandState.GetLegalActions"/> for <paramref name="seat"/>.</param>
        PlayerAction Choose(HandState hand, int seat, IReadOnlyList<PlayerAction> legal);
    }
}
