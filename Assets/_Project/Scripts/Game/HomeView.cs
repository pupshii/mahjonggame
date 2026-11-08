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
        public const float BallRadius = 0.07f;

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

            // Latte greets you just inside the door.
            // Latte's blue ball, on the rug. It rolls about inside the play area (in front of the couch).
            GameObject ball = RoomView.Prim(PrimitiveType.Sphere, root, "BlueBall", new Vector3(o.x + 0.9f, y0 + BallRadius, o.z + 0.9f),
                Vector3.one * BallRadius * 2f, TileFactory.CreateMaterial(new Color(0.15f, 0.45f, 1f)));
            ball.AddComponent<Interactable>().Kind = InteractKind.Ball;
            GameObject stripe = RoomView.Prim(PrimitiveType.Cylinder, ball.transform, "Stripe", ball.transform.position,
                Vector3.one, TileFactory.CreateMaterial(new Color(1f, 0.9f, 0.3f)));
            stripe.transform.localScale = new Vector3(1.03f, 0.09f, 1.03f);   // a yellow band, so you can see it roll
            stripe.AddComponent<Interactable>().Kind = InteractKind.Ball;
            var playArea = new Rect(o.x - HalfX + 1.0f, o.z - HalfZ + 0.3f, HalfX * 2f - 1.3f, HalfZ * 2f - 0.6f);

            // Latte greets you just inside the door.
            Dog = new DogView(root, new Vector3(o.x + 0.4f, y0, o.z + 1.1f), bowlPos, Spawn + new Vector3(0f, 0f, 0.95f), kibble,
                ball.transform, playArea);
        }

        public Vector3 Origin { get; }
        /// <summary>Where you appear when you walk in (facing into the flat, +z).</summary>
        public Vector3 Spawn { get; }
        public DogView Dog { get; }
    }

    /// <summary>
    /// Latte, a black-and-white border collie built from primitives to look like the real one:
    /// black coat, a wide white blaze that opens into an all-white muzzle, floppy black ears,
    /// brown eyes, a big fluffy white bib, white front legs with a few black flecks, white paws,
    /// and a long bushy tail with a white tip.
    /// <para>
    /// When you walk in, Latte runs to the door, stands up on their hind legs and claps their
    /// front paws like a person — "FINALLY. You're home. Now. Food." — and keeps doing it by the
    /// bowl until you feed them. They wag faster the happier they are, droop when starving, and
    /// hop when you pet them.
    /// </para>
    /// <para>
    /// Latte walks like a person: whenever they go anywhere they stand up on their hind legs and
    /// stroll, arms swinging, with a little waddle — then drop to all fours when they get there.
    /// They play with their blue ball: throw it (E) and they fetch it back to you; left alone
    /// they wander over now and then and boop it across the rug.
    /// </para>
    /// </summary>
    public sealed class DogView
    {
        private const float RearAngle = 65f;       // how far up the body tips when standing to clap
        private const float WalkUpright = 82f;     // ...and when walking like a person
        private const float GreetSeconds = 5.5f;   // running to the door + clapping
        private const float BegSeconds = 2.6f;     // a burst of clapping by the bowl
        private const float BegEvery = 6f;

        private readonly Transform root, hips, head, tail;
        private readonly Transform[] shoulders = new Transform[2];
        private readonly TextMesh label;
        private readonly GameObject food;
        private readonly Material foodMat;
        private readonly Vector3 restPos, eatPos, greetPos, bowlPos;
        private float clock, hopUntil, yaw;
        private float rear;                  // 0 = on all fours, 1 = standing up
        private float greetUntil = -1f, clapUntil = -1f, nextBeg;
        private string shout;

        private readonly Transform[] hindHips = new Transform[2];
        private float gait;                  // walking cycle (radians)
        private float walk;                  // 0 = still, 1 = walking (smoothed)

        // The blue ball: rolls on the floor, flies when thrown, or sits in Latte's mouth.
        public enum Fetch { None, Chasing, Returning }
        private readonly Transform ball;
        private readonly Rect playArea;
        private Vector3 ballPos, ballVel;
        private bool ballInAir, ballHeld;
        private Vector3 airFrom, airTo;
        private float airT, airTime;
        private Fetch fetch;
        private float nextBoop = 8f, celebrateUntil = -1f;
        private readonly System.Random rng = new System.Random(7);

        public bool BallInMouth => ballHeld;
        /// <summary>What Latte is up to right now (for the Latte Cam app).</summary>
        public string Doing { get; private set; } = "lying on the rug";
        public Fetch Fetching => fetch;

        public DogView(Transform parent, Vector3 restPosition, Vector3 bowlPosition, Vector3 greetPosition, GameObject bowlFood,
                       Transform ballTransform, Rect ballArea)
        {
            ball = ballTransform;
            playArea = ballArea;
            ballPos = ball.position;
            restPos = restPosition;
            bowlPos = bowlPosition;
            greetPos = greetPosition;
            Vector3 toRest = restPosition - bowlPosition;
            toRest.y = 0f;
            eatPos = bowlPosition + toRest.normalized * 0.42f;
            food = bowlFood;
            foodMat = bowlFood.GetComponent<Renderer>().sharedMaterial;

            root = new GameObject("Latte").transform;
            root.SetParent(parent, false);
            root.position = restPosition;

            Material black = TileFactory.CreateMaterial(new Color(0.06f, 0.06f, 0.07f));
            Material white = TileFactory.CreateMaterial(new Color(0.95f, 0.95f, 0.93f));
            Material nose = TileFactory.CreateMaterial(new Color(0.02f, 0.02f, 0.02f));
            Material brown = TileFactory.CreateMaterial(new Color(0.36f, 0.2f, 0.08f));

            // Hind legs: what Latte stands and walks on, each swinging from a hip joint.
            for (int i = 0; i < 2; i++)
            {
                int side = i == 0 ? -1 : 1;
                Transform hip = Pivot(root, i == 0 ? "HipL" : "HipR", new Vector3(0.07f * side, 0.24f, -0.17f));
                hindHips[i] = hip;
                Part(PrimitiveType.Sphere, hip, "Thigh", Vector3.zero, new Vector3(0.1f, 0.13f, 0.14f), black);
                Part(PrimitiveType.Cylinder, hip, "HindLeg", new Vector3(0f, -0.13f, 0f), new Vector3(0.07f, 0.11f, 0.075f), black);
                Part(PrimitiveType.Cylinder, hip, "HindPaw", new Vector3(0f, -0.215f, 0.01f), new Vector3(0.075f, 0.025f, 0.085f), white);
            }

            // Everything else hangs off the hips, which tip up when Latte stands.
            hips = Pivot(root, "Hips", new Vector3(0f, 0.3f, -0.17f));
            GameObject body = Part(PrimitiveType.Capsule, hips, "Body", new Vector3(0f, 0.03f, 0.15f), new Vector3(0.25f, 0.27f, 0.25f), black);
            body.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Part(PrimitiveType.Sphere, hips, "Bib", new Vector3(0f, 0.01f, 0.37f), new Vector3(0.21f, 0.25f, 0.17f), white);     // big fluffy chest
            Part(PrimitiveType.Sphere, hips, "Belly", new Vector3(0f, -0.07f, 0.22f), new Vector3(0.14f, 0.1f, 0.2f), white);
            Part(PrimitiveType.Sphere, hips, "Neck", new Vector3(0f, 0.12f, 0.38f), new Vector3(0.17f, 0.19f, 0.16f), black);
            Part(PrimitiveType.Sphere, hips, "Ruff", new Vector3(0f, 0.08f, 0.44f), new Vector3(0.13f, 0.16f, 0.09f), white);     // white down the throat

            // Front legs ("arms"): white with a few black flecks, pivoting at the shoulders so they can clap.
            for (int i = 0; i < 2; i++)
            {
                int side = i == 0 ? -1 : 1;
                Transform shoulder = Pivot(hips, i == 0 ? "ShoulderL" : "ShoulderR", new Vector3(0.07f * side, -0.05f, 0.33f));
                shoulders[i] = shoulder;
                Part(PrimitiveType.Cylinder, shoulder, "Arm", new Vector3(0f, -0.1f, 0f), new Vector3(0.065f, 0.11f, 0.065f), white);
                Part(PrimitiveType.Sphere, shoulder, "Fleck", new Vector3(0.028f * side, -0.07f, 0.012f), new Vector3(0.018f, 0.025f, 0.018f), black);
                Part(PrimitiveType.Sphere, shoulder, "Fleck", new Vector3(0.02f * side, -0.13f, 0.024f), new Vector3(0.014f, 0.02f, 0.014f), black);
                Part(PrimitiveType.Sphere, shoulder, "FrontPaw", new Vector3(0f, -0.205f, 0.015f), new Vector3(0.075f, 0.05f, 0.09f), white);
            }

            // Head: black, with a wide white blaze widening into an all-white muzzle.
            head = Pivot(hips, "HeadPivot", new Vector3(0f, 0.2f, 0.45f));
            Part(PrimitiveType.Sphere, head, "Head", Vector3.zero, new Vector3(0.21f, 0.2f, 0.21f), black);
            Part(PrimitiveType.Sphere, head, "CheekL", new Vector3(-0.08f, -0.03f, 0.02f), new Vector3(0.1f, 0.12f, 0.12f), black);   // fluffy cheeks
            Part(PrimitiveType.Sphere, head, "CheekR", new Vector3(0.08f, -0.03f, 0.02f), new Vector3(0.1f, 0.12f, 0.12f), black);
            GameObject blaze = Part(PrimitiveType.Cube, head, "Blaze", new Vector3(0f, 0.03f, 0.092f), new Vector3(0.045f, 0.1f, 0.03f), white);
            blaze.transform.localRotation = Quaternion.Euler(-30f, 0f, 0f);
            Part(PrimitiveType.Sphere, head, "BlazeTop", new Vector3(0f, 0.07f, 0.07f), new Vector3(0.04f, 0.045f, 0.04f), white);
            Part(PrimitiveType.Sphere, head, "Muzzle", new Vector3(0f, -0.035f, 0.12f), new Vector3(0.11f, 0.09f, 0.15f), white);
            Part(PrimitiveType.Sphere, head, "Chin", new Vector3(0f, -0.075f, 0.09f), new Vector3(0.08f, 0.05f, 0.09f), white);
            Part(PrimitiveType.Sphere, head, "Nose", new Vector3(0f, -0.012f, 0.198f), new Vector3(0.05f, 0.04f, 0.035f), nose);
            Part(PrimitiveType.Sphere, head, "EyeL", new Vector3(-0.047f, 0.032f, 0.087f), Vector3.one * 0.03f, brown);
            Part(PrimitiveType.Sphere, head, "EyeR", new Vector3(0.047f, 0.032f, 0.087f), Vector3.one * 0.03f, brown);
            for (int side = -1; side <= 1; side += 2)
            {
                // Floppy ears: hanging down the sides of the head, a little fluffy.
                GameObject ear = Part(PrimitiveType.Cube, head, "Ear", new Vector3(0.1f * side, 0.035f, -0.025f), new Vector3(0.03f, 0.11f, 0.075f), black);
                ear.transform.localRotation = Quaternion.Euler(10f, 0f, 25f * side);
                Part(PrimitiveType.Sphere, head, "EarFluff", new Vector3(0.12f * side, -0.02f, -0.03f), new Vector3(0.04f, 0.07f, 0.07f), black);
            }

            // A long, bushy tail that hangs down from the pivot and lifts when happy (see Tick).
            tail = Pivot(root, "TailPivot", new Vector3(0f, 0.36f, -0.28f));
            Part(PrimitiveType.Cylinder, tail, "Tail", new Vector3(0f, -0.12f, 0f), new Vector3(0.055f, 0.12f, 0.055f), black);
            Part(PrimitiveType.Sphere, tail, "TailFluff", new Vector3(0f, -0.14f, -0.01f), new Vector3(0.07f, 0.16f, 0.07f), black);
            Part(PrimitiveType.Sphere, tail, "TailTip", new Vector3(0f, -0.26f, 0f), new Vector3(0.06f, 0.08f, 0.06f), white);

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

        private static Transform Pivot(Transform parent, string name, Vector3 localPos)
        {
            Transform t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = localPos;
            return t;
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

        /// <summary>You just walked in: Latte runs to the door, stands up and claps.</summary>
        public void Greet(bool hungry)
        {
            greetUntil = clock + GreetSeconds;
            shout = hungry ? "FINALLY!!\nNOW. FOOD." : "YOU'RE HOME!!";
            nextBeg = clock + GreetSeconds + 2f;
        }

        /// <summary>
        /// Throw the ball from where you stand, the way you're facing. If <paramref name="latteWillFetch"/>,
        /// Latte runs after it and brings it back. Returns false if Latte has it in their mouth.
        /// </summary>
        public bool ThrowBall(Vector3 from, Vector3 forward, bool latteWillFetch)
        {
            if (ballHeld)
                return false;
            var dir = new Vector3(forward.x, 0f, forward.z);
            if (dir.sqrMagnitude < 0.0001f)
                dir = Vector3.forward;
            dir = dir.normalized;
            Vector3 land = from + dir * 2.6f;
            land = new Vector3(Mathf.Clamp(land.x, playArea.xMin, playArea.xMax), ballPos.y, Mathf.Clamp(land.z, playArea.yMin, playArea.yMax));
            airFrom = new Vector3(from.x, ballPos.y + 1.0f, from.z) + dir * 0.3f;
            airTo = land;
            airT = 0f;
            airTime = 0.75f;
            ballInAir = true;
            ballVel = dir * 1.1f;            // keeps rolling a bit after it lands
            fetch = latteWillFetch ? Fetch.Chasing : Fetch.None;
            return true;
        }

        /// <summary>Animates Latte (and the ball). Returns true on the frame Latte drops the ball back at your feet.</summary>
        public bool Tick(float dt, Dog dog, Vector3 cameraPosition, bool playerHome)
        {
            clock += dt;
            DogMood mood = dog.Mood;
            bool eating = dog.IsEating;
            bool greeting = playerHome && !eating && clock < greetUntil;
            bool returnedBall = false;
            var you = new Vector3(cameraPosition.x, restPos.y, cameraPosition.z);

            // Fetch is off if you leave, or once dinner's served.
            if (fetch != Fetch.None && (!playerHome || eating))
            {
                fetch = Fetch.None;
                DropBall(root.position + root.forward * 0.3f);
            }

            // Between greetings, a hungry Latte stands up by the bowl and claps again: food, please.
            if (playerHome && !eating && !greeting && fetch == Fetch.None && mood != DogMood.Content && clock >= nextBeg)
            {
                clapUntil = clock + BegSeconds;
                nextBeg = clock + BegEvery;
                shout = mood == DogMood.Starving ? "FOOD!!! FOOD!!!" : mood == DogMood.Hungry ? "FOOD. PLEASE." : "food? :)";
            }
            if (eating || !playerHome)
                clapUntil = -1f;

            // Where to go.
            bool booping = false;
            Vector3 target;
            float speed = 1.3f;                                    // a leisurely stroll on two legs
            if (eating) target = eatPos;
            else if (greeting) { target = greetPos; speed = 1.9f; }
            else if (fetch == Fetch.Chasing) { target = ballInAir ? airTo : ballPos; speed = 2.2f; }
            else if (fetch == Fetch.Returning)
            {
                Vector3 toDog = root.position - you;
                toDog.y = 0f;
                target = you + (toDog.sqrMagnitude > 0.01f ? toDog.normalized : Vector3.forward) * 0.75f;
                speed = 1.7f;
            }
            else if (clock < clapUntil || clock < celebrateUntil) target = root.position;   // stay put and clap
            else if (mood <= DogMood.Peckish && clock >= nextBoop && !ballHeld)
            {
                target = ballPos;                                  // wander over and boop the ball
                booping = true;
            }
            else target = restPos;
            target.y = restPos.y;

            Vector3 flat = new Vector3(root.position.x, target.y, root.position.z);
            Vector3 pos = Vector3.MoveTowards(flat, target, speed * dt);
            float reach = fetch == Fetch.Chasing || booping ? 0.2f : 0.05f;
            bool arrived = (pos - target).magnitude <= reach;
            if (arrived && reach > 0.05f)
                pos = flat;                                        // stop short of the ball, don't stand on it
            bool moving = (pos - flat).sqrMagnitude > 0.000001f;

            // Arriving somewhere does something.
            if (arrived && fetch == Fetch.Chasing && !ballInAir)
            {
                fetch = Fetch.Returning;
                ballHeld = true;
            }
            else if (arrived && fetch == Fetch.Returning)
            {
                fetch = Fetch.None;
                DropBall(pos + (you - pos).normalized * 0.3f);
                returnedBall = true;
                celebrateUntil = clock + 1.6f;
                shout = "AGAIN! AGAIN!";
            }
            else if (arrived && booping)
            {
                Vector3 kick = ballPos - pos;
                kick.y = 0f;
                ballVel = (kick.sqrMagnitude > 0.0001f ? kick.normalized : root.forward) * (0.9f + (float)rng.NextDouble() * 0.6f);
                nextBoop = clock + 10f + (float)rng.NextDouble() * 8f;
            }

            bool clapping = !eating && !moving && (greeting && arrived || clock < clapUntil || clock < celebrateUntil);

            Doing = eating ? "eating dinner"
                : fetch == Fetch.Chasing ? "chasing the ball!"
                : fetch == Fetch.Returning ? "bringing the ball back"
                : clapping ? "standing up, clapping"
                : booping ? "booping the ball around"
                : moving ? "strolling around on two legs"
                : mood == DogMood.Starving ? "lying by the door, sighing"
                : mood == DogMood.Hungry ? "staring at the empty bowl"
                : "lying on the rug";

            // Face where they're walking, else you (or the bowl while eating).
            Vector3 look = moving ? target - pos : (eating ? bowlPos : you) - pos;
            look.y = 0f;
            if (look.sqrMagnitude > 0.0001f)
                yaw = Mathf.MoveTowardsAngle(yaw, Quaternion.LookRotation(look).eulerAngles.y, 360f * dt);

            // Walking like a person: stand up, step, swing the arms, waddle a little.
            walk = Mathf.MoveTowards(walk, moving ? 1f : 0f, dt * 5f);
            if (moving)
                gait += dt * speed * 9f;
            rear = Mathf.MoveTowards(rear, clapping || moving ? 1f : 0f, dt * 3.5f);
            bool hopping = clock < hopUntil;
            float step = Mathf.Sin(gait);
            float bob = (hopping ? Mathf.Abs(Mathf.Sin(clock * 9f)) * 0.08f : 0f)
                      + (clapping ? Mathf.Abs(Mathf.Sin(clock * 7f)) * 0.03f : 0f)
                      + Mathf.Abs(step) * 0.025f * walk;
            root.position = new Vector3(pos.x, target.y + bob, pos.z);
            root.rotation = Quaternion.Euler(0f, yaw, step * 5f * walk);              // the waddle
            float upright = Mathf.Lerp(RearAngle, WalkUpright, walk);   // stands up straighter to walk
            hips.localRotation = Quaternion.Euler(-upright * rear + step * 3f * walk, 0f, 0f);
            hindHips[0].localRotation = Quaternion.Euler(-28f * step * walk, 0f, 0f);
            hindHips[1].localRotation = Quaternion.Euler(28f * step * walk, 0f, 0f);

            // Front legs: on all fours they hang straight; walking upright they hang down and swing
            // opposite the legs; standing still, they come up to the chest and clap.
            float clap = 0.5f + 0.5f * Mathf.Sin(clock * 13f);    // 0 = open, 1 = paws together (~2 claps/s)
            float armLift, armIn;
            if (clapping) { armLift = -42f; armIn = Mathf.Lerp(-12f, 19f, clap); }
            else { armLift = upright; armIn = -4f; }                // hanging at the sides
            float swing = 30f * step * walk;
            shoulders[0].localRotation = Quaternion.Euler(armLift * rear + swing, 0f, armIn * rear);
            shoulders[1].localRotation = Quaternion.Euler(armLift * rear - swing, 0f, -armIn * rear);

            // Head: level while upright (looking at you or ahead), dips into the bowl, droops when starving.
            float headPitch = eating && !moving ? 35f + Mathf.Sin(clock * 10f) * 8f
                : mood == DogMood.Starving ? 20f
                : mood == DogMood.Hungry ? Mathf.Sin(clock * 3f) * 10f - 10f
                : -5f;
            if (clapping)
                headPitch = -15f + Mathf.Sin(clock * 7f) * 6f;      // looking up at you, nodding along
            else if (moving)
                headPitch = 0f;
            float headRoll = !eating && (clapping || mood == DogMood.Content) ? Mathf.Sin(clock * 1.6f) * 12f : 0f;
            head.localRotation = Quaternion.Euler(headPitch + upright * rear, 0f, headRoll);

            // Tail (lift: 0 = hanging straight down, 90 = straight out behind, more = raised).
            float tSpeed, amp, tailLift;
            if (hopping || eating || clapping || fetch != Fetch.None) { tSpeed = 16f; amp = 40f; tailLift = 105f; }
            else if (mood == DogMood.Content) { tSpeed = 10f; amp = 30f; tailLift = 85f; }
            else if (mood == DogMood.Peckish) { tSpeed = 5f; amp = 20f; tailLift = 60f; }
            else if (mood == DogMood.Hungry) { tSpeed = 3f; amp = 10f; tailLift = 35f; }
            else { tSpeed = 0f; amp = 0f; tailLift = 8f; }                 // tucked down
            tailLift = Mathf.Lerp(tailLift, 20f, rear * 0.7f);            // upright: the tail sweeps the floor
            tail.localRotation = Quaternion.Euler(tailLift, Mathf.Sin(clock * tSpeed) * amp, 0f);

            UpdateBall(dt);

            // The bowl has food in it while they eat (soup-coloured if it's a treat).
            food.SetActive(eating);
            if (eating)
                foodMat.color = dog.TreatLeft > 0 ? new Color(0.95f, 0.8f, 0.45f) : new Color(0.45f, 0.28f, 0.15f);

            // "LATTE / hungry" (or what they're shouting) above their head, facing you.
            Vector3 lp = head.position + Vector3.up * 0.22f;
            label.transform.position = lp;
            Vector3 away = lp - cameraPosition;
            if (away.sqrMagnitude > 0.0001f)
                label.transform.rotation = Quaternion.LookRotation(away);
            label.text = clapping && shout != null ? $"LATTE\n<b>{shout}</b>"
                : fetch != Fetch.None ? "LATTE\n<b>BALL!!</b>"
                : "LATTE\n" + (eating ? "nom nom nom" : Dog.Describe(mood));
            label.color = mood == DogMood.Content ? new Color(0.6f, 1f, 0.6f)
                : mood == DogMood.Peckish ? new Color(1f, 0.95f, 0.6f)
                : mood == DogMood.Hungry ? new Color(1f, 0.7f, 0.3f)
                : new Color(1f, 0.3f, 0.25f);
            return returnedBall;
        }

        private void DropBall(Vector3 at)
        {
            if (!ballHeld)
                return;
            ballHeld = false;
            ballPos = new Vector3(Mathf.Clamp(at.x, playArea.xMin, playArea.xMax), restPos.y + HomeView.BallRadius,
                                  Mathf.Clamp(at.z, playArea.yMin, playArea.yMax));
            ballVel = Vector3.zero;
        }

        /// <summary>The ball: in Latte's mouth, flying in an arc, or rolling (slowing down, bouncing off the edges).</summary>
        private void UpdateBall(float dt)
        {
            float floorY = restPos.y + HomeView.BallRadius;
            if (ballHeld)
            {
                ball.position = head.TransformPoint(new Vector3(0f, -0.06f, 0.24f));
                return;
            }
            Vector3 before = ballPos;
            if (ballInAir)
            {
                airT += dt / airTime;
                float t = Mathf.Clamp01(airT);
                Vector3 p = Vector3.Lerp(airFrom, airTo, t);
                p.y = Mathf.Lerp(airFrom.y, floorY, t) + Mathf.Sin(t * Mathf.PI) * 0.6f;
                ballPos = p;
                if (t >= 1f)
                {
                    ballInAir = false;
                    ballPos.y = floorY;
                }
            }
            else if (ballVel.sqrMagnitude > 0.0004f)
            {
                ballPos += ballVel * dt;
                ballVel *= Mathf.Max(0f, 1f - 1.4f * dt);
                if (ballPos.x < playArea.xMin || ballPos.x > playArea.xMax) { ballVel.x = -ballVel.x * 0.6f; ballPos.x = Mathf.Clamp(ballPos.x, playArea.xMin, playArea.xMax); }
                if (ballPos.z < playArea.yMin || ballPos.z > playArea.yMax) { ballVel.z = -ballVel.z * 0.6f; ballPos.z = Mathf.Clamp(ballPos.z, playArea.yMin, playArea.yMax); }
                ballPos.y = floorY;
            }
            ball.position = ballPos;

            // Spin it as it rolls (the yellow band shows it).
            Vector3 moved = ballPos - before;
            moved.y = 0f;
            float dist = moved.magnitude;
            if (dist > 0.0001f && !ballInAir)
            {
                Vector3 axis = Vector3.Cross(Vector3.up, moved / dist);
                ball.rotation = Quaternion.AngleAxis(dist / HomeView.BallRadius * Mathf.Rad2Deg, axis) * ball.rotation;
            }
        }
    }
}
