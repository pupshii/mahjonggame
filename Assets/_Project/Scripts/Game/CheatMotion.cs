using MahjongNight.Core;

namespace MahjongNight.Game
{
    public enum CheatStep
    {
        None,
        /// <summary>1. Slipping a tile from the hand into the pocket (timed).</summary>
        Stashing,
        /// <summary>Hand in the pocket, waiting for the player to choose a pocket tile.</summary>
        ChoosingPocketTile,
        /// <summary>2. Pulling the chosen tile out of the pocket (timed).</summary>
        PullingOut,
        /// <summary>Holding the tile by the pocket, waiting for the player to slip it in.</summary>
        ReadyToReturn,
        /// <summary>3. Slipping the tile into the hand (timed); the swap happens at the end.</summary>
        Returning,
        Done
    }

    /// <summary>
    /// One player's pocket swap in progress. While <see cref="Exposed"/> is true, anyone looking
    /// at them long enough can spot it.
    /// </summary>
    public sealed class CheatMotion
    {
        /// <summary>Seconds after finishing during which a call-out still counts.</summary>
        public const float GraceSeconds = 1f;

        public CheatMotion(int seat, Tile handTile, float stepSeconds, float now)
        {
            Seat = seat;
            HandTile = handTile;
            StepSeconds = stepSeconds;
            StartedAt = now;
            Begin(CheatStep.Stashing, now);
        }

        public int Seat { get; }
        public Tile HandTile { get; }
        public int PocketIndex { get; set; } = -1;
        public float StepSeconds { get; }
        public float StartedAt { get; }
        public CheatStep Step { get; private set; }
        public float StepStartedAt { get; private set; }
        public float FinishedAt { get; private set; } = -1f;
        /// <summary>The swap was applied to the hand.</summary>
        public bool Completed { get; private set; }

        public bool InProgress => Step != CheatStep.None && Step != CheatStep.Done;

        /// <summary>Whether a call-out right now would catch this player.</summary>
        public bool Exposed(float now) => InProgress || (FinishedAt >= 0f && now - FinishedAt <= GraceSeconds);

        public float Progress(float now) =>
            StepSeconds <= 0f ? 1f : UnityEngine.Mathf.Clamp01((now - StepStartedAt) / StepSeconds);

        public bool StepTimeUp(float now) => now - StepStartedAt >= StepSeconds;

        public void Begin(CheatStep step, float now)
        {
            Step = step;
            StepStartedAt = now;
        }

        public void Finish(float now, bool completed)
        {
            Step = CheatStep.Done;
            FinishedAt = now;
            Completed = completed;
        }

        public HandPose Pose
        {
            get
            {
                switch (Step)
                {
                    case CheatStep.Stashing: return HandPose.ToPocket;
                    case CheatStep.ChoosingPocketTile:
                    case CheatStep.PullingOut:
                    case CheatStep.ReadyToReturn: return HandPose.InPocket;
                    case CheatStep.Returning: return HandPose.FromPocket;
                    default: return HandPose.Rest;
                }
            }
        }
    }
}
