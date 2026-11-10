using MahjongNight.Core;
using UnityEngine;

namespace MahjongNight.Game
{
    /// <summary>
    /// First-person walking for when you get up from the table: mouse to look (cursor locked),
    /// WASD to move, Shift to sprint. Collides with the room's walls and furniture.
    /// Uses Unity's classic Input Manager (the project's active input handling).
    /// </summary>
    public sealed class Walker
    {
        public const float EyeHeight = 1.6f;
        public const float WalkSpeed = 1.7f;
        public const float SprintSpeed = 3.6f;
        public const float Radius = 0.25f;
        public const float Fov = 70f;
        private const float MouseSensitivity = 2.2f;

        private readonly RoomView room;
        private Vector3 feet;
        private float yaw, pitch;

        /// <summary>Touch joystick (-1..1; x = strafe, y = forward). Pushed all the way = sprint.</summary>
        public Vector2 TouchMove { get; set; }
        /// <summary>Touch look this frame, in degrees (x = turn right, y = look up). Used up by <see cref="Tick"/>.</summary>
        public Vector2 TouchLook { get; set; }
        /// <summary>Turn with the mouse (off with touch controls).</summary>
        public bool UseMouse { get; set; } = true;

        public Walker(RoomView room)
        {
            this.room = room;
        }

        public Vector3 Eye => feet + Vector3.up * EyeHeight;
        public Quaternion Rotation => Quaternion.Euler(pitch, yaw, 0f);
        public Vector3 FeetPosition => feet;

        public void Place(Vector3 feetPosition, float yawDegrees)
        {
            feet = feetPosition;
            yaw = yawDegrees;
            pitch = 10f;
        }

        /// <summary>Reads input and moves. Returns how hard you're exerting yourself (for hunger/thirst).</summary>
        public Exertion Tick(float dt, bool canMove)
        {
            Vector2 touchLook = TouchLook;
            TouchLook = Vector2.zero;
            if (!canMove)
                return Exertion.Resting;

            if (UseMouse)
            {
                yaw += Input.GetAxis("Mouse X") * MouseSensitivity;
                pitch = Mathf.Clamp(pitch - Input.GetAxis("Mouse Y") * MouseSensitivity, -80f, 85f);
            }
            yaw += touchLook.x;
            pitch = Mathf.Clamp(pitch - touchLook.y, -80f, 85f);

            float forward = Mathf.Clamp((Input.GetKey(KeyCode.W) ? 1f : 0f) - (Input.GetKey(KeyCode.S) ? 1f : 0f) + TouchMove.y, -1f, 1f);
            float strafe = Mathf.Clamp((Input.GetKey(KeyCode.D) ? 1f : 0f) - (Input.GetKey(KeyCode.A) ? 1f : 0f) + TouchMove.x, -1f, 1f);
            if (Mathf.Abs(forward) < 0.05f && Mathf.Abs(strafe) < 0.05f)
                return Exertion.Resting;

            bool sprint = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) || TouchMove.magnitude > 0.92f;
            float rad = yaw * Mathf.Deg2Rad;
            var fwd = new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad));
            var right = new Vector3(Mathf.Cos(rad), 0f, -Mathf.Sin(rad));
            Vector3 move = fwd * forward + right * strafe;
            if (move.sqrMagnitude > 1f)
                move = move.normalized;
            move *= (sprint ? SprintSpeed : WalkSpeed) * dt;

            // Slide along walls: try each axis separately.
            if (!room.Blocked(feet.x + move.x, feet.z, Radius))
                feet.x += move.x;
            if (!room.Blocked(feet.x, feet.z + move.z, Radius))
                feet.z += move.z;
            return sprint ? Exertion.Sprinting : Exertion.Walking;
        }

        public void ApplyTo(Camera cam)
        {
            cam.transform.position = Eye;
            cam.transform.rotation = Rotation;
            cam.fieldOfView = Fov;
        }
    }
}
