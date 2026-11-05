using System;

namespace MahjongNight.Core
{
    public enum ActionType : byte
    {
        // On your own turn
        Discard,
        ConcealedKong,  // 暗槓 four of a kind from the hand
        PromoteKong,    // 加槓 add a tile to an exposed pung
        SelfDrawnWin,   // 自摸
        Draw,           // choose the tile kind to draw (RuleSet.ChooseDraws)

        // Responding to someone else's discard (or promoted kong)
        Pass,
        Chow,           // 上 next player only
        Pung,           // 碰
        Kong,           // 槓 (exposed, from a discard)
        Win             // 食糊 (also robbing a kong, 搶槓)
    }

    /// <summary>
    /// Something a player wants to do. <see cref="Tile"/> is the tile for Discard / Draw /
    /// ConcealedKong / PromoteKong, the lowest tile of the sequence for Chow, and unused
    /// (default) for the rest.
    /// </summary>
    public readonly struct PlayerAction : IEquatable<PlayerAction>
    {
        public PlayerAction(int seat, ActionType type, Tile tile = default)
        {
            Seat = seat;
            Type = type;
            Tile = tile;
        }

        public int Seat { get; }
        public ActionType Type { get; }
        public Tile Tile { get; }

        public static PlayerAction Discard(int seat, Tile tile) => new PlayerAction(seat, ActionType.Discard, tile);
        public static PlayerAction ConcealedKong(int seat, Tile tile) => new PlayerAction(seat, ActionType.ConcealedKong, tile);
        public static PlayerAction PromoteKong(int seat, Tile tile) => new PlayerAction(seat, ActionType.PromoteKong, tile);
        public static PlayerAction SelfDrawnWin(int seat) => new PlayerAction(seat, ActionType.SelfDrawnWin);
        public static PlayerAction Draw(int seat, Tile kind) => new PlayerAction(seat, ActionType.Draw, kind);
        public static PlayerAction Pass(int seat) => new PlayerAction(seat, ActionType.Pass);
        public static PlayerAction Chow(int seat, Tile lowest) => new PlayerAction(seat, ActionType.Chow, lowest);
        public static PlayerAction Pung(int seat) => new PlayerAction(seat, ActionType.Pung);
        public static PlayerAction Kong(int seat) => new PlayerAction(seat, ActionType.Kong);
        public static PlayerAction Win(int seat) => new PlayerAction(seat, ActionType.Win);

        public bool IsClaim => Type >= ActionType.Pass;

        public bool Equals(PlayerAction other) => Seat == other.Seat && Type == other.Type && Tile == other.Tile;
        public override bool Equals(object obj) => obj is PlayerAction other && Equals(other);
        public override int GetHashCode() => (Seat << 24) ^ ((int)Type << 16) ^ Tile.GetHashCode();
        public static bool operator ==(PlayerAction a, PlayerAction b) => a.Equals(b);
        public static bool operator !=(PlayerAction a, PlayerAction b) => !a.Equals(b);

        public override string ToString() =>
            Tile.IsValid ? $"Seat{Seat}:{Type}({Tile})" : $"Seat{Seat}:{Type}";
    }
}
