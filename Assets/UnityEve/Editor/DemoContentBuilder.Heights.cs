using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Unity.Eve.Battle;
using Unity.Eve.Cameras;
using Unity.Eve.Interaction;

namespace Unity.Eve.Editor
{
    /// <summary>
    /// Ashgrove Heights, west of the bus stop: Willow Lane, a residential street of detached houses
    /// (lawns, porches, gable roofs, driveways, garages, fences, mailboxes, parked cars), and at its
    /// south end the Kingsport Foods grocery store with its parking lot. Houses and cars are generated
    /// from a seed so no two lots look alike.
    /// </summary>
    public static partial class DemoContentBuilder
    {
        // ------------------------------------------------------------------ orientation

        /// <summary>
        /// Yaw that turns a Kenney model (whose front faces local -Z: bench seats, lamp arms, dumpster
        /// lids, traffic-light heads) to face <paramref name="direction"/>.
        /// </summary>
        private static float FaceYaw(Vector3 direction) => Mathf.Atan2(-direction.x, -direction.z) * Mathf.Rad2Deg;

        private static GameObject Bench(SceneKit k, Vector3 at, Vector3 faceToward)
        {
            var go = Model(k.Level, Urban("detail-bench"), at, FaceYaw(faceToward), size: 1.8f, name: "Bench");
            go.AddComponent<CoverProp>();
            return go;
        }

        // ------------------------------------------------------------------ primitives in a local frame

        private static GameObject LocalBox(Transform parent, string name, Vector3 min, Vector3 max, Material mat, bool collider = true, Vector3? euler = null)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = (min + max) * 0.5f;
            go.transform.localRotation = Quaternion.Euler(euler ?? Vector3.zero);
            go.transform.localScale = max - min;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        private static GameObject LocalCylinder(Transform parent, string name, Vector3 centre, Vector3 scale, Material mat, Vector3 euler, bool collider = false)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = centre;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        private static Mesh _prism;

        /// <summary>A unit gable prism (ridge along X, apex at y=1, eaves at z=±0.5), saved as an asset.</summary>
        private static Mesh RoofPrism()
        {
            if (_prism != null) return _prism;
            const string path = Gen + "/Meshes/RoofPrism.asset";
            System.IO.Directory.CreateDirectory(Gen + "/Meshes");
            AssetDatabase.DeleteAsset(path);
            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var t = new List<int>();
            void Face(Vector3 normal, params Vector3[] pts)
            {
                var start = v.Count;
                foreach (var p in pts) { v.Add(p); n.Add(normal.normalized); }
                for (var i = 1; i + 1 < pts.Length; i++)
                {
                    // Unity front faces wind clockwise as seen from outside.
                    var a = pts[0]; var b = pts[i]; var c = pts[i + 1];
                    if (Vector3.Dot(Vector3.Cross(b - a, c - a), normal) > 0f) t.AddRange(new[] { start, start + i, start + i + 1 });
                    else t.AddRange(new[] { start, start + i + 1, start + i });
                }
            }
            Face(new Vector3(0f, 0.5f, 1f), new Vector3(-0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, 0.5f), new Vector3(0.5f, 1f, 0f), new Vector3(-0.5f, 1f, 0f));
            Face(new Vector3(0f, 0.5f, -1f), new Vector3(0.5f, 0f, -0.5f), new Vector3(-0.5f, 0f, -0.5f), new Vector3(-0.5f, 1f, 0f), new Vector3(0.5f, 1f, 0f));
            Face(Vector3.down, new Vector3(-0.5f, 0f, -0.5f), new Vector3(0.5f, 0f, -0.5f), new Vector3(0.5f, 0f, 0.5f), new Vector3(-0.5f, 0f, 0.5f));
            Face(Vector3.left, new Vector3(-0.5f, 0f, -0.5f), new Vector3(-0.5f, 0f, 0.5f), new Vector3(-0.5f, 1f, 0f));
            Face(Vector3.right, new Vector3(0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, -0.5f), new Vector3(0.5f, 1f, 0f));
            _prism = new Mesh { name = "RoofPrism" };
            _prism.SetVertices(v);
            _prism.SetNormals(n);
            _prism.SetTriangles(t, 0);
            _prism.RecalculateBounds();
            AssetDatabase.CreateAsset(_prism, path);
            return _prism;
        }

        private static GameObject Roof(Transform parent, string name, Vector3 centreBase, Vector3 size, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = centreBase;
            go.transform.localScale = size;
            go.AddComponent<MeshFilter>().sharedMesh = RoofPrism();
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        // ------------------------------------------------------------------ props

        private static readonly Color[] CarPaints =
        {
            new Color(0.62f, 0.12f, 0.1f), new Color(0.16f, 0.3f, 0.55f), new Color(0.8f, 0.78f, 0.7f), new Color(0.2f, 0.42f, 0.3f),
            new Color(0.55f, 0.52f, 0.5f), new Color(0.75f, 0.55f, 0.15f), new Color(0.12f, 0.12f, 0.14f),
        };

        /// <summary>A low-poly sedan or wagon, front along its +Z.</summary>
        private static GameObject Car(SceneKit k, string name, Vector3 at, float yaw, int seed)
        {
            var rng = new System.Random(seed);
            var paint = SimpleMaterial($"CarPaint{seed % CarPaints.Length}", CarPaints[seed % CarPaints.Length], null, 0.7f);
            var glass = SimpleMaterial("CarGlass", new Color(0.1f, 0.13f, 0.17f), null, 0.9f);
            var tyre = SimpleMaterial("CarTyre", new Color(0.06f, 0.06f, 0.06f));
            var chrome = SimpleMaterial("CarChrome", new Color(0.7f, 0.72f, 0.75f), null, 0.8f);
            var head = SimpleMaterial("CarHeadlight", new Color(1f, 0.95f, 0.8f), null, 0.5f, emissive: true);
            var tail = SimpleMaterial("CarTaillight", new Color(0.7f, 0.05f, 0.05f), null, 0.5f, emissive: true);
            var wagon = rng.NextDouble() < 0.35;

            var car = new GameObject(name).transform;
            car.SetParent(k.Level);
            car.SetPositionAndRotation(at, Quaternion.Euler(0f, yaw, 0f));
            var box = car.gameObject.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.75f, 0f);
            box.size = new Vector3(1.8f, 1.5f, 4.4f);
            LocalBox(car, "Body", new Vector3(-0.9f, 0.32f, -2.2f), new Vector3(0.9f, 0.95f, 2.2f), paint, false);
            var cabinBack = wagon ? -2.05f : -1.2f;
            LocalBox(car, "Cabin", new Vector3(-0.8f, 0.95f, cabinBack), new Vector3(0.8f, 1.5f, 0.75f), glass, false);
            LocalBox(car, "RoofPanel", new Vector3(-0.82f, 1.48f, cabinBack - 0.02f), new Vector3(0.82f, 1.56f, 0.6f), paint, false);
            LocalBox(car, "Bumper", new Vector3(-0.92f, 0.3f, 2.18f), new Vector3(0.92f, 0.45f, 2.3f), chrome, false);
            LocalBox(car, "BumperRear", new Vector3(-0.92f, 0.3f, -2.3f), new Vector3(0.92f, 0.45f, -2.18f), chrome, false);
            LocalBox(car, "HeadL", new Vector3(-0.82f, 0.6f, 2.19f), new Vector3(-0.5f, 0.78f, 2.23f), head, false);
            LocalBox(car, "HeadR", new Vector3(0.5f, 0.6f, 2.19f), new Vector3(0.82f, 0.78f, 2.23f), head, false);
            LocalBox(car, "TailL", new Vector3(-0.85f, 0.6f, -2.23f), new Vector3(-0.55f, 0.75f, -2.19f), tail, false);
            LocalBox(car, "TailR", new Vector3(0.55f, 0.6f, -2.23f), new Vector3(0.85f, 0.75f, -2.19f), tail, false);
            foreach (var x in new[] { -0.86f, 0.86f })
            foreach (var z in new[] { -1.4f, 1.35f })
                LocalCylinder(car, "Wheel", new Vector3(x, 0.34f, z), new Vector3(0.66f, 0.12f, 0.66f), tyre, new Vector3(0f, 0f, 90f));
            return car.gameObject;
        }

        /// <summary>A shopping cart, handle at -Z.</summary>
        private static void Cart(SceneKit k, Vector3 at, float yaw, bool tipped = false)
        {
            var wire = SimpleMaterial("CartWire", new Color(0.72f, 0.74f, 0.78f), null, 0.7f);
            var red = SimpleMaterial("CartHandle", new Color(0.75f, 0.12f, 0.1f));
            var cart = new GameObject("Cart").transform;
            cart.SetParent(k.Level);
            cart.SetPositionAndRotation(at, Quaternion.Euler(tipped ? 80f : 0f, yaw, tipped ? 10f : 0f));
            LocalBox(cart, "Basket", new Vector3(-0.3f, 0.45f, -0.45f), new Vector3(0.3f, 0.95f, 0.45f), wire, false);
            LocalBox(cart, "Base", new Vector3(-0.28f, 0.12f, -0.4f), new Vector3(0.28f, 0.16f, 0.4f), wire, false);
            LocalBox(cart, "Handle", new Vector3(-0.3f, 0.98f, -0.55f), new Vector3(0.3f, 1.04f, -0.5f), red, false);
            foreach (var x in new[] { -0.24f, 0.24f })
            foreach (var z in new[] { -0.36f, 0.36f })
                LocalBox(cart, "Leg", new Vector3(x - 0.02f, 0.06f, z - 0.02f), new Vector3(x + 0.02f, 0.45f, z + 0.02f), wire, false);
        }

        /// <summary>Unlit signage text (street names, the store sign).</summary>
        private static TextMesh SignText(Transform parent, string text, Vector3 localPos, float yaw, float charSize, Color color, TextAnchor anchor = TextAnchor.MiddleCenter)
        {
            var go = new GameObject($"Sign_{text}");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            var tm = go.AddComponent<TextMesh>();
            var font = AssetDatabase.LoadAssetAtPath<Font>($"{Art}/Fonts/Bangers-Regular.ttf");
            tm.font = font;
            go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            tm.text = text;
            tm.fontSize = 64;
            tm.characterSize = charSize;
            tm.anchor = anchor;
            tm.alignment = TextAlignment.Center;
            tm.color = color;
            return tm;
        }

        private static void StreetSign(SceneKit k, Vector3 at, string a, float yawA, string b, float yawB)
        {
            var post = new GameObject("StreetSign").transform;
            post.SetParent(k.Level);
            post.position = at;
            var green = SimpleMaterial("SignGreen", new Color(0.1f, 0.35f, 0.18f));
            LocalBox(post, "Post", new Vector3(-0.04f, 0f, -0.04f), new Vector3(0.04f, 2.9f, 0.04f), k.Metal);
            var blade = new GameObject("BladeA").transform;
            blade.SetParent(post, false);
            blade.localPosition = new Vector3(0f, 2.75f, 0f);
            blade.localRotation = Quaternion.Euler(0f, yawA, 0f);
            LocalBox(blade, "Plate", new Vector3(-0.65f, -0.12f, -0.02f), new Vector3(0.65f, 0.12f, 0.02f), green, false);
            SignText(blade, a, new Vector3(0f, 0f, -0.03f), 0f, 0.02f, Color.white);
            var bladeB = new GameObject("BladeB").transform;
            bladeB.SetParent(post, false);
            bladeB.localPosition = new Vector3(0f, 2.5f, 0f);
            bladeB.localRotation = Quaternion.Euler(0f, yawB, 0f);
            LocalBox(bladeB, "Plate", new Vector3(-0.65f, -0.12f, -0.02f), new Vector3(0.65f, 0.12f, 0.02f), green, false);
            SignText(bladeB, b, new Vector3(0f, 0f, -0.03f), 0f, 0.02f, Color.white);
        }

        /// <summary>A cel-shaded tree: trunk, a few branch stubs and a clustered canopy (seeded).</summary>
        private static void Tree(SceneKit k, Vector3 at, float height, int seed)
        {
            var rng = new System.Random(seed);
            float R(float a, float b) => a + (b - a) * (float)rng.NextDouble();
            var bark = SimpleMaterial("Bark", new Color(0.3f, 0.21f, 0.15f));
            var leaves = SimpleMaterial($"Leaves{seed % 3}", new[] { new Color(0.2f, 0.36f, 0.18f), new Color(0.26f, 0.4f, 0.16f), new Color(0.17f, 0.3f, 0.22f) }[seed % 3]);
            var tree = new GameObject("Tree").transform;
            tree.SetParent(k.Level);
            tree.position = at;
            var trunkH = height * 0.45f;
            var trunk = LocalCylinder(tree, "Trunk", new Vector3(0f, trunkH * 0.5f, 0f), new Vector3(0.28f, trunkH * 0.5f, 0.28f), bark, Vector3.zero, collider: true);
            trunk.GetComponent<CapsuleCollider>();
            var crown = height * 0.42f;
            for (var i = 0; i < 5; i++)
            {
                var off = i == 0 ? Vector3.zero : new Vector3(R(-0.9f, 0.9f), R(-0.4f, 0.5f), R(-0.9f, 0.9f)) * crown * 0.45f;
                var s = crown * (i == 0 ? 1f : R(0.55f, 0.8f));
                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Object.DestroyImmediate(go.GetComponent<Collider>());
                go.name = "Canopy";
                go.transform.SetParent(tree, false);
                go.transform.localPosition = new Vector3(0f, trunkH + crown * 0.45f, 0f) + off;
                go.transform.localScale = new Vector3(s, s * 0.85f, s);
                go.GetComponent<Renderer>().sharedMaterial = leaves;
            }
        }

        // ------------------------------------------------------------------ houses

        private static readonly Color[] SidingColours =
        {
            new Color(0.62f, 0.7f, 0.76f), new Color(0.8f, 0.74f, 0.6f), new Color(0.6f, 0.68f, 0.56f),
            new Color(0.78f, 0.6f, 0.55f), new Color(0.66f, 0.66f, 0.68f), new Color(0.82f, 0.8f, 0.72f),
        };
        private static readonly Color[] RoofColours =
        {
            new Color(0.2f, 0.2f, 0.23f), new Color(0.32f, 0.22f, 0.16f), new Color(0.38f, 0.16f, 0.14f), new Color(0.25f, 0.3f, 0.33f),
        };

        /// <summary>
        /// A detached house on a 14 x 18 m lot. The lot's origin is the middle of its street edge and
        /// it faces <paramref name="faceDir"/> (toward the street). Seeded: one or two storeys, colours,
        /// garage side, porch, fence or hedge, tree, a car in the driveway, porch light.
        /// </summary>
        private static Transform House(SceneKit k, string name, Vector3 lotFront, Vector3 faceDir, int seed, Transform interact)
        {
            var rng = new System.Random(seed);
            float R(float a, float b) => a + (b - a) * (float)rng.NextDouble();
            var siding = SimpleMaterial($"HouseSiding{seed % SidingColours.Length}", SidingColours[seed % SidingColours.Length], Tex("planks"), 0.15f);
            var roofMat = SimpleMaterial($"HouseRoof{seed % RoofColours.Length}", RoofColours[seed % RoofColours.Length], null, 0.2f);
            var trim = SimpleMaterial("HouseTrim", new Color(0.88f, 0.86f, 0.8f));
            var grass = SimpleMaterial("Lawn", new Color(0.24f, 0.36f, 0.2f), Tex("dirt"));
            var hedgeMat = SimpleMaterial("Hedge", new Color(0.16f, 0.3f, 0.14f));
            var door = SimpleMaterial($"FrontDoor{seed % 3}", new[] { new Color(0.45f, 0.12f, 0.1f), new Color(0.12f, 0.22f, 0.35f), new Color(0.2f, 0.32f, 0.2f) }[seed % 3]);
            var garageMat = SimpleMaterial("GarageDoor", new Color(0.82f, 0.82f, 0.8f), Tex("metal"));

            var lot = new GameObject(name).transform;
            lot.SetParent(k.Level);
            lot.SetPositionAndRotation(lotFront, Quaternion.LookRotation(faceDir));

            var mirror = rng.NextDouble() < 0.5 ? -1f : 1f; // garage on the left or right
            var twoStorey = rng.NextDouble() < 0.5;
            var wallH = twoStorey ? 5.6f : 3.0f;
            var bodyW = R(7.5f, 9f);
            var bodyD = R(7.5f, 9f);
            var setback = R(6.5f, 8f);
            float X(float x) => x * mirror;
            Vector3 V(float x, float y, float z) => new Vector3(X(x), y, z);
            GameObject Box(string n, float x0, float y0, float z0, float x1, float y1, float z1, Material m, bool col = true) =>
                LocalBox(lot, n, new Vector3(Mathf.Min(X(x0), X(x1)), y0, z0), new Vector3(Mathf.Max(X(x0), X(x1)), y1, z1), m, col);

            // Grounds: lawn, path, driveway.
            Box("Lawn", -7f, 0f, -18f, 7f, 0.08f, 0f, grass);
            var bx0 = -6f;
            var bx1 = bx0 + bodyW;
            var doorX = bx0 + bodyW * 0.45f;
            Box("Path", doorX - 0.6f, 0.08f, -setback, doorX + 0.6f, 0.11f, 0f, k.Sidewalk, false);
            Box("Driveway", 3.4f, 0.08f, -setback - 6f, 6.6f, 0.11f, 0f, k.Sidewalk, false);

            // The house: body, gable roof, chimney, porch, door, windows.
            var bz0 = -setback - bodyD;
            var bz1 = -setback;
            Box("Body", bx0, 0f, bz0, Mathf.Min(bx1, 3.2f), wallH, bz1, siding);
            Box("Foundation", bx0 - 0.05f, 0f, bz0 - 0.05f, Mathf.Min(bx1, 3.2f) + 0.05f, 0.35f, bz1 + 0.05f, k.Plaster, false);
            var bodyRight = Mathf.Min(bx1, 3.2f);
            Roof(lot, "Roof", new Vector3(X((bx0 + bodyRight) * 0.5f), wallH, (bz0 + bz1) * 0.5f),
                new Vector3(bodyRight - bx0 + 0.7f, R(2f, 2.8f), bodyD + 0.8f), roofMat);
            Box("Chimney", bx0 + 1f, wallH, bz0 + 1.5f, bx0 + 1.8f, wallH + 2.6f, bz0 + 2.3f, k.Brick, false);
            Box("Door", doorX - 0.5f, 0.35f, bz1, doorX + 0.5f, 2.45f, bz1 + 0.06f, door, false);
            Box("DoorFrame", doorX - 0.62f, 0.35f, bz1, doorX + 0.62f, 2.58f, bz1 + 0.03f, trim, false);
            // Porch with posts and a little roof.
            if (rng.NextDouble() < 0.75)
            {
                Box("Porch", doorX - 1.8f, 0f, bz1, doorX + 1.8f, 0.35f, bz1 + 1.8f, k.Wood);
                Box("Step", doorX - 0.8f, 0f, bz1 + 1.8f, doorX + 0.8f, 0.18f, bz1 + 2.2f, k.Wood);
                Box("PostL", doorX - 1.7f, 0.35f, bz1 + 1.6f, doorX - 1.55f, 2.7f, bz1 + 1.75f, trim, false);
                Box("PostR", doorX + 1.55f, 0.35f, bz1 + 1.6f, doorX + 1.7f, 2.7f, bz1 + 1.75f, trim, false);
                Box("PorchRoof", doorX - 1.95f, 2.7f, bz1, doorX + 1.95f, 2.85f, bz1 + 1.95f, roofMat, false);
            }
            if (rng.NextDouble() < 0.55)
                PointLight(k, "PorchLight", lot.TransformPoint(V(doorX + 0.9f, 2.3f, bz1 + 0.4f)), new Color(1f, 0.82f, 0.55f), 2.2f, 4.5f, flicker: rng.NextDouble() < 0.3);
            // Windows on the street face, one or two rows, some lit.
            var rows = twoStorey ? new[] { 1.1f, 3.9f } : new[] { 1.1f };
            foreach (var y in rows)
            foreach (var wx in new[] { bx0 + 1.2f, bodyRight - 1.6f })
            {
                if (Mathf.Abs(wx - doorX) < 1.2f && y < 2f) continue;
                var lit = rng.NextDouble() < 0.3;
                Box("WindowFrame", wx - 0.62f, y - 0.08f, bz1, wx + 0.62f, y + 1.3f, bz1 + 0.05f, trim, false);
                Box("Window", wx - 0.5f, y, bz1, wx + 0.5f, y + 1.2f, bz1 + 0.08f, lit ? k.WindowLit : k.Window, false);
            }

            // Garage beside the house, its door facing the street.
            var gx0 = Mathf.Max(bodyRight, 3.2f);
            Box("Garage", gx0, 0f, -setback - 6.8f, 6.8f, 2.9f, -setback - 0.4f, siding);
            Roof(lot, "GarageRoof", new Vector3(X((gx0 + 6.8f) * 0.5f), 2.9f, -setback - 3.6f), new Vector3(6.8f - gx0 + 0.4f, 1.1f, 6.9f), roofMat);
            Box("GarageDoor", 3.6f, 0.05f, -setback - 0.4f, 6.4f, 2.3f, -setback - 0.32f, garageMat, false);

            // Front edge: picket fence or hedge, with gaps for the path and the driveway.
            var hedge = rng.NextDouble() < 0.4;
            void Edge(float x0, float x1)
            {
                if (x1 - x0 < 0.3f) return;
                if (hedge) Box("Hedge", x0, 0f, -0.9f, x1, 0.9f, -0.3f, hedgeMat);
                else
                {
                    Box("FenceRail", x0, 0.45f, -0.55f, x1, 0.55f, -0.5f, trim, false);
                    Box("FenceRailLow", x0, 0.2f, -0.55f, x1, 0.28f, -0.5f, trim, false);
                    for (var x = x0 + 0.1f; x < x1; x += 0.35f) Box("Picket", x - 0.04f, 0f, -0.56f, x + 0.04f, 0.8f, -0.49f, trim, false);
                    Box("FenceCollider", x0, 0f, -0.6f, x1, 0.8f, -0.45f, trim).GetComponent<Renderer>().enabled = false;
                }
            }
            Edge(-6.9f, doorX - 0.8f);
            Edge(doorX + 0.8f, 3.2f);

            // Mailbox by the driveway; a yard tree; sometimes a car in the driveway.
            var mail = new GameObject("Mailbox").transform;
            mail.SetParent(lot, false);
            mail.localPosition = V(2.8f, 0f, -0.4f);
            LocalBox(mail, "Post", new Vector3(-0.04f, 0f, -0.04f), new Vector3(0.04f, 1.05f, 0.04f), k.Wood);
            LocalBox(mail, "Box", new Vector3(-0.14f, 1.05f, -0.25f), new Vector3(0.14f, 1.32f, 0.25f), k.Metal, false);
            // The box's render collider stays off; give it a trigger volume matching its silhouette
            // instead, so the cover raycasts see the wide mailbox body without changing collision.
            var coverVolume = mail.gameObject.AddComponent<BoxCollider>();
            coverVolume.center = new Vector3(0f, 0.66f, -0.1f);
            coverVolume.size = new Vector3(0.3f, 1.34f, 0.52f);
            coverVolume.isTrigger = true;
            mail.gameObject.AddComponent<CoverProp>();
            var mailboxName = "Container_Mailbox_" + name.Replace("House_", "");
            var mailbox = LocalBox(interact, mailboxName, Vector3.zero, Vector3.one * 0.01f, k.Metal, false);
            mailbox.transform.position = lot.TransformPoint(V(2.8f, 1.1f, -0.4f));
            mailbox.GetComponent<Renderer>().enabled = false;
            Attach<Container>(mailbox, lot.TransformPoint(V(2.8f, 0f, 0.7f)), 1.4f, 0f)
                .Configure("mailbox", k.Content.Loot["mail"], "Bills, a church flyer, a coupon book...");
            if (rng.NextDouble() < 0.7)
                Tree(k, lot.TransformPoint(V(R(-5.5f, -3f), 0.08f, R(-5f, -3f))), R(4.5f, 6.5f), seed * 7 + 3);
            if (rng.NextDouble() < 0.55)
                Car(k, $"{name}_Car", lot.TransformPoint(V(5f, 0.08f, -setback + 2.6f)), lot.eulerAngles.y + 180f + R(-4f, 4f), seed * 31 + 7);
            return lot;
        }

        // ------------------------------------------------------------------ the neighbourhood

        private static void BuildHeights(SceneKit k)
        {
            var c = k.Content;
            Block(k.Level, "GroundWest", new Vector3(-140f, -0.5f, -140f), new Vector3(-60f, 0f, 70f), k.Ground);
            Block(k.Level, "GroundSouth", new Vector3(-60f, -0.5f, -140f), new Vector3(90f, 0f, -60f), k.Ground);

            // Ashgrove Street runs on west to meet Willow Lane.
            Block(k.Level, "RoadWestLink", new Vector3(-40f, 0f, -4f), new Vector3(-26f, 0.02f, 4f), k.Asphalt);
            Block(k.Level, "WalkWestN", new Vector3(-29f, 0f, 4f), new Vector3(-26f, 0.14f, 6.5f), k.Sidewalk);
            Block(k.Level, "WalkWestS", new Vector3(-29f, 0f, -6.5f), new Vector3(-26f, 0.14f, -4f), k.Sidewalk);

            // Willow Lane: 8 m road with sidewalks, from the north cul-de-sac to the grocery lot.
            Block(k.Level, "WillowRoad", new Vector3(-40f, 0f, -60f), new Vector3(-32f, 0.02f, 52f), k.Asphalt);
            Block(k.Level, "WillowWalkW", new Vector3(-43f, 0f, -60f), new Vector3(-40f, 0.14f, 52f), k.Sidewalk);
            Block(k.Level, "WillowWalkEN", new Vector3(-32f, 0f, 4f), new Vector3(-29f, 0.14f, 52f), k.Sidewalk);
            Block(k.Level, "WillowWalkES", new Vector3(-32f, 0f, -60f), new Vector3(-29f, 0.14f, -4f), k.Sidewalk);
            for (var z = -58f; z < 50f; z += 4f)
                if (z < -6f || z > 4f) Block(k.Level, "LaneMark", new Vector3(-36.08f, 0.02f, z), new Vector3(-35.92f, 0.03f, z + 2f), k.Plaster, false);
            Block(k.Level, "CulDeSac", new Vector3(-43f, 0f, 52f), new Vector3(-29f, 0.14f, 56f), k.Sidewalk);
            Block(k.Level, "NorthWall", new Vector3(-64f, 0f, 56f), new Vector3(-10f, 2.2f, 57f), k.Brick);
            StreetSign(k, new Vector3(-30.5f, 0.14f, 5f), "ASHGROVE ST", 90f, "WILLOW LN", 0f);

            // Houses: west side face east (+X), east side face west (-X). Lots are 14 m wide.
            var houses = new List<Transform>();
            var seed = 101;
            foreach (var z in new[] { 44f, 28f, 12f, -12f, -28f, -44f })
                houses.Add(House(k, $"House_W{(int)z}", new Vector3(-43f, 0f, z), Vector3.right, seed++, k.Interact));
            foreach (var z in new[] { 44f, 28f, -28f, -44f })
                houses.Add(House(k, $"House_E{(int)z}", new Vector3(-29f, 0f, z), Vector3.left, seed++, k.Interact));
            // East side behind the Ashgrove corner: the backs of the shop and laundromat.
            Block(k.Level, "CornerWallN", new Vector3(-29f, 0f, 15f), new Vector3(-26f, 4f, 20f), k.Brick);
            Block(k.Level, "CornerWallS", new Vector3(-29f, 0f, -20f), new Vector3(-26f, 4f, -15f), k.Brick);
            Block(k.Level, "CornerFillN", new Vector3(-29f, 0f, 6.5f), new Vector3(-26f, 6f, 15f), k.Brick);
            Block(k.Level, "CornerFillS", new Vector3(-29f, 0f, -15f), new Vector3(-26f, 5f, -6.5f), k.Plaster);
            Block(k.Level, "LotsWallW", new Vector3(-64f, 0f, -56f), new Vector3(-63f, 3f, 56f), k.Brick);
            Block(k.Level, "LotsWallE", new Vector3(-12f, 0f, 20f), new Vector3(-11f, 3f, 56f), k.Brick);
            Block(k.Level, "LotsWallE2", new Vector3(-12f, 0f, -56f), new Vector3(-11f, 3f, -20f), k.Brick);

            // Street furniture along Willow Lane: lamps over the road, benches facing it, parked cars.
            foreach (var z in new[] { 40f, 20f, -20f, -40f })
            {
                StreetLamp(k, new Vector3(-41f, 0.14f, z), 90f, flicker: z == -20f);
                StreetLamp(k, new Vector3(-31f, 0.14f, z + 10f), -90f);
            }
            Bench(k, new Vector3(-42f, 0.14f, 4f), Vector3.right);
            Bench(k, new Vector3(-30.3f, 0.14f, 36f), Vector3.left);
            Bench(k, new Vector3(-42f, 0.14f, -36f), Vector3.right);
            Car(k, "ParkedCar", new Vector3(-38.6f, 0.02f, 33f), 180f, 3);
            Car(k, "ParkedCar", new Vector3(-33.4f, 0.02f, -16f), 0f, 5);
            Car(k, "CrashedCar", new Vector3(-36.5f, 0.02f, -36f), 150f, 1);
            Model(k.Level, Urban("detail-barrier-type-a"), new Vector3(-34f, 0.02f, -34f), 40f, size: 1.6f, name: "Barrier");

            // ---- Kingsport Foods and its parking lot (south end of Willow Lane).
            Block(k.Level, "ParkingLot", new Vector3(-72f, 0f, -96f), new Vector3(-26f, 0.02f, -60f), k.Asphalt);
            Block(k.Level, "LotCurbN", new Vector3(-72f, 0f, -60.4f), new Vector3(-43f, 0.18f, -60f), k.Sidewalk);
            Block(k.Level, "LotCurbN2", new Vector3(-29f, 0f, -60.4f), new Vector3(-26f, 0.18f, -60f), k.Sidewalk);
            Block(k.Level, "LotWallW", new Vector3(-73f, 0f, -122f), new Vector3(-72f, 2.2f, -56f), k.Brick);
            Block(k.Level, "LotWallE", new Vector3(-26f, 0f, -122f), new Vector3(-25f, 2.2f, -60f), k.Brick);
            Block(k.Level, "LotWallN", new Vector3(-64f, 0f, -57f), new Vector3(-43f, 2.2f, -56f), k.Brick);
            Block(k.Level, "LotWallS", new Vector3(-73f, 0f, -123f), new Vector3(-25f, 2.2f, -122f), k.Brick);
            // Stall lines in three rows; parking islands with trees and lamp poles.
            // Stall lines every 2.8 m in four rows; cars park between them.
            var line = SimpleMaterial("StallPaint", new Color(0.85f, 0.85f, 0.78f));
            foreach (var (z0, z1) in new[] { (-66f, -61f), (-76f, -71f), (-86f, -81f), (-93.5f, -88.5f) })
                for (var x = -70f; x <= -29f; x += 2.8f)
                    Block(k.Level, "StallLine", new Vector3(x - 0.06f, 0.02f, z0), new Vector3(x + 0.06f, 0.03f, z1), line, false);
            Block(k.Level, "IslandWest", new Vector3(-62f, 0f, -81f), new Vector3(-56f, 0.18f, -76f), k.Sidewalk);
            Block(k.Level, "IslandEast", new Vector3(-42f, 0f, -81f), new Vector3(-36f, 0.18f, -76f), k.Sidewalk);
            Tree(k, new Vector3(-59f, 0.18f, -78.5f), 5f, 71);
            Tree(k, new Vector3(-39f, 0.18f, -78.5f), 5f, 72);
            foreach (var x in new[] { -59f, -39f })
            {
                Model(k.Level, Urban("detail-light-double"), new Vector3(x + 1.8f, 0.18f, -78.5f), 0f, size: 6f, collider: false, name: "LotLight");
                Block(k.Level, "LotPole", new Vector3(x + 1.7f, 0.18f, -78.6f), new Vector3(x + 1.9f, 5.6f, -78.4f), k.Metal);
                PointLight(k, "LotLight", new Vector3(x + 1.8f, 5.6f, -78.5f), new Color(1f, 0.88f, 0.7f), 10f, 16f, flicker: x > -50f, shadows: true);
            }
            var lotSeed = 40;
            foreach (var (x, z, yaw) in new[] { (-65.8f, -63.5f, 0f), (-57.4f, -63.5f, 0f), (-49f, -63.5f, 180f), (-43.4f, -73.5f, 180f),
                         (-63f, -83.5f, 0f), (-51.8f, -83.5f, 0f), (-35f, -83.5f, 180f), (-60.2f, -91f, 180f) })
                Car(k, "LotCar", new Vector3(x, 0.02f, z), yaw + (lotSeed % 3 - 1) * 2f, lotSeed++);
            Cart(k, new Vector3(-54f, 0.02f, -70f), 35f);
            Cart(k, new Vector3(-47f, 0.02f, -88f), 200f, tipped: true);
            Cart(k, new Vector3(-41f, 0.02f, -66.5f), 110f);
            // Cart corral by the store.
            var corral = new GameObject("CartCorral").transform;
            corral.SetParent(k.Level);
            corral.position = new Vector3(-64f, 0.02f, -92.5f);
            LocalBox(corral, "RailL", new Vector3(-0.9f, 0f, -2f), new Vector3(-0.85f, 1f, 2f), k.Metal);
            LocalBox(corral, "RailR", new Vector3(0.85f, 0f, -2f), new Vector3(0.9f, 1f, 2f), k.Metal);
            for (var i = 0; i < 3; i++) Cart(k, corral.position + new Vector3(0f, 0f, -1.4f + i * 0.9f), 0f);

            // The store: a big box with a lit window band, an entrance canopy and a red sign.
            var store = new GameObject("KingsportFoods").transform;
            store.SetParent(k.Level);
            store.position = new Vector3(-51f, 0f, -96f);
            var storeWall = SimpleMaterial("StoreWall", new Color(0.78f, 0.72f, 0.6f), Tex("concrete"));
            var signRed = SimpleMaterial("StoreSign", new Color(0.7f, 0.08f, 0.06f), null, 0.4f, emissive: true);
            var glassLit = SimpleMaterial("StoreGlass", new Color(0.16f, 0.24f, 0.22f), null, 0.9f, emissive: true);
            LocalBox(store, "Building", new Vector3(-17f, 0f, -24f), new Vector3(17f, 7f, 0f), storeWall);
            LocalBox(store, "Parapet", new Vector3(-17.2f, 7f, -24.2f), new Vector3(17.2f, 7.6f, 0.2f), k.Trim);
            LocalBox(store, "WindowBand", new Vector3(-14f, 0.5f, 0f), new Vector3(14f, 3.3f, 0.06f), glassLit, false);
            for (var x = -14f; x <= 14f; x += 2.8f) LocalBox(store, "Mullion", new Vector3(x - 0.08f, 0.4f, 0f), new Vector3(x + 0.08f, 3.4f, 0.1f), k.Trim, false);
            LocalBox(store, "Entrance", new Vector3(-2f, 0f, 0f), new Vector3(2f, 3f, 0.12f), k.Window, false);
            LocalBox(store, "Canopy", new Vector3(-3.5f, 3.4f, 0f), new Vector3(3.5f, 3.65f, 2.5f), signRed, false);
            LocalBox(store, "SignBoard", new Vector3(-9f, 4.2f, 0f), new Vector3(9f, 6.3f, 0.15f), signRed, false);
            SignText(store, "KINGSPORT FOODS", new Vector3(0f, 5.25f, 0.2f), 180f, 0.12f, new Color(1f, 0.95f, 0.85f));
            SignText(store, "OPEN 24 HRS", new Vector3(-9f, 2.6f, 0.12f), 180f, 0.035f, new Color(1f, 0.4f, 0.3f));
            PointLight(k, "StoreGlow", new Vector3(-51f, 2.5f, -93f), new Color(0.75f, 0.95f, 0.85f), 6f, 12f, flicker: true);
            PointLight(k, "SignGlow", new Vector3(-51f, 5.2f, -94.5f), new Color(1f, 0.3f, 0.2f), 4f, 8f);
            Block(k.Level, "StoreApron", new Vector3(-68f, 0f, -96f), new Vector3(-34f, 0.14f, -93.5f), k.Sidewalk);
            Bench(k, new Vector3(-58f, 0.14f, -94.3f), Vector3.forward);
            Bench(k, new Vector3(-44f, 0.14f, -94.3f), Vector3.forward);

            // ---- Things to find.
            // The trunk marker already sits on the lot-loop's own procedural car above (x=-63, z=-83.5)
            // — no separate car model needed here; one was mistakenly added on top of it and overlapped.
            var trunk = Marker(k, "Container_Lot_Trunk", new Vector3(-63f, 0.6f, -85.8f), 0f);
            Attach<Container>(trunk, new Vector3(-63f, 0.02f, -86.8f), 1.6f, 0f).Configure("car trunk", c.Loot["garage"], "The trunk pops open. Someone was packing to leave.");
            var cooler = Model(k.Interact, Surv("chest"), new Vector3(-38f, 0.14f, -94.5f), 180f, size: 0.9f, name: "Container_Store_Cooler");
            Attach<Container>(cooler, new Vector3(-38f, 0.14f, -93.2f), 1.6f).Configure("ice chest", c.Loot["kitchen"]);
            foreach (var (n, at) in new[] { ("Pickup_WillowAmmo", new Vector3(-42.2f, 0.14f, 30f)), ("Pickup_CulDeSacAmmo", new Vector3(-36f, 0.14f, 54f)) })
            {
                var box = Marker(k, n, at, 0f);
                Attach<Pickup>(box, at + new Vector3(0f, 0f, -1f), 1.3f, 0.2f).Configure(c.Items["ammo_9mm"], 1, Glint(k, box.transform));
            }
            var ammo = Marker(k, "Pickup_LotAmmo", new Vector3(-47f, 0.02f, -87.4f), 0f);
            Attach<Pickup>(ammo, new Vector3(-47f, 0.02f, -86.4f), 1.3f, 0.2f).Configure(c.Items["ammo_9mm"], 2, Glint(k, ammo.transform));
            var watch = Marker(k, "Readable_WatchSign", new Vector3(-30.2f, 1.6f, 22f), 270f);
            Attach<Readable>(watch, new Vector3(-31.2f, 0.14f, 22f), 1.6f, 0f).Configure("Read the sign",
                "NEIGHBORHOOD WATCH. Someone has stapled a flyer under it: 'Kingsport Foods closed until further notice. Do NOT go in the back.'");
            Block(k.Level, "WatchSign", new Vector3(-29.95f, 1.2f, 21.5f), new Vector3(-29.9f, 2f, 22.5f), k.WindowLit, false);

            // ---- Encounters.
            Encounter(k, "7_Willow", "Willow Lane", new Vector3(-42f, 0f, 14f), new Vector3(-30f, 0f, 34f),
                ("crawler", new Vector3(1f, 0f, 5f)), ("rat", new Vector3(-2f, 0f, -4f)), ("sporesac", new Vector3(3f, 0f, -2f)));
            Encounter(k, "8_Parking", "Kingsport Foods Lot", new Vector3(-56f, 0f, -75f), new Vector3(-36f, 0f, -64f),
                ("tendril", new Vector3(4f, 0f, 3f)), ("crawler", new Vector3(-5f, 0f, -2f)), ("spider", new Vector3(2f, 0f, -3f)));

            // ---- Screens.
            var junction = Shot(k, "WillowJunction", new Vector3(-28.4f, 6f, -6f), new Vector3(-38f, 0.5f, 2f), ShotMode.Fixed, 60f);
            Zone(k, "WillowJunction", new Vector3(-44f, 0, -8f), new Vector3(-25f, 0, 8f), junction);
            var north1 = Shot(k, "WillowNorthCCTV", new Vector3(-30f, 6.5f, 8.5f), new Vector3(-38f, 0f, 22f), ShotMode.Cctv, 60f, 60f, 30f);
            Zone(k, "WillowNorth", new Vector3(-63f, 0, 7f), new Vector3(-12f, 0, 31f), north1);
            var north2 = Shot(k, "WillowCulDeSac", new Vector3(-36f, 8f, 26f), new Vector3(-36f, 0f, 46f), ShotMode.Fixed, 62f);
            Zone(k, "WillowCulDeSac", new Vector3(-63f, 0, 30f), new Vector3(-12f, 0, 56f), north2);
            var south1 = Shot(k, "WillowSouth", new Vector3(-30.5f, 6f, -7f), new Vector3(-37f, 0f, -24f), ShotMode.Fixed, 60f);
            Zone(k, "WillowSouth", new Vector3(-63f, 0, -32f), new Vector3(-12f, 0, -7f), south1);
            var south2 = Shot(k, "WillowSouthRail", new Vector3(-30.5f, 5f, -34f), new Vector3(-37f, 0.5f, -48f), ShotMode.Fixed, 58f);
            south2.ConfigureRail(new Vector3(-30.5f, 5f, -34f), new Vector3(-30.5f, 5f, -50f));
            Zone(k, "WillowSouthEnd", new Vector3(-63f, 0, -61f), new Vector3(-12f, 0, -31f), south2);
            var lotShot = Shot(k, "ParkingCCTV", new Vector3(-27f, 8.5f, -61f), new Vector3(-50f, 0f, -82f), ShotMode.Cctv, 64f, 60f, 30f);
            Zone(k, "Parking", new Vector3(-72f, 0, -88f), new Vector3(-26f, 0, -60f), lotShot);
            var front = Shot(k, "Storefront", new Vector3(-40f, 4.5f, -80f), new Vector3(-53f, 1.2f, -95f), ShotMode.Fixed, 55f);
            Zone(k, "Storefront", new Vector3(-72f, 0, -96f), new Vector3(-26f, 0, -87f), front);

            Tour(k, "16_willow_junction", new Vector3(-36f, 0.02f, 0f), 270f);
            Tour(k, "17_willow_north", new Vector3(-36f, 0.02f, 20f), 0f);
            Tour(k, "18_willow_culdesac", new Vector3(-36f, 0.02f, 44f), 0f);
            Tour(k, "19_willow_south", new Vector3(-36f, 0.02f, -20f), 180f);
            Tour(k, "20_willow_south_end", new Vector3(-36f, 0.02f, -46f), 180f);
            Tour(k, "21_parking", new Vector3(-50f, 0.02f, -72f), 180f);
            Tour(k, "22_storefront", new Vector3(-51f, 0.14f, -91f), 180f);
        }
    }
}
