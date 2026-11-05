using UnityEngine;

namespace MahjongNight.Game
{
    /// <summary>
    /// The human's view, steered by the cursor. The middle of the screen is a calm zone (so
    /// you can click tiles); pushing the cursor toward an edge turns your head that way —
    /// left/right to watch the other players, up to look across, and the bottom edge to look
    /// down into your lap (needed to reach into your pocket). The head turns smoothly.
    /// </summary>
    public sealed class HeadLook
    {
        public const float BasePitch = 25f;   // degrees down when looking at the table
        public const float MaxYaw = 75f;
        public const float MaxUp = 22f;
        public const float MaxDown = 58f;
        /// <summary>Pitch beyond which you're "looking down" at your lap.</summary>
        public const float LookingDownPitch = BasePitch + 32f;
        private const float TurnSpeed = 7f;

        private readonly Transform camera;
        private readonly Vector3 eye;
        private float yaw, pitch = BasePitch;
        private float targetYaw, targetPitch = BasePitch;

        public HeadLook(Transform camera, Vector3 eyePosition)
        {
            this.camera = camera;
            eye = eyePosition;
            Apply();
        }

        public float Yaw => yaw;
        public float Pitch => pitch;
        public bool LookingDown => pitch >= LookingDownPitch;
        public Quaternion Rotation => Quaternion.Euler(pitch, yaw, 0f);

        /// <summary>Sets where to look from a cursor position in GUI coordinates (origin top-left).</summary>
        public void AimFromCursor(Vector2 gui, float screenWidth, float screenHeight)
        {
            float nx = gui.x / screenWidth * 2f - 1f;
            float ny = 1f - gui.y / screenHeight * 2f;

            float side = Mathf.Clamp01((Mathf.Abs(nx) - 0.72f) / 0.28f);
            targetYaw = Mathf.Sign(nx) * Mathf.SmoothStep(0f, 1f, side) * MaxYaw;

            float down = Mathf.Clamp01((-ny - 0.8f) / 0.12f);   // bottom ~6% of the screen = fully down
            float up = Mathf.Clamp01((ny - 0.75f) / 0.25f);
            targetPitch = BasePitch + Mathf.SmoothStep(0f, 1f, down) * MaxDown - Mathf.SmoothStep(0f, 1f, up) * MaxUp;
        }

        /// <summary>Ease back to looking at the table.</summary>
        public void AimNeutral()
        {
            targetYaw = 0f;
            targetPitch = BasePitch;
        }

        public void Tick(float dt)
        {
            float k = 1f - Mathf.Exp(-TurnSpeed * dt);
            yaw = Mathf.Lerp(yaw, targetYaw, k);
            pitch = Mathf.Lerp(pitch, targetPitch, k);
            Apply();
        }

        private void Apply()
        {
            camera.position = eye;
            camera.rotation = Rotation;
        }
    }
}
