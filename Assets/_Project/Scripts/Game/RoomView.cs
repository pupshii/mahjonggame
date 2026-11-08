using System.Collections.Generic;
using UnityEngine;

namespace MahjongNight.Game
{
    public enum InteractKind
    {
        Toilet,
        Water,
        Food,
        Seat,   // your chair at a table: sit down and play there
    }

    /// <summary>Marks a room object you can use with E.</summary>
    public sealed class Interactable : MonoBehaviour
    {
        public InteractKind Kind { get; set; }
        /// <summary>For seats: which table this chair belongs to.</summary>
        public int TableIndex { get; set; }
    }

    /// <summary>
    /// The greybox parlour: a floor and four walls sized to fit every table, a chair for each
    /// seat (yours is usable at every table), a toilet, a water jar on a side table, and a food
    /// counter in the corners. Also answers "can I stand here?" for the walker.
    /// </summary>
    public sealed class RoomView
    {
        public const float FloorY = -0.75f;
        private const float WallHeight = 2.8f;
        private const float TableBlock = 1.5f;   // half-size of a table plus the chairs around it

        // Floor-plan obstacles (x/z rectangles).
        private readonly List<Rect> obstacles = new List<Rect>();

        public RoomView(Transform parent, int humanSeat, IReadOnlyList<Vector3> tableOrigins)
        {
            var root = new GameObject("Room").transform;
            root.SetParent(parent, false);

            float reach = 0f;
            foreach (Vector3 o in tableOrigins)
                reach = Mathf.Max(reach, Mathf.Max(Mathf.Abs(o.x), Mathf.Abs(o.z)));
            HalfSize = reach + TableBlock + 2.3f;

            Material floorMat = TileFactory.CreateMaterial(new Color(0.18f, 0.14f, 0.12f));
            Material wall = TileFactory.CreateMaterial(new Color(0.42f, 0.36f, 0.3f));
            Material wood = TileFactory.CreateMaterial(new Color(0.45f, 0.28f, 0.16f));
            Material white = TileFactory.CreateMaterial(new Color(0.95f, 0.95f, 0.95f));
            Material water = TileFactory.CreateMaterial(new Color(0.35f, 0.6f, 0.95f));
            Material bun = TileFactory.CreateMaterial(new Color(0.98f, 0.93f, 0.8f));

            // Floor (a Unity plane is 10 m across at scale 1) and walls
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.SetParent(root, false);
            floor.transform.position = new Vector3(0f, FloorY, 0f);
            floor.transform.localScale = new Vector3(HalfSize / 5f, 1f, HalfSize / 5f);
            floor.GetComponent<Renderer>().sharedMaterial = floorMat;

            float mid = FloorY + WallHeight / 2f;
            Box(root, "WallN", new Vector3(0f, mid, HalfSize), new Vector3(HalfSize * 2f, WallHeight, 0.1f), wall);
            Box(root, "WallS", new Vector3(0f, mid, -HalfSize), new Vector3(HalfSize * 2f, WallHeight, 0.1f), wall);
            Box(root, "WallE", new Vector3(HalfSize, mid, 0f), new Vector3(0.1f, WallHeight, HalfSize * 2f), wall);
            Box(root, "WallW", new Vector3(-HalfSize, mid, 0f), new Vector3(0.1f, WallHeight, HalfSize * 2f), wall);

            // Chairs at every table (each table and its four sitters is one obstacle).
            for (int t = 0; t < tableOrigins.Count; t++)
            {
                Vector3 o = tableOrigins[t];
                for (int seat = 0; seat < 4; seat++)
                {
                    int relative = (seat - humanSeat + 4) % 4;
                    Quaternion frame = Quaternion.Euler(0f, -90f * relative, 0f);
                    GameObject chair = Box(root, $"Chair{t}-{seat}", o + frame * new Vector3(0f, -0.3f, -1.2f), new Vector3(0.45f, 0.06f, 0.45f), wood);
                    chair.transform.rotation = frame;
                    GameObject back = Box(root, $"ChairBack{t}-{seat}", o + frame * new Vector3(0f, 0f, -1.42f), new Vector3(0.45f, 0.55f, 0.05f), wood);
                    back.transform.rotation = frame;
                    if (seat == humanSeat)
                    {
                        foreach (GameObject part in new[] { chair, back })
                        {
                            var it = part.AddComponent<Interactable>();
                            it.Kind = InteractKind.Seat;
                            it.TableIndex = t;
                        }
                    }
                }
                obstacles.Add(new Rect(o.x - TableBlock, o.z - TableBlock, TableBlock * 2f, TableBlock * 2f));
            }

            float corner = HalfSize - 0.75f;

            // Toilet (north-east corner)
            var toiletPos = new Vector3(corner, 0f, corner);
            GameObject bowl = Cylinder(root, "ToiletBowl", new Vector3(toiletPos.x, FloorY + 0.2f, toiletPos.z), new Vector3(0.42f, 0.2f, 0.5f), white);
            GameObject tank = Box(root, "ToiletTank", new Vector3(toiletPos.x, FloorY + 0.6f, toiletPos.z + 0.33f), new Vector3(0.42f, 0.42f, 0.18f), white);
            bowl.AddComponent<Interactable>().Kind = InteractKind.Toilet;
            tank.AddComponent<Interactable>().Kind = InteractKind.Toilet;
            Sign(root, "WC  廁所", new Vector3(toiletPos.x, 1.2f, HalfSize - 0.06f), Quaternion.identity);
            obstacles.Add(new Rect(corner - 0.4f, corner - 0.4f, 1.2f, 1.2f));

            // Water jar on a side table (north-west corner)
            var waterPos = new Vector3(-corner, 0f, corner);
            Box(root, "WaterTable", new Vector3(waterPos.x, FloorY + 0.4f, waterPos.z), new Vector3(0.7f, 0.8f, 0.7f), wood);
            GameObject jar = Cylinder(root, "WaterJar", new Vector3(waterPos.x, FloorY + 0.98f, waterPos.z), new Vector3(0.22f, 0.18f, 0.22f), water);
            jar.AddComponent<Interactable>().Kind = InteractKind.Water;
            Sign(root, "WATER  水", new Vector3(waterPos.x, 1.2f, HalfSize - 0.06f), Quaternion.identity);
            obstacles.Add(new Rect(-corner - 0.4f, corner - 0.4f, 0.8f, 0.8f));

            // Food counter (south-east corner)
            var foodPos = new Vector3(corner + 0.1f, 0f, -corner + 0.45f);
            GameObject counter = Box(root, "FoodCounter", new Vector3(foodPos.x, FloorY + 0.45f, foodPos.z), new Vector3(0.8f, 0.9f, 1.4f), wood);
            counter.AddComponent<Interactable>().Kind = InteractKind.Food;
            Cylinder(root, "Plate", new Vector3(foodPos.x, FloorY + 0.91f, foodPos.z), new Vector3(0.45f, 0.01f, 0.45f), white);
            for (int i = 0; i < 3; i++)
            {
                GameObject b = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                b.name = "Bun" + i;
                b.transform.SetParent(root, false);
                b.transform.position = new Vector3(foodPos.x + (i - 1) * 0.11f, FloorY + 0.96f, foodPos.z + (i % 2) * 0.08f);
                b.transform.localScale = new Vector3(0.1f, 0.08f, 0.1f);
                b.GetComponent<Renderer>().sharedMaterial = bun;
                b.AddComponent<Interactable>().Kind = InteractKind.Food;
            }
            Sign(root, "FOOD  食物", new Vector3(HalfSize - 0.06f, 1.2f, foodPos.z), Quaternion.Euler(0f, 90f, 0f));
            obstacles.Add(new Rect(foodPos.x - 0.5f, foodPos.z - 0.8f, 1.2f, 1.6f));
        }

        /// <summary>Half the width of the (square) room.</summary>
        public float HalfSize { get; }

        /// <summary>Where you stand after getting up from your chair at the table centred on <paramref name="tableOrigin"/>.</summary>
        public static Vector3 StandUpPoint(Vector3 tableOrigin) => new Vector3(tableOrigin.x, FloorY, tableOrigin.z - 1.85f);

        /// <summary>True if a body of <paramref name="radius"/> at floor point (x, z) would hit something.</summary>
        public bool Blocked(float x, float z, float radius)
        {
            float limit = HalfSize - 0.05f - radius;
            if (x < -limit || x > limit || z < -limit || z > limit)
                return true;
            foreach (Rect r in obstacles)
                if (x > r.xMin - radius && x < r.xMax + radius && z > r.yMin - radius && z < r.yMax + radius)
                    return true;
            return false;
        }

        private static GameObject Box(Transform parent, string name, Vector3 pos, Vector3 scale, Material m) =>
            Prim(PrimitiveType.Cube, parent, name, pos, scale, m);

        private static GameObject Cylinder(Transform parent, string name, Vector3 pos, Vector3 scale, Material m) =>
            Prim(PrimitiveType.Cylinder, parent, name, pos, scale, m);

        private static GameObject Prim(PrimitiveType type, Transform parent, string name, Vector3 pos, Vector3 scale, Material m)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = m;
            return go;
        }

        /// <summary>A wall sign, readable from inside the room.</summary>
        private static void Sign(Transform parent, string text, Vector3 pos, Quaternion rotation)
        {
            var go = new GameObject("Sign " + text);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.rotation = rotation;
            var tm = go.AddComponent<TextMesh>();
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            tm.font = font;
            tm.text = text;
            tm.fontSize = 64;
            tm.characterSize = 0.03f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.color = new Color(1f, 0.95f, 0.7f);
            go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
        }
    }
}
