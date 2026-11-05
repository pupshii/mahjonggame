namespace MahjongNight.Core
{
    public enum HandPhase : byte
    {
        /// <summary><see cref="HandState.CurrentSeat"/> must discard, kong or declare a self-drawn win.</summary>
        AwaitingAction,
        /// <summary>
        /// <see cref="HandState.CurrentSeat"/> must choose which tile kind to draw
        /// (<see cref="RuleSet.ChooseDraws"/> only).
        /// </summary>
        AwaitingDraw,
        /// <summary>Everyone except the discarder responds to <see cref="HandState.LastDiscard"/>.</summary>
        AwaitingClaims,
        /// <summary>Everyone except the current seat may rob the kong being promoted.</summary>
        AwaitingRobKong,
        Ended
    }
}
