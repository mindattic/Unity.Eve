using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Unity.Eve.Cameras;
using Unity.Eve.Enemies;
using Unity.Eve.Game;
using Unity.Eve.Interaction;

namespace Unity.Eve.Editor
{
    /// <summary>
    /// The vertical slice's spine: Saint Ansel's nave and the Mito Mother (the boss), payphone save
    /// points across town, the story's objectives, and the music.
    /// </summary>
    public static partial class DemoContentBuilder
    {
        private static readonly Vector3 ChurchOrigin = new Vector3(110f, 0f, -20f);

        /// <summary>The Mito Mother: a vast pulsing sac on a fleshy mound, crowned with eyes, ringed by long tentacles.</summary>
        private static void BuildMother(Content c)
        {
            var p = AlienPalette("mother", new Color(0.55f, 0.24f, 0.36f), new Color(0.2f, 0.08f, 0.14f), new Color(0.8f, 0.55f, 0.5f), new Color(1f, 0.35f, 0.5f));
            var (root, model, body) = AlienRoot("mother", 0.4f);
            Part(body, PrimitiveType.Sphere, "Mound", Vector3.zero, new Vector3(3.4f, 1.2f, 3.4f), p.Dark);
            var sac = Joint(body, "Sac", new Vector3(0f, 1.9f, 0f), Vector3.zero);
            Part(sac, PrimitiveType.Sphere, "Body", Vector3.zero, new Vector3(2.2f, 2.7f, 2.2f), p.Skin);
            Part(sac, PrimitiveType.Sphere, "Crown", new Vector3(0f, 1.1f, 0f), new Vector3(1.5f, 0.9f, 1.5f), p.Dark);
            Part(sac, PrimitiveType.Sphere, "Throat", new Vector3(0f, -0.1f, 0.95f), new Vector3(0.8f, 1.2f, 0.4f), p.Glow);
            for (var i = 0; i < 9; i++)
            {
                var a = i * Mathf.PI * 2f / 9f;
                Part(sac, PrimitiveType.Sphere, "Eye", new Vector3(Mathf.Cos(a) * 0.72f, 1.25f + (i % 2) * 0.15f, Mathf.Sin(a) * 0.72f), Vector3.one * 0.16f, p.Glow);
                Part(sac, PrimitiveType.Sphere, "Vein", new Vector3(Mathf.Cos(a + 0.3f) * 1.05f, -0.4f + (i % 3) * 0.4f, Mathf.Sin(a + 0.3f) * 1.05f), new Vector3(0.08f, 0.5f, 0.08f), p.Glow);
            }
            var limbs = new List<CreatureLimb>();
            for (var t = 0; t < 8; t++)
            {
                var yaw = t * 45f + 22.5f;
                var j = Joint(body, "Tentacle", Quaternion.Euler(0f, yaw, 0f) * new Vector3(0f, 0.5f, 1.3f), new Vector3(-35f, yaw, 0f));
                var segs = new List<Transform>();
                for (var i = 0; i < 7; i++)
                {
                    segs.Add(j);
                    j = Bone(j, "Tentacle", 0.5f, Mathf.Lerp(0.42f, 0.12f, i / 6f), i % 2 == 0 ? p.Skin : p.Belly, new Vector3(9f, 0f, 0f));
                }
                Part(j, PrimitiveType.Sphere, "Tip", Vector3.zero, Vector3.one * 0.14f, p.Glow);
                limbs.Add(new CreatureLimb { Segments = segs.ToArray(), Style = LimbStyle.Tentacle, Phase = t * 0.8f, Amplitude = 7f, SwingAxis = Vector3.right, LiftAxis = Vector3.up });
            }
            FinishAlien(c, "mother", root, model, sac, limbs, stride: 0.5f, bob: 0.08f, pulse: true, bodyRadius: 1.7f, aim: 2.4f);
        }

        /// <summary>A payphone save point: a booth post, a handset, and a green lamp that pulses.</summary>
        private static void SavePhone(SceneKit k, string name, string location, Vector3 at, Vector3 faceDir)
        {
            var booth = new GameObject(name).transform;
            booth.SetParent(k.Interact);
            booth.SetPositionAndRotation(at, Quaternion.LookRotation(faceDir));
            var phoneBody = SimpleMaterial("PhoneBody", new Color(0.18f, 0.3f, 0.5f), null, 0.6f);
            var hood = SimpleMaterial("PhoneHood", new Color(0.75f, 0.76f, 0.78f), null, 0.7f);
            var glow = SimpleMaterial("PhoneGlow", new Color(0.3f, 1f, 0.5f), null, 0.5f, emissive: true);
            LocalBox(booth, "Post", new Vector3(-0.06f, 0f, -0.06f), new Vector3(0.06f, 1.2f, 0.06f), k.Metal);
            LocalBox(booth, "Hood", new Vector3(-0.4f, 1.2f, -0.2f), new Vector3(0.4f, 2.2f, 0.25f), hood, false);
            LocalBox(booth, "Phone", new Vector3(-0.2f, 1.3f, 0.12f), new Vector3(0.2f, 1.85f, 0.26f), phoneBody, false);
            LocalBox(booth, "Handset", new Vector3(-0.27f, 1.4f, 0.2f), new Vector3(-0.2f, 1.75f, 0.3f), glow, false);
            LocalBox(booth, "SignPlate", new Vector3(-0.38f, 2.2f, 0.18f), new Vector3(0.38f, 2.45f, 0.24f), glow, false);
            SignText(booth, "PHONE", new Vector3(0f, 2.32f, 0.26f), 180f, 0.016f, new Color(0.05f, 0.2f, 0.08f));
            var lamp = PointLight(k, "SaveLamp", booth.TransformPoint(new Vector3(0f, 2f, 0.6f)), new Color(0.35f, 1f, 0.55f), 1.6f, 3.5f);
            lamp.transform.SetParent(booth, true);
            Attach<SavePoint>(booth.gameObject, booth.TransformPoint(new Vector3(0f, 0f, 1.1f)), 1.6f, 1.4f).Configure(location, lamp);
        }

        private static void BuildChurch(SceneKit k)
        {
            var c = k.Content;
            var o = ChurchOrigin;
            var stone = SimpleMaterial("ChurchStone", new Color(0.58f, 0.56f, 0.52f), Tex("concrete"));
            var pewWood = SimpleMaterial("PewWood", new Color(0.38f, 0.24f, 0.15f));
            var carpet = SimpleMaterial("Carpet", new Color(0.45f, 0.08f, 0.1f));
            var gold = SimpleMaterial("AltarGold", new Color(0.8f, 0.62f, 0.25f), null, 0.6f);
            var glassColours = new[] { new Color(0.8f, 0.2f, 0.2f), new Color(0.2f, 0.35f, 0.85f), new Color(0.85f, 0.7f, 0.2f), new Color(0.3f, 0.7f, 0.4f) };

            RoomH(k, "Nave", o + new Vector3(-7f, 0f, 0f), o + new Vector3(7f, 0f, 30f), k.Floor, stone, 9f);
            var root = new GameObject("NaveFittings").transform;
            root.SetParent(k.Level);
            root.position = o;
            LocalBox(root, "Aisle", new Vector3(-1f, 0f, 0.5f), new Vector3(1f, 0.02f, 22f), carpet, false);
            LocalBox(root, "Dais", new Vector3(-6.5f, 0f, 24f), new Vector3(6.5f, 0.5f, 30f), stone);
            LocalBox(root, "Altar", new Vector3(-1.6f, 0.5f, 27f), new Vector3(1.6f, 1.6f, 28.2f), gold);
            LocalBox(root, "AltarCross", new Vector3(-0.12f, 1.6f, 27.5f), new Vector3(0.12f, 3.4f, 27.7f), gold, false);
            LocalBox(root, "AltarCrossBar", new Vector3(-0.6f, 2.7f, 27.5f), new Vector3(0.6f, 2.92f, 27.7f), gold, false);
            // Pews face the altar (+Z): the back rest is on the -Z side.
            for (var z = 4f; z <= 18f; z += 2.2f)
            foreach (var (x0, x1) in new[] { (-6.2f, -1.4f), (1.4f, 6.2f) })
            {
                LocalBox(root, "PewSeat", new Vector3(x0, 0.42f, z), new Vector3(x1, 0.5f, z + 0.5f), pewWood, false);
                LocalBox(root, "PewBack", new Vector3(x0, 0.5f, z - 0.05f), new Vector3(x1, 1.1f, z + 0.05f), pewWood, false);
                LocalBox(root, "PewEndL", new Vector3(x0, 0f, z - 0.05f), new Vector3(x0 + 0.08f, 1.15f, z + 0.55f), pewWood, false);
                LocalBox(root, "PewEndR", new Vector3(x1 - 0.08f, 0f, z - 0.05f), new Vector3(x1, 1.15f, z + 0.55f), pewWood, false);
            }
            // Stained glass down both walls and a rose window behind the altar.
            for (var i = 0; i < 5; i++)
            {
                var z = 5f + i * 5f;
                var col = glassColours[i % glassColours.Length];
                var glass = SimpleMaterial($"StainedGlass{i % glassColours.Length}", col * 0.5f, null, 0.8f, emissive: true);
                LocalBox(root, "GlassW", new Vector3(-7f, 3f, z - 0.7f), new Vector3(-6.94f, 7f, z + 0.7f), glass, false);
                LocalBox(root, "GlassE", new Vector3(6.94f, 3f, z - 0.7f), new Vector3(7f, 7f, z + 0.7f), glass, false);
                LocalBox(root, "Column", new Vector3(-6.6f, 0f, z + 2.2f), new Vector3(-6f, 9f, z + 2.8f), stone);
                LocalBox(root, "Column", new Vector3(6f, 0f, z + 2.2f), new Vector3(6.6f, 9f, z + 2.8f), stone);
            }
            LocalBox(root, "RoseWindow", new Vector3(-2f, 5f, 29.94f), new Vector3(2f, 8f, 30f), SimpleMaterial("RoseGlass", new Color(0.55f, 0.15f, 0.3f), null, 0.8f, emissive: true), false);
            foreach (var x in new[] { -2.5f, 2.5f })
                PointLight(k, "Candles", o + new Vector3(x, 1.8f, 27.4f), new Color(1f, 0.7f, 0.35f), 4f, 9f, flicker: true, shadows: x < 0f);
            PointLight(k, "RoseLight", o + new Vector3(0f, 6f, 27f), new Color(0.9f, 0.3f, 0.45f), 6f, 16f, flicker: true);
            PointLight(k, "NaveLamp", o + new Vector3(0f, 7f, 10f), new Color(0.8f, 0.75f, 0.65f), 8f, 18f);
            PointLight(k, "NaveLampBack", o + new Vector3(0f, 7f, 20f), new Color(0.85f, 0.6f, 0.65f), 8f, 16f, flicker: true);
            foreach (var x in new[] { -5.5f, 5.5f })
                PointLight(k, "Sconce", o + new Vector3(x, 3.2f, 15f), new Color(1f, 0.75f, 0.45f), 3.5f, 8f, flicker: x > 0f);
            // Supplies left in the vestibule by the survivors.
            var shells = Marker(k, "Pickup_ChurchAmmo", o + new Vector3(-5.5f, 0.05f, 2.5f), 0f);
            Attach<Pickup>(shells, o + new Vector3(-4.5f, 0f, 2.5f), 1.3f, 0.2f).Configure(c.Items["ammo_9mm"], 2, Glint(k, shells.transform));
            var shot = Marker(k, "Pickup_ChurchShells", o + new Vector3(-5.5f, 0.05f, 4f), 0f);
            Attach<Pickup>(shot, o + new Vector3(-4.5f, 0f, 4f), 1.3f, 0.2f).Configure(c.Items["ammo_shells"], 1, Glint(k, shot.transform));
            var med = Marker(k, "Pickup_ChurchMedicine", o + new Vector3(5.5f, 0.05f, 5.2f), 0f);
            Attach<Pickup>(med, o + new Vector3(4.5f, 0f, 5.2f), 1.3f, 0.2f).Configure(c.Items["medicine"], 2, Glint(k, med.transform));
            // The Mother's nest: fleshy growths over the dais.
            var flesh = SimpleMaterial("NestFlesh", new Color(0.4f, 0.12f, 0.2f));
            for (var i = 0; i < 7; i++)
            {
                var a = i * 0.9f;
                LocalBox(root, "Growth", new Vector3(Mathf.Cos(a) * 4f - 0.6f, 0f, 22f + Mathf.Sin(a) * 1.5f), new Vector3(Mathf.Cos(a) * 4f + 0.6f, 0.4f + (i % 3) * 0.3f, 23f + Mathf.Sin(a) * 1.5f), flesh, false);
            }

            // Doors: square <-> nave.
            var naveIn = Marker(k, "Spawn_NaveInside", o + new Vector3(0f, 0f, 1.4f), 0f).transform;
            var squareOut = Marker(k, "Spawn_ChurchOutside", new Vector3(60.3f, 0.06f, 0f), 270f).transform;
            var enter = Attach<Door>(Marker(k, "Door_Church_Enter", new Vector3(61.8f, 0.3f, 0f), 270f), new Vector3(60.4f, 0.06f, 0f), 1.8f, 1.2f);
            enter.Configure("Enter Saint Ansel's", naveIn, c.Items["key_church"],
                "Barred from inside. A note is pinned to the door: 'Spare side-door key is with Frank at Kingsport Foods. - Fr. Ames'");
            enter.SetStoryFacts("flag:church_tried", "flag:church_entered");
            Locks.Edge("Street", "ChurchInside", "key_church");
            var leave = Attach<Door>(Marker(k, "Door_Church_Exit", o + new Vector3(0f, 0f, 0.1f), 0f), o + new Vector3(0f, 0f, 1.1f), 1.6f, 1.2f);
            leave.Configure("Go back out to the square", squareOut);

            // The fight: the Mother and two of her brood.
            Encounter(k, "Boss_Nave", "Saint Ansel's", o + new Vector3(-6.5f, 0f, 8f), o + new Vector3(6.5f, 0f, 29f),
                ("mother", new Vector3(0f, 0f, 4.5f)), ("crawler", new Vector3(-4f, 0f, 1f)), ("crawler", new Vector3(4f, 0f, 1f)));

            var entry = Shot(k, "NaveEntry", o + new Vector3(6.4f, 6.5f, 0.6f), o + new Vector3(-1f, 1f, 12f), ShotMode.Fixed, 62f);
            Zone(k, "NaveEntry", o + new Vector3(-7f, 0, -0.5f), o + new Vector3(7f, 0, 8.5f), entry);
            // Straight down the aisle from above the doors: the Mother framed by the rose window.
            var nave = Shot(k, "Nave", o + new Vector3(0f, 8.2f, 3.5f), o + new Vector3(0f, 1.2f, 22f), ShotMode.Cctv, 62f, 30f, 20f);
            Zone(k, "Nave", o + new Vector3(-7f, 0, 8f), o + new Vector3(7f, 0, 30f), nave);
            Tour(k, "27_nave", o + new Vector3(0f, 0f, 6f), 0f);
        }

        private static void BuildVerticalSlice(SceneKit k)
        {
            // Save points: wherever the night turns a corner.
            SavePhone(k, "SavePoint_BusStop", "Ashgrove Street", new Vector3(-21.5f, 0.14f, -6.1f), Vector3.forward);
            SavePhone(k, "SavePoint_MercerHouse", "the Mercer house", HouseOrigin + new Vector3(0.6f, 0f, 5.75f), Vector3.back);
            SavePhone(k, "SavePoint_Willow", "Willow Lane", new Vector3(-30.6f, 0.14f, -5.6f), Vector3.left);
            SavePhone(k, "SavePoint_Square", "Market Square", new Vector3(60.2f, 0.06f, -4.5f), Vector3.left);
            SavePhone(k, "SavePoint_Grocery", "Kingsport Foods", GroceryOrigin + new Vector3(-13.6f, 0f, 1.6f), Vector3.right);
            SavePhone(k, "SavePoint_Church", "Saint Ansel's", ChurchOrigin + new Vector3(6.6f, 0f, 3f), Vector3.left);

            // The story.
            var steps = new (string text, string fact)[]
            {
                ("Find out what happened to the Mercers. Their house is on Ashgrove Street.", "item:key_shed"),
                ("Tom Mercer locked a shotgun in the shed. Take it.", "weapon:shotgun"),
                ("Survivors were gathering at Saint Ansel's. Head east to the Market Square.", "flag:church_tried"),
                ("The church is barred. Find Frank's spare key at Kingsport Foods, south down Willow Lane.", "item:key_church"),
                ("Return to Saint Ansel's and get inside.", "flag:church_entered"),
                ("Destroy what waits in the nave.", "cleared:Encounter_Boss_Nave"),
            };
            var story = new GameObject("StoryDirector").AddComponent<StoryDirector>();
            story.Configure(System.Array.ConvertAll(steps, s => s.text), System.Array.ConvertAll(steps, s => s.fact));

            // Music.
            AudioClip Clip(string n) => AssetDatabase.LoadAssetAtPath<AudioClip>($"{AudioDir}/{n}.wav");
            new GameObject("Music").AddComponent<MusicDirector>().Configure(Clip("music_explore"), Clip("music_battle"), Clip("music_boss"));
        }
    }
}
