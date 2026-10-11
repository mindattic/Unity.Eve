using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEve.Battle;
using UnityEve.Cameras;
using UnityEve.Interaction;

namespace UnityEve.Editor
{
    /// <summary>
    /// The Market district, east of Ashgrove Street past the broken barricade: Market Crossing, the
    /// Market Square (fountain, stalls, Saint Ansel's church and steeple), a back lane and the loading
    /// dock. Five more screens, three more encounters. Also wires every outdoor screen's random
    /// encounters (<see cref="ScreenEncounters"/>).
    /// </summary>
    public static partial class DemoContentBuilder
    {
        private static void BuildMarket(SceneKit k)
        {
            var c = k.Content;
            Block(k.Level, "GroundEast", new Vector3(60f, -0.5f, -60f), new Vector3(90f, 0f, 60f), k.Ground);

            // ---- Market Crossing (x 26..40): the road runs on past the barricade.
            Block(k.Level, "CrossingRoad", new Vector3(26f, 0f, -4f), new Vector3(40f, 0.02f, 4f), k.Asphalt);
            Block(k.Level, "CrossingWalkN", new Vector3(26f, 0f, 4f), new Vector3(40f, 0.14f, 6.5f), k.Sidewalk);
            Block(k.Level, "CrossingWalkS", new Vector3(26f, 0f, -6.5f), new Vector3(40f, 0.14f, -4f), k.Sidewalk);
            for (var x = 28f; x < 40f; x += 4f)
                Block(k.Level, "LaneMark", new Vector3(x, 0.02f, -0.08f), new Vector3(x + 2f, 0.03f, 0.08f), k.Plaster, false);
            Facade(k, "Pharmacy", new Vector3(26f, 0f, 6.5f), new Vector3(40f, 6.5f, 15f), k.Plaster, 11, faceSouth: true);
            Facade(k, "Pawnshop", new Vector3(26f, 0f, -15f), new Vector3(40f, 5.5f, -6.5f), k.Brick, 12, faceSouth: false);
            Model(k.Level, Urban("detail-light-traffic"), new Vector3(38.5f, 0.14f, 5.6f), FaceYaw(Vector3.back), size: 4f, collider: false, name: "TrafficLight");
            Model(k.Level, Urban("detail-light-traffic"), new Vector3(27.5f, 0.14f, -5.6f), FaceYaw(Vector3.forward), size: 4f, collider: false, name: "TrafficLight");
            PointLight(k, "TrafficRed", new Vector3(38.5f, 3.4f, 5.2f), new Color(1f, 0.15f, 0.1f), 2.5f, 5f, flicker: true);
            Model(k.Level, Urban("truck-flat"), new Vector3(37.6f, 0.02f, 2.9f), 15f, size: 4.6f, name: "WreckedTruck");
            Model(k.Level, Urban("detail-barrier-type-a"), new Vector3(29f, 0.02f, -2.9f), 30f, size: 1.6f, name: "Barrier");
            Model(k.Level, Urban("wall-broken-type-a"), new Vector3(26.5f, 0.14f, 5.4f), 90f, size: 2.4f, name: "BrokenBarricade");
            Model(k.Level, Urban("wall-broken-type-a"), new Vector3(26.5f, 0.14f, -5.4f), 90f, size: 2.4f, name: "BrokenBarricade");
            StreetLamp(k, new Vector3(31f, 0.14f, 5.8f), 180f, flicker: true);
            StreetLamp(k, new Vector3(35f, 0.14f, -5.8f), 0f);

            // ---- Market Square (x 40..62, z -14..14).
            Block(k.Level, "SquarePaving", new Vector3(40f, 0f, -14f), new Vector3(62f, 0.06f, 14f), k.Sidewalk);
            Facade(k, "Clinic", new Vector3(40f, 0f, 14f), new Vector3(62f, 7f, 20f), k.Brick, 13, faceSouth: true);
            Facade(k, "ArcadeWest", new Vector3(40f, 0f, -20f), new Vector3(44f, 6f, -14f), k.Siding, 14, faceSouth: false);
            Facade(k, "ArcadeEast", new Vector3(48f, 0f, -20f), new Vector3(62f, 6f, -14f), k.Siding, 15, faceSouth: false);
            // The square's old north-east wall is gone: that gap is now the way through to Ashgrove Park.
            Block(k.Level, "SquareWallSE", new Vector3(62f, 0f, -20f), new Vector3(63f, 4f, -8f), k.Brick);
            // Saint Ansel's: nave, steeple, lit rose window, dark doors.
            Block(k.Level, "Church", new Vector3(62f, 0f, -8f), new Vector3(74f, 9f, 8f), k.Plaster);
            Block(k.Level, "ChurchRoof", new Vector3(61.8f, 9f, -8.2f), new Vector3(74.2f, 9.4f, 8.2f), k.Trim);
            Block(k.Level, "Steeple", new Vector3(64f, 9f, -2f), new Vector3(68f, 17f, 2f), k.Plaster);
            Block(k.Level, "SteepleCap", new Vector3(64.5f, 17f, -1.5f), new Vector3(67.5f, 20f, 1.5f), k.Trim);
            Block(k.Level, "RoseWindow", new Vector3(61.95f, 5.2f, -1.2f), new Vector3(62.02f, 7.4f, 1.2f), k.WindowLit, false);
            Block(k.Level, "ChurchDoors", new Vector3(61.95f, 0.06f, -1.1f), new Vector3(62.02f, 3.2f, 1.1f), k.DoorMat, false);
            Block(k.Level, "ChurchSteps", new Vector3(60.5f, 0.06f, -2f), new Vector3(62f, 0.3f, 2f), k.Plaster);
            PointLight(k, "RoseGlow", new Vector3(60.5f, 6f, 0f), new Color(0.9f, 0.4f, 0.3f), 4f, 9f, flicker: true);

            // The dry fountain at the heart of the square.
            var fountain = new GameObject("Fountain").transform;
            fountain.SetParent(k.Level);
            void Cyl(string name, Vector3 at, float radius, float height, Material m, bool collide)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                go.name = name;
                go.transform.SetParent(fountain);
                go.transform.position = at + Vector3.up * (height * 0.5f);
                go.transform.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f);
                go.GetComponent<Renderer>().sharedMaterial = m;
                if (!collide) Object.DestroyImmediate(go.GetComponent<Collider>());
            }
            var water = SimpleMaterial("FountainSludge", new Color(0.18f, 0.32f, 0.26f), null, 0.9f, emissive: false);
            Cyl("Basin", new Vector3(51f, 0.06f, 0f), 2.3f, 0.6f, k.Plaster, true);
            Cyl("Sludge", new Vector3(51f, 0.06f, 0f), 2.05f, 0.62f, water, false);
            Cyl("Pillar", new Vector3(51f, 0.06f, 0f), 0.35f, 1.8f, k.Plaster, true);
            Cyl("Bowl", new Vector3(51f, 1.8f, 0f), 0.95f, 0.25f, k.Plaster, false);
            PointLight(k, "FountainGlow", new Vector3(51f, 1.2f, 0f), new Color(0.35f, 0.9f, 0.6f), 3f, 6f, flicker: true);

            // Market stalls along the north side.
            foreach (var x in new[] { 44f, 50f, 56f })
            {
                Block(k.Level, "StallCounter", new Vector3(x - 1.6f, 0.06f, 9.5f), new Vector3(x + 1.6f, 1f, 10.6f), k.Wood);
                foreach (var dx in new[] { -1.55f, 1.55f })
                    Block(k.Level, "StallPost", new Vector3(x + dx - 0.06f, 0.06f, 10.4f), new Vector3(x + dx + 0.06f, 2.6f, 10.52f), k.Metal);
                Model(k.Level, Urban("detail-awning-wide"), new Vector3(x, 2.4f, 10f), 180f, size: 3.6f, collider: false, name: "StallAwning");
                Model(k.Level, Surv("box"), new Vector3(x - 0.8f, 1f, 10f), 15f, size: 0.5f, collider: false, name: "StallBox");
            }
            Tree(k, new Vector3(42.5f, 0.06f, -11.5f), 5.5f, 81);
            Tree(k, new Vector3(59.5f, 0.06f, 11.5f), 5.5f, 82);
            // Benches face the fountain.
            Bench(k, new Vector3(47f, 0.06f, -4.5f), new Vector3(51f, 0f, 0f) - new Vector3(47f, 0f, -4.5f));
            Bench(k, new Vector3(55f, 0.06f, 4.5f), new Vector3(51f, 0f, 0f) - new Vector3(55f, 0f, 4.5f));
            Bench(k, new Vector3(51f, 0.06f, -5.2f), Vector3.forward);
            foreach (var (x, z) in new[] { (44f, -8f), (58f, 8f), (44f, 8f), (58f, -8f) })
                StreetLamp(k, new Vector3(x, 0.06f, z), z > 0 ? 180f : 0f, flicker: x > 50f && z < 0f);

            // ---- Back lane (x 44..48) down to the loading dock.
            Block(k.Level, "BackLaneFloor", new Vector3(44f, 0f, -30f), new Vector3(48f, 0.03f, -14f), k.Asphalt);
            Block(k.Level, "BackLaneWallW", new Vector3(43f, 0f, -30f), new Vector3(44f, 4.5f, -20f), k.Brick);
            Block(k.Level, "BackLaneWallE", new Vector3(48f, 0f, -30f), new Vector3(49f, 4.5f, -20f), k.Brick);
            PointLight(k, "LaneBulb", new Vector3(46f, 3.6f, -22f), new Color(0.75f, 0.9f, 1f), 3f, 7f, flicker: true);
            Model(k.Level, Urban("detail-cables-type-b"), new Vector3(46f, 4.2f, -18f), 0f, size: 4.5f, collider: false, name: "Cables");

            Block(k.Level, "DockFloor", new Vector3(36f, 0f, -42f), new Vector3(58f, 0.03f, -30f), k.Asphalt);
            Block(k.Level, "DockWallS", new Vector3(35f, 0f, -43f), new Vector3(59f, 4f, -42f), k.Brick);
            Block(k.Level, "DockWallW", new Vector3(35f, 0f, -42f), new Vector3(36f, 4f, -30f), k.Brick);
            Block(k.Level, "DockWallE", new Vector3(58f, 0f, -42f), new Vector3(59f, 4f, -30f), k.Brick);
            Block(k.Level, "DockWallNW", new Vector3(35f, 0f, -30f), new Vector3(44f, 4f, -29f), k.Brick);
            Block(k.Level, "DockWallNE", new Vector3(48f, 0f, -30f), new Vector3(59f, 4f, -29f), k.Brick);
            Block(k.Level, "DockPlatform", new Vector3(40f, 0f, -42f), new Vector3(54f, 1.1f, -39f), k.Plaster);
            Model(k.Level, Urban("truck-grey-cargo"), new Vector3(39f, 0.03f, -34f), 0f, size: 5.5f, name: "DockTruck");
            Model(k.Level, Urban("pallet"), new Vector3(52f, 1.1f, -40.5f), 10f, size: 1.3f, name: "Pallet");
            Model(k.Level, Surv("box-large"), new Vector3(44f, 1.1f, -40.5f), 0f, size: 1.1f, name: "Crate");
            Model(k.Level, Surv("barrel"), new Vector3(56.5f, 0.03f, -32f), 0f, size: 1.1f, name: "Barrel");
            PointLight(k, "DockFlood", new Vector3(47f, 5f, -41.5f), new Color(1f, 0.85f, 0.6f), 6f, 14f, shadows: true);

            // ---- Things to find.
            var stall = Model(k.Interact, Surv("box-open"), new Vector3(50f, 0.06f, 8.6f), 0f, size: 0.9f, name: "Container_Market_Stall");
            Attach<Container>(stall, new Vector3(50f, 0.06f, 7.6f), 1.6f).Configure("market crate", c.Loot["kitchen"], "Bruised fruit and a few things worth taking.");
            var pawn = Model(k.Interact, Urban("detail-dumpster-closed"), new Vector3(37f, 0.14f, -6f), FaceYaw(Vector3.forward), size: 2.1f, name: "Container_Crossing_Dumpster");
            Attach<Container>(pawn, new Vector3(37f, 0.14f, -4.6f), 1.8f).Configure("dumpster", c.Loot["dumpster"]);
            var dockCrate = Model(k.Interact, Surv("chest"), new Vector3(56.8f, 0.03f, -37f), 270f, size: 1.1f, name: "Container_Dock_Chest");
            Attach<Container>(dockCrate, new Vector3(55.6f, 0.03f, -37f), 1.6f).Configure("tool chest", c.Loot["garage"]);
            var tonic = Marker(k, "Pickup_Tonic", new Vector3(60.8f, 0.3f, 1.4f), 0f);
            Attach<Pickup>(tonic, new Vector3(59.8f, 0.06f, 1.4f), 1.3f, 0.2f).Configure(c.Items["pe_tonic"], 1, Glint(k, tonic.transform));
            var med = Marker(k, "Pickup_DockMedicine", new Vector3(46f, 0.03f, -31f), 0f);
            Attach<Pickup>(med, new Vector3(46f, 0.03f, -32f), 1.3f, 0.2f).Configure(c.Items["medicine"], 1, Glint(k, med.transform));
            var notice = Marker(k, "Readable_ChurchNotice", new Vector3(61.9f, 1.5f, 2.6f), 270f);
            Attach<Readable>(notice, new Vector3(60.6f, 0.06f, 2.6f), 1.6f, 0f).Configure("Read the church notice",
                "EVENING MASS CANCELLED. Someone has added in pencil: 'the drains are singing again. the steeple bats came out of them.'");
            Block(k.Level, "Notice", new Vector3(61.94f, 1.1f, 2.2f), new Vector3(62f, 1.9f, 3f), k.WindowLit, false);

            // ---- Encounters.
            Encounter(k, "4_Crossing", "Market Crossing", new Vector3(28f, 0f, -4f), new Vector3(39f, 0f, 4f),
                ("crawler", new Vector3(3f, 0f, 1.5f)), ("crawler", new Vector3(4f, 0f, -1.5f)), ("wasp", new Vector3(2f, 0f, 0f)));
            // Centred south of the fountain so the arena's middle is open ground.
            Encounter(k, "5_Square", "Market Square", new Vector3(41f, 0f, -13f), new Vector3(61f, 0f, 5f),
                ("tendril", new Vector3(5f, 0f, -6f)), ("sporesac", new Vector3(-5f, 0f, 6f)), ("crawler", new Vector3(-5f, 0f, -5f)), ("crawler", new Vector3(6f, 0f, 6f)));
            Encounter(k, "6_Dock", "Loading Dock", new Vector3(37f, 0f, -38f), new Vector3(57f, 0f, -31f),
                ("brute", new Vector3(1f, 0f, -0.5f)), ("crawler", new Vector3(4f, 0f, 2f)));

            // ---- Screens.
            var crossing = Shot(k, "Crossing", new Vector3(39.5f, 5.5f, -6f), new Vector3(30f, 0.6f, 1f), ShotMode.Fixed, 55f);
            Zone(k, "Crossing", new Vector3(25f, 0, -6.5f), new Vector3(41f, 0, 6.5f), crossing);
            var squareWest = Shot(k, "SquareWestCCTV", new Vector3(40.8f, 6.5f, 13.4f), new Vector3(48f, 0f, 0f), ShotMode.Cctv, 62f, 55f, 30f);
            Zone(k, "SquareWest", new Vector3(40f, 0, -14f), new Vector3(51.5f, 0, 14f), squareWest);
            var squareEast = Shot(k, "SquareEast", new Vector3(61.5f, 7f, -13.2f), new Vector3(52f, 0f, 2f), ShotMode.Fixed, 58f);
            Zone(k, "SquareEast", new Vector3(50.5f, 0, -14f), new Vector3(62f, 0, 14f), squareEast);
            var lane = Shot(k, "BackLaneCCTV", new Vector3(44.3f, 5f, -14.6f), new Vector3(46f, 0f, -26f), ShotMode.Cctv, 60f, 35f, 30f);
            Zone(k, "BackLane", new Vector3(43.5f, 0, -30f), new Vector3(48.5f, 0, -13.5f), lane, priority: 1);
            var dock = Shot(k, "Dock", new Vector3(57.4f, 6.5f, -30.5f), new Vector3(46f, 0f, -37f), ShotMode.Fixed, 62f);
            Zone(k, "Dock", new Vector3(36f, 0, -42f), new Vector3(58f, 0, -29.5f), dock);

            Tour(k, "11_crossing", new Vector3(32f, 0.02f, -2.5f), 90f);
            Tour(k, "12_square_west", new Vector3(45f, 0.06f, -4f), 90f);
            Tour(k, "13_square_east", new Vector3(57f, 0.06f, 4f), 90f);
            Tour(k, "14_back_lane", new Vector3(46f, 0.03f, -22f), 180f);
            Tour(k, "15_dock", new Vector3(46f, 0.03f, -35f), 90f);
        }

        /// <summary>
        /// Random encounters on returning to an outdoor screen: each gets a roaming arena (its zone's
        /// footprint) and a table of packs that can turn up there.
        /// </summary>
        private static void BuildRoaming(SceneKit k)
        {
            var e = k.Content.Enemies;
            RoamingPack P(params string[] ids) => new RoamingPack { Enemies = ids.Select(id => e[id]).ToArray() };
            var tables = new Dictionary<string, RoamingPack[]>
            {
                ["StreetWest"] = new[] { P("rat", "rat"), P("crawler", "bat"), P("rat", "crawler") },
                ["StreetCCTV"] = new[] { P("rat", "bat"), P("crawler", "crawler") },
                ["StreetEast"] = new[] { P("spider", "crawler"), P("wasp", "bat") },
                ["Alley"] = new[] { P("crawler"), P("rat", "rat") },
                ["Yard"] = new[] { P("spider", "frog"), P("sporesac", "rat") },
                ["Crossing"] = new[] { P("crawler", "crawler"), P("wasp", "sporesac") },
                ["SquareWest"] = new[] { P("tendril", "crawler"), P("sporesac", "sporesac", "crawler") },
                ["SquareEast"] = new[] { P("brute"), P("tendril", "sporesac") },
                ["Park"] = new[] { P("rat", "crawler"), P("bat", "bat") },
                ["BackLane"] = new[] { P("crawler", "crawler") },
                ["Dock"] = new[] { P("brute", "crawler"), P("spider", "spider") },
                ["WillowJunction"] = new[] { P("rat", "crawler"), P("bat", "bat") },
                ["WillowNorth"] = new[] { P("crawler", "rat"), P("sporesac", "frog") },
                ["WillowCulDeSac"] = new[] { P("tendril", "rat"), P("crawler", "crawler", "bat") },
                ["WillowSouth"] = new[] { P("spider", "rat"), P("crawler", "sporesac") },
                ["WillowSouthEnd"] = new[] { P("crawler", "crawler"), P("snake", "frog") },
                ["Parking"] = new[] { P("tendril", "crawler"), P("brute"), P("sporesac", "sporesac", "rat") },
            };
            var screens = new List<RoamingScreen>();
            foreach (var (name, packs) in tables)
            {
                if (!k.Screens.TryGetValue(name, out var zone)) continue;
                var b = zone.GetComponent<BoxCollider>().bounds;
                var go = new GameObject($"Roaming_{name}");
                go.transform.SetParent(k.Level);
                go.transform.position = new Vector3(b.center.x, 0f, b.center.z);
                var box = go.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.center = new Vector3(0f, 1.5f, 0f);
                box.size = new Vector3(b.size.x - 0.6f, 3f, b.size.z - 0.6f);
                var arena = go.AddComponent<EncounterZone>();
                arena.Configure(ScreenTitle(name), new List<EncounterSpawn>(), roaming: true);
                screens.Add(new RoamingScreen { Screen = zone, Arena = arena, Packs = packs.ToList() });
            }
            new GameObject("ScreenEncounters").AddComponent<ScreenEncounters>().Configure(screens);
        }

        private static string ScreenTitle(string screen)
        {
            switch (screen)
            {
                case "StreetWest": case "StreetCCTV": case "StreetEast": return "Ashgrove Street";
                case "Alley": return "Back Alley";
                case "Yard": return "Service Yard";
                case "Crossing": return "Market Crossing";
                case "SquareWest": case "SquareEast": return "Market Square";
                case "Park": return "Ashgrove Park";
                case "BackLane": return "Back Lane";
                case "Dock": return "Loading Dock";
                case "WillowJunction": case "WillowNorth": case "WillowCulDeSac": case "WillowSouth": case "WillowSouthEnd": return "Willow Lane";
                case "Parking": return "Kingsport Foods Lot";
                default: return screen;
            }
        }
    }
}
