namespace MahjongNight.Core
{
    /// <summary>
    /// Decides whether a hand is a legal win. Plugged into <see cref="HandState"/> so the
    /// turn loop doesn't depend on scoring. The real implementation is
    /// <see cref="FaanWinValidator"/>: a complete hand shape worth at least the minimum faan.
    /// </summary>
    public interface IWinValidator
    {
        bool IsWinningHand(WinContext context);
    }

    /// <summary>Never allows a win. For tests and tutorials.</summary>
    public sealed class NeverWinValidator : IWinValidator
    {
        public static readonly NeverWinValidator Instance = new NeverWinValidator();
        public bool IsWinningHand(WinContext context) => false;
    }
}
