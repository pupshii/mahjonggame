using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace MahjongNight.Core.Tests
{
    public class BotTests
    {
        private static HandState PlayBotHand(ulong seed, RuleSet rules)
        {
            HandState hand = HandState.Deal(rules, dealer: (int)(seed % 4), Wind.East, seed);
            var bots = new IBot[4];
            for (int s = 0; s < 4; s++)
                bots[s] = new SimpleBot(seed * 31 + (ulong)s);

            for (int step = 0; hand.Phase != HandPhase.Ended; step++)
            {
                Assert.That(step, Is.LessThan(5000), $"Seed {seed}: hand did not end");
                int seat = hand.Phase == HandPhase.AwaitingAction ? hand.CurrentSeat : hand.SeatsAwaitingResponse()[0];
                List<PlayerAction> legal = hand.GetLegalActions(seat);
                PlayerAction action = bots[seat].Choose(hand, seat, legal);
                Assert.That(legal, Has.Member(action), $"Seed {seed}: bot chose an illegal action");
                hand.Apply(action);

                List<string> problems = HandInvariants.Check(hand);
                Assert.That(problems, Is.Empty, string.Join("\n", problems));
            }
            return hand;
        }

        [Test]
        public void BotsPlayCompleteHands_AndWinOften()
        {
            int wins = 0;
            const int hands = 100;
            for (ulong seed = 1; seed <= hands; seed++)
                if (PlayBotHand(seed, RuleSet.Party()).Result.Outcome == HandOutcome.Win)
                    wins++;
            TestContext.WriteLine($"Bot wins: {wins}/{hands}");
            Assert.That(wins, Is.GreaterThan(hands / 4), "Bots should usually finish a hand under party rules");
        }

        [Test]
        public void BotsPlayClassicRules()
        {
            for (ulong seed = 1; seed <= 30; seed++)
                PlayBotHand(seed, RuleSet.Classic());
        }

        [Test]
        public void Bot_AlwaysTakesAWin()
        {
            var bot = new SimpleBot(1);
            HandState hand = HandState.Deal(RuleSet.Party(), 0, Wind.East, 1);
            var legal = new List<PlayerAction> { PlayerAction.Discard(0, hand.Seats[0].Concealed[0]), PlayerAction.SelfDrawnWin(0) };
            Assert.That(bot.Choose(hand, 0, legal), Is.EqualTo(PlayerAction.SelfDrawnWin(0)));
        }

        [Test]
        public void Bot_IsDeterministicPerSeed()
        {
            HandState a = PlayBotHand(42, RuleSet.Party());
            HandState b = PlayBotHand(42, RuleSet.Party());
            Assert.That(a.Events.Select(e => e.ToString()), Is.EqualTo(b.Events.Select(e => e.ToString())));
        }

        [Test]
        public void PreviewWin_MatchesLegalWin()
        {
            HandState hand = HandState.Deal(RuleSet.Party(), 0, Wind.East, 7);
            WinContext preview = hand.PreviewWin(0);
            Assert.That(preview, Is.Not.Null);
            Assert.That(preview.SelfDrawn, Is.True);
            Assert.That(preview.Concealed, Has.Count.EqualTo(14));
            Assert.That(hand.PreviewWin(1), Is.Null, "Not seat 1's turn");
            bool canWin = hand.GetLegalActions(0).Contains(PlayerAction.SelfDrawnWin(0));
            Assert.That(FaanCalculator.Calculate(preview, hand.Rules) != null, Is.EqualTo(canWin));
        }
    }
}
