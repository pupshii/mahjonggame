using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace MahjongNight.Core.Tests
{
    public class SteamerTests
    {
        [Test]
        public void ExactlyOnePepper_FoundWithinSixBites()
        {
            for (ulong seed = 1; seed <= 50; seed++)
            {
                var steamer = new PartyMatch(RuleSet.Party(), PartyRules.Default(), seed).SteamerFor(0);
                int peppers = 0;
                foreach (int slot in steamer.UneatenSlots())
                    if (slot == steamer.RevealPepperSlot())
                        peppers++;
                Assert.That(peppers, Is.EqualTo(1));
                Assert.That(steamer.Remaining, Is.EqualTo(6));
                Assert.That(steamer.PepperChance, Is.EqualTo(1.0 / 6).Within(1e-9));
            }
        }

        [Test]
        public void PepperPosition_IsRoughlyUniform()
        {
            var counts = new int[6];
            for (ulong seed = 1; seed <= 3000; seed++)
                counts[new PartyMatch(RuleSet.Party(), PartyRules.Default(), seed).SteamerFor(0).RevealPepperSlot()]++;
            foreach (int c in counts)
                Assert.That(c, Is.InRange(400, 600), string.Join(",", counts));
        }
    }

    public class PenaltyTests
    {
        // Seat 1 waits on 5D (1B-6B, 7C-9C, 2C-4C chows + 5D). Seat 0 (dealer) holds 5D.
        private const string Dealer = "5D 4D 7D 1B 4B 7B 1C 4C 7C E S W N RD";
        private const string Seat1Ready = "1B 2B 3B 4B 5B 6B 7C 8C 9C 2C 3C 4C 5D";
        private const string Seat2 = "3D 6D 9D 3B 6B 9B 3C 6C 9C E S W WD";
        private const string Seat3 = "1D 4D 7D 1B 4B 7B 1C 4C 7C N N RD GD";

        private static readonly bool[] AllAlive = { true, true, true, true };

        private static HandState WinOnDealerDiscard(RuleSet rules)
        {
            HandState hand = HandState.DealFromWall(rules, 0, Wind.East,
                TestWall.Build(0, new[] { Dealer, Seat1Ready, Seat2, Seat3 }));
            hand.Apply(PlayerAction.Discard(0, Tile.Dots(5)));
            hand.Apply(PlayerAction.Win(1));
            return hand;
        }

        [Test]
        public void DiscardWin_DiscarderEats_BigHandEatsTwo()
        {
            // Dealer's first discard → earthly hand (limit) → a big hand.
            HandState hand = WinOnDealerDiscard(RuleSet.Party());
            int[] d = PartyMatch.ComputeDumplings(hand, RuleSet.Party(), PartyRules.Default(), AllAlive, out bool big);
            Assert.That(big, Is.True);
            Assert.That(d, Is.EqualTo(new[] { 2, 0, 0, 0 }));
        }

        [Test]
        public void DiscardWin_SmallHand_DiscarderEatsOne()
        {
            var party = PartyRules.Default();
            party.BigHandFaan = 99;
            HandState hand = WinOnDealerDiscard(RuleSet.Party());
            int[] d = PartyMatch.ComputeDumplings(hand, RuleSet.Party(), party, AllAlive, out bool big);
            Assert.That(big, Is.False);
            Assert.That(d, Is.EqualTo(new[] { 1, 0, 0, 0 }));
        }

        [Test]
        public void KnockedOutDiscarder_CostsNobody()
        {
            HandState hand = WinOnDealerDiscard(RuleSet.Party());
            int[] d = PartyMatch.ComputeDumplings(hand, RuleSet.Party(), PartyRules.Default(),
                new[] { false, true, true, true }, out _);
            Assert.That(d, Is.EqualTo(new[] { 0, 0, 0, 0 }));
        }

        [Test]
        public void SelfDrawnWin_EveryoneElseAliveEats()
        {
            HandState hand = HandState.DealFromWall(RuleSet.Party(), 0, Wind.East, TestWall.Build(0, new[]
            {
                "1D 2D 3D 4B 5B 6B 7C 8C 9C 2C 3C 4C 5D 5D",
                "2D 5D 8D 2B 5B 8B 2C 5C 8C E S W GD",
                Seat2, Seat3,
            }));
            hand.Apply(PlayerAction.SelfDrawnWin(0));
            var party = PartyRules.Default();
            party.BigHandFaan = 99; // heavenly hand would otherwise be big
            int[] d = PartyMatch.ComputeDumplings(hand, RuleSet.Party(), party, new[] { true, true, false, true }, out _);
            Assert.That(d, Is.EqualTo(new[] { 0, 1, 0, 1 }), "Seat 2 is out");
        }

        [Test]
        public void DrawGame_NotReadyPlayersEat()
        {
            var rules = RuleSet.Party();
            rules.DeadWallSize = 144 - 53;
            HandState hand = HandState.DealFromWall(rules, 0, Wind.East,
                TestWall.Build(0, new[] { Dealer, Seat1Ready, Seat2, Seat3 }), NeverWinValidator.Instance);
            hand.Apply(PlayerAction.Discard(0, Tile.Dots(4)));
            Assert.That(hand.Result.Outcome, Is.EqualTo(HandOutcome.ExhaustiveDraw));

            int[] d = PartyMatch.ComputeDumplings(hand, rules, PartyRules.Default(), AllAlive, out _);
            Assert.That(d, Is.EqualTo(new[] { 1, 0, 1, 1 }), "Seat 1 is ready (waiting on 5D)");

            var lenient = PartyRules.Default();
            lenient.DrawGameNotReadyEat = false;
            Assert.That(PartyMatch.ComputeDumplings(hand, rules, lenient, AllAlive, out _), Is.EqualTo(new[] { 0, 0, 0, 0 }));
        }
    }

    public class PartyMatchTests
    {
        /// <summary>Plays the current hand to the end with bots.</summary>
        private static void PlayHandWithBots(PartyMatch match, ulong seed)
        {
            HandState hand = match.Hand;
            var bots = Enumerable.Range(0, 4).Select(s => new SimpleBot(seed * 13 + (ulong)s)).ToArray();
            for (int step = 0; hand.Phase != HandPhase.Ended; step++)
            {
                Assert.That(step, Is.LessThan(5000));
                int seat = hand.Phase == HandPhase.AwaitingAction ? hand.CurrentSeat : hand.SeatsAwaitingResponse()[0];
                hand.Apply(bots[seat].Choose(hand, seat, hand.GetLegalActions(seat)));
            }
        }

        private static PartyMatch PlayMatch(ulong seed, PartyRules party)
        {
            var match = new PartyMatch(RuleSet.Party(), party, seed);
            var picker = new SeededRandom(seed);
            for (int guard = 0; match.Phase != MatchPhase.MatchOver; guard++)
            {
                Assert.That(guard, Is.LessThan(2000), $"Seed {seed}: match never ended");
                switch (match.Phase)
                {
                    case MatchPhase.BetweenHands:
                        match.StartHand();
                        break;
                    case MatchPhase.PlayingHand:
                        PlayHandWithBots(match, seed + (ulong)match.HandNumber);
                        match.ResolveHand();
                        break;
                    case MatchPhase.Roulette:
                        int eater = match.CurrentEater;
                        Assert.That(match.Players[eater].IsAlive, Is.True, "Only living players eat");
                        List<int> slots = match.SteamerFor(eater).UneatenSlots();
                        match.Eat(eater, slots[picker.NextInt(slots.Count)]);
                        break;
                }
            }
            return match;
        }

        [TestCase(true, 1)]
        [TestCase(false, 1)]
        [TestCase(true, 2)]
        public void BotMatches_EndWithExactlyOneSurvivor(bool shared, int lives)
        {
            var party = PartyRules.Default();
            party.SharedSteamer = shared;
            party.Lives = lives;
            for (ulong seed = 1; seed <= 8; seed++)
            {
                PartyMatch match = PlayMatch(seed, party);
                Assert.That(match.AliveCount, Is.EqualTo(1));
                Assert.That(match.Players[match.Winner].IsAlive, Is.True);
                Assert.That(match.Events.OfType<KnockedOutEvent>().Count(), Is.EqualTo(3));
                Assert.That(match.Events.OfType<MatchWonEvent>().Single().Seat, Is.EqualTo(match.Winner));
                Assert.That(match.PendingEaters, Is.Empty);
            }
        }

        [Test]
        public void Matches_AreDeterministicPerSeed()
        {
            PartyMatch a = PlayMatch(77, PartyRules.Default());
            PartyMatch b = PlayMatch(77, PartyRules.Default());
            Assert.That(a.Winner, Is.EqualTo(b.Winner));
            Assert.That(a.Events.Select(e => e.ToString()), Is.EqualTo(b.Events.Select(e => e.ToString())));
        }

        [Test]
        public void EatingThePepper_KnocksOutAndRefills()
        {
            var match = new PartyMatch(RuleSet.Party(), PartyRules.Default(), 5);
            ForceRoulette(match, eater: 2);
            Steamer steamer = match.SteamerFor(2);
            int pepper = steamer.RevealPepperSlot();
            match.Eat(2, pepper);

            Assert.That(match.Players[2].IsAlive, Is.False);
            Assert.That(match.Events.OfType<KnockedOutEvent>().Single().Seat, Is.EqualTo(2));
            Assert.That(match.Events.OfType<SteamerRefilledEvent>().Count(), Is.EqualTo(1));
            Assert.That(steamer.Remaining, Is.EqualTo(6), "Refilled");
        }

        [Test]
        public void SafeDumplings_AreNotReplaced()
        {
            var match = new PartyMatch(RuleSet.Party(), PartyRules.Default(), 5);
            ForceRoulette(match, eater: 2);
            Steamer steamer = match.SteamerFor(2);
            int safe = steamer.UneatenSlots().First(s => s != steamer.RevealPepperSlot());
            match.Eat(2, safe);
            Assert.That(match.Players[2].IsAlive, Is.True);
            Assert.That(steamer.Remaining, Is.EqualTo(5));
            Assert.That(steamer.PepperChance, Is.EqualTo(0.2).Within(1e-9));
        }

        [Test]
        public void TryEat_RejectsWrongSeatEatenSlotAndWrongPhase()
        {
            var match = new PartyMatch(RuleSet.Party(), PartyRules.Default(), 5);
            Assert.That(match.TryEat(0, 0, out _), Is.False, "Not in roulette");

            ForceRoulette(match, eater: 2);
            Assert.That(match.TryEat(1, 0, out string error), Is.False, "Not seat 1's turn");
            Assert.That(error, Does.Contain("seat 2"));
            Assert.That(match.TryEat(2, 6, out _), Is.False, "No slot 6");
        }

        [Test]
        public void RouletteDisabled_NobodyEats()
        {
            var party = PartyRules.Default();
            party.RouletteEnabled = false;
            var match = new PartyMatch(RuleSet.Party(), party, 3);
            for (int i = 0; i < 5; i++)
            {
                match.StartHand();
                PlayHandWithBots(match, (ulong)i);
                match.ResolveHand();
                Assert.That(match.Phase, Is.EqualTo(MatchPhase.BetweenHands));
            }
            Assert.That(match.AliveCount, Is.EqualTo(4));
        }

        /// <summary>Plays bot hands until <paramref name="eater"/> is first in line to eat.</summary>
        private static void ForceRoulette(PartyMatch match, int eater)
        {
            for (ulong i = 0; i < 200; i++)
            {
                match.StartHand();
                PlayHandWithBots(match, i);
                match.ResolveHand();
                if (match.Phase == MatchPhase.Roulette && match.CurrentEater == eater)
                    return;
                // Let everyone else eat safely so nobody is knocked out while we search.
                while (match.Phase == MatchPhase.Roulette)
                {
                    Steamer st = match.SteamerFor(match.CurrentEater);
                    int seat = match.CurrentEater;
                    int safe = st.UneatenSlots().FirstOrDefault(s => s != st.RevealPepperSlot());
                    if (st.Remaining == 1)
                        Assert.Inconclusive("Steamer ran down to the pepper while searching");
                    match.Eat(seat, safe);
                    if (match.Phase == MatchPhase.Roulette && match.CurrentEater == eater)
                        return;
                }
            }
            Assert.Fail($"Seat {eater} never had to eat");
        }
    }
}
