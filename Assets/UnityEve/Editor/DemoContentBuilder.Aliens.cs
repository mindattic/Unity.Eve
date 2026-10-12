using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEve.Enemies;

namespace UnityEve.Editor
{
    /// <summary>
    /// The alien bestiary, built from primitives (original work, no third-party models) and animated by
    /// <see cref="ProceduralCreature"/>: the Mito Crawler, Drain Tendril, Spore Sac and Husk Brute. Also
    /// mutates the CC0 animal models so they read as infected things rather than plain animals.
    /// </summary>
    public static partial class DemoContentBuilder
    {
        private sealed class AlienKit
        {
            public Material Skin, Dark, Belly, Glow;
        }

        private static AlienKit AlienPalette(string id, Color skin, Color dark, Color belly, Color glow) => new AlienKit
        {
            Skin = SimpleMaterial($"Alien_{id}_Skin", skin, null, 0.35f),
            Dark = SimpleMaterial($"Alien_{id}_Dark", dark, null, 0.5f),
            Belly = SimpleMaterial($"Alien_{id}_Belly", belly, null, 0.2f),
            Glow = TransparentMaterial($"Alien_{id}_Glow", glow, additive: true, particles: true),
        };

        private static Transform Part(Transform parent, PrimitiveType type, string name, Vector3 pos, Vector3 scale, Material mat, Vector3? euler = null)
        {
            var go = GameObject.CreatePrimitive(type);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(euler ?? Vector3.zero);
            go.transform.localScale = scale;
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            if (mat.renderQueue >= 3000) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go.transform;
        }

        private static Transform Joint(Transform parent, string name, Vector3 pos, Vector3 euler)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = pos;
            t.localRotation = Quaternion.Euler(euler);
            return t;
        }

        /// <summary>A capsule bone running along the joint's +Z; returns the joint at its far end.</summary>
        private static Transform Bone(Transform joint, string name, float length, float thickness, Material mat, Vector3 nextEuler)
        {
            Part(joint, PrimitiveType.Capsule, name, new Vector3(0f, 0f, length * 0.5f),
                new Vector3(thickness, length * 0.5f + thickness * 0.5f, thickness), mat, new Vector3(90f, 0f, 0f));
            return Joint(joint, name + "_End", new Vector3(0f, 0f, length), nextEuler);
        }

        private static (GameObject root, Transform model, Transform body) AlienRoot(string id, float bodyHeight)
        {
            var root = new GameObject($"Enemy_{id}");
            var model = new GameObject("Model").transform;
            model.SetParent(root.transform, false);
            var body = Joint(model, "Body", new Vector3(0f, bodyHeight, 0f), Vector3.zero);
            return (root, model, body);
        }

        private static void FinishAlien(Content c, string id, GameObject root, Transform model, Transform body, List<CreatureLimb> limbs,
            float stride, float bob, bool pulse, float bodyRadius, float aim)
        {
            model.gameObject.AddComponent<ProceduralCreature>().Configure(body, limbs.ToArray(), stride, bob, pulse);
            Rim(model.gameObject, new Color(1f, 0.7f, 0.45f, 0.3f));
            root.AddComponent<EnemyActor>().Configure(c.Enemies[id], null, bodyRadius, aim);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabDir}/Enemy_{id}.prefab");
            Object.DestroyImmediate(root);
            c.Enemies[id].Prefab = prefab;
            EditorUtility.SetDirty(c.Enemies[id]);
        }

        private static void BuildAlienPrefabs(Content c)
        {
            BuildCrawler(c);
            BuildTendril(c);
            BuildSporeSac(c);
            BuildBrute(c);
            BuildSpitter(c);
            BuildWarden(c);
        }

        /// <summary>Mito Crawler: a low six-legged skitterer with a glowing eye cluster.</summary>
        private static void BuildCrawler(Content c)
        {
            var p = AlienPalette("crawler", new Color(0.3f, 0.2f, 0.38f), new Color(0.14f, 0.1f, 0.18f), new Color(0.55f, 0.32f, 0.42f), new Color(0.6f, 1f, 0.3f));
            var (root, model, body) = AlienRoot("crawler", 0.4f);
            Part(body, PrimitiveType.Sphere, "Thorax", Vector3.zero, new Vector3(0.75f, 0.38f, 0.85f), p.Skin);
            Part(body, PrimitiveType.Sphere, "Abdomen", new Vector3(0f, 0.1f, -0.55f), new Vector3(0.62f, 0.5f, 0.72f), p.Belly);
            Part(body, PrimitiveType.Sphere, "Head", new Vector3(0f, 0.04f, 0.5f), new Vector3(0.36f, 0.28f, 0.34f), p.Dark);
            Part(body, PrimitiveType.Cube, "MandibleL", new Vector3(-0.09f, -0.06f, 0.69f), new Vector3(0.05f, 0.05f, 0.2f), p.Dark, new Vector3(0f, -20f, 0f));
            Part(body, PrimitiveType.Cube, "MandibleR", new Vector3(0.09f, -0.06f, 0.69f), new Vector3(0.05f, 0.05f, 0.2f), p.Dark, new Vector3(0f, 20f, 0f));
            foreach (var (x, y) in new[] { (-0.08f, 0.1f), (0.08f, 0.1f), (0f, 0.15f), (-0.13f, 0.03f), (0.13f, 0.03f) })
                Part(body, PrimitiveType.Sphere, "Eye", new Vector3(x, y, 0.64f), Vector3.one * 0.07f, p.Glow);
            for (var i = 0; i < 4; i++)
                Part(body, PrimitiveType.Sphere, "Spot", new Vector3(0f, 0.34f, -0.3f - i * 0.17f), Vector3.one * (0.09f - i * 0.012f), p.Glow);

            var limbs = new List<CreatureLimb>();
            var leg = 0;
            foreach (var side in new[] { -1f, 1f })
            foreach (var z in new[] { 0.3f, 0f, -0.3f })
            {
                var dir = new Vector3(side, 0f, z * 0.8f).normalized;
                var hip = Joint(body, "Hip", new Vector3(side * 0.3f, 0f, z), (Quaternion.LookRotation(dir) * Quaternion.Euler(-25f, 0f, 0f)).eulerAngles);
                var knee = Bone(hip, "Thigh", 0.45f, 0.08f, p.Dark, new Vector3(80f, 0f, 0f));
                Bone(knee, "Shin", 0.5f, 0.055f, p.Skin, Vector3.zero);
                var tripod = (leg % 2 == 0) == (side < 0f) ? 0f : Mathf.PI;
                limbs.Add(new CreatureLimb { Segments = new[] { hip, knee }, Style = LimbStyle.Leg, Phase = tripod, Amplitude = 28f, SwingAxis = Vector3.up, LiftAxis = Vector3.right });
                leg++;
            }
            FinishAlien(c, "crawler", root, model, body, limbs, stride: 2.6f, bob: 0.04f, pulse: false, bodyRadius: 0.55f, aim: 0.5f);
        }

        /// <summary>Drain Tendril: a swaying segmented stalk on a fleshy mound, crowned by a glowing maw.</summary>
        private static void BuildTendril(Content c)
        {
            var p = AlienPalette("tendril", new Color(0.5f, 0.22f, 0.3f), new Color(0.22f, 0.1f, 0.14f), new Color(0.7f, 0.42f, 0.38f), new Color(1f, 0.5f, 0.2f));
            var (root, model, body) = AlienRoot("tendril", 0.12f);
            Part(body, PrimitiveType.Sphere, "Mound", Vector3.zero, new Vector3(1.0f, 0.4f, 1.0f), p.Dark);
            for (var i = 0; i < 6; i++)
            {
                var a = i * Mathf.PI / 3f;
                Part(body, PrimitiveType.Sphere, "Spot", new Vector3(Mathf.Cos(a) * 0.38f, 0.1f, Mathf.Sin(a) * 0.38f), Vector3.one * 0.08f, p.Glow);
            }

            var limbs = new List<CreatureLimb>();
            var segs = new List<Transform>();
            var j = Joint(body, "Stalk0", new Vector3(0f, 0.12f, 0f), new Vector3(-90f, 0f, 0f));
            for (var i = 0; i < 7; i++)
            {
                segs.Add(j);
                var thick = Mathf.Lerp(0.34f, 0.17f, i / 6f);
                j = Bone(j, $"Stalk{i}", 0.27f, thick, i % 2 == 0 ? p.Skin : p.Belly, Vector3.zero);
            }
            // The maw: a dark head split by a glowing throat, ringed with eyes and teeth.
            Part(j, PrimitiveType.Sphere, "Head", new Vector3(0f, 0f, 0.12f), new Vector3(0.48f, 0.42f, 0.4f), p.Dark);
            Part(j, PrimitiveType.Sphere, "Throat", new Vector3(0f, 0.12f, 0.16f), new Vector3(0.26f, 0.12f, 0.24f), p.Glow);
            for (var i = 0; i < 5; i++)
            {
                var a = i * Mathf.PI * 2f / 5f;
                Part(j, PrimitiveType.Cube, "Tooth", new Vector3(Mathf.Cos(a) * 0.17f, 0.2f, 0.12f + Mathf.Sin(a) * 0.15f), new Vector3(0.04f, 0.12f, 0.04f), p.Belly);
                Part(j, PrimitiveType.Sphere, "Eye", new Vector3(Mathf.Cos(a) * 0.24f, 0.05f, 0.12f + Mathf.Sin(a) * 0.2f), Vector3.one * 0.06f, p.Glow);
            }
            limbs.Add(new CreatureLimb { Segments = segs.ToArray(), Style = LimbStyle.Tentacle, Amplitude = 9f, SwingAxis = Vector3.right, LiftAxis = Vector3.up });

            // Three feeler tendrils creeping off the mound.
            for (var t = 0; t < 3; t++)
            {
                var a = t * 120f + 30f;
                var f = Joint(body, "Feeler", Quaternion.Euler(0f, a, 0f) * new Vector3(0f, 0.05f, 0.4f), new Vector3(-10f, a, 0f));
                var fs = new List<Transform>();
                for (var i = 0; i < 4; i++)
                {
                    fs.Add(f);
                    f = Bone(f, "Feeler", 0.22f, Mathf.Lerp(0.09f, 0.04f, i / 3f), p.Skin, new Vector3(8f, 0f, 0f));
                }
                limbs.Add(new CreatureLimb { Segments = fs.ToArray(), Style = LimbStyle.Tentacle, Phase = t * 2f, Amplitude = 14f, SwingAxis = Vector3.up, LiftAxis = Vector3.right });
            }
            FinishAlien(c, "tendril", root, model, body, limbs, stride: 1.2f, bob: 0.02f, pulse: true, bodyRadius: 0.6f, aim: 1.5f);
        }

        /// <summary>Spore Sac: a hovering, pulsing sac with one great eye and dangling tentacles.</summary>
        private static void BuildSporeSac(Content c)
        {
            var p = AlienPalette("sporesac", new Color(0.62f, 0.36f, 0.58f), new Color(0.24f, 0.12f, 0.26f), new Color(0.85f, 0.85f, 0.75f), new Color(0.4f, 0.9f, 1f));
            var (root, model, body) = AlienRoot("sporesac", 0f);
            Part(body, PrimitiveType.Sphere, "Sac", Vector3.zero, new Vector3(0.78f, 0.7f, 0.78f), p.Skin);
            Part(body, PrimitiveType.Sphere, "Cap", new Vector3(0f, 0.22f, 0f), new Vector3(0.6f, 0.36f, 0.6f), p.Dark);
            Part(body, PrimitiveType.Sphere, "Eye", new Vector3(0f, 0.02f, 0.3f), new Vector3(0.3f, 0.3f, 0.18f), p.Belly);
            Part(body, PrimitiveType.Sphere, "Pupil", new Vector3(0f, 0.02f, 0.39f), new Vector3(0.13f, 0.16f, 0.06f), p.Glow);
            for (var i = 0; i < 7; i++)
            {
                var a = i * Mathf.PI * 2f / 7f;
                Part(body, PrimitiveType.Sphere, "Spore", new Vector3(Mathf.Cos(a) * 0.36f, -0.08f + (i % 2) * 0.16f, Mathf.Sin(a) * 0.36f), Vector3.one * 0.08f, p.Glow);
            }
            var limbs = new List<CreatureLimb>();
            for (var t = 0; t < 6; t++)
            {
                var a = t * Mathf.PI * 2f / 6f;
                var j = Joint(body, "Tentacle", new Vector3(Mathf.Cos(a) * 0.22f, -0.28f, Mathf.Sin(a) * 0.22f), new Vector3(90f, 0f, 0f));
                var segs = new List<Transform>();
                for (var i = 0; i < 5; i++)
                {
                    segs.Add(j);
                    j = Bone(j, "Tentacle", 0.17f, Mathf.Lerp(0.07f, 0.03f, i / 4f), i % 2 == 0 ? p.Dark : p.Skin, Vector3.zero);
                }
                limbs.Add(new CreatureLimb { Segments = segs.ToArray(), Style = LimbStyle.Tentacle, Phase = t * 1.1f, Amplitude = 16f, SwingAxis = Vector3.right, LiftAxis = Vector3.up });
            }
            FinishAlien(c, "sporesac", root, model, body, limbs, stride: 1.5f, bob: 0.06f, pulse: true, bodyRadius: 0.45f, aim: 0f);
        }

        /// <summary>Husk Brute: a hunched, plated knuckle-walker with a glowing split belly.</summary>
        private static void BuildBrute(Content c)
        {
            var p = AlienPalette("brute", new Color(0.36f, 0.4f, 0.32f), new Color(0.17f, 0.19f, 0.16f), new Color(0.55f, 0.48f, 0.4f), new Color(1f, 0.28f, 0.15f));
            var (root, model, body) = AlienRoot("brute", 1.25f);
            var torso = Joint(body, "Torso", Vector3.zero, new Vector3(22f, 0f, 0f));
            Part(torso, PrimitiveType.Sphere, "Chest", Vector3.zero, new Vector3(1.2f, 1.1f, 0.95f), p.Skin);
            Part(torso, PrimitiveType.Sphere, "Hump", new Vector3(0f, 0.38f, -0.2f), new Vector3(0.9f, 0.6f, 0.7f), p.Dark);
            Part(torso, PrimitiveType.Sphere, "ShoulderL", new Vector3(-0.55f, 0.25f, 0.05f), Vector3.one * 0.45f, p.Dark);
            Part(torso, PrimitiveType.Sphere, "ShoulderR", new Vector3(0.55f, 0.25f, 0.05f), Vector3.one * 0.45f, p.Dark);
            for (var i = 0; i < 3; i++)
                Part(torso, PrimitiveType.Sphere, "Crack", new Vector3(-0.15f + i * 0.15f, -0.15f - i * 0.04f, 0.44f), new Vector3(0.07f, 0.32f, 0.05f), p.Glow, new Vector3(0f, 0f, -12f + i * 12f));
            var head = Joint(torso, "Head", new Vector3(0f, 0.32f, 0.45f), Vector3.zero);
            Part(head, PrimitiveType.Sphere, "Skull", Vector3.zero, new Vector3(0.34f, 0.3f, 0.34f), p.Belly);
            foreach (var x in new[] { -0.08f, 0f, 0.08f })
                Part(head, PrimitiveType.Sphere, "Eye", new Vector3(x, 0.03f + (x == 0f ? 0.06f : 0f), 0.15f), Vector3.one * 0.07f, p.Glow);

            var limbs = new List<CreatureLimb>();
            foreach (var side in new[] { -1f, 1f })
            {
                var hip = Joint(body, "Hip", new Vector3(side * 0.32f, -0.4f, -0.1f), new Vector3(90f, 0f, 0f));
                var knee = Bone(hip, "Thigh", 0.45f, 0.28f, p.Skin, new Vector3(-15f, 0f, 0f));
                Bone(knee, "Shin", 0.45f, 0.24f, p.Dark, Vector3.zero);
                limbs.Add(new CreatureLimb { Segments = new[] { hip, knee }, Style = LimbStyle.Leg, Phase = side < 0 ? 0f : Mathf.PI, Amplitude = 22f, SwingAxis = Vector3.right, LiftAxis = Vector3.right });

                var shoulder = Joint(torso, "Shoulder", new Vector3(side * 0.62f, 0.15f, 0.1f), new Vector3(70f, side * 12f, 0f));
                var elbow = Bone(shoulder, "UpperArm", 0.6f, 0.22f, p.Skin, new Vector3(-10f, 0f, 0f));
                var wrist = Bone(elbow, "Forearm", 0.62f, 0.26f, p.Dark, Vector3.zero);
                Part(wrist, PrimitiveType.Sphere, "Fist", Vector3.zero, Vector3.one * 0.34f, p.Belly);
                limbs.Add(new CreatureLimb { Segments = new[] { shoulder, elbow }, Style = LimbStyle.Arm, Phase = side < 0 ? Mathf.PI : 0f, Amplitude = 24f, SwingAxis = Vector3.right, LiftAxis = Vector3.right });
            }
            FinishAlien(c, "brute", root, model, body, limbs, stride: 1.1f, bob: 0.07f, pulse: false, bodyRadius: 0.85f, aim: 1.5f);
        }

        /// <summary>Bloat Spitter: a hovering bladder with a nozzle that lobs acid from range instead of
        /// closing to melee (<see cref="EnemyStats.Ranged"/>).</summary>
        private static void BuildSpitter(Content c)
        {
            var p = AlienPalette("spitter", new Color(0.42f, 0.58f, 0.28f), new Color(0.2f, 0.28f, 0.14f), new Color(0.75f, 0.8f, 0.55f), new Color(0.6f, 1f, 0.3f));
            var (root, model, body) = AlienRoot("spitter", 0f);
            Part(body, PrimitiveType.Sphere, "Bladder", Vector3.zero, new Vector3(0.9f, 0.8f, 0.9f), p.Skin);
            Part(body, PrimitiveType.Sphere, "Cap", new Vector3(0f, 0.3f, -0.1f), new Vector3(0.6f, 0.4f, 0.6f), p.Dark);
            Part(body, PrimitiveType.Sphere, "Nozzle", new Vector3(0f, -0.05f, 0.48f), new Vector3(0.22f, 0.22f, 0.3f), p.Belly);
            Part(body, PrimitiveType.Sphere, "NozzleTip", new Vector3(0f, -0.05f, 0.64f), Vector3.one * 0.14f, p.Glow);
            for (var i = 0; i < 6; i++)
            {
                var a = i * Mathf.PI * 2f / 6f;
                Part(body, PrimitiveType.Sphere, "Spot", new Vector3(Mathf.Cos(a) * 0.4f, -0.1f + (i % 2) * 0.12f, Mathf.Sin(a) * 0.4f), Vector3.one * 0.07f, p.Glow);
            }
            var limbs = new List<CreatureLimb>();
            for (var t = 0; t < 4; t++)
            {
                var a = t * Mathf.PI * 2f / 4f + 0.6f;
                var j = Joint(body, "Tendril", new Vector3(Mathf.Cos(a) * 0.3f, -0.3f, Mathf.Sin(a) * 0.3f), new Vector3(90f, 0f, 0f));
                var segs = new List<Transform>();
                for (var i = 0; i < 4; i++)
                {
                    segs.Add(j);
                    j = Bone(j, "Tendril", 0.18f, Mathf.Lerp(0.07f, 0.03f, i / 3f), i % 2 == 0 ? p.Dark : p.Skin, Vector3.zero);
                }
                limbs.Add(new CreatureLimb { Segments = segs.ToArray(), Style = LimbStyle.Tentacle, Phase = t * 1.4f, Amplitude = 14f, SwingAxis = Vector3.right, LiftAxis = Vector3.up });
            }
            FinishAlien(c, "spitter", root, model, body, limbs, stride: 1.3f, bob: 0.08f, pulse: true, bodyRadius: 0.5f, aim: 0.3f);
        }

        /// <summary>Husk Warden: a slow, armour-plated knuckle-walker built to block a hallway, not race her.</summary>
        private static void BuildWarden(Content c)
        {
            var p = AlienPalette("warden", new Color(0.3f, 0.32f, 0.36f), new Color(0.14f, 0.15f, 0.17f), new Color(0.5f, 0.5f, 0.52f), new Color(1f, 0.6f, 0.15f));
            var (root, model, body) = AlienRoot("warden", 1.35f);
            var torso = Joint(body, "Torso", Vector3.zero, new Vector3(12f, 0f, 0f));
            Part(torso, PrimitiveType.Sphere, "Chest", Vector3.zero, new Vector3(1.4f, 1.3f, 1.1f), p.Skin);
            Part(torso, PrimitiveType.Sphere, "Plate", new Vector3(0f, 0.05f, 0.42f), new Vector3(1.1f, 1f, 0.3f), p.Dark);
            Part(torso, PrimitiveType.Sphere, "ShoulderL", new Vector3(-0.68f, 0.3f, 0.05f), Vector3.one * 0.55f, p.Dark);
            Part(torso, PrimitiveType.Sphere, "ShoulderR", new Vector3(0.68f, 0.3f, 0.05f), Vector3.one * 0.55f, p.Dark);
            for (var i = 0; i < 3; i++)
                Part(torso, PrimitiveType.Sphere, "Rivet", new Vector3(-0.3f + i * 0.3f, 0.1f, 0.56f), Vector3.one * 0.08f, p.Glow);
            var head = Joint(torso, "Head", new Vector3(0f, 0.4f, 0.45f), Vector3.zero);
            Part(head, PrimitiveType.Sphere, "Skull", Vector3.zero, new Vector3(0.36f, 0.3f, 0.34f), p.Belly);
            foreach (var x in new[] { -0.09f, 0.09f })
                Part(head, PrimitiveType.Sphere, "Eye", new Vector3(x, 0.02f, 0.16f), Vector3.one * 0.065f, p.Glow);

            var limbs = new List<CreatureLimb>();
            foreach (var side in new[] { -1f, 1f })
            {
                var hip = Joint(body, "Hip", new Vector3(side * 0.38f, -0.45f, -0.1f), new Vector3(90f, 0f, 0f));
                var knee = Bone(hip, "Thigh", 0.5f, 0.32f, p.Skin, new Vector3(-12f, 0f, 0f));
                Bone(knee, "Shin", 0.5f, 0.27f, p.Dark, Vector3.zero);
                limbs.Add(new CreatureLimb { Segments = new[] { hip, knee }, Style = LimbStyle.Leg, Phase = side < 0 ? 0f : Mathf.PI, Amplitude = 18f, SwingAxis = Vector3.right, LiftAxis = Vector3.right });

                var shoulder = Joint(torso, "Shoulder", new Vector3(side * 0.7f, 0.2f, 0.1f), new Vector3(65f, side * 10f, 0f));
                var elbow = Bone(shoulder, "UpperArm", 0.65f, 0.25f, p.Skin, new Vector3(-8f, 0f, 0f));
                Bone(elbow, "Forearm", 0.68f, 0.3f, p.Dark, Vector3.zero);
                limbs.Add(new CreatureLimb { Segments = new[] { shoulder, elbow }, Style = LimbStyle.Arm, Phase = side < 0 ? Mathf.PI : 0f, Amplitude = 18f, SwingAxis = Vector3.right, LiftAxis = Vector3.right });
            }
            FinishAlien(c, "warden", root, model, body, limbs, stride: 0.9f, bob: 0.05f, pulse: false, bodyRadius: 0.95f, aim: 1.6f);
        }

        // ------------------------------------------------------------------ mutated animals

        /// <summary>Sickly mutation tints per species: the animal models read as infected, not as pets.</summary>
        private static Color MutationTint(string id)
        {
            switch (id)
            {
                case "rat": return new Color(0.78f, 0.95f, 0.72f);
                case "spider": return new Color(0.82f, 0.62f, 1f);
                case "frog": return new Color(0.75f, 1f, 0.95f);
                case "snake": return new Color(0.95f, 0.75f, 1f);
                case "wasp": return new Color(1f, 0.75f, 0.65f);
                case "bat": return new Color(0.78f, 0.7f, 1f);
                default: return Color.white;
            }
        }

        /// <summary>Glowing pustules and eyes grown onto an animal model, each parented to the nearest bone.</summary>
        private static void Mutate(GameObject model, string id, int seed)
        {
            var glow = TransparentMaterial($"Mutation_{id}", new Color(0.65f, 1f, 0.35f), additive: true, particles: true);
            var bones = model.GetComponentsInChildren<Transform>(true).Where(t => t.GetComponent<Renderer>() == null).ToList();
            var b = RendererBounds(model);
            var rng = new System.Random(seed);
            for (var i = 0; i < 5; i++)
            {
                var at = new Vector3(
                    Mathf.Lerp(b.min.x, b.max.x, 0.25f + 0.5f * (float)rng.NextDouble()),
                    b.max.y - b.size.y * 0.18f * (float)rng.NextDouble(),
                    Mathf.Lerp(b.min.z, b.max.z, 0.2f + 0.6f * (float)rng.NextDouble()));
                var host = bones.OrderBy(t => (t.position - at).sqrMagnitude).FirstOrDefault() ?? model.transform;
                var size = Mathf.Max(b.size.x, b.size.z) * (0.05f + 0.04f * (float)rng.NextDouble());
                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Object.DestroyImmediate(go.GetComponent<Collider>());
                go.name = "Pustule";
                go.transform.position = at;
                go.transform.localScale = Vector3.one * size;
                go.transform.SetParent(host, true);
                var r = go.GetComponent<Renderer>();
                r.sharedMaterial = glow;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }
    }
}
