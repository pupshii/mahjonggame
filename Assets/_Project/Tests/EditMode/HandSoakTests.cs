using System.Collections.Generic;
using NUnit.Framework;

namespace MahjongNight.Core.Tests
{
    /// <summary>
    /// Plays many complete hands with random legal moves and checks every invariant after
    /// every action. Catches illegal states that hand-written scenarios miss.
    /// </summary>
    public class HandSoakTests
    {
        private const int MaxSteps = 5000;

        private static HandState PlayRandomHand(ulong seed, IWinValidator validator, RuleSet rules, bool alwaysWin = false)
        {
            var rng = new SeededRandom(seed * 7919 + 1);
            HandState hand = HandState.Deal(rules, dealer: (int)(seed % 4), Wind.East, seed, validator);
            CheckInvariants(hand, seed, 0);

            for (int step = 1; hand.Phase != HandPhase.Ended; step++)
            {
                Assert.That(step, Is.LessThan(MaxSteps), $"Seed {seed}: hand did not end");

                int seat = hand.Phase == HandPhase.AwaitingAction
                    ? hand.CurrentSeat
                    : hand.SeatsAwaitingResponse()[0];
                List<PlayerAction> legal = hand.GetLegalActions(seat);
                Assert.That(legal, Is.Not.Empty, $"Seed {seed}: seat {seat} is stuck in {hand.Phase}");

                PlayerAction action = legal[rng.NextInt(legal.Count)];
                if (alwaysWin)
                {
                    foreach (PlayerAction a in legal)
                        if (a.Type == ActionType.Win || a.Type == ActionType.SelfDrawnWin)
                            action = a;
                }
                Assert.That(hand.TryApply(action, out string error), Is.True, $"Seed {seed}: {error}");
                CheckInvariants(hand, seed, step);
            }
            return hand;
        }

        private static void CheckInvariants(HandState hand, ulong seed, int step)
        {
            List<string> problems = HandInvariants.Check(hand);
            Assert.That(problems, Is.Empty, $"Seed {seed}, step {step}:\n" + string.Join("\n", problems));
        }

        [Test]
        public void RandomHands_WithoutWins_AlwaysEndInDrawGame()
        {
            for (ulong seed = 1; seed <= 200; seed++)
            {
                HandState hand = PlayRandomHand(seed, NeverWinValidator.Instance, RuleSet.Party());
                Assert.That(hand.Result.Outcome, Is.EqualTo(HandOutcome.ExhaustiveDraw), $"Seed {seed}");
                Assert.That(hand.Wall.CanDraw, Is.False);
            }
        }

        [Test]
        public void RandomHands_WithWins_StayConsistent()
        {
            int wins = 0, draws = 0, selfDrawn = 0, claimed = 0;
            for (ulong seed = 1; seed <= 300; seed++)
            {
                var rules = RuleSet.Party();
                rules.AllowMultipleWinners = seed % 2 == 0;
                rules.DeadWallSize = (int)(seed % 3) * 8;
                HandState hand = PlayRandomHand(seed, new HashWinValidator(400), rules);

                if (hand.Result.Outcome == HandOutcome.ExhaustiveDraw)
                {
                    draws++;
                    continue;
                }
                wins++;
                if (hand.Result.SelfDrawn) selfDrawn++; else claimed++;
            }
            TestContext.WriteLine($"wins {wins} (self-drawn {selfDrawn}, on discard {claimed}), draws {draws}");
            Assert.That(selfDrawn, Is.GreaterThan(0));
            Assert.That(claimed, Is.GreaterThan(0));
            Assert.That(draws, Is.GreaterThan(0), "Some hands should play to the end of the wall");
        }

        [Test]
        public void RandomHands_WithRealRules_ScoreEveryWin()
        {
            int wins = 0;
            foreach (RuleSet rules in new[] { RuleSet.Party(), RuleSet.Classic() })
            {
                for (ulong seed = 1; seed <= 200; seed++)
                {
                    HandState hand = PlayRandomHand(seed, null, rules, alwaysWin: true);
                    if (hand.Result.Outcome != HandOutcome.Win)
                        continue;
                    wins++;
                    Assert.That(hand.Result.WinContexts.Count, Is.EqualTo(hand.Result.Winners.Count));
                    foreach (WinContext ctx in hand.Result.WinContexts)
                    {
                        FaanResult score = FaanCalculator.Calculate(ctx, rules);
                        Assert.That(score, Is.Not.Null, $"Seed {seed}: declared win is not a complete hand");
                        Assert.That(score.Total, Is.GreaterThanOrEqualTo(rules.MinFaan), $"Seed {seed}: {score}");
                    }
                }
            }
            TestContext.WriteLine($"real-rule wins: {wins} of 400");
        }
    }
}
