using System.Collections.Generic;
using MahjongNight.Core;
using UnityEngine;

namespace MahjongNight.Game
{
    /// <summary>
    /// Trickshots: once your hand is ready (one tile from winning), press T and stack it into a
    /// leaning diagonal tower (a quick timing mini-game). If it stands, your hand stays stacked
    /// on the table, and if you win this hand while it's up, every payment to you is ×2 (×3 for
    /// a flawless stack). Change your hand so it's no longer ready and the tower topples.
    /// Bots with ready hands try it too — and a tower going up (or crashing down) tells you
    /// they're one tile away.
    /// </summary>
    public sealed partial class GreyboxGame
    {
        private const float BotTrickshotChance = 0.3f;     // per ready hand
        private const float BotTrickshotSuccess = 0.65f;

        private void ResetTrickshots()
        {
            for (int s = 0; s < HandState.SeatCount; s++)
            {
                ctx.Ready[s] = false;
                ctx.TrickTried[s] = false;
                ctx.BotTrickAt[s] = -1f;
            }
        }

        /// <summary>Is this seat one tile from winning (13 tiles held, at least one winning tile)?</summary>
        private bool ComputeReady(int seat)
        {
            SeatState st = Hand.Seats[seat];
            if (st.Concealed.Count + 3 * st.Melds.Count != rules.HandSize)
                return false;
            return FaanCalculator.GetWaits(st.Concealed, st.Melds, rules).Count > 0;
        }

        /// <summary>After every action: who's ready now — and whose tower falls because they no longer are.</summary>
        private void UpdateReadiness()
        {
            if (Hand == null || Hand.Phase == HandPhase.Ended || match.Phase != MatchPhase.PlayingHand)
                return;
            for (int s = 0; s < HandState.SeatCount; s++)
            {
                SeatState st = Hand.Seats[s];
                if (st.Concealed.Count + 3 * st.Melds.Count != rules.HandSize)
                    continue;                                   // mid-turn (holding the extra tile): judge after the discard
                ctx.Ready[s] = ComputeReady(s);
                if (!ctx.Ready[s] && match.TrickshotMultiplier(s) > 1)
                {
                    match.SetTrickshot(s, 1);
                    AddLog(s == HumanSeat ? "Your hand isn't ready any more — your tower topples!" : $"{SeatName(s)}'s tile tower topples.");
                }
            }
        }

        private bool[] StackedSeats()
        {
            var stacked = new bool[HandState.SeatCount];
            for (int s = 0; s < stacked.Length; s++)
                stacked[s] = match.TrickshotMultiplier(s) > 1;
            return stacked;
        }

        // ------------------------------------------------------------------ you

        private void TryHumanTrickshot()
        {
            if (standing || !HandLive || OnPhone(HumanSeat) || InMiniGame)
                return;
            if (match.TrickshotMultiplier(HumanSeat) > 1)
            {
                ShowToast("Your tower is already standing. Now win!");
                return;
            }
            if (ctx.TrickTried[HumanSeat])
            {
                ShowToast("You've had your trickshot this hand.");
                return;
            }
            if (!ctx.Ready[HumanSeat] || !ComputeReady(HumanSeat))
            {
                ShowToast(Hand.Seats[HumanSeat].Concealed.Count + 3 * Hand.Seats[HumanSeat].Melds.Count == rules.HandSize + 1
                    ? "Discard first — then stack your ready hand."
                    : "Trickshots are only for a ready hand (one tile from winning).");
                return;
            }
            ctx.TrickTried[HumanSeat] = true;
            TableContext at = ctx;
            StartMiniGame(new TrickshotGame(), score =>
            {
                TableContext keep = ctx;
                ctx = at;
                int mult = score >= 100 ? 3 : score >= 50 ? 2 : 1;
                if (mult > 1 && HandLive && ComputeReady(HumanSeat))
                {
                    match.SetTrickshot(HumanSeat, mult);
                    RebuildTable();
                    ShowToast($"Your tower stands! Win this hand for ×{mult}.");
                    AddLog($"TRICKSHOT: you stack your ready hand into a leaning tower — a win pays ×{mult}.");
                }
                else if (mult > 1)
                {
                    ShowToast("Too late — the hand moved on.");
                }
                else
                {
                    ShowToast("CRASH! Your tiles scatter across the felt. Everyone saw that.");
                    AddLog("Your trickshot tower crashes. (Everyone now knows you're ready…)");
                }
                ctx = keep;
            });
        }

        // ------------------------------------------------------------------ bots

        /// <summary>Bots with a ready hand sometimes try a trickshot after a few seconds.</summary>
        private void UpdateBotTrickshots(float now)
        {
            for (int s = 0; s < HandState.SeatCount; s++)
            {
                if (s == HumanSeat || ctx.TrickTried[s] || !ctx.Ready[s] || match.TrickshotMultiplier(s) > 1 || OnPhone(s))
                    continue;
                if (ctx.BotTrickAt[s] < 0f)
                {
                    if (Range(0f, 1f) < BotTrickshotChance)
                        ctx.BotTrickAt[s] = now + Range(3f, 9f);
                    else
                        ctx.TrickTried[s] = true;              // not this hand
                    continue;
                }
                if (now < ctx.BotTrickAt[s])
                    continue;
                ctx.TrickTried[s] = true;
                if (Range(0f, 1f) < BotTrickshotSuccess)
                {
                    int mult = Range(0f, 1f) < 0.2f ? 3 : 2;
                    match.SetTrickshot(s, mult);
                    RebuildTable();
                    AddLog($"{SeatName(s)} stacks their hand into a leaning tower — TRICKSHOT ×{mult} if they win!");
                    if (ctx == Seated && !standing)
                        ShowToast($"{SeatName(s)} built a trickshot tower (×{mult}). They're one tile away…");
                }
                else
                {
                    AddLog($"{SeatName(s)}'s tile tower crashes down. (They must be ready…)");
                    if (ctx == Seated && !standing)
                        ShowToast($"{SeatName(s)}'s trickshot tower crashes! They're one tile from winning.");
                }
            }
        }

        // ------------------------------------------------------------------ the payoff

        private void AnnounceTrickshotWins(HandResult result)
        {
            if (result.Outcome != HandOutcome.Win)
                return;
            foreach (int w in result.Winners)
            {
                int mult = match.TrickshotMultiplier(w);
                if (mult <= 1)
                    continue;
                resultLines.Add($"TRICKSHOT ×{mult}! {SeatName(w)} won with their hand stacked — every payment is ×{mult}.");
                AddLog($"TRICKSHOT ×{mult}! {SeatName(w)} wins off a standing tower.");
                if (w == HumanSeat && ctx == Seated)
                    ShowGrade($"TRICKSHOT ×{mult}!", new Color(1f, 0.85f, 0.2f));
            }
        }

        private string TrickshotHint()
        {
            int mult = match.TrickshotMultiplier(HumanSeat);
            if (mult > 1)
                return $"<color=#ffd040>TRICKSHOT ×{mult} tower standing — win this hand!</color>";
            return ctx.TrickTried[HumanSeat] ? null : "<color=#ffd040>T: trickshot — stack it for ×2</color>";
        }
    }
}
