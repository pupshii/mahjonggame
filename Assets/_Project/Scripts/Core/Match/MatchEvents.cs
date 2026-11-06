namespace MahjongNight.Core
{
    /// <summary>Something that happened at the match level (scores, upgrades, cheats).</summary>
    public abstract class MatchEvent
    {
        protected MatchEvent(int seat)
        {
            Seat = seat;
        }

        /// <summary>Seat concerned, or -1 for table-wide events.</summary>
        public int Seat { get; }
    }

    public sealed class MatchHandStartedEvent : MatchEvent
    {
        public MatchHandStartedEvent(int handNumber, int dealer, Wind prevailingWind) : base(-1)
        {
            HandNumber = handNumber;
            Dealer = dealer;
            PrevailingWind = prevailingWind;
        }

        public int HandNumber { get; }
        public int Dealer { get; }
        public Wind PrevailingWind { get; }
        public override string ToString() => $"Hand {HandNumber} starts, seat {Dealer} deals";
    }

    public enum PaymentReason : byte
    {
        WinOnDiscard,
        SelfDrawnWin,
        CheatPenalty,
        FalseAccusation,
    }

    /// <summary>Faan moving from one player to another.</summary>
    public sealed class PaymentEvent : MatchEvent
    {
        public PaymentEvent(int from, int to, int amount, PaymentReason reason) : base(from)
        {
            From = from;
            To = to;
            Amount = amount;
            Reason = reason;
        }

        public int From { get; }
        public int To { get; }
        public int Amount { get; }
        public PaymentReason Reason { get; }
        public override string ToString() => $"Seat{From} pays seat{To} {Amount} ({Reason})";
    }

    public sealed class CheaterCaughtEvent : MatchEvent
    {
        public CheaterCaughtEvent(int cheater, int caller) : base(cheater)
        {
            Caller = caller;
        }

        public int Caller { get; }
        public override string ToString() => $"Seat{Seat} caught cheating by seat{Caller}";
    }

    public sealed class FalseAccusationEvent : MatchEvent
    {
        public FalseAccusationEvent(int caller, int accused) : base(caller)
        {
            Accused = accused;
        }

        public int Accused { get; }
        public override string ToString() => $"Seat{Seat} wrongly accuses seat{Accused}";
    }

    public sealed class UpgradeBoughtEvent : MatchEvent
    {
        public UpgradeBoughtEvent(int seat, Upgrade upgrade, int level, int cost) : base(seat)
        {
            Upgrade = upgrade;
            Level = level;
            Cost = cost;
        }

        public Upgrade Upgrade { get; }
        public int Level { get; }
        public int Cost { get; }
        public override string ToString() => $"Seat{Seat} buys {Upgrade} {Level} for {Cost}";
    }

    public sealed class PlayerOutEvent : MatchEvent
    {
        public PlayerOutEvent(int seat, string reason) : base(seat)
        {
            Reason = reason;
        }

        public string Reason { get; }
        public override string ToString() => $"Seat{Seat} is out: {Reason}";
    }

    public sealed class MatchOverEvent : MatchEvent
    {
        public MatchOverEvent(System.Collections.Generic.IReadOnlyList<int> winners) : base(-1)
        {
            Winners = winners;
        }

        public System.Collections.Generic.IReadOnlyList<int> Winners { get; }
        public override string ToString() => "Match over, winners: " + string.Join(",", Winners);
    }
}
