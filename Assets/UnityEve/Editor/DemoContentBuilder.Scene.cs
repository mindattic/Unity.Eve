using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;
using UnityEve.Battle;
using UnityEve.Cameras;
using UnityEve.Data;
using UnityEve.Game;
using UnityEve.Input;
using UnityEve.Interaction;
using UnityEve.Player;
using UnityEve.UI;

namespace UnityEve.Editor
{
    /// <summary>
    /// The Demo level: Ashgrove Street at night. A bus stop, a street of shuttered houses, an alley to a
    /// service yard, two buildings to enter (the Mercer house and a locked shed), and east past the
    /// broken barricade the Market district (DemoContentBuilder.Market). Six encounters plus random
    /// encounters on returning to a screen, rummageable containers, fixed / CCTV / rail cameras.
    /// </summary>
    public static partial class DemoContentBuilder
    {
        private static readonly Vector3 HouseOrigin = new Vector3(0f, 0f, 60f);
        private static readonly Vector3 ShedOrigin = new Vector3(40f, 0f, 60f);

        private sealed class SceneKit
        {
            public Content Content;
            public Transform Level, Cameras, Zones, Lights, Interact;
            public Material Asphalt, Sidewalk, Ground, Brick, Plaster, Siding, Wood, Floor, Tile, Ceiling, Trim, Window, WindowLit, DoorMat, Metal;
            public int ShotCount;
            /// <summary>Camera zones by name: the screens random encounters hang off.</summary>
            public readonly Dictionary<string, CameraZone> Screens = new Dictionary<string, CameraZone>();
        }

        private static void BuildScene(Content content)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var k = new SceneKit { Content = content };
            k.Level = new GameObject("Level").transform;
            k.Cameras = new GameObject("CameraShots").transform;
            k.Zones = new GameObject("CameraZones").transform;
            k.Lights = new GameObject("Lights").transform;
            k.Interact = new GameObject("Interactables").transform;
            CreatePalette(k);
            SetupLighting(k);

            BuildStreet(k, out var spawn, out var spawnShot);
            BuildHouse(k);
            BuildShed(k);
            BuildMarket(k);
            BuildHeights(k);
            BuildGrocery(k);
            BuildChurch(k);
            BuildVerticalSlice(k);
            BuildRoaming(k);

            var player = (GameObject)PrefabUtility.InstantiatePrefab(content.PlayerPrefab);
            player.transform.SetPositionAndRotation(spawn.position, spawn.rotation);
            var character = player.GetComponent<PlayerCharacter>();
            // The pistol is the gun; the katana (always carried) replaces the old steel pipe as her melee.
            character.Configure(content.Database, new[] { content.Weapons["pistol"] }, content.Abilities.ToArray());

            BuildSystems(k, character, spawn, spawnShot);

            var path = UnityEveBuild.DemoScene;
            EditorSceneManager.SaveScene(scene, path);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(path, true) };
        }

        // ------------------------------------------------------------------ palette and lighting

        private static Texture2D Tex(string name) => AssetDatabase.LoadAssetAtPath<Texture2D>($"{Art}/Models/retro-urban/Textures/{name}.png");

        private static void CreatePalette(SceneKit k)
        {
            k.Asphalt = SimpleMaterial("Asphalt", new Color(0.32f, 0.32f, 0.34f), Tex("asphalt"), 0.25f);
            k.Sidewalk = SimpleMaterial("Sidewalk", new Color(0.55f, 0.54f, 0.52f), Tex("concrete"));
            k.Ground = SimpleMaterial("Ground", new Color(0.22f, 0.25f, 0.18f), Tex("dirt"));
            k.Brick = SimpleMaterial("Brick", new Color(0.55f, 0.32f, 0.27f), Tex("wall"));
            k.Plaster = SimpleMaterial("Plaster", new Color(0.62f, 0.6f, 0.52f), Tex("concrete"));
            k.Siding = SimpleMaterial("Siding", new Color(0.42f, 0.5f, 0.55f), Tex("planks"));
            k.Wood = SimpleMaterial("Wood", new Color(0.45f, 0.32f, 0.22f), Tex("planks"));
            k.Floor = SimpleMaterial("Floorboards", new Color(0.5f, 0.38f, 0.26f), Tex("planks"), 0.3f);
            k.Tile = SimpleMaterial("Tile", new Color(0.75f, 0.78f, 0.78f), Tex("tiles"), 0.4f);
            k.Ceiling = SimpleMaterial("Ceiling", new Color(0.7f, 0.68f, 0.62f));
            k.Trim = SimpleMaterial("Trim", new Color(0.2f, 0.18f, 0.16f));
            k.Window = SimpleMaterial("WindowDark", new Color(0.05f, 0.07f, 0.1f), null, 0.8f);
            k.WindowLit = SimpleMaterial("WindowLit", new Color(1f, 0.75f, 0.4f), null, 0.5f, emissive: true);
            k.DoorMat = SimpleMaterial("Door", new Color(0.35f, 0.18f, 0.12f), Tex("doors"));
            k.Metal = SimpleMaterial("Metal", new Color(0.4f, 0.42f, 0.45f), Tex("metal"), 0.4f);
        }

        private static void SetupLighting(SceneKit k)
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            // A readable comic-book night: strong blue moonlight and fill, so the cel bands carry the form.
            RenderSettings.ambientSkyColor = new Color(0.4f, 0.45f, 0.64f);
            RenderSettings.ambientEquatorColor = new Color(0.27f, 0.27f, 0.36f);
            RenderSettings.ambientGroundColor = new Color(0.13f, 0.12f, 0.14f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.024f;
            RenderSettings.fogColor = new Color(0.04f, 0.05f, 0.09f);
            RenderSettings.skybox = null;

            var moon = new GameObject("Moonlight").AddComponent<Light>();
            moon.transform.SetParent(k.Lights);
            moon.type = LightType.Directional;
            moon.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
            moon.color = new Color(0.55f, 0.65f, 1f);
            moon.intensity = 1.05f;
            moon.shadows = LightShadows.Soft;
        }

        private static Light PointLight(SceneKit k, string name, Vector3 at, Color color, float intensity, float range, bool flicker = false, bool shadows = false)
        {
            var l = new GameObject(name).AddComponent<Light>();
            l.transform.SetParent(k.Lights);
            l.transform.position = at;
            l.type = LightType.Point;
            l.color = color;
            l.intensity = intensity;
            l.range = range;
            l.shadows = shadows ? LightShadows.Hard : LightShadows.None;
            if (flicker) l.gameObject.AddComponent<LightFlicker>();
            return l;
        }

        // ------------------------------------------------------------------ geometry helpers

        /// <summary>An axis-aligned box from min to max corner, with tiled texture.</summary>
        private static GameObject Block(Transform parent, string name, Vector3 min, Vector3 max, Material mat, bool collider = true, float tile = 1f)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent);
            go.transform.position = (min + max) * 0.5f;
            go.transform.localScale = max - min;
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            return go;
        }

        /// <summary>
        /// Places a glTF model so its footprint is centred on <paramref name="at"/> and it rests on the
        /// floor, scaled to a target size (largest dimension) or a uniform scale; adds a box collider.
        /// </summary>
        private static GameObject Model(Transform parent, string assetPath, Vector3 at, float yaw, float size = 0f, float scale = 1f,
            bool collider = true, string name = null)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (asset == null)
            {
                Debug.LogWarning($"[UnityEve] missing model {assetPath}");
                return new GameObject(name ?? "Missing");
            }
            var wrapper = new GameObject(name ?? asset.name);
            wrapper.transform.SetParent(parent);
            var model = (GameObject)Object.Instantiate(asset, wrapper.transform);
            model.name = asset.name;
            var b = RendererBounds(model);
            var s = size > 0f ? size / Mathf.Max(0.01f, Mathf.Max(b.size.x, b.size.y, b.size.z)) : scale;
            model.transform.localScale = Vector3.one * s;
            b = RendererBounds(model);
            model.transform.localPosition = new Vector3(-b.center.x, -b.min.y, -b.center.z);
            ConvertRenderers(model, MatDir + "/Models");
            if (collider)
            {
                var box = wrapper.AddComponent<BoxCollider>();
                box.center = new Vector3(0f, b.size.y * 0.5f, 0f);
                box.size = b.size;
            }
            wrapper.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, yaw, 0f));
            return wrapper;
        }

        private static string Urban(string n) => $"{Art}/Models/retro-urban/{n}.glb";
        private static string Furn(string n) => $"{Art}/Models/furniture/{n}.glb";
        private static string Surv(string n) => $"{Art}/Models/survival/{n}.glb";

        private static CameraShot Shot(SceneKit k, string name, Vector3 position, Vector3 lookAt, ShotMode mode, float fov = 50f, float maxYaw = 55f, float maxPitch = 25f)
        {
            var go = new GameObject($"Shot_{name}");
            go.transform.SetParent(k.Cameras);
            go.transform.SetPositionAndRotation(position, Quaternion.LookRotation(lookAt - position));
            var shot = go.AddComponent<CameraShot>();
            shot.Configure(mode, fov, maxYaw, maxPitch);
            k.ShotCount++;
            return shot;
        }

        private static CameraZone Zone(SceneKit k, string name, Vector3 min, Vector3 max, CameraShot shot, int priority = 0)
        {
            var go = new GameObject($"Zone_{name}");
            go.transform.SetParent(k.Zones);
            go.transform.position = new Vector3((min.x + max.x) * 0.5f, 0f, (min.z + max.z) * 0.5f);
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = new Vector3(0f, 2f, 0f);
            box.size = new Vector3(max.x - min.x, 4f, max.z - min.z);
            var zone = go.AddComponent<CameraZone>();
            zone.Configure(shot, priority);
            k.Screens[name] = zone;
            return zone;
        }

        private static void Tour(SceneKit k, string name, Vector3 at, float yaw)
        {
            var go = new GameObject($"Tour_{name}");
            go.transform.SetParent(k.Cameras);
            go.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, yaw, 0f));
        }

        private static EncounterZone Encounter(SceneKit k, string name, string display, Vector3 min, Vector3 max, params (string enemy, Vector3 local)[] spawns)
        {
            var go = new GameObject($"Encounter_{name}");
            go.transform.SetParent(k.Level);
            go.transform.position = new Vector3((min.x + max.x) * 0.5f, 0f, (min.z + max.z) * 0.5f);
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = new Vector3(0f, 1.5f, 0f);
            box.size = new Vector3(max.x - min.x, 3f, max.z - min.z);
            var zone = go.AddComponent<EncounterZone>();
            zone.Configure(display, spawns.Select(s => new EncounterSpawn { Enemy = k.Content.Enemies[s.enemy], LocalPosition = s.local }).ToList());
            return zone;
        }

        private static T Attach<T>(GameObject host, Vector3 approach, float radius = 1.5f, float focusHeight = 0.8f) where T : Interactable
        {
            var i = host.AddComponent<T>();
            i.SetRadius(radius);
            i.SetFocusOffset(new Vector3(0f, focusHeight, 0f));
            i.SetApproachWorld(approach);
            return i;
        }

        private static GameObject Marker(SceneKit k, string name, Vector3 at, float yaw)
        {
            var go = new GameObject(name);
            go.transform.SetParent(k.Interact);
            go.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, yaw, 0f));
            return go;
        }

        /// <summary>A facade: solid block plus a grid of windows on its street face, some lit.</summary>
        private static void Facade(SceneKit k, string name, Vector3 min, Vector3 max, Material wall, int seed, bool faceSouth)
        {
            Block(k.Level, name, min, max, wall);
            var rng = new System.Random(seed);
            var z = faceSouth ? min.z - 0.03f : max.z + 0.03f;
            for (var x = min.x + 1.2f; x < max.x - 0.8f; x += 2.2f)
            for (var y = 1.2f; y < max.y - 0.8f; y += 2.6f)
            {
                var lit = rng.NextDouble() < 0.25;
                Block(k.Level, "Window", new Vector3(x - 0.5f, y, z - 0.02f), new Vector3(x + 0.5f, y + 1.1f, z + 0.02f), lit ? k.WindowLit : k.Window, false);
            }
            // Roof lip so silhouettes read against the night.
            Block(k.Level, name + "_Roof", new Vector3(min.x - 0.15f, max.y, min.z - 0.15f), new Vector3(max.x + 0.15f, max.y + 0.25f, max.z + 0.15f), k.Trim);
        }

        /// <summary>The PE-style item sparkle, parented so it vanishes with the pickup.</summary>
        private static Light Glint(SceneKit k, Transform host)
        {
            var l = PointLight(k, "Glint", host.position + Vector3.up * 0.3f, new Color(0.6f, 0.9f, 1f), 1.2f, 1.6f);
            l.transform.SetParent(host, true);
            return l;
        }

        /// <param name="yaw">Direction the lamp's arm reaches (0 = +Z): point it over the road.</param>
        private static void StreetLamp(SceneKit k, Vector3 at, float yaw, bool flicker = false)
        {
            var arm = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            Model(k.Level, Urban("detail-light-single"), at, FaceYaw(arm), size: 4.6f, collider: false, name: "StreetLamp");
            Block(k.Level, "LampPost", at + new Vector3(-0.08f, 0f, -0.08f), at + new Vector3(0.08f, 4.2f, 0.08f), k.Metal);
            PointLight(k, "LampLight", at + arm * 1f + new Vector3(0f, 4.05f, 0f), new Color(1f, 0.72f, 0.42f), 9f, 11f, flicker, shadows: true);
        }

        // ------------------------------------------------------------------ street

        private static void BuildStreet(SceneKit k, out Transform spawn, out CameraShot spawnShot)
        {
            var c = k.Content;
            Block(k.Level, "Ground", new Vector3(-60f, -0.5f, -60f), new Vector3(60f, 0f, 60f), k.Ground);
            Block(k.Level, "Road", new Vector3(-26f, 0f, -4f), new Vector3(26f, 0.02f, 4f), k.Asphalt);
            Block(k.Level, "SidewalkNorth", new Vector3(-26f, 0f, 4f), new Vector3(26f, 0.14f, 6.5f), k.Sidewalk);
            Block(k.Level, "SidewalkSouth", new Vector3(-26f, 0f, -6.5f), new Vector3(26f, 0.14f, -4f), k.Sidewalk);
            for (var x = -24f; x < 26f; x += 4f)
                Block(k.Level, "LaneMark", new Vector3(x, 0.02f, -0.08f), new Vector3(x + 2f, 0.03f, 0.08f), k.Plaster, false);
            Block(k.Level, "AlleyFloor", new Vector3(12f, 0f, -21f), new Vector3(17f, 0.03f, -6.5f), k.Sidewalk);
            Block(k.Level, "YardFloor", new Vector3(6f, 0f, -30f), new Vector3(24f, 0.03f, -21f), k.Asphalt);

            // North side: Mercer house (enterable), shop, shed (locked), brick block.
            Facade(k, "Shop", new Vector3(-26f, 0f, 6.5f), new Vector3(-11f, 6f, 15f), k.Brick, 1, faceSouth: true);
            Facade(k, "MercerHouse", new Vector3(-10.5f, 0f, 6.5f), new Vector3(-1.5f, 5.2f, 14f), k.Siding, 2, faceSouth: true);
            Facade(k, "Block", new Vector3(-1f, 0f, 6.5f), new Vector3(3f, 7f, 15f), k.Plaster, 3, faceSouth: true);
            Block(k.Level, "Shed", new Vector3(3.5f, 0f, 6.5f), new Vector3(9f, 3.4f, 12f), k.Metal);
            Block(k.Level, "ShedRoof", new Vector3(3.3f, 3.4f, 6.3f), new Vector3(9.2f, 3.6f, 12.2f), k.Trim);
            Facade(k, "Tenement", new Vector3(9.5f, 0f, 6.5f), new Vector3(26f, 8f, 15f), k.Brick, 4, faceSouth: true);
            // South side, with the alley gap at x 12..17.
            Facade(k, "Laundromat", new Vector3(-26f, 0f, -15f), new Vector3(-8f, 5f, -6.5f), k.Plaster, 5, faceSouth: false);
            Facade(k, "Apartments", new Vector3(-7.5f, 0f, -15f), new Vector3(12f, 7f, -6.5f), k.Brick, 6, faceSouth: false);
            Facade(k, "Garage", new Vector3(17f, 0f, -15f), new Vector3(26f, 5f, -6.5f), k.Siding, 7, faceSouth: false);
            // Alley and yard walls.
            Block(k.Level, "AlleyWallW", new Vector3(11f, 0f, -21f), new Vector3(12f, 4f, -15f), k.Brick);
            Block(k.Level, "AlleyWallE", new Vector3(17f, 0f, -21f), new Vector3(18f, 4f, -15f), k.Brick);
            Block(k.Level, "YardWallS", new Vector3(5f, 0f, -31f), new Vector3(25f, 3f, -30f), k.Brick);
            Block(k.Level, "YardWallW", new Vector3(5f, 0f, -30f), new Vector3(6f, 3f, -21f), k.Brick);
            Block(k.Level, "YardWallE", new Vector3(24f, 0f, -30f), new Vector3(25f, 3f, -21f), k.Brick);
            Block(k.Level, "YardWallNW", new Vector3(5f, 0f, -21f), new Vector3(11f, 3f, -20f), k.Brick);
            Block(k.Level, "YardWallNE", new Vector3(18f, 0f, -21f), new Vector3(25f, 3f, -20f), k.Brick);
            // Street ends: police barriers and a broken wall so the play space reads as closed.
            // West: the street runs on to Willow Lane (DemoContentBuilder.Heights).
            // East: the police barricade has been smashed through; the road runs on to the Market district.
            Model(k.Level, Urban("detail-barrier-strong-damaged"), new Vector3(24.5f, 0.02f, -3.4f), 70f, size: 1.6f, name: "Barrier");
            Model(k.Level, Urban("detail-barrier-strong-damaged"), new Vector3(25f, 0.02f, 3.2f), 120f, size: 1.6f, name: "Barrier");

            // Doors: Mercer house front door and the shed roll door.
            Block(k.Level, "MercerDoor", new Vector3(-6.7f, 0.14f, 6.42f), new Vector3(-5.3f, 2.4f, 6.5f), k.DoorMat, false);
            Block(k.Level, "MercerStep", new Vector3(-7f, 0.14f, 6f), new Vector3(-5f, 0.3f, 6.5f), k.Sidewalk);
            Block(k.Level, "ShedDoor", new Vector3(4.5f, 0.14f, 6.42f), new Vector3(8f, 2.8f, 6.5f), k.Metal, false);
            PointLight(k, "PorchLight", new Vector3(-6f, 2.8f, 5.9f), new Color(1f, 0.8f, 0.5f), 3f, 5f, flicker: true);

            var houseEntry = Marker(k, "Spawn_HouseInside", HouseOrigin + new Vector3(-1f, 0f, 1.1f), 0f).transform;
            var shedEntry = Marker(k, "Spawn_ShedInside", ShedOrigin + new Vector3(0f, 0f, 1.1f), 0f).transform;
            var mercerDoor = Attach<Door>(Marker(k, "Door_HouseA_Enter", new Vector3(-6f, 0.14f, 6.4f), 180f), new Vector3(-6f, 0.14f, 5.2f), 1.6f, 1.2f);
            mercerDoor.Configure("Enter the Mercer house", houseEntry);
            var shedDoor = Attach<Door>(Marker(k, "Door_Shed_Enter", new Vector3(6.25f, 0.14f, 6.4f), 180f), new Vector3(6.25f, 0.14f, 5.2f), 1.8f, 1.2f);
            shedDoor.Configure("Open the shed", shedEntry, c.Items["key_shed"], "The roll door is padlocked. The Mercers kept the shed key somewhere.");

            // Street furniture.
            for (var x = -20f; x <= 20f; x += 10f)
            {
                StreetLamp(k, new Vector3(x, 0.14f, 5.8f), 180f, flicker: x == 0f);
                StreetLamp(k, new Vector3(x + 5f, 0.14f, -5.8f), 0f);
            }
            Model(k.Level, Urban("truck-grey-cargo"), new Vector3(-14f, 0.02f, 2.2f), 90f, size: 5.5f, name: "ParkedTruck");
            Model(k.Level, Urban("truck-green"), new Vector3(19f, 0.02f, -2.4f), -90f, size: 5f, name: "AbandonedTruck");
            Bench(k, new Vector3(-18f, 0.14f, -5.9f), Vector3.forward).name = "BusStopBench"; // faces the road
            Model(k.Level, Urban("pallet"), new Vector3(10.5f, 0.14f, 5.5f), 20f, size: 1.3f, name: "Pallet");
            Model(k.Level, Urban("planks"), new Vector3(-2f, 0.14f, -5.6f), 80f, size: 1.6f, name: "Planks");
            Tree(k, new Vector3(-9f, 0.14f, -5.5f), 5f, 91);
            Block(k.Level, "Planter", new Vector3(0.4f, 0.14f, -6.2f), new Vector3(1.6f, 0.6f, -5.2f), k.Plaster);
            Tree(k, new Vector3(1f, 0.6f, -5.7f), 1.6f, 92);
            Model(k.Level, Urban("scaffolding-structure"), new Vector3(14f, 0.14f, 7.5f), 0f, size: 4.5f, name: "Scaffolding");
            Model(k.Level, Urban("detail-cables-type-a"), new Vector3(6f, 5.5f, 0f), 90f, size: 12f, collider: false, name: "Cables");

            // Rummage: street dumpster and the yard crate.
            var dumpster = Model(k.Interact, Urban("detail-dumpster-closed"), new Vector3(-22.5f, 0.14f, 5.6f), FaceYaw(Vector3.back), size: 2.1f, name: "Container_Street_Dumpster");
            Attach<Container>(dumpster, new Vector3(-22.5f, 0.14f, 4.1f), 1.8f)
                .Configure("dumpster", c.Loot["dumpster"], "You hold your breath and dig through the trash.");
            var mailbox = Model(k.Interact, Urban("detail-dumpster-open"), new Vector3(20f, 0.03f, -28.6f), FaceYaw(Vector3.forward), size: 2.1f, name: "Container_Yard_Dumpster");
            Attach<Container>(mailbox, new Vector3(20f, 0.03f, -27f), 1.8f).Configure("dumpster", c.Loot["dumpster"]);
            var crate = Model(k.Interact, Surv("box-large"), new Vector3(8f, 0.03f, -28.5f), 15f, size: 1.1f, name: "Container_Yard_Crate");
            Attach<Container>(crate, new Vector3(8.2f, 0.03f, -27.2f), 1.6f).Configure("crate", c.Loot["garage"]);

            // Pickups and readables.
            var med = Marker(k, "Pickup_Medicine", new Vector3(-12f, 0.14f, -5.4f), 0f);
            Attach<Pickup>(med, new Vector3(-12f, 0.14f, -4.6f), 1.3f, 0.2f).Configure(c.Items["medicine"], 1,
                Glint(k, med.transform));
            var gren = Marker(k, "Pickup_Grenade", new Vector3(14.5f, 0.03f, -18f), 0f);
            Attach<Pickup>(gren, new Vector3(14.5f, 0.03f, -16.8f), 1.3f, 0.2f).Configure(c.Items["grenade"], 1,
                Glint(k, gren.transform));
            var busNote = Marker(k, "Readable_BusSchedule", new Vector3(-19.5f, 1.2f, -6.4f), 0f);
            Attach<Readable>(busNote, new Vector3(-19.5f, 0.14f, -5.2f), 1.6f, 0f)
                .Configure("Read the bus schedule", "LAST BUS 11:40 PM. Someone has scrawled over it: DON'T WAIT FOR IT. THEY COME OUT OF THE DRAINS.");
            Block(k.Level, "BusSign", new Vector3(-20f, 0.9f, -6.5f), new Vector3(-19f, 1.6f, -6.42f), k.Plaster, false);
            var poster = Marker(k, "Readable_MissingPoster", new Vector3(-0f, 1.5f, -6.45f), 0f);
            Attach<Readable>(poster, new Vector3(0f, 0.14f, -5.3f), 1.6f, 0f)
                .Configure("Read the poster", "MISSING: CHARLIE, tabby cat. Last seen near the Mercer house, June 19. He was afraid of the drains.");
            Block(k.Level, "Poster", new Vector3(-0.4f, 1.1f, -6.5f), new Vector3(0.4f, 1.9f, -6.44f), k.WindowLit, false);

            // Encounters.
            Encounter(k, "1_Street", "Ashgrove Street", new Vector3(-2f, 0f, -4f), new Vector3(12f, 0f, 4f),
                ("rat", new Vector3(4f, 0f, 1.5f)), ("crawler", new Vector3(5f, 0f, -2f)), ("bat", new Vector3(3f, 0f, 0f)));
            Encounter(k, "2_Yard", "Service Yard", new Vector3(6.5f, 0f, -29.5f), new Vector3(23.5f, 0f, -21.5f),
                ("spider", new Vector3(3f, 0f, -1.5f)), ("sporesac", new Vector3(-3f, 0f, 1f)), ("rat", new Vector3(0f, 0f, -3f)));

            // Cameras. Neighbouring zones overlap by a metre: the overlap is the hysteresis.
            var west = Shot(k, "StreetWest", new Vector3(-24.5f, 5.5f, -6f), new Vector3(-12f, 0.6f, 1.5f), ShotMode.Fixed, 55f);
            Zone(k, "StreetWest", new Vector3(-26f, 0, -6.5f), new Vector3(-8f, 0, 6.5f), west);
            var porch = Shot(k, "MercerPorch", new Vector3(-2.2f, 3.6f, -2.5f), new Vector3(-6f, 1f, 5.5f), ShotMode.Fixed, 50f);
            Zone(k, "MercerPorch", new Vector3(-9f, 0, 3.5f), new Vector3(-3f, 0, 6.5f), porch, priority: 1);
            var cctv = Shot(k, "StreetCCTV", new Vector3(2.5f, 5.2f, 6.2f), new Vector3(5f, 0f, -1f), ShotMode.Cctv, 58f, 65f, 30f);
            Zone(k, "StreetCCTV", new Vector3(-9f, 0, -6.5f), new Vector3(13f, 0, 6.5f), cctv);
            var rail = Shot(k, "StreetRail", new Vector3(18f, 3.4f, -6.2f), new Vector3(18f, 0.8f, 3f), ShotMode.Fixed, 55f);
            rail.ConfigureRail(new Vector3(13f, 3.4f, -6.2f), new Vector3(23f, 3.4f, -6.2f));
            Zone(k, "StreetEast", new Vector3(12f, 0, -6.5f), new Vector3(26f, 0, 6.5f), rail);
            var alley = Shot(k, "AlleyCCTV", new Vector3(12.4f, 4.6f, -7f), new Vector3(14.5f, 0f, -16f), ShotMode.Cctv, 60f, 40f, 30f);
            Zone(k, "Alley", new Vector3(12f, 0, -21f), new Vector3(17f, 0, -6f), alley, priority: 1);
            var yard = Shot(k, "Yard", new Vector3(23.3f, 6f, -20.6f), new Vector3(13f, 0f, -26f), ShotMode.Fixed, 60f);
            Zone(k, "Yard", new Vector3(6f, 0, -30f), new Vector3(24f, 0, -20f), yard);

            Tour(k, "01_street_west", new Vector3(-16f, 0.14f, -5f), 90f);
            Tour(k, "02_mercer_porch", new Vector3(-6f, 0.14f, 5f), 0f);
            Tour(k, "03_street_cctv", new Vector3(-4f, 0.02f, -2.5f), 90f);
            Tour(k, "04_street_rail", new Vector3(20f, 0.02f, 1f), 90f);
            Tour(k, "05_alley_cctv", new Vector3(14.5f, 0.03f, -13f), 180f);
            Tour(k, "06_yard", new Vector3(10f, 0.03f, -24f), 90f);

            spawn = Marker(k, "Spawn_BusStop", new Vector3(-18f, 0.14f, -4.8f), 90f).transform;
            spawnShot = west;
        }

        // ------------------------------------------------------------------ Mercer house interior

        private static void Room(SceneKit k, string name, Vector3 min, Vector3 max, Material floor, Material walls, params (char side, float from, float to)[] openings) =>
            RoomH(k, name, min, max, floor, walls, 2.8f, openings);

        private static void RoomH(SceneKit k, string name, Vector3 min, Vector3 max, Material floor, Material walls, float h, params (char side, float from, float to)[] openings)
        {
            const float t = 0.2f;
            Block(k.Level, name + "_Floor", new Vector3(min.x, -0.2f, min.z), new Vector3(max.x, 0f, max.z), floor);
            Block(k.Level, name + "_Ceiling", new Vector3(min.x - t, h, min.z - t), new Vector3(max.x + t, h + 0.2f, max.z + t), k.Ceiling);
            void Wall(char side, float a, float b)
            {
                switch (side)
                {
                    case 'S': Block(k.Level, name + "_WallS", new Vector3(a, 0, min.z - t), new Vector3(b, h, min.z), walls); break;
                    case 'N': Block(k.Level, name + "_WallN", new Vector3(a, 0, max.z), new Vector3(b, h, max.z + t), walls); break;
                    case 'W': Block(k.Level, name + "_WallW", new Vector3(min.x - t, 0, a), new Vector3(min.x, h, b), walls); break;
                    case 'E': Block(k.Level, name + "_WallE", new Vector3(max.x, 0, a), new Vector3(max.x + t, h, b), walls); break;
                }
            }
            foreach (var side in "SNWE")
            {
                var lo = side == 'S' || side == 'N' ? min.x - (side == 'S' || side == 'N' ? t : 0) : min.z;
                var hi = side == 'S' || side == 'N' ? max.x + t : max.z;
                var cuts = openings.Where(o => o.side == side).OrderBy(o => o.from).ToList();
                var cursor = lo;
                foreach (var cut in cuts)
                {
                    if (cut.from > cursor) Wall(side, cursor, cut.from);
                    // Lintel over the doorway.
                    if (side == 'S' || side == 'N')
                    {
                        var z0 = side == 'S' ? min.z - t : max.z;
                        Block(k.Level, name + "_Lintel", new Vector3(cut.from, 2.2f, z0), new Vector3(cut.to, h, z0 + t), walls);
                    }
                    else
                    {
                        var x0 = side == 'W' ? min.x - t : max.x;
                        Block(k.Level, name + "_Lintel", new Vector3(x0, 2.2f, cut.from), new Vector3(x0 + t, h, cut.to), walls);
                    }
                    cursor = cut.to;
                }
                if (cursor < hi) Wall(side, cursor, hi);
            }
        }

        private static GameObject Furniture(SceneKit k, string model, Vector3 local, float yaw, float scale = 2f, Vector3? origin = null, bool collider = true, string name = null)
        {
            return Model(k.Interact, Furn(model), (origin ?? HouseOrigin) + local, yaw, scale: scale, collider: collider, name: name ?? model);
        }

        private static void BuildHouse(SceneKit k)
        {
            var c = k.Content;
            var o = HouseOrigin;
            var wallpaper = SimpleMaterial("Wallpaper", new Color(0.55f, 0.48f, 0.38f), Tex("wall_lines"));
            var kitchenWall = SimpleMaterial("KitchenWall", new Color(0.62f, 0.66f, 0.58f), Tex("tiles"));
            // Living room x -5..3, z 0..6; kitchen x 3..8; bathroom x -5..-1, z 6..9.
            Room(k, "Living", o + new Vector3(-5f, 0f, 0f), o + new Vector3(3f, 0f, 6f), k.Floor, wallpaper,
                ('S', o.x - 1.7f, o.x - 0.3f), ('E', o.z + 2.4f, o.z + 3.8f), ('N', o.x - 3.8f, o.x - 2.6f));
            Room(k, "Kitchen", o + new Vector3(3.2f, 0f, 0f), o + new Vector3(8f, 0f, 6f), k.Tile, kitchenWall, ('W', o.z + 2.4f, o.z + 3.8f));
            Room(k, "Bathroom", o + new Vector3(-5f, 0f, 6.2f), o + new Vector3(-1f, 0f, 9f), k.Tile, kitchenWall, ('S', o.x - 3.8f, o.x - 2.6f));
            Block(k.Level, "FrontDoorInside", o + new Vector3(-1.7f, 0f, -0.25f), o + new Vector3(-0.3f, 2.2f, -0.2f), k.DoorMat, false);

            PointLight(k, "LivingLamp", o + new Vector3(-1f, 2.5f, 3f), new Color(1f, 0.78f, 0.5f), 5f, 8f, shadows: true);
            PointLight(k, "KitchenLamp", o + new Vector3(5.6f, 2.5f, 3f), new Color(0.8f, 0.95f, 1f), 4f, 7f, flicker: true);
            PointLight(k, "BathLamp", o + new Vector3(-3f, 2.5f, 7.6f), new Color(0.9f, 1f, 0.9f), 2.5f, 5f, flicker: true);

            // Living room.
            Furniture(k, "loungeSofa", new Vector3(-3.6f, 0f, 4.9f), 180f);
            Furniture(k, "tableCoffee", new Vector3(-3.4f, 0f, 3.4f), 0f);
            Furniture(k, "rugRectangle", new Vector3(-3.2f, 0f, 3.6f), 0f, collider: false);
            var tv = Furniture(k, "televisionVintage", new Vector3(-3.5f, 0f, 1.0f), 0f, name: "Readable_TV");
            Attach<Readable>(tv, o + new Vector3(-3.5f, 0f, 2.2f), 1.8f).Configure("Look at the TV",
                "Static. Under the hiss, a news anchor repeats: '...residents of Kingsport are advised to stay indoors and seal all floor drains...'");
            Furniture(k, "loungeChair", new Vector3(-1.2f, 0f, 4.6f), 200f);
            Furniture(k, "lampRoundFloor", new Vector3(-4.5f, 0f, 5.5f), 0f);
            Furniture(k, "bookcaseOpen", new Vector3(1.6f, 0f, 5.6f), 180f);
            Furniture(k, "pottedPlant", new Vector3(2.5f, 0f, 0.5f), 0f);
            var sideTable = Furniture(k, "sideTableDrawers", new Vector3(-4.5f, 0f, 2.4f), 90f, name: "Container_House_SideTable");
            Attach<Container>(sideTable, o + new Vector3(-3.4f, 0f, 2.4f), 1.6f)
                .Configure("side table", c.Loot["drawer"], "Under a stack of unpaid bills...", c.Items["key_shed"]);
            var desk = Furniture(k, "desk", new Vector3(1.5f, 0f, 0.6f), 0f, name: "Readable_Desk");
            Attach<Readable>(desk, o + new Vector3(1.5f, 0f, 1.7f), 1.6f).Configure("Read the letter on the desk",
                "'Dana - the thing in the basement drain is getting bigger. I locked the good shotgun in the shed. Key's in the side table. - Tom'");
            var coats = Furniture(k, "coatRackStanding", new Vector3(0.2f, 0f, 0.5f), 0f, name: "Container_House_CoatRack");
            Attach<Container>(coats, o + new Vector3(0.2f, 0f, 1.5f), 1.5f).Configure("coat pockets", c.Loot["drawer"]);

            // Kitchen.
            var fridge = Furniture(k, "kitchenFridge", new Vector3(7.4f, 0f, 5.4f), 270f, name: "Container_House_Fridge");
            Attach<Container>(fridge, o + new Vector3(6.2f, 0f, 5.2f), 1.6f, 1.2f).Configure("fridge", c.Loot["kitchen"], "The light flickers on. Most of it has spoiled.");
            Furniture(k, "kitchenStove", new Vector3(7.4f, 0f, 3.8f), 270f);
            Furniture(k, "kitchenSink", new Vector3(7.4f, 0f, 2.7f), 270f);
            var drawers = Furniture(k, "kitchenCabinetDrawer", new Vector3(7.4f, 0f, 1.7f), 270f, name: "Container_House_KitchenDrawer");
            Attach<Container>(drawers, o + new Vector3(6.2f, 0f, 1.7f), 1.6f).Configure("kitchen drawer", c.Loot["drawer"]);
            Furniture(k, "kitchenCabinetUpper", new Vector3(7.6f, 1.5f, 2.7f), 270f, collider: false);
            Furniture(k, "table", new Vector3(4.8f, 0f, 1.5f), 0f);
            Furniture(k, "chair", new Vector3(4.2f, 0f, 0.8f), 30f);
            Furniture(k, "chair", new Vector3(5.6f, 0f, 2.3f), 200f);
            Furniture(k, "trashcan", new Vector3(3.6f, 0f, 5.5f), 0f);

            // Bathroom.
            Furniture(k, "bathtub", new Vector3(-4f, 0f, 8.3f), 0f);
            Furniture(k, "toilet", new Vector3(-1.6f, 0f, 8.4f), 180f);
            var cabinet = Furniture(k, "bathroomCabinet", new Vector3(-1.4f, 0f, 6.8f), 270f, name: "Container_House_Medicine");
            Attach<Container>(cabinet, o + new Vector3(-2.5f, 0f, 7f), 1.6f, 1f).Configure("medicine cabinet", c.Loot["bathroom"]);

            // Exit.
            var exitOutside = Marker(k, "Spawn_HouseOutside", new Vector3(-6f, 0.14f, 5.2f), 180f).transform;
            var exit = Attach<Door>(Marker(k, "Door_HouseA_Exit", o + new Vector3(-1f, 0f, -0.1f), 0f), o + new Vector3(-1f, 0f, 0.9f), 1.6f, 1.2f);
            exit.Configure("Go back outside", exitOutside);

            // The kitchen is where the drain-thing's children wait.
            Encounter(k, "3_Kitchen", "Mercer Kitchen", o + new Vector3(3.6f, 0f, 0.3f), o + new Vector3(7.8f, 0f, 5.8f),
                ("slime", new Vector3(1.2f, 0f, 1.6f)), ("snake", new Vector3(-0.8f, 0f, -1.5f)));

            // Cameras: a fixed corner shot in the living room, a CCTV-style nanny cam in the kitchen,
            // a high corner in the cramped bathroom.
            var living = Shot(k, "Living", o + new Vector3(2.7f, 2.55f, 0.25f), o + new Vector3(-2.5f, 0.6f, 4f), ShotMode.Fixed, 62f);
            Zone(k, "Living", o + new Vector3(-5f, 0, -0.5f), o + new Vector3(3.6f, 0, 6.1f), living);
            var kitchen = Shot(k, "KitchenNannyCam", o + new Vector3(3.5f, 2.6f, 5.75f), o + new Vector3(6f, 0.5f, 2f), ShotMode.Cctv, 64f, 45f, 25f);
            Zone(k, "Kitchen", o + new Vector3(3.2f, 0, 0f), o + new Vector3(8f, 0, 6f), kitchen, priority: 1);
            var bath = Shot(k, "Bathroom", o + new Vector3(-1.25f, 2.6f, 6.45f), o + new Vector3(-3.6f, 0.5f, 8.4f), ShotMode.Fixed, 70f);
            Zone(k, "Bathroom", o + new Vector3(-5f, 0, 6.1f), o + new Vector3(-1f, 0, 9f), bath, priority: 1);

            Tour(k, "07_house_living", o + new Vector3(-1.5f, 0f, 2.5f), 300f);
            Tour(k, "08_house_kitchen", o + new Vector3(4.5f, 0f, 3.8f), 90f);
            Tour(k, "09_house_bathroom", o + new Vector3(-2.8f, 0f, 7.2f), 270f);
        }

        // ------------------------------------------------------------------ shed interior

        private static void BuildShed(SceneKit k)
        {
            var c = k.Content;
            var o = ShedOrigin;
            Room(k, "Shed", o + new Vector3(-3f, 0f, 0f), o + new Vector3(3f, 0f, 5f), k.Sidewalk, k.Metal);
            PointLight(k, "ShedBulb", o + new Vector3(0f, 2.5f, 2.5f), new Color(1f, 0.85f, 0.6f), 4f, 7f, flicker: true, shadows: true);
            Model(k.Interact, Surv("workbench"), o + new Vector3(0f, 0f, 4.4f), 180f, size: 2.2f, name: "Workbench");
            Model(k.Interact, Surv("barrel"), o + new Vector3(-2.4f, 0f, 4.3f), 0f, size: 1.1f, name: "Barrel");
            Model(k.Interact, Surv("barrel-open"), o + new Vector3(-2.4f, 0f, 3.2f), 40f, size: 1.1f, name: "Barrel");
            var chest = Model(k.Interact, Surv("chest"), o + new Vector3(2.3f, 0f, 3.8f), 270f, size: 1.1f, name: "Container_Shed_Chest");
            Attach<Container>(chest, o + new Vector3(1.2f, 0f, 3.8f), 1.6f).Configure("tool chest", c.Loot["garage"]);
            var shotgun = Model(k.Interact, Surv("tool-axe"), o + new Vector3(0.3f, 1.05f, 4.4f), 90f, size: 0.9f, collider: false, name: "WeaponPickup_Shotgun");
            Attach<WeaponPickup>(shotgun, o + new Vector3(0.3f, 0f, 3.2f), 1.8f, 0f).Configure(c.Weapons["shotgun"], c.Items["ammo_shells"], 2);
            Glint(k, shotgun.transform);

            var outside = Marker(k, "Spawn_ShedOutside", new Vector3(6.25f, 0.14f, 5.2f), 180f).transform;
            var exit = Attach<Door>(Marker(k, "Door_Shed_Exit", o + new Vector3(0f, 0f, -0.1f), 0f), o + new Vector3(0f, 0f, 0.9f), 1.6f, 1.2f);
            exit.Configure("Go back outside", outside);
            Block(k.Level, "ShedDoorInside", o + new Vector3(-1.2f, 0f, -0.25f), o + new Vector3(1.2f, 2.4f, -0.2f), k.Metal, false);

            var shot = Shot(k, "Shed", o + new Vector3(2.7f, 2.6f, 0.3f), o + new Vector3(-0.5f, 0.6f, 3.5f), ShotMode.Fixed, 68f);
            Zone(k, "Shed", o + new Vector3(-3f, 0, -0.5f), o + new Vector3(3f, 0, 5f), shot);
            Tour(k, "10_shed", o + new Vector3(0f, 0f, 2.6f), 0f);
        }

        // ------------------------------------------------------------------ systems, camera, HUD

        private static void BuildSystems(SceneKit k, PlayerCharacter player, Transform spawn, CameraShot spawnShot)
        {
            var c = k.Content;

            // The HUD gives the camera a full-resolution target sized to the window at runtime.
            AssetDatabase.DeleteAsset($"{Gen}/PsxScreen.renderTexture");

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.02f, 0.025f, 0.05f);
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 90f;
            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<CameraDirector>().Configure(player.transform, spawnShot);
            camGo.transform.SetPositionAndRotation(spawnShot.transform.position, spawnShot.transform.rotation);

            var clearGo = new GameObject("ScreenClear");
            var clear = clearGo.AddComponent<Camera>();
            clear.cullingMask = 0;
            clear.clearFlags = CameraClearFlags.SolidColor;
            clear.backgroundColor = Color.black;
            clear.depth = -10;

            var game = new GameObject("Game");
            game.AddComponent<WorldTime>();
            game.AddComponent<GameInput>();
            game.AddComponent<AudioService>().Bank = c.Sounds;
            game.AddComponent<BattleFx>().Configure(TransparentMaterial("FxAdditive", Color.white, additive: true, particles: true, twoSided: true));

            var dome = new GameObject("RangeDome");
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.DestroyImmediate(sphere.GetComponent<Collider>());
            sphere.name = "Dome";
            sphere.transform.SetParent(dome.transform, false);
            var sphereRenderer = sphere.GetComponent<Renderer>();
            sphereRenderer.sharedMaterial = HexDomeMaterial(new Color(0.33f, 1f, 0.73f));
            sphereRenderer.shadowCastingMode = ShadowCastingMode.Off;
            var marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(marker.GetComponent<Collider>());
            marker.name = "TargetMarker";
            marker.transform.SetParent(dome.transform, false);
            marker.transform.rotation = Quaternion.Euler(45f, 0f, 45f);
            var markerRenderer = marker.GetComponent<Renderer>();
            markerRenderer.sharedMaterial = SimpleMaterial("TargetMarker", new Color(1f, 0.87f, 0.33f), null, 0.5f, emissive: true);
            markerRenderer.shadowCastingMode = ShadowCastingMode.Off;
            // A crisp ring on the ground marks the range even when the camera sits inside the dome.
            var ring = new GameObject("Ring").AddComponent<LineRenderer>();
            ring.transform.SetParent(sphere.transform, false);
            ring.useWorldSpace = false;
            ring.loop = true;
            ring.positionCount = 64;
            for (var i = 0; i < 64; i++)
            {
                var a = i / 64f * Mathf.PI * 2f;
                ring.SetPosition(i, new Vector3(Mathf.Cos(a) * 0.5f, 0.005f, Mathf.Sin(a) * 0.5f));
            }
            ring.widthMultiplier = 0.035f;
            ring.startColor = ring.endColor = new Color(0.33f, 1f, 0.73f, 0.45f);
            ring.sharedMaterial = TransparentMaterial("RangeRing", Color.white, additive: true, particles: true, twoSided: true);
            ring.shadowCastingMode = ShadowCastingMode.Off;
            var rangeDome = dome.AddComponent<RangeDome>();
            rangeDome.Configure(sphere.transform, marker.transform);

            game.AddComponent<BattleDirector>().Configure(player, rangeDome);
            game.AddComponent<GameSession>().Configure(player, spawn, spawnShot,
                new[] { c.Items["medicine"], c.Items["ammo_9mm"], c.Items["grenade"], c.Items["flash"] }, new[] { 2, 2, 2, 1 });

            // HUD: UI Toolkit document drawing the PS1 render target plus every overlay.
            var themePath = $"{Root}/UI/UnityEveTheme.tss";
            if (!System.IO.File.Exists(themePath))
            {
                System.IO.File.WriteAllText(themePath, "@import url(\"unity-theme://default\");\n");
                AssetDatabase.ImportAsset(themePath);
            }
            var panelPath = $"{Gen}/HudPanel.asset";
            var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(panelPath);
            if (panel == null)
            {
                panel = ScriptableObject.CreateInstance<PanelSettings>();
                AssetDatabase.CreateAsset(panel, panelPath);
            }
            panel.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(themePath);
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(1280, 720);
            panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panel.match = 1f;
            EditorUtility.SetDirty(panel);

            var hudGo = new GameObject("HUD");
            var doc = hudGo.AddComponent<UIDocument>();
            doc.panelSettings = panel;
            var hud = hudGo.AddComponent<GameHud>();
            hud.Configure(AssetDatabase.LoadAssetAtPath<StyleSheet>($"{Root}/UI/Hud.uss"),
                AssetDatabase.LoadAssetAtPath<Font>($"{Art}/Fonts/Bangers-Regular.ttf"));
        }
    }
}
