using System;
using System.Collections.Generic;
using System.Linq;

namespace MahjongNight.Core
{
    /// <summary>A customer's food order.</summary>
    public sealed class FoodOrder
    {
        public int Id { get; internal set; }
        public int TableIndex { get; internal set; }
        public int Seat { get; internal set; }
        public string Dish { get; internal set; } = "Chicken soup";
        public double PlacedAt { get; internal set; }
        public double Deadline { get; internal set; }

        public double SecondsLeft(double now) => Math.Max(0, Deadline - now);
    }

    public sealed class OrderRules
    {
        /// <summary>Seconds between new orders (random in this range).</summary>
        public double MinGapSeconds { get; set; } = 90;
        public double MaxGapSeconds { get; set; } = 150;
        /// <summary>Delay before the very first order.</summary>
        public double FirstOrderSeconds { get; set; } = 45;
        /// <summary>How long a customer waits for their food.</summary>
        public double PatienceSeconds { get; set; } = 240;
        public int MaxOpenOrders { get; set; } = 2;
        /// <summary>Faan the customer tips you when you serve them.</summary>
        public int Tip { get; set; } = 8;
        /// <summary>Faan you refund a customer whose order ran out of time.</summary>
        public int LateRefund { get; set; } = 5;

        public static OrderRules Default() => new OrderRules();
    }

    /// <summary>
    /// Customers (bots) order food now and then. You're the manager: cook it and serve it before
    /// they give up. Pure rules; time is passed in.
    /// </summary>
    public sealed class OrderBook
    {
        private readonly List<FoodOrder> open = new List<FoodOrder>();
        private readonly SeededRandom rng;
        private int nextId = 1;

        public OrderBook(OrderRules rules, ulong seed, double now)
        {
            Rules = rules ?? OrderRules.Default();
            rng = new SeededRandom(seed);
            NextOrderAt = now + Rules.FirstOrderSeconds;
        }

        public OrderRules Rules { get; }
        public IReadOnlyList<FoodOrder> Open => open;
        public double NextOrderAt { get; private set; }

        /// <summary>
        /// Places a new order if one is due: a random customer (seats 1-3) at a random table who
        /// isn't already waiting. <paramref name="canOrder"/>(table, seat) can veto a customer.
        /// </summary>
        public FoodOrder TryCreate(double now, int tableCount, Func<int, int, bool> canOrder = null)
        {
            if (now < NextOrderAt || open.Count >= Rules.MaxOpenOrders || tableCount <= 0)
                return null;
            NextOrderAt = now + Rules.MinGapSeconds + rng.NextDouble() * (Rules.MaxGapSeconds - Rules.MinGapSeconds);

            var candidates = new List<(int table, int seat)>();
            for (int t = 0; t < tableCount; t++)
                for (int s = 1; s < HandState.SeatCount; s++)
                    if (Find(t, s) == null && (canOrder == null || canOrder(t, s)))
                        candidates.Add((t, s));
            if (candidates.Count == 0)
                return null;

            var (table, seat) = candidates[rng.NextInt(candidates.Count)];
            var order = new FoodOrder
            {
                Id = nextId++,
                TableIndex = table,
                Seat = seat,
                PlacedAt = now,
                Deadline = now + Rules.PatienceSeconds,
            };
            open.Add(order);
            return order;
        }

        public FoodOrder Find(int table, int seat) => open.FirstOrDefault(o => o.TableIndex == table && o.Seat == seat);

        /// <summary>Serves an open order (removes it).</summary>
        public bool Deliver(FoodOrder order) => open.Remove(order);

        /// <summary>Removes and returns orders whose customers ran out of patience.</summary>
        public List<FoodOrder> Expire(double now)
        {
            List<FoodOrder> gone = open.Where(o => now >= o.Deadline).ToList();
            foreach (FoodOrder o in gone)
                open.Remove(o);
            return gone;
        }
    }
}
