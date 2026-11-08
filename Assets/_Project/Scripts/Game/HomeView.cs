using System.Collections.Generic;
using MahjongNight.Core;
using UnityEngine;

namespace MahjongNight.Game
{
    /// <summary>
    /// Your flat, a short (teleporting) walk from the club: a couch, the dog's bowl, a bag of
    /// dog food, and Latte. Built off to the side of the club and registered with the room as
    /// a separate walkable area.
    /// </summary>
    public sealed class HomeView
    {
        private const float HalfX = 3f, HalfZ = 2.5f, WallHeight = 2.6f;

        public HomeView(Transform parent, RoomView room)
        {
            var root = new GameObject("Home").transform;
            root.SetParent(parent, false);
            float y0 = RoomView.FloorY;
            var o = new Vector3(0f, y0, room.HalfSize + 25f);   // well clear of the club
            Origin = o;

            Material floor = TileFactory.CreateMaterial(new Color(0.55f, 0.42f, 0.3f));
            Material wall = TileFactory.CreateMaterial(new Color(0.78f, 0.74f, 0.62f));
            Material couch = TileFactory.CreateMaterial(new Color(0.35f, 0.42f, 0.6f));
            Material rug = TileFactory.CreateMaterial(new Color(0.65f, 0.25f, 0.25f));
            Material bowlMat = TileFactory.CreateMaterial(new Color(0.85f, 0.2f, 0.2f));
            Material bag = TileFactory.CreateMaterial(new Color(0.9f, 0.75f, 0.25f));
            Material door = TileFactory.CreateMaterial(new Color(0.3f, 0.16f, 0.08f));

            RoomView.Box(root, "HomeFloor", o + new Vector3(0f, -0.01f, 0f), new Vector3(HalfX * 2f, 0.02f, HalfZ * 2f), floor);
            float mid = y0 + WallHeight / 2f;
            RoomView.Box(root, "HomeWallN", new Vector3(o.x, mid, o.z + HalfZ), new Vector3(HalfX * 2f, WallHeight, 0.1f), wall);
            RoomView.Box(root, "HomeWallS", new Vector3(o.x, mid, o.z - HalfZ), new Vector3(HalfX * 2f, WallHeight, 0.1f), wall);
            RoomView.Box(root, "HomeWallE", new Vector3(o.x + HalfX, mid, o.z), new Vector3(0.1f, WallHeight, HalfZ * 2f), wall);
            RoomView.Box(root, "HomeWallW", new Vector3(o.x - HalfX, mid, o.z), new Vector3(0.1f, WallHeight, HalfZ * 2f), wall);
            RoomView.Sign(root, "HOME SWEET HOME  家", new Vector3(o.x, y0 + 2.0f, o.z + HalfZ - 0.06f), Quaternion.identity);

            var obstacles = new List<Rect>();

            // Front door (south wall) — back to the club.
            GameObject d = RoomView.Box(root, "HomeDoor", new Vector3(o.x, y0 + 1.05f, o.z - HalfZ + 0.06f), new Vector3(1.0f, 2.1f, 0.08f), door);
            d.AddComponent<Interactable>().Kind = InteractKind.HomeDoor;
            RoomView.Sign(root, "BACK TO THE CLUB", new Vector3(o.x, y0 + 2.3f, o.z - HalfZ + 0.08f), Quaternion.Euler(0f, 180f, 0f));
            Spawn = new Vector3(o.x, y0, o.z - HalfZ + 0.7f);

            // Couch (west wall), slightly chewed.
            RoomView.Box(root, "Couch", new Vector3(o.x - HalfX + 0.45f, y0 + 0.25f, o.z + 0.2f), new Vector3(0.8f, 0.5f, 1.9f), couch);
            RoomView.Box(root, "CouchBack", new Vector3(o.x - HalfX + 0.12f, y0 + 0.6f, o.z + 0.2f), new Vector3(0.18f, 0.7f, 1.9f), couch);
            RoomView.Box(root, "ChewedCushion", new Vector3(o.x - HalfX + 0.5f, y0 + 0.53f, o.z + 0.75f), new Vector3(0.35f, 0.1f, 0.3f),
                TileFactory.CreateMaterial(new Color(0.95f, 0.95f, 0.9f)));
            obstacles.Add(new Rect(o.x - HalfX, o.z - 0.8f, 0.9f, 2.0f));

            // Rug, bowl and food bag (north-east).
            RoomView.Box(root, "Rug", new Vector3(o.x + 0.6f, y0 + 0.005f, o.z + 1.2f), new Vector3(2.0f, 0.01f, 1.3f), rug);
            Vector3 bowlPos = new Vector3(o.x + 1.3f, y0, o.z + 1.7f);
            GameObject bowl = RoomView.Cylinder(root, "DogBowl", bowlPos + new Vector3(0f, 0.04f, 0f), new Vector3(0.32f, 0.04f, 0.32f), bowlMat);
            bowl.AddComponent<Interactable>().Kind = InteractKind.DogBowl;
            GameObject kibble = RoomView.Cylinder(root, "BowlFood", bowlPos + new Vector3(0f, 0.085f, 0f), new Vector3(0.26f, 0.006f, 0.26f),
                TileFactory.CreateMaterial(new Color(0.45f, 0.28f, 0.15f)));
            Object.Destroy(kibble.GetComponent<Collider>());
            kibble.GetComponent<Renderer>().sharedMaterial = new Material(kibble.GetComponent<Renderer>().sharedMaterial);
            kibble.SetActive(false);
            obstacles.Add(new Rect(bowlPos.x - 0.2f, bowlPos.z - 0.2f, 0.4f, 0.4f));

            Vector3 bagPos = new Vector3(o.x + HalfX - 0.45f, y0, o.z + HalfZ - 0.4f);
            GameObject bagGo = RoomView.Box(root, "DogFoodBag", bagPos + new Vector3(0f, 0.32f, 0f), new Vector3(0.45f, 0.64f, 0.28f), bag);
            bagGo.AddComponent<Interactable>().Kind = InteractKind.DogFood;
            RoomView.Sign(root, "DOG FOOD", bagPos + new Vector3(0f, 0.9f, 0f), Quaternion.identity).characterSize = 0.018f;
            obstacles.Add(new Rect(bagPos.x - 0.3f, bagPos.z - 0.2f, 0.6f, 0.4f));

            room.AddWalkArea(new Rect(o.x - HalfX + 0.05f, o.z - HalfZ + 0.05f, HalfX * 2f - 0.1f, HalfZ * 2f - 0.1f), obstacles);

            Dog = new DogView(root, new Vector3(o.x + 0.4f, y0, o.z + 1.1f), bowlPos, kibble);
        }

        public Vector3 Origin { get; }
        /// <summary>Where you appear when you walk in (facing into the flat, +z).</summary>
        public Vector3 Spawn { get; }
        public DogView Dog { get; }
    }

    /// <summary>
    /// Latte: a little black-and-white border collie made of primitives. Wags faster the happier they are, droops when starving,
    /// trots to the bowl to eat when you fill it, and hops when you pet them.
    /// </summary>
    public sealed class DogView
    {
        private readonly Transform root, head, tail;
        private readonly TextMesh label;
        private readonly GameObject food;
        private readonly Material foodMat;
        private readonly Vector3 restPos, eatPos;
        private readonly Vector3 bowlPos;
        private float clock, hopUntil;
        private float yaw;

        public DogView(Transform parent, Vector3 restPosition, Vector3 bowlPosition, GameObject bowlFood)
        {
            restPos = restPosition;
            bowlPos = bowlPosition;
            Vector3 toRest = restPosition - bowlPosition;
            toRest.y = 0f;
            eatPos = bowlPosition + toRest.normalized * 0.42f;
            food = bowlFood;
            foodMat = bowlFood.GetComponent<Renderer>().sharedMaterial;

            root = new GameObject("Latte").transform;
            root.SetParent(parent, false);
            root.position = restPosition;

            // A black-and-white border collie: black coat; white collar, chest, muzzle, blaze,
            // front legs, paws and tail tip; amber eyes; semi-pricked ears with folded tips.
            Material black = TileFactory.CreateMaterial(new Color(0.06f, 0.06f, 0.07f));
            Material white = TileFactory.CreateMaterial(new Color(0.95f, 0.95f, 0.93f));
            Material nose = TileFactory.CreateMaterial(new Color(0.02f, 0.02f, 0.02f));
            Material amber = TileFactory.CreateMaterial(new Color(0.6f, 0.38f, 0.12f));

            GameObject body = Part(PrimitiveType.Capsule, root, "Body", new Vector3(0f, 0.33f, -0.02f), new Vector3(0.25f, 0.28f, 0.25f), black);
            body.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Part(PrimitiveType.Sphere, root, "Chest", new Vector3(0f, 0.32f, 0.2f), new Vector3(0.19f, 0.22f, 0.15f), white);
            GameObject collar = Part(PrimitiveType.Cylinder, root, "Collar", new Vector3(0f, 0.42f, 0.2f), new Vector3(0.2f, 0.04f, 0.2f), white);
            collar.transform.localRotation = Quaternion.Euler(-35f, 0f, 0f);
            for (int i = 0; i < 4; i++)
            {
                bool front = i < 2;
                float x = i % 2 == 0 ? -0.07f : 0.07f, z = front ? 0.16f : -0.17f;
                Part(PrimitiveType.Cylinder, root, "Leg" + i, new Vector3(x, 0.11f, z), new Vector3(0.065f, 0.11f, 0.065f), front ? white : black);
                Part(PrimitiveType.Cylinder, root, "Paw" + i, new Vector3(x, 0.025f, z + 0.01f), new Vector3(0.075f, 0.025f, 0.08f), white);
            }

            head = new GameObject("HeadPivot").transform;
            head.SetParent(root, false);
            head.localPosition = new Vector3(0f, 0.48f, 0.26f);
            Part(PrimitiveType.Sphere, head, "Head", Vector3.zero, new Vector3(0.2f, 0.19f, 0.21f), black);
            Part(PrimitiveType.Cube, head, "Muzzle", new Vector3(0f, -0.035f, 0.13f), new Vector3(0.085f, 0.07f, 0.13f), white);
            GameObject blaze = Part(PrimitiveType.Cube, head, "Blaze", new Vector3(0f, 0.035f, 0.085f), new Vector3(0.035f, 0.11f, 0.03f), white);
            blaze.transform.localRotation = Quaternion.Euler(-35f, 0f, 0f);
            Part(PrimitiveType.Sphere, head, "Nose", new Vector3(0f, -0.01f, 0.2f), Vector3.one * 0.038f, nose);
            Part(PrimitiveType.Sphere, head, "EyeL", new Vector3(-0.048f, 0.03f, 0.088f), Vector3.one * 0.03f, amber);
            Part(PrimitiveType.Sphere, head, "EyeR", new Vector3(0.048f, 0.03f, 0.088f), Vector3.one * 0.03f, amber);
            for (int side = -1; side <= 1; side += 2)
            {
                string n = side < 0 ? "L" : "R";
                GameObject ear = Part(PrimitiveType.Cube, head, "Ear" + n, new Vector3(0.068f * side, 0.1f, -0.01f), new Vector3(0.05f, 0.075f, 0.022f), black);
                ear.transform.localRotation = Quaternion.Euler(0f, 0f, -15f * side);
                GameObject tip = Part(PrimitiveType.Cube, head, "EarTip" + n, new Vector3(0.08f * side, 0.14f, 0.012f), new Vector3(0.042f, 0.035f, 0.02f), black);
                tip.transform.localRotation = Quaternion.Euler(55f, 0f, -15f * side);   // the folded-over tip
            }

            // A long, bushy tail that hangs down from the pivot and lifts when happy (see Tick).
            tail = new GameObject("TailPivot").transform;
            tail.SetParent(root, false);
            tail.localPosition = new Vector3(0f, 0.38f, -0.27f);
            Part(PrimitiveType.Cylinder, tail, "Tail", new Vector3(0f, -0.12f, 0f), new Vector3(0.05f, 0.12f, 0.05f), black);
            Part(PrimitiveType.Sphere, tail, "TailTip", new Vector3(0f, -0.25f, 0f), new Vector3(0.06f, 0.08f, 0.06f), white);

            var labelGo = new GameObject("LatteLabel");
            labelGo.transform.SetParent(parent, false);
            label = labelGo.AddComponent<TextMesh>();
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.font = font;
            label.fontSize = 64;
            label.characterSize = 0.012f;
            label.anchor = TextAnchor.LowerCenter;
            label.alignment = TextAlignment.Center;
            labelGo.GetComponent<MeshRenderer>().sharedMaterial = font.material;
        }

        /// <summary>Every part is pettable.</summary>
        private static GameObject Part(PrimitiveType type, Transform parent, string name, Vector3 localPos, Vector3 scale, Material m)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = m;
            go.AddComponent<Interactable>().Kind = InteractKind.Dog;
            return go;
        }

        /// <summary>A happy hop (when you pet them).</summary>
        public void Hop() => hopUntil = clock + 1.2f;

        public void Tick(float dt, Dog dog, Vector3 cameraPosition)
        {
            clock += dt;
            DogMood mood = dog.Mood;
            bool eating = dog.IsEating;

            // Where to be, and which way to face: at the bowl while eating, else on the rug watching you.
            Vector3 target = eating ? eatPos : restPos;
            Vector3 pos = Vector3.MoveTowards(root.position, target, 1.5f * dt);
            Vector3 look = (eating ? bowlPos : cameraPosition) - pos;
            look.y = 0f;
            if (look.sqrMagnitude > 0.0001f)
                yaw = Mathf.MoveTowardsAngle(yaw, Quaternion.LookRotation(look).eulerAngles.y, 240f * dt);
            bool hopping = clock < hopUntil;
            float hop = hopping ? Mathf.Abs(Mathf.Sin(clock * 9f)) * 0.08f : 0f;
            root.position = new Vector3(pos.x, target.y + hop, pos.z);
            root.rotation = Quaternion.Euler(0f, yaw, 0f);

            // Head: dips into the bowl while eating; a sad droop when starving; a hopeful tilt otherwise.
            float headPitch = eating ? 35f + Mathf.Sin(clock * 10f) * 8f
                : mood == DogMood.Starving ? 20f
                : mood == DogMood.Hungry ? Mathf.Sin(clock * 3f) * 10f - 10f   // barking at the door
                : -5f;
            float headRoll = mood == DogMood.Content && !eating ? Mathf.Sin(clock * 0.8f) * 10f : 0f;
            head.localRotation = Quaternion.Euler(headPitch, 0f, headRoll);

            // Tail: the happier, the faster.
            float speed, amp, lift;
            // (lift: 0 = hanging straight down, 90 = straight out behind, more = raised)
            if (hopping || eating) { speed = 16f; amp = 40f; lift = 105f; }
            else if (mood == DogMood.Content) { speed = 10f; amp = 30f; lift = 85f; }
            else if (mood == DogMood.Peckish) { speed = 5f; amp = 20f; lift = 60f; }
            else if (mood == DogMood.Hungry) { speed = 3f; amp = 10f; lift = 35f; }
            else { speed = 0f; amp = 0f; lift = 8f; }                      // tucked down
            tail.localRotation = Quaternion.Euler(lift, Mathf.Sin(clock * speed) * amp, 0f);

            // The bowl has food in it while they eat (soup-coloured if it's a treat).
            food.SetActive(eating);
            if (eating)
                foodMat.color = dog.TreatLeft > 0 ? new Color(0.95f, 0.8f, 0.45f) : new Color(0.45f, 0.28f, 0.15f);

            // "LATTE / hungry" above their head, facing you.
            Vector3 lp = root.position + Vector3.up * 0.75f;
            label.transform.position = lp;
            Vector3 away = lp - cameraPosition;
            if (away.sqrMagnitude > 0.0001f)
                label.transform.rotation = Quaternion.LookRotation(away);
            label.text = "LATTE\n" + (eating ? "nom nom nom" : Dog.Describe(mood));
            label.color = mood == DogMood.Content ? new Color(0.6f, 1f, 0.6f)
                : mood == DogMood.Peckish ? new Color(1f, 0.95f, 0.6f)
                : mood == DogMood.Hungry ? new Color(1f, 0.7f, 0.3f)
                : new Color(1f, 0.3f, 0.25f);
        }
    }
}
