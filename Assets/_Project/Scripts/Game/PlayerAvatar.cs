using UnityEngine;

namespace MahjongNight.Game
{
    /// <summary>Marks an avatar collider so clicks can be mapped back to the player (click to call out).</summary>
    public sealed class AvatarView : MonoBehaviour
    {
        public int Seat { get; set; }
    }

    /// <summary>Where a player's right hand is during the pocket swap.</summary>
    public enum HandPose
    {
        Rest,          // on the table by their tiles
        ToPocket,      // carrying a tile down to the pocket
        InPocket,      // rummaging
        FromPocket,    // carrying a tile back up to the hand
    }

    /// <summary>
    /// Greybox player: torso, a head that turns (nose + eyes show where they're looking), two
    /// arms that stretch to the hands, and a pocket on the right hip. Positions are in the
    /// seat's frame (local -Z points away from the table). The human's own head is hidden —
    /// the camera sits where their eyes are.
    /// </summary>
    public sealed class PlayerAvatar
    {
        private static readonly Vector3 TorsoPos = new Vector3(0f, 0.02f, -1.18f);
        private static readonly Vector3 NeckPos = new Vector3(0f, 0.36f, -1.15f);
        private static readonly Vector3 RightShoulder = new Vector3(0.17f, 0.26f, -1.13f);
        private static readonly Vector3 LeftShoulder = new Vector3(-0.17f, 0.26f, -1.13f);
        private static readonly Vector3 RightRest = new Vector3(0.3f, 0.035f, -0.86f);
        private static readonly Vector3 LeftRest = new Vector3(-0.32f, 0.035f, -0.86f);
        private static readonly Vector3 PocketPos = new Vector3(0.21f, -0.07f, -1.1f);
        private static readonly Vector3 PhonePos = new Vector3(0.05f, 0.12f, -0.97f);
        private const float EyeOffset = 0.1f;
        private const float HeadTurnSpeed = 6f;

        private readonly Quaternion frame;
        private readonly Transform head;
        private readonly Transform rightHand, leftHand, rightArm, leftArm;
        private readonly Transform carriedTile;
        private readonly Transform root;
        private readonly GameObject phone;
        private Vector3 gazeTarget;

        private readonly Vector3 origin;

        /// <param name="origin">Centre of the table this player sits at.</param>
        public PlayerAvatar(Transform parent, int seat, int relative, Color color, bool firstPerson, Vector3 origin)
        {
            this.origin = origin;
            Seat = seat;
            frame = Quaternion.Euler(0f, -90f * relative, 0f);
            root = new GameObject($"Avatar{seat}").transform;
            root.SetParent(parent, false);

            Material body = TileFactory.CreateMaterial(color);
            Material skin = TileFactory.CreateMaterial(new Color(0.93f, 0.78f, 0.62f));
            Material dark = TileFactory.CreateMaterial(new Color(0.08f, 0.08f, 0.08f));
            Material pocketMat = TileFactory.CreateMaterial(color * 0.55f);

            GameObject torso = Part(PrimitiveType.Capsule, root, "Torso", body, firstPerson);
            torso.transform.position = World(TorsoPos);
            torso.transform.rotation = frame;
            torso.transform.localScale = new Vector3(0.34f, 0.3f, 0.22f);

            GameObject pocket = Part(PrimitiveType.Cube, root, "Pocket", pocketMat, true);
            pocket.transform.position = World(PocketPos + new Vector3(0.02f, 0f, 0f));
            pocket.transform.rotation = frame;
            pocket.transform.localScale = new Vector3(0.03f, 0.09f, 0.09f);

            head = new GameObject("Head").transform;
            head.SetParent(root, false);
            head.position = World(NeckPos);
            GameObject skull = Part(PrimitiveType.Sphere, head, "Skull", skin, firstPerson);
            skull.transform.localPosition = new Vector3(0f, 0.09f, 0f);
            skull.transform.localScale = Vector3.one * 0.2f;
            GameObject nose = Part(PrimitiveType.Cube, head, "Nose", skin, true);
            nose.transform.localPosition = new Vector3(0f, 0.08f, 0.1f);
            nose.transform.localScale = new Vector3(0.035f, 0.035f, 0.06f);
            foreach (float side in new[] { -1f, 1f })
            {
                GameObject eye = Part(PrimitiveType.Sphere, head, "Eye", dark, true);
                eye.transform.localPosition = new Vector3(0.04f * side, 0.115f, 0.087f);
                eye.transform.localScale = Vector3.one * 0.03f;
            }
            if (firstPerson)
                foreach (Renderer r in head.GetComponentsInChildren<Renderer>())
                    r.enabled = false;

            rightArm = Part(PrimitiveType.Cube, root, "RightArm", body, true).transform;
            leftArm = Part(PrimitiveType.Cube, root, "LeftArm", body, true).transform;
            rightHand = Part(PrimitiveType.Sphere, root, "RightHand", skin, true).transform;
            leftHand = Part(PrimitiveType.Sphere, root, "LeftHand", skin, true).transform;
            rightHand.localScale = leftHand.localScale = Vector3.one * 0.065f;

            carriedTile = Part(PrimitiveType.Cube, rightHand, "CarriedTile", TileFactory.CreateMaterial(new Color(0.96f, 0.94f, 0.86f)), true).transform;
            carriedTile.localPosition = new Vector3(0f, 0.6f, 0f);
            carriedTile.localScale = new Vector3(0.6f, 0.8f, 0.45f);
            carriedTile.gameObject.SetActive(false);

            phone = Part(PrimitiveType.Cube, root, "Phone", dark, true);
            phone.transform.position = World(PhonePos);
            phone.transform.rotation = frame * Quaternion.Euler(-50f, 0f, 0f);
            phone.transform.localScale = new Vector3(0.07f, 0.13f, 0.012f);
            phone.SetActive(false);

            gazeTarget = Vector3.zero;
            head.rotation = Quaternion.LookRotation(gazeTarget - EyePosition);
            SetRightHand(HandPose.Rest, 0f, 0f);
            UpdateArms();
        }

        public int Seat { get; }

        /// <summary>World position of the eyes.</summary>
        public Vector3 EyePosition => head.position + Vector3.up * EyeOffset;

        /// <summary>World direction the player is looking.</summary>
        public Vector3 Gaze => head.forward;

        /// <summary>Degrees the head is tilted down (0 = level, 90 = straight down).</summary>
        public float PitchDown => Mathf.Asin(Mathf.Clamp(-head.forward.y, -1f, 1f)) * Mathf.Rad2Deg;

        /// <summary>What others look at to see this player's hands and pocket.</summary>
        public Vector3 CheatAnchor => World(new Vector3(0.15f, 0.1f, -1.0f));

        /// <summary>Upper body, where others glance when looking at this player.</summary>
        public Vector3 BodyPoint => World(new Vector3(0f, 0.2f, -1.05f));

        public Vector3 OwnTilesPoint => World(new Vector3(0f, 0.03f, -0.78f));
        /// <summary>The discards on this player's side of the middle (where they glance at the table).</summary>
        public Vector3 DiscardsPoint => World(new Vector3(0f, 0.02f, -0.42f));
        public Vector3 PocketPoint => World(PocketPos);
        public Vector3 TagPoint => World(NeckPos + new Vector3(0f, 0.3f, 0f));

        /// <summary>Where the player's phone is held (they look here while texting or on a call).</summary>
        public Vector3 PhonePoint => World(PhonePos);

        /// <summary>Show or hide the phone in their hands.</summary>
        public void SetPhone(bool on) => phone.SetActive(on);

        /// <summary>Show or hide the whole body (the human's body hides while they walk around).</summary>
        public void SetVisible(bool visible) => root.gameObject.SetActive(visible);

        /// <summary>Turn the head toward <paramref name="worldTarget"/> (smoothly, in <see cref="Tick"/>).</summary>
        public void LookAt(Vector3 worldTarget) => gazeTarget = worldTarget;

        /// <summary>Snap the head to a rotation (the human's head follows the camera).</summary>
        public void SetHeadRotation(Quaternion rotation) => head.rotation = rotation;

        /// <summary>Place the right hand for the cheat animation. <paramref name="t"/> is 0..1 within the step.</summary>
        public void SetRightHand(HandPose pose, float t, float time)
        {
            Vector3 local;
            bool carrying = false;
            switch (pose)
            {
                case HandPose.ToPocket:
                    local = Arc(RightRest, PocketPos, t);
                    carrying = true;
                    break;
                case HandPose.InPocket:
                    local = PocketPos + new Vector3(0f, 0.012f * Mathf.Sin(time * 18f), 0.01f * Mathf.Sin(time * 11f));
                    break;
                case HandPose.FromPocket:
                    local = Arc(PocketPos, RightRest, t);
                    carrying = true;
                    break;
                default:
                    local = RightRest;
                    break;
            }
            rightHand.position = World(local);
            carriedTile.gameObject.SetActive(carrying);
        }

        public void Tick(float dt, bool followGaze)
        {
            if (followGaze)
            {
                Vector3 dir = gazeTarget - EyePosition;
                if (dir.sqrMagnitude > 1e-6f)
                    head.rotation = Quaternion.Slerp(head.rotation, Quaternion.LookRotation(dir), 1f - Mathf.Exp(-HeadTurnSpeed * dt));
            }
            leftHand.position = World(LeftRest);
            UpdateArms();
        }

        private void UpdateArms()
        {
            Stretch(rightArm, World(RightShoulder), rightHand.position);
            Stretch(leftArm, World(LeftShoulder), leftHand.position);
        }

        private static void Stretch(Transform arm, Vector3 from, Vector3 to)
        {
            Vector3 d = to - from;
            arm.position = (from + to) * 0.5f;
            if (d.sqrMagnitude > 1e-6f)
                arm.rotation = Quaternion.LookRotation(d);
            arm.localScale = new Vector3(0.055f, 0.055f, d.magnitude);
        }

        /// <summary>A path that lifts over the table edge rather than going through it.</summary>
        private static Vector3 Arc(Vector3 from, Vector3 to, float t)
        {
            t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
            Vector3 p = Vector3.Lerp(from, to, t);
            p.y += Mathf.Sin(t * Mathf.PI) * 0.06f;
            return p;
        }

        private Vector3 World(Vector3 seatLocal) => origin + frame * seatLocal;

        private GameObject Part(PrimitiveType type, Transform parent, string name, Material material, bool removeCollider)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.GetComponent<Renderer>().sharedMaterial = material;
            if (removeCollider)
                Object.Destroy(go.GetComponent<Collider>());
            else
                go.AddComponent<AvatarView>().Seat = Seat;
            return go;
        }
    }
}
