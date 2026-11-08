using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace MahjongNight.Core.Tests
{
    public class PocketSwapTests
    {
        private const string Seat0 = "1D 4D 7D 1B 4B 7B 1C 4C 7C E S W N RD"; // dealer
        private const string Seat1 = "2D 5D 8D 2B 5B 8B 2C 5C 8C E S W GD";
        private const string Seat2 = "3D 6D 9D 3B 6B 9B 3C 6C 9C E S W WD";
        private const string Seat3 = "1D 4D 7D 1B 4B 7B 1C 4C 7C N N RD GD";

        private static HandState Deal() => TestWall.Deal(new[] { Seat0, Seat1, Seat2, Seat3 });

        private static void AssertConsistent(HandState hand)
        {
            List<string> problems = HandInvariants.Check(hand);
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
        }

        [Test]
        public void Swap_ReplacesTheTile_AndAllowsAFifthCopy()
        {
            HandState hand = Deal();
            // Seat 1 swaps GD for an East from outside the game: five Easts are now in play.
            Tile east = Tile.WindTile(Wind.East);
            Assert.That(hand.TryPocketSwap(1, Tile.DragonTile(Dragon.Green), east, out _), Is.True);

            Assert.That(hand.Seats[1].Concealed, Has.Count.EqualTo(13));
            Assert.That(hand.Seats[1].Concealed, Has.No.Member(Tile.DragonTile(Dragon.Green)));
            Assert.That(hand.Seats[1].CountConcealed(east), Is.EqualTo(2));
            int eastsInPlay = hand.Wall.RemainingTiles().Count(t => t == east)
                + hand.Seats.Sum(s => s.CountConcealed(east));
            Assert.That(eastsInPlay, Is.EqualTo(5));
            Assert.That(hand.StashedTiles, Is.EqualTo(new[] { Tile.DragonTile(Dragon.Green) }));
            Assert.That(hand.InjectedTiles, Is.EqualTo(new[] { Tile.WindTile(Wind.East) }));
            Assert.That(hand.Events.Last(), Is.TypeOf<PocketSwapEvent>());
            AssertConsistent(hand); // conservation accounts for the swap
        }

        [Test]
        public void Swap_RejectsMissingTilesAndBonusTiles()
        {
            HandState hand = Deal();
            Assert.That(hand.TryPocketSwap(1, Tile.Bamboo(9), Tile.Dots(1), out string error), Is.False);
            Assert.That(error, Does.Contain("isn't in your hand"));
            Assert.That(hand.TryPocketSwap(1, Tile.DragonTile(Dragon.Green), Tile.Flower(1), out _), Is.False);
        }

        [Test]
        public void Swap_NotAllowedWithAPendingClaim()
        {
            HandState hand = TestWall.Deal(new[]
            {
                "5B 4D 7D 1B 4B 7B 1C 4C 7C E S W N RD",
                "2D 5D 8D 2B 9B 8B 2C 5C 8C E S W GD",
                "5B 5B 6B 7B 6D 9D 3C 6C 9C E S W WD",
                Seat3,
            });
            hand.Apply(PlayerAction.Discard(0, Tile.Bamboo(5)));
            Assert.That(hand.CanPocketSwap(2), Is.True, "Hasn't responded yet");
            hand.Apply(PlayerAction.Pung(2));
            // Pung resolved immediately (everyone else auto-passed), so seat 2 is now on turn.
            Assert.That(hand.CurrentSeat, Is.EqualTo(2));
            Assert.That(hand.CanPocketSwap(2), Is.True);
        }

        [Test]
        public void CatchCheater_EndsTheHand()
        {
            HandState hand = Deal();
            hand.CatchCheater(2);
            Assert.That(hand.Phase, Is.EqualTo(HandPhase.Ended));
            Assert.That(hand.Result.Outcome, Is.EqualTo(HandOutcome.CheatCaught));
            Assert.That(hand.Result.Cheater, Is.EqualTo(2));
            Assert.That(hand.Result.Winners, Is.Empty);
            Assert.That(hand.GetLegalActions(0), Is.Empty);
            Assert.That(hand.CanPocketSwap(1), Is.False);
            AssertConsistent(hand);
        }

        [Test]
        public void SwapSoak_RandomCheatsKeepTheTableConsistent()
        {
            for (ulong seed = 1; seed <= 100; seed++)
            {
                var rng = new SeededRandom(seed);
                HandState hand = HandState.Deal(TestRules.RandomDraws(), 0, Wind.East, seed);
                for (int step = 0; hand.Phase != HandPhase.Ended; step++)
                {
                    Assert.That(step, Is.LessThan(20000));
                    if (rng.NextInt(5) == 0)
                    {
                        int cheater = rng.NextInt(4);
                        IReadOnlyList<Tile> mine = hand.Seats[cheater].Concealed;
                        if (hand.CanPocketSwap(cheater) && mine.Count > 0)
                            hand.TryPocketSwap(cheater, mine[rng.NextInt(mine.Count)],
                                Tile.FromKindIndex(rng.NextInt(TileSet.PlayableKindCount)), out _);
                    }
                    int seat = hand.SeatToAct();
                    List<PlayerAction> legal = hand.GetLegalActions(seat);
                    hand.Apply(legal[rng.NextInt(legal.Count)]);
                    List<string> problems = HandInvariants.Check(hand);
                    Assert.That(problems, Is.Empty, $"seed {seed}: " + string.Join("\n", problems));
                }
            }
        }
    }

    public class ScoreMatchTests
    {
        private const string Dealer = "5D 4D 7D 1B 4B 7B 1C 4C 7C E S W N RD";
        private const string Seat1Ready = "1B 2B 3B 4B 5B 6B 7C 8C 9C 2C 3C 4C 5D";
        private const string Seat2 = "3D 6D 9D 3B 6B 9B 3C 6C 9C E S W WD";
        private const string Seat3 = "1D 4D 7D 1B 4B 7B 1C 4C 7C N N RD GD";

        private static void PlayHandWithBots(ScoreMatch match, ulong seed)
        {
            HandState hand = match.Hand;
            var bots = Enumerable.Range(0, 4).Select(s => new SimpleBot(seed * 13 + (ulong)s)).ToArray();
            for (int step = 0; hand.Phase != HandPhase.Ended; step++)
            {
                Assert.That(step, Is.LessThan(5000));
                int seat = hand.SeatToAct();
                hand.Apply(bots[seat].Choose(hand, seat, hand.GetLegalActions(seat)));
            }
        }

        private static int Total(ScoreMatch m) => m.Players.Sum(p => p.Score);

        [Test]
        public void Trickshot_MultipliesTheWinnersPayout_AndResetsEachHand()
        {
            bool sawWin = false;
            for (ulong seed = 1; seed < 40 && !sawWin; seed++)
            {
                var plain = new ScoreMatch(RuleSet.Party(), MatchRules.Default(), seed);
                var stacked = new ScoreMatch(RuleSet.Party(), MatchRules.Default(), seed);
                plain.StartHand();
                stacked.StartHand();
                for (int s = 0; s < 4; s++)
                    stacked.SetTrickshot(s, 2);
                Assert.That(stacked.TrickshotMultiplier(1), Is.EqualTo(2));
                PlayHandWithBots(plain, seed);
                PlayHandWithBots(stacked, seed);
                if (plain.Hand.Result.Outcome != HandOutcome.Win)
                    continue;
                sawWin = true;
                int[] before = plain.Players.Select(p => p.Score).ToArray();
                plain.ResolveHand();
                stacked.ResolveHand();
                for (int s = 0; s < 4; s++)
                    Assert.That(stacked.Players[s].Score - before[s], Is.EqualTo(2 * (plain.Players[s].Score - before[s])), $"seed {seed}, seat {s}");
                Assert.That(Total(stacked), Is.EqualTo(Total(plain)), "Still zero-sum");

                if (stacked.Phase == MatchPhase.BetweenHands)
                {
                    stacked.StartHand();
                    Assert.That(stacked.TrickshotMultiplier(1), Is.EqualTo(1), "A new hand starts without a stack");
                }
            }
            Assert.That(sawWin, Is.True, "Some seed should produce a win");

            var m = new ScoreMatch(RuleSet.Party(), MatchRules.Default(), 1);
            Assert.Throws<System.InvalidOperationException>(() => m.SetTrickshot(0, 2), "Only during a hand");
            m.StartHand();
            m.SetTrickshot(0, 99);
            Assert.That(m.TrickshotMultiplier(0), Is.EqualTo(ScoreMatch.MaxTrickshot));
            m.SetTrickshot(0, 1);
            Assert.That(m.TrickshotMultiplier(0), Is.EqualTo(1), "Knocked down");
        }

        [Test]
        public void StartHand_FillsPockets()
        {
            var match = new ScoreMatch(RuleSet.Party(), MatchRules.Default(), 1);
            match.StartHand();
            foreach (MatchPlayer p in match.Players)
            {
                Assert.That(p.Pocket, Has.Count.EqualTo(5));
                Assert.That(p.Pocket.All(t => t.IsValid && !t.IsBonus), Is.True);
            }
        }

        [Test]
        public void FirstDealer_DealsTheFirstHand()
        {
            var match = new ScoreMatch(RuleSet.Party(), MatchRules.Default(), 1, firstDealer: 2);
            match.StartHand();
            Assert.That(match.Dealer, Is.EqualTo(2));
            Assert.That(match.Hand.CurrentSeat, Is.EqualTo(2), "East (the dealer) acts first");
            Assert.That(match.Hand.Seats[2].Concealed, Has.Count.EqualTo(14));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new ScoreMatch(RuleSet.Party(), MatchRules.Default(), 1, firstDealer: 4));
        }

        [Test]
        public void PocketSwap_TradesTileIntoPocketSlot()
        {
            var match = new ScoreMatch(RuleSet.Party(), MatchRules.Default(), 1);
            match.StartHand();
            Tile fromHand = match.Hand.Seats[2].Concealed[0];
            Tile fromPocket = match.Players[2].Pocket[3];
            Assert.That(match.TryPocketSwap(2, fromHand, 3, out _), Is.True);
            Assert.That(match.Players[2].Pocket[3], Is.EqualTo(fromHand));
            Assert.That(match.Hand.Seats[2].Concealed, Has.Member(fromPocket));
            Assert.That(match.TryPocketSwap(2, fromHand, 9, out _), Is.False, "No slot 9");
        }

        [Test]
        public void CaughtCheater_PaysEachPlayer_AndHandEnds()
        {
            var match = new ScoreMatch(RuleSet.Party(), MatchRules.Default(), 1);
            match.StartHand();
            match.CatchCheater(cheater: 3, caller: 0);

            Assert.That(match.Hand.Result.Outcome, Is.EqualTo(HandOutcome.CheatCaught));
            Assert.That(match.Players.Select(p => p.Score), Is.EqualTo(new[] { 10, 10, 10, -30 }));
            Assert.That(match.Players[3].CheatsCaught, Is.EqualTo(1));
            Assert.That(match.Phase, Is.EqualTo(MatchPhase.BetweenHands), "Match continues with the next hand");
            Assert.That(match.Events.OfType<CheaterCaughtEvent>().Single().Caller, Is.EqualTo(0));
            Assert.That(Total(match), Is.EqualTo(0));
        }

        [Test]
        public void FalseAccusation_FinesTheAccuser()
        {
            var match = new ScoreMatch(RuleSet.Party(), MatchRules.Default(), 1);
            match.StartHand();
            match.FalseAccusation(caller: 0, accused: 2);
            Assert.That(match.Players[0].Score, Is.EqualTo(-3));
            Assert.That(match.Players[2].Score, Is.EqualTo(3));
            Assert.That(match.Phase, Is.EqualTo(MatchPhase.PlayingHand), "Hand carries on");
        }

        [Test]
        public void WinOnDiscard_DiscarderPaysFaan()
        {
            var match = new ScoreMatch(RuleSet.Party(), MatchRules.Default(), 1);
            match.StartHand();
            // Swap in a rigged hand: dealer's first discard gives seat 1 an earthly hand (limit, 10 faan in Party).
            HandState rigged = HandState.DealFromWall(RuleSet.Party(), 0, Wind.East,
                TestWall.Build(0, new[] { Dealer, Seat1Ready, Seat2, Seat3 }));
            ReplaceHand(match, rigged);
            rigged.Apply(PlayerAction.Discard(0, Tile.Dots(5)));
            rigged.Apply(PlayerAction.Win(1));
            match.ResolveHand();

            Assert.That(match.Players[1].Score, Is.EqualTo(10));
            Assert.That(match.Players[0].Score, Is.EqualTo(-10));
            Assert.That(match.Players[2].Score + match.Players[3].Score, Is.EqualTo(0));
            Assert.That(match.Dealer, Is.EqualTo(1), "Dealer lost, so the deal passes on");
        }

        [Test]
        public void SelfDrawnWin_EveryonePays()
        {
            var match = new ScoreMatch(RuleSet.Party(), MatchRules.Default(), 1);
            match.StartHand();
            HandState rigged = HandState.DealFromWall(RuleSet.Party(), 0, Wind.East, TestWall.Build(0, new[]
            {
                "1D 2D 3D 4B 5B 6B 7C 8C 9C 2C 3C 4C 5D 5D",
                "2D 5D 8D 2B 5B 8B 2C 5C 8C E S W GD", Seat2, Seat3,
            }));
            ReplaceHand(match, rigged);
            rigged.Apply(PlayerAction.SelfDrawnWin(0)); // heavenly hand: limit = 10
            match.ResolveHand();

            Assert.That(match.Players.Select(p => p.Score), Is.EqualTo(new[] { 30, -10, -10, -10 }));
            Assert.That(match.Dealer, Is.EqualTo(0), "Dealer won, keeps the deal");
        }

        [Test]
        public void Upgrades_CostFaan_AndLevelUp()
        {
            var rules = MatchRules.Default();
            rules.StartingScore = 20;
            var match = new ScoreMatch(RuleSet.Party(), rules, 1);

            Assert.That(match.TryBuy(0, Upgrade.DeepPockets, out _), Is.True);   // 6
            Assert.That(match.TryBuy(0, Upgrade.DeepPockets, out _), Is.True);   // 12
            Assert.That(match.Players[0].Score, Is.EqualTo(2));
            Assert.That(match.Players[0].Level(Upgrade.DeepPockets), Is.EqualTo(2));
            Assert.That(match.TryBuy(0, Upgrade.DeepPockets, out string maxed), Is.False);
            Assert.That(maxed, Does.Contain("maxed"));
            Assert.That(match.TryBuy(0, Upgrade.EagleEye, out string broke), Is.False);
            Assert.That(broke, Does.Contain("costs 6"));

            match.StartHand();
            Assert.That(match.Players[0].Pocket, Has.Count.EqualTo(9), "Deep Pockets 2 = 5 + 4");
            Assert.That(match.TryBuy(1, Upgrade.EagleEye, out _), Is.False, "Not during a hand");
        }

        [Test]
        public void UpgradeEffects()
        {
            Assert.That(Upgrades.CheatStepSeconds(0), Is.EqualTo(1.2).Within(1e-9));
            Assert.That(Upgrades.CheatStepSeconds(2), Is.EqualTo(0.6).Within(1e-9));
            Assert.That(Upgrades.SpotSeconds(2, 0), Is.GreaterThan(Upgrades.SpotSeconds(0, 0)));
            Assert.That(Upgrades.SpotSeconds(0, 2), Is.LessThan(Upgrades.SpotSeconds(0, 0)));
        }

        [Test]
        public void BotMatch_RunsAllHands_AndScoresStayZeroSum()
        {
            for (ulong seed = 1; seed <= 6; seed++)
            {
                var match = new ScoreMatch(RuleSet.Party(), MatchRules.Default(), seed);
                while (match.Phase != MatchPhase.MatchOver)
                {
                    match.StartHand();
                    PlayHandWithBots(match, seed * 100 + (ulong)match.HandNumber);
                    match.ResolveHand();
                    Assert.That(Total(match), Is.EqualTo(0), "Faan only moves between players");
                }
                Assert.That(match.HandNumber, Is.EqualTo(8));
                Assert.That(match.Winners, Is.Not.Empty);
                int best = match.Players.Max(p => p.Score);
                Assert.That(match.Winners.All(w => match.Players[w].Score == best), Is.True);
            }
        }

        [Test]
        public void CheatAdvisor_FindsAWinningSwap()
        {
            // Dealer is one tile from a 3+ faan hand (all chows + concealed... self-drawn): swap N for 5D.
            HandState hand = HandState.DealFromWall(RuleSet.Party(), 0, Wind.East, TestWall.Build(0, new[]
            {
                "1D 2D 3D 4B 5B 6B 7C 8C 9C 2C 3C 4C 5D N",
                "2D 5D 8D 2B 5B 8B 2C 5C 8C E S W GD", Seat2, Seat3,
            }));
            Assert.That(hand.GetLegalActions(0).Any(a => a.Type == ActionType.SelfDrawnWin), Is.False);
            var pocket = Tile.ParseList("9B 5D RD");
            CheatPlan? plan = CheatAdvisor.FindWorthwhileSwap(hand, 0, pocket);
            Assert.That(plan.HasValue, Is.True);
            Assert.That(plan.Value.MakesWin, Is.True);
            Assert.That(plan.Value.FromHand, Is.EqualTo(Tile.WindTile(Wind.North)));
            Assert.That(plan.Value.FromPocket, Is.EqualTo(Tile.Dots(5)));

            Assert.That(hand.TryPocketSwap(0, plan.Value.FromHand, plan.Value.FromPocket, out _), Is.True);
            Assert.That(hand.GetLegalActions(0), Has.Member(PlayerAction.SelfDrawnWin(0)));
        }

        [Test]
        public void CheatAdvisor_SkipsUselessSwaps()
        {
            HandState hand = HandState.DealFromWall(RuleSet.Party(), 0, Wind.East, TestWall.Build(0, new[]
            {
                "1D 4D 7D 1B 4B 7B 1C 4C 7C E S W N RD",
                "2D 5D 8D 2B 5B 8B 2C 5C 8C E S W GD", Seat2, Seat3,
            }));
            Assert.That(CheatAdvisor.FindWorthwhileSwap(hand, 0, Tile.ParseList("9B 9C")).HasValue, Is.False);
        }

        /// <summary>Test hook: play a rigged hand inside a match.</summary>
        private static void ReplaceHand(ScoreMatch match, HandState hand)
        {
            typeof(ScoreMatch).GetProperty(nameof(ScoreMatch.Hand))!.SetValue(match, hand);
        }
    }
}
