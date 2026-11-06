using UnityEngine;

namespace MahjongNight.Game
{
    /// <summary>
    /// The human's view, steered by the cursor. The middle of the screen is a calm zone (so
    /// you can click tiles); pushing the cursor toward an edge turns your head that way —
    /// left/right to watch the other players, up to look across, and the bottom edge to look
    /// down into your lap (needed to reach into your pocket). The head turns smoothly.
    /// </summary>
    /// <remarks>
    /// <b>Table view:</b> while held (right mouse / Tab) the camera glides up to a top-down view
    /// of the whole table — discards, melds, flowers, the wall. You're leaning over the table,
    /// so your head is pointed at the middle: you can't watch anyone's hands meanwhile.
    /// <b>Zoom:</b> the scroll wheel narrows the field of view in either view. Zoomed in on the
    /// table view, the cursor pans across the table (e.g. to read one player's discards).
    /// </remarks>
    public sealed class HeadLook
    {
        public const float BasePitch = 25f;   // degrees down when looking at the table
        public const float MaxYaw = 75f;
        public const float MaxUp = 22f;
        public const float MaxDown = 58f;
        /// <summary>Pitch beyond which you're "looking down" at your lap.</summary>
        public const float LookingDownPitch = BasePitch + 32f;

        public const float SeatFov = 65f;
        public const float TableViewFov = 55f;
        /// <summary>Most zoomed-in field of view, as a fraction of the normal one.</summary>
        public const float MaxZoomIn = 0.35f;

        private const float TurnSpeed = 7f;
        private const float TableViewSpeed = 6f;
        private const float ZoomSpeed = 10f;

        // Above the table, slightly toward your side so your own seat is at the bottom of the view.
        private static readonly Vector3 TableViewPosition = new Vector3(0f, 1.95f, -0.42f);
        private static readonly Vector3 TableViewTarget = new Vector3(0f, 0f, -0.02f);

        private readonly Camera camera;
        private readonly Vector3 eye;
        private float yaw, pitch = BasePitch;
        private float targetYaw, targetPitch = BasePitch;
        private float tableBlend, tableTarget;
        private float zoom = 1f, zoomTarget = 1f;
        private Vector3 tableAim = TableViewTarget, tableAimTarget = TableViewTarget;

        public HeadLook(Camera camera, Vector3 eyePosition)
        {
            this.camera = camera;
            eye = eyePosition;
            Apply();
        }

        public float Yaw => yaw;

        /// <summary>How far down your head is tilted (the table view counts as looking at the middle).</summary>
        public float Pitch => InTableView ? BasePitch : pitch;

        /// <summary>Looking down into your lap (not while leaning over the table).</summary>
        public bool LookingDown => tableBlend < 0.05f && pitch >= LookingDownPitch;

        /// <summary>Leaning over the table (at least halfway into the top-down view).</summary>
        public bool InTableView => tableBlend > 0.5f;

        /// <summary>Where your head points: at the table's middle while leaning over it.</summary>
        public Quaternion HeadRotation =>
            InTableView ? Quaternion.LookRotation(TableViewTarget - eye) : Quaternion.Euler(pitch, yaw, 0f);

        public float Zoom => zoom;

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

        /// <summary>
        /// In the table view, point at the part of the table under the cursor — more strongly the
        /// further you're zoomed in (no panning at all when not zoomed).
        /// </summary>
        public void AimTableView(Vector2 gui, float screenWidth, float screenHeight)
        {
            float nx = gui.x / screenWidth * 2f - 1f;
            float ny = 1f - gui.y / screenHeight * 2f;
            float strength = Mathf.Clamp01((1f - zoomTarget) / (1f - MaxZoomIn));
            tableAimTarget = TableViewTarget + new Vector3(nx * 0.75f, 0f, ny * 0.75f) * strength;
        }

        /// <summary>Lean over the table (true) or sit back (false).</summary>
        public void SetTableView(bool on) => tableTarget = on ? 1f : 0f;

        /// <summary>Zoom in (positive) or out (negative), e.g. from the scroll wheel.</summary>
        public void AddZoom(float amount) => zoomTarget = Mathf.Clamp(zoomTarget - amount, MaxZoomIn, 1f);

        public void ResetZoom() => zoomTarget = 1f;

        public void Tick(float dt)
        {
            float k = 1f - Mathf.Exp(-TurnSpeed * dt);
            yaw = Mathf.Lerp(yaw, targetYaw, k);
            pitch = Mathf.Lerp(pitch, targetPitch, k);
            tableBlend = Mathf.Lerp(tableBlend, tableTarget, 1f - Mathf.Exp(-TableViewSpeed * dt));
            zoom = Mathf.Lerp(zoom, zoomTarget, 1f - Mathf.Exp(-ZoomSpeed * dt));
            if (tableTarget < 0.5f)
                tableAimTarget = TableViewTarget;
            tableAim = Vector3.Lerp(tableAim, tableAimTarget, 1f - Mathf.Exp(-TableViewSpeed * dt));
            Apply();
        }

        private void Apply()
        {
            float t = Mathf.SmoothStep(0f, 1f, tableBlend);
            Quaternion seat = Quaternion.Euler(pitch, yaw, 0f);
            Quaternion top = Quaternion.LookRotation(tableAim - TableViewPosition);
            camera.transform.position = Vector3.Lerp(eye, TableViewPosition, t);
            camera.transform.rotation = Quaternion.Slerp(seat, top, t);
            camera.fieldOfView = Mathf.Lerp(SeatFov, TableViewFov, t) * zoom;
        }
    }
}
