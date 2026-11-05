using System.Collections.Generic;

namespace MahjongNight.Core
{
    /// <summary>Something that happened in the party layer, for presentation and networking.</summary>
    public abstract class PartyEvent
    {
        protected PartyEvent(int seat)
        {
            Seat = seat;
        }

        /// <summary>Seat concerned, or -1 for table-wide events.</summary>
        public int Seat { get; }
    }

    public sealed class HandStartedEvent : PartyEvent
    {
        public HandStartedEvent(int handNumber, int dealer, Wind prevailingWind) : base(-1)
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

    /// <summary>Who has to eat after a hand, and how many dumplings each.</summary>
    public sealed class PenaltiesAssignedEvent : PartyEvent
    {
        public PenaltiesAssignedEvent(IReadOnlyList<int> dumplingsPerSeat, bool bigHand) : base(-1)
        {
            DumplingsPerSeat = dumplingsPerSeat;
            BigHand = bigHand;
        }

        public IReadOnlyList<int> DumplingsPerSeat { get; }
        public bool BigHand { get; }
        public override string ToString() => "Penalties: " + string.Join(",", DumplingsPerSeat);
    }

    public sealed class DumplingEatenEvent : PartyEvent
    {
        public DumplingEatenEvent(int seat, int slot, bool pepper, int livesLeft, int remainingInSteamer) : base(seat)
        {
            Slot = slot;
            Pepper = pepper;
            LivesLeft = livesLeft;
            RemainingInSteamer = remainingInSteamer;
        }

        public int Slot { get; }
        public bool Pepper { get; }
        public int LivesLeft { get; }
        public int RemainingInSteamer { get; }
        public override string ToString() => $"Seat{Seat} eats dumpling {Slot}: {(Pepper ? "PEPPER" : "safe")}";
    }

    public sealed class KnockedOutEvent : PartyEvent
    {
        public KnockedOutEvent(int seat) : base(seat) { }
        public override string ToString() => $"Seat{Seat} is knocked out";
    }

    public sealed class SteamerRefilledEvent : PartyEvent
    {
        /// <param name="seat">Owner for personal steamers, -1 for the shared one.</param>
        public SteamerRefilledEvent(int seat) : base(seat) { }
        public override string ToString() => "Steamer refilled";
    }

    public sealed class MatchWonEvent : PartyEvent
    {
        public MatchWonEvent(int seat) : base(seat) { }
        public override string ToString() => $"Seat{Seat} wins the match";
    }
}
