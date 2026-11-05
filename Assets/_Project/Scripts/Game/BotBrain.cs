using System.Collections.Generic;
using MahjongNight.Core;
using UnityEngine;

namespace MahjongNight.Game
{
    /// <summary>
    /// A bot's eyes and nerves: where it looks (its own tiles on its turn, the player on turn,
    /// the table, short glances at other players — a little more often at anyone who's been
    /// staring at their lap), when it dares to cheat (only when it can see nobody watching),
    /// and its pending call-out when it spots a cheat.
    /// </summary>
    /// <remarks>
    /// Glances at people are short (0.4–1.0 s) while spotting needs 0.9 s of continuous
    /// watching, so a cheat is caught by a lingering look, not by every glance. Simulated catch
    /// rate for an unupgraded 3.6 s swap during a bot's turn: ~1 in 3 (Poker Face 2: ~1 in 25).
    /// </remarks>
    public sealed class BotBrain
    {
        private readonly SeededRandom rng;
        private float nextGazeChange;

        public BotBrain(int seat, ulong seed)
        {
            Seat = seat;
            rng = new SeededRandom(seed);
        }

        public int Seat { get; }

        // --- cheating
        public bool CheatedThisHand { get; set; }
        public CheatPlan? Plan { get; set; }
        public float WantCheatSince { get; set; } = -1f;
        public float NextCheatCheck { get; set; }
        public bool CheckedThisTurn { get; set; }

        // --- spotting: seconds of continuous watching per cheater, and a pending call-out
        public readonly float[] WatchSeconds = new float[HandState.SeatCount];
        public readonly bool[] Shrugged = new bool[HandState.SeatCount];
        public int CallOutTarget { get; set; } = -1;
        public float CallOutAt { get; set; }

        public void ResetForHand(float now)
        {
            CheatedThisHand = false;
            Plan = null;
            WantCheatSince = -1f;
            NextCheatCheck = now + 2f + (float)rng.NextDouble() * 3f;
            CheckedThisTurn = false;
            CallOutTarget = -1;
            for (int i = 0; i < WatchSeconds.Length; i++)
            {
                WatchSeconds[i] = 0f;
                Shrugged[i] = false;
            }
        }

        public double Roll() => rng.NextDouble();

        /// <summary>Picks a new place to look every second or two.</summary>
        /// <param name="lookingDownFor">Seconds each seat has been looking down (a tell).</param>
        public void UpdateGaze(float now, PlayerAvatar me, IReadOnlyList<PlayerAvatar> all, int seatOnTurn,
            IReadOnlyList<float> lookingDownFor)
        {
            if (now < nextGazeChange)
                return;

            bool myTurn = seatOnTurn == Seat;
            // (point, weight, min seconds, max seconds)
            var options = new List<(Vector3 point, float weight, float min, float max)>
            {
                (me.DiscardsPoint, 1.5f, 0.8f, 1.6f),                     // the discards (its side of the middle)
                (me.OwnTilesPoint, myTurn ? 8f : 2f, 1.0f, 2.2f),          // own tiles (busy on its turn)
            };
            if (seatOnTurn >= 0 && !myTurn)
                options.Add((all[seatOnTurn].OwnTilesPoint, 3f, 0.8f, 1.8f));
            foreach (PlayerAvatar other in all)
            {
                if (other.Seat == Seat)
                    continue;
                float suspicion = lookingDownFor[other.Seat] > 1.5f ? 1.5f : 0f;
                options.Add((other.BodyPoint, 1.2f + suspicion, 0.4f, 1.0f)); // short glance
            }

            float total = 0f;
            foreach (var o in options)
                total += o.weight;
            float pick = (float)rng.NextDouble() * total;
            foreach (var o in options)
            {
                pick -= o.weight;
                if (pick <= 0f)
                {
                    me.LookAt(o.point);
                    nextGazeChange = now + o.min + (float)rng.NextDouble() * (o.max - o.min);
                    return;
                }
            }
            nextGazeChange = now + 1f;
        }

        /// <summary>Look down at the pocket while cheating.</summary>
        public void LookAtPocket(PlayerAvatar me)
        {
            me.LookAt(me.PocketPoint);
            nextGazeChange = float.MaxValue;
        }

        public void ReleaseGaze(float now) => nextGazeChange = now;
    }
}
