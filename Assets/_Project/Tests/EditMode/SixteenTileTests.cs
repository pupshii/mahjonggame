using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace MahjongNight.Core.Tests
{
    /// <summary>16-tile mahjong (as in Taiwan): 16 tiles in hand, five sets and a pair to win.</summary>
    public class SixteenTileTests
    {
        private static RuleSet Sixteen(RuleSet basis = null)
        {
            RuleSet r = basis ?? RuleSet.Classic();
            r.HandSets = 5;
            r.MinFaan = 0;
            return r;
        }

        [Test]
        public void HandSize_FollowsTheNumberOfSets()
        {
            Assert.That(RuleSet.Classic().HandSize, Is.EqualTo(13));
            Assert.That(Sixteen().HandSize, Is.EqualTo(16));
        }

        [Test]
        public void Deal_Gives16Each_And17ToTheDealer()
        {
            for (ulong seed = 1; seed <= 30; seed++)
            {
                int dealer = (int)(seed % 4);
                HandState hand = HandState.Deal(Sixteen(), dealer, Wind.East, seed);
                for (int s = 0; s < 4; s++)
                {
                    SeatState st = hand.Seats[s];
                    Assert.That(st.Concealed.Count, Is.EqualTo(s == dealer ? 17 : 16), $"seed {seed} seat {s}");
                    Assert.That(st.Concealed.Any(t => t.IsBonus), Is.False, "Flowers are replaced");
                }
                Assert.That(HandInvariants.Check(hand), Is.Empty);
            }
        }

        [Test]
        public void FiveSetsAndAPair_IsAWin_ButOnlyWithFiveSetRules()
        {
            List<Tile> hand = Tile.ParseList("1B 2B 3B 4B 5B 6B 7B 8B 9B 1C 2C 3C 4C 5C 6C E E");
            Assert.That(FaanCalculator.IsCompleteShape(hand, new List<Meld>(), Sixteen()), Is.True);
            Assert.That(FaanCalculator.IsCompleteShape(hand, new List<Meld>(), RuleSet.Classic()), Is.False, "17 tiles isn't a 13-tile hand");
            Assert.That(HandDecomposer.Decompose(hand, new List<Meld>(), 5).Single().Groups, Has.Count.EqualTo(5));

            // A 14-tile shape no longer wins under 16-tile rules.
            List<Tile> fourteen = Tile.ParseList("1B 2B 3B 4B 5B 6B 7B 8B 9B 1C 2C 3C E E");
            Assert.That(FaanCalculator.IsCompleteShape(fourteen, new List<Meld>(), Sixteen()), Is.False);
        }

        [Test]
        public void Waits_ForA16TileHand()
        {
            // Four sets + 4C5C + a pair of E: waiting on 3C or 6C.
            List<Tile> sixteen = Tile.ParseList("1B 2B 3B 4B 5B 6B 7B 8B 9B 1C 2C 3C 4C 5C E E");
            List<Tile> waits = FaanCalculator.GetWaits(sixteen, new List<Meld>(), Sixteen());
            Assert.That(waits, Is.EquivalentTo(Tile.ParseList("3C 6C")));
        }

        [Test]
        public void RandomHands_StayConsistent_AndSomeoneWins()
        {
            int wins = 0;
            for (ulong seed = 1; seed <= 120; seed++)
            {
                var rng = new SeededRandom(seed * 31 + 7);
                HandState hand = HandState.Deal(Sixteen(), (int)(seed % 4), Wind.East, seed);
                for (int step = 0; hand.Phase != HandPhase.Ended; step++)
                {
                    Assert.That(step, Is.LessThan(20000), $"seed {seed}: hand did not end");
                    int seat = hand.SeatToAct();
                    List<PlayerAction> legal = hand.GetLegalActions(seat);
                    Assert.That(legal, Is.Not.Empty, $"seed {seed}: seat {seat} stuck in {hand.Phase}");
                    PlayerAction action = legal[rng.NextInt(legal.Count)];
                    foreach (PlayerAction a in legal)
                        if (a.Type == ActionType.Win || a.Type == ActionType.SelfDrawnWin)
                            action = a;
                    Assert.That(hand.TryApply(action, out string error), Is.True, $"seed {seed}: {error}");
                    List<string> problems = HandInvariants.Check(hand);
                    Assert.That(problems, Is.Empty, $"seed {seed}, step {step}:\n" + string.Join("\n", problems));
                }
                if (hand.Result.Outcome == HandOutcome.Win)
                {
                    wins++;
                    int w = hand.Result.Winners[0];
                    SeatState ws = hand.Seats[w];
                    Assert.That(ws.Concealed.Count + 3 * ws.Melds.Count, Is.EqualTo(17), "A 16-tile win holds 17");
                }
            }
            Assert.That(wins, Is.GreaterThan(0));
        }

        [Test]
        public void Bots_PlayFullMatches_With16Tiles()
        {
            RuleSet rules = Sixteen(RuleSet.Party());
            rules.MinFaan = 3;
            var match = new ScoreMatch(rules, MatchRules.Default(), 5);
            for (int h = 0; h < 8 && match.Phase != MatchPhase.MatchOver; h++)
            {
                match.StartHand();
                HandState hand = match.Hand;
                var bots = Enumerable.Range(0, 4).Select(s => new SimpleBot((ulong)(h * 10 + s), ambition: 1)).ToArray();
                for (int step = 0; hand.Phase != HandPhase.Ended; step++)
                {
                    Assert.That(step, Is.LessThan(20000));
                    int seat = hand.SeatToAct();
                    hand.Apply(bots[seat].Choose(hand, seat, hand.GetLegalActions(seat)));
                    Assert.That(HandInvariants.Check(hand), Is.Empty);
                }
                match.ResolveHand();
                Assert.That(match.Players.Sum(p => p.Score), Is.EqualTo(4 * MatchRules.Default().StartingScore), "Zero-sum");
            }
        }
    }
}
