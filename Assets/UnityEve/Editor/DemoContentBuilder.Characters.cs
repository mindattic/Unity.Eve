using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEve.Data;
using UnityEve.Enemies;
using UnityEve.Interaction;
using UnityEve.Player;

namespace UnityEve.Editor
{
    public static partial class DemoContentBuilder
    {
        private static readonly Dictionary<Material, Material> Converted = new Dictionary<Material, Material>();

        /// <summary>Every opaque surface is cel-shaded with an ink outline (owner direction 2026-10-11).</summary>
        private const string ToonShader = "UnityEve/Toon";

        // ------------------------------------------------------------------ materials

        /// <summary>
        /// Converts an imported glTF material to a cel-shaded material we own (base colour/texture kept,
        /// emission available so creatures can glow during their attack telegraph).
        /// </summary>
        private static Material ToUrp(Material src, string folder, Color? tint = null)
        {
            if (src == null) return null;
            if (tint == null && Converted.TryGetValue(src, out var done)) return done;
            System.IO.Directory.CreateDirectory(folder);
            var m = new Material(Shader.Find(ToonShader));
            var color = src.HasProperty("baseColorFactor") ? src.GetColor("baseColorFactor")
                : src.HasProperty("_BaseColor") ? src.GetColor("_BaseColor") : Color.white;
            Texture tex = src.HasProperty("baseColorTexture") ? src.GetTexture("baseColorTexture")
                : src.HasProperty("_BaseMap") ? src.GetTexture("_BaseMap") : null;
            m.SetColor("_BaseColor", color * (tint ?? Color.white));
            if (tex != null) m.SetTexture("_BaseMap", tex);
            m.SetFloat("_Smoothness", 0.15f);
            m.SetFloat("_Metallic", 0f);
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", Color.black);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            var name = (src.name + (tint != null ? "_tinted" : "")).Replace("/", "_").Replace(":", "_");
            var path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{name}.mat");
            AssetDatabase.CreateAsset(m, path);
            if (tint == null) Converted[src] = m;
            return m;
        }

        private static void ConvertRenderers(GameObject root, string folder, System.Func<Material, Color?> tintFor = null)
        {
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                r.sharedMaterials = r.sharedMaterials.Select(m => ToUrp(m, folder, tintFor?.Invoke(m))).ToArray();
        }

        private static Material SimpleMaterial(string name, Color color, Texture texture = null, float smoothness = 0.1f, bool emissive = false)
        {
            var path = $"{MatDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find(ToonShader));
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = Shader.Find(ToonShader);
            m.SetColor("_BaseColor", color);
            m.SetTexture("_BaseMap", texture);
            m.SetFloat("_Smoothness", smoothness);
            if (emissive)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", color * 2f);
            }
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>The weapon range dome: translucent hexagon shell.</summary>
        private static Material HexDomeMaterial(Color color)
        {
            var path = $"{MatDir}/RangeDome.mat";
            AssetDatabase.DeleteAsset(path);
            var m = new Material(Shader.Find("UnityEve/HexDome"));
            m.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        /// <param name="particles">Use URP's particle shader, which honours vertex colours (tracers, sparks).</param>
        private static Material TransparentMaterial(string name, Color color, bool additive, bool particles = false, bool twoSided = false)
        {
            var path = $"{MatDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find(particles ? "Universal Render Pipeline/Particles/Unlit" : "Universal Render Pipeline/Unlit"));
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", additive ? 2f : 0f);
            m.SetFloat("_Cull", twoSided ? 0f : 2f);
            m.SetFloat("_ZWrite", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", additive ? (int)UnityEngine.Rendering.BlendMode.One : (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            if (additive) m.EnableKeyword("_ALPHAPREMULTIPLY_ON");
            m.SetColor("_BaseColor", color);
            EditorUtility.SetDirty(m);
            return m;
        }

        // ------------------------------------------------------------------ animation

        /// <summary>Copies a read-only imported clip into an owned .anim so loop flags can be set.</summary>
        private static AnimationClip OwnedClip(string sourcePath, string clipName, bool loop, string folder)
        {
            var src = AssetDatabase.LoadAllAssetsAtPath(sourcePath).OfType<AnimationClip>().FirstOrDefault(c => c.name == clipName);
            if (src == null)
            {
                Debug.LogWarning($"[UnityEve] clip {clipName} not found in {sourcePath}");
                return null;
            }
            System.IO.Directory.CreateDirectory(folder);
            var path = $"{folder}/{clipName}.anim";
            var copy = Object.Instantiate(src);
            copy.name = clipName;
            var settings = AnimationUtility.GetAnimationClipSettings(copy);
            settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(copy, settings);
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(copy, path);
            return copy;
        }

        private static AnimatorState AddState(AnimatorStateMachine sm, string name, Motion motion, Vector3 pos, float speed = 1f)
        {
            var s = sm.AddState(name, pos);
            s.motion = motion;
            s.speed = speed;
            s.writeDefaultValues = true;
            return s;
        }

        private static void TriggerFromAny(AnimatorStateMachine sm, AnimatorState state, string trigger, AnimatorState back, float exitTime = 0.85f)
        {
            var t = sm.AddAnyStateTransition(state);
            t.AddCondition(AnimatorConditionMode.If, 0, trigger);
            t.duration = 0.05f;
            t.canTransitionToSelf = true;
            if (back == null) return;
            var r = state.AddTransition(back);
            r.hasExitTime = true;
            r.exitTime = exitTime;
            r.duration = 0.12f;
        }

        private static AnimatorController PlayerAnimator()
        {
            const string ual = Art + "/Models/kat/UAL1_Standard.glb";
            var folder = AnimDir + "/Kat";
            // OwnedClip deletes and recreates the .anim, so a second request for the same clip would
            // orphan the first tree's reference (explore lost its walk/jog to the battle tree). Copy once.
            var owned = new Dictionary<string, AnimationClip>();
            AnimationClip C(string name, bool loop) =>
                owned.TryGetValue(name, out var clip) ? clip : owned[name] = OwnedClip(ual, name, loop, folder);

            var path = AnimDir + "/Kat.controller";
            AssetDatabase.DeleteAsset(path);
            var ac = AnimatorController.CreateAnimatorControllerAtPath(path);
            foreach (var p in new[] { "Speed" }) ac.AddParameter(p, AnimatorControllerParameterType.Float);
            foreach (var p in new[] { "InBattle", "Searching" }) ac.AddParameter(p, AnimatorControllerParameterType.Bool);
            foreach (var p in new[] { "Shoot", "Hit", "Dodge", "Cast", "Throw", "Die", "Revive" }) ac.AddParameter(p, AnimatorControllerParameterType.Trigger);

            var sm = ac.layers[0].stateMachine;
            BlendTree Locomotion(string name, AnimationClip idle)
            {
                var tree = new BlendTree { name = name, blendParameter = "Speed", useAutomaticThresholds = false };
                AssetDatabase.AddObjectToAsset(tree, ac);
                tree.AddChild(idle, 0f);
                tree.AddChild(C("Walk_Loop", true), 1.6f);
                tree.AddChild(C("Jog_Fwd_Loop", true), 4f);
                return tree;
            }
            var explore = AddState(sm, "Explore", Locomotion("ExploreTree", C("Idle_Loop", true)), new Vector3(300, 0));
            var battle = AddState(sm, "Battle", Locomotion("BattleTree", C("Pistol_Idle_Loop", true)), new Vector3(300, 100));
            sm.defaultState = explore;
            var toBattle = explore.AddTransition(battle);
            toBattle.AddCondition(AnimatorConditionMode.If, 0, "InBattle");
            toBattle.duration = 0.2f;
            var toExplore = battle.AddTransition(explore);
            toExplore.AddCondition(AnimatorConditionMode.IfNot, 0, "InBattle");
            toExplore.duration = 0.3f;

            var search = AddState(sm, "Search", C("Interact", true), new Vector3(600, -100));
            var into = explore.AddTransition(search);
            into.AddCondition(AnimatorConditionMode.If, 0, "Searching");
            into.duration = 0.15f;
            var outOf = search.AddTransition(explore);
            outOf.AddCondition(AnimatorConditionMode.IfNot, 0, "Searching");
            outOf.duration = 0.2f;

            TriggerFromAny(sm, AddState(sm, "Shoot", C("Pistol_Shoot", false), new Vector3(600, 0), 1.4f), "Shoot", battle);
            TriggerFromAny(sm, AddState(sm, "Hit", C("Hit_Chest", false), new Vector3(600, 60)), "Hit", battle);
            TriggerFromAny(sm, AddState(sm, "Dodge", C("Roll", false), new Vector3(600, 120), 3.4f), "Dodge", battle, 0.9f);
            TriggerFromAny(sm, AddState(sm, "Cast", C("Spell_Simple_Shoot", false), new Vector3(600, 180)), "Cast", battle);
            TriggerFromAny(sm, AddState(sm, "Throw", C("Punch_Cross", false), new Vector3(600, 240), 1.6f), "Throw", battle);
            var die = AddState(sm, "Die", C("Death01", false), new Vector3(600, 300));
            TriggerFromAny(sm, die, "Die", null);
            var revive = die.AddTransition(explore);
            revive.AddCondition(AnimatorConditionMode.If, 0, "Revive");
            revive.duration = 0.1f;
            return ac;
        }

        private static AnimatorController CreatureAnimator(string species, string modelPath, bool flier)
        {
            var folder = $"{AnimDir}/{species}";
            var clips = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<AnimationClip>().Select(c => c.name).ToList();
            AnimationClip Find(params string[] suffixes)
            {
                foreach (var s in suffixes)
                {
                    var name = clips.FirstOrDefault(c => c.EndsWith("_" + s));
                    if (name != null) return OwnedClip(modelPath, name, s != "Attack" && s != "Death", folder);
                }
                return null;
            }

            var path = $"{AnimDir}/{species}.controller";
            AssetDatabase.DeleteAsset(path);
            var ac = AnimatorController.CreateAnimatorControllerAtPath(path);
            ac.AddParameter("Speed", AnimatorControllerParameterType.Float);
            ac.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
            ac.AddParameter("Die", AnimatorControllerParameterType.Trigger);
            var sm = ac.layers[0].stateMachine;

            var idleClip = flier ? Find("Flying") : Find("Idle");
            var moveClip = flier ? idleClip : Find("Walk", "Run", "Jump") ?? idleClip;
            var tree = new BlendTree { name = "Move", blendParameter = "Speed", useAutomaticThresholds = false };
            AssetDatabase.AddObjectToAsset(tree, ac);
            tree.AddChild(idleClip, 0f);
            tree.AddChild(moveClip, 1.5f);
            var move = AddState(sm, "Move", tree, new Vector3(300, 0));
            sm.defaultState = move;
            TriggerFromAny(sm, AddState(sm, "Attack", Find("Attack"), new Vector3(600, 0)), "Attack", move, 0.9f);
            var die = AddState(sm, "Die", Find("Death"), new Vector3(600, 100));
            TriggerFromAny(sm, die, "Die", null);
            return ac;
        }

        // ------------------------------------------------------------------ player

        /// <summary>
        /// Kat: the Quaternius Universal Animation Library skeleton (which carries the clips), wearing the
        /// Universal Base Character body, hair and brows. Both ship the same 65-joint rig, so each skinned
        /// mesh is simply re-pointed at the animated skeleton's bones by name — no retargeting.
        /// </summary>
        private static GameObject BuildPlayerPrefab()
        {
            const string kat = Art + "/Models/kat";
            var rig = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>($"{kat}/UAL1_Standard.glb"));
            rig.name = "Kat";
            var bones = rig.GetComponentsInChildren<Transform>(true).GroupBy(t => t.name).ToDictionary(g => g.Key, g => g.First());

            foreach (var r in rig.GetComponentsInChildren<SkinnedMeshRenderer>(true)) Object.DestroyImmediate(r.gameObject);

            foreach (var part in new[] { "Superhero_Female_FullBody.gltf", "Hair_Long.gltf" })
            {
                var src = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>($"{kat}/{part}"));
                foreach (var smr in src.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    smr.bones = smr.bones.Select(b => b != null && bones.TryGetValue(b.name, out var nb) ? nb : null).ToArray();
                    if (smr.rootBone != null && bones.TryGetValue(smr.rootBone.name, out var root)) smr.rootBone = root;
                    smr.transform.SetParent(rig.transform, false);
                    smr.updateWhenOffscreen = true;
                }
                Object.DestroyImmediate(src);
            }

            ConvertRenderers(rig, MatDir + "/Kat");
            // Paint her clothes into the body albedo (boots, jeans, tank top), as ExperimentEve does.
            var body = rig.GetComponentsInChildren<SkinnedMeshRenderer>(true).FirstOrDefault(r => r.name.Contains("Superhero"));
            if (body != null)
            {
                var outfit = OutfitPainter.Paint(body.sharedMesh, $"{MatDir}/Kat/KatOutfit.png");
                var mat = body.sharedMaterial;
                mat.SetTexture("_BaseMap", outfit);
                mat.SetColor("_BaseColor", Color.white);
                mat.SetFloat("_Smoothness", 0.1f);
                EditorUtility.SetDirty(mat);
            }

            var animator = rig.GetComponent<Animator>() ?? rig.AddComponent<Animator>();
            animator.runtimeAnimatorController = PlayerAnimator();
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            var cc = rig.AddComponent<CharacterController>();
            cc.height = 1.7f;
            cc.radius = 0.3f;
            cc.center = new Vector3(0f, 0.87f, 0f);
            cc.stepOffset = 0.35f;
            rig.AddComponent<PlayerController>().Configure(animator);
            rig.AddComponent<PlayerCharacter>();
            rig.AddComponent<Interactor>();
            rig.tag = "Player";

            var prefab = PrefabUtility.SaveAsPrefabAsset(rig, $"{PrefabDir}/Kat.prefab");
            Object.DestroyImmediate(rig);
            return prefab;
        }

        // ------------------------------------------------------------------ creatures

        private static void BuildEnemyPrefabs(Content c)
        {
            // species, model, longest horizontal size in metres, aim height, model yaw correction
            var specs = new (string id, string model, float size, float aim, bool flier)[]
            {
                ("rat", "Rat", 0.95f, 0.3f, false),
                ("spider", "Spider", 1.1f, 0.35f, false),
                ("frog", "Frog", 0.9f, 0.4f, false),
                ("snake", "Snake", 1.0f, 0.45f, false),
                ("slime", "Slime", 0.85f, 0.4f, false),
                ("wasp", "Wasp", 0.75f, 0.0f, true),
                ("bat", "Bat", 0.8f, 0.0f, true),
            };
            foreach (var s in specs)
            {
                var modelPath = $"{Art}/Models/creatures/{s.model}.glb";
                var root = new GameObject($"Enemy_{s.id}");
                var model = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(modelPath), root.transform);
                model.name = "Model";
                var bounds = RendererBounds(model);
                var scale = s.size / Mathf.Max(0.01f, Mathf.Max(bounds.size.x, bounds.size.z));
                model.transform.localScale = Vector3.one * scale;
                bounds = RendererBounds(model);
                model.transform.localPosition = new Vector3(-bounds.center.x, -bounds.min.y, -bounds.center.z);
                ConvertRenderers(model, $"{MatDir}/{s.id}");

                var animator = model.GetComponent<Animator>() ?? model.AddComponent<Animator>();
                animator.runtimeAnimatorController = CreatureAnimator(s.id, modelPath, s.flier);
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                foreach (var smr in model.GetComponentsInChildren<SkinnedMeshRenderer>()) smr.updateWhenOffscreen = true;

                var height = RendererBounds(model).size.y;
                var actor = root.AddComponent<EnemyActor>();
                actor.Configure(c.Enemies[s.id], animator, s.size * 0.45f, s.flier ? 0f : Mathf.Max(s.aim, height * 0.6f));

                var prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabDir}/Enemy_{s.id}.prefab");
                Object.DestroyImmediate(root);
                c.Enemies[s.id].Prefab = prefab;
                EditorUtility.SetDirty(c.Enemies[s.id]);
            }
        }

        private static Bounds RendererBounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>(true);
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            var b = rs[0].bounds;
            foreach (var r in rs.Skip(1)) b.Encapsulate(r.bounds);
            return b;
        }
    }
}
