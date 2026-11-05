namespace MahjongNight.Core
{
    /// <summary>
    /// Something that happened during a hand, in order. Presentation plays these back as
    /// animations; the network layer forwards them. Some events carry private information
    /// (<see cref="TileDrawnEvent"/>) — the host must only send those to the owning player.
    /// </summary>
    public abstract class HandEvent
    {
        protected HandEvent(int seat)
        {
            Seat = seat;
        }

        /// <summary>Seat the event concerns, or -1 for table-wide events.</summary>
        public int Seat { get; }
    }

    /// <summary>A player drew a tile. Private: only the drawing player may see <see cref="Tile"/>.</summary>
    public sealed class TileDrawnEvent : HandEvent
    {
        public TileDrawnEvent(int seat, Tile tile, bool fromBack) : base(seat)
        {
            Tile = tile;
            FromBack = fromBack;
        }

        public Tile Tile { get; }
        /// <summary>True for replacement draws (after a kong or bonus tile).</summary>
        public bool FromBack { get; }
        public override string ToString() => $"Seat{Seat} draws {Tile}{(FromBack ? " (replacement)" : "")}";
    }

    /// <summary>
    /// A chosen draw failed: no copy of <see cref="Tile"/> was left in the wall, so the player
    /// loses their turn. Private: others only learn that the draw failed, not which tile.
    /// </summary>
    public sealed class DrawFailedEvent : HandEvent
    {
        public DrawFailedEvent(int seat, Tile tile) : base(seat)
        {
            Tile = tile;
        }

        public Tile Tile { get; }
        public override string ToString() => $"Seat{Seat} tries to draw {Tile} — none left, turn lost";
    }

    /// <summary>
    /// A player secretly swapped a tile from their hand with one from their pocket. Private:
    /// only the cheater (and the host) knows; everyone else only sees the motion.
    /// </summary>
    public sealed class PocketSwapEvent : HandEvent
    {
        public PocketSwapEvent(int seat, Tile intoPocket, Tile outOfPocket) : base(seat)
        {
            IntoPocket = intoPocket;
            OutOfPocket = outOfPocket;
        }

        public Tile IntoPocket { get; }
        public Tile OutOfPocket { get; }
        public override string ToString() => $"Seat{Seat} pockets {IntoPocket} and slips in {OutOfPocket}";
    }

    /// <summary>A flower or season was set aside (public).</summary>
    public sealed class BonusTileRevealedEvent : HandEvent
    {
        public BonusTileRevealedEvent(int seat, Tile tile) : base(seat)
        {
            Tile = tile;
        }

        public Tile Tile { get; }
        public override string ToString() => $"Seat{Seat} reveals {Tile}";
    }

    public sealed class TileDiscardedEvent : HandEvent
    {
        public TileDiscardedEvent(int seat, Tile tile) : base(seat)
        {
            Tile = tile;
        }

        public Tile Tile { get; }
        public override string ToString() => $"Seat{Seat} discards {Tile}";
    }

    public sealed class MeldDeclaredEvent : HandEvent
    {
        public MeldDeclaredEvent(int seat, Meld meld) : base(seat)
        {
            Meld = meld;
        }

        public Meld Meld { get; }
        public override string ToString() => $"Seat{Seat} declares {Meld}";
    }

    public sealed class HandEndedEvent : HandEvent
    {
        public HandEndedEvent(HandResult result) : base(-1)
        {
            Result = result;
        }

        public HandResult Result { get; }
        public override string ToString() => $"Hand ended: {Result}";
    }
}
