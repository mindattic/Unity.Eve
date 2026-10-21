using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Unity.Eve.Editor
{
    /// <summary>Dumps hierarchy, clip names and curve bindings of imported models (diagnostics for the asset pipeline).</summary>
    public static class AssetProbe
    {
        [MenuItem("Unity Eve/Diagnostics/Probe Models")]
        public static void Run()
        {
            var sb = new StringBuilder();
            foreach (var guid in AssetDatabase.FindAssets("", new[] { "Assets/UnityEve/Art/Models" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".glb") && !path.EndsWith(".gltf") && !path.EndsWith(".fbx")) continue;
                var all = AssetDatabase.LoadAllAssetsAtPath(path);
                sb.AppendLine($"== {path} importer={AssetImporter.GetAtPath(path)?.GetType().Name}");
                foreach (var clip in all.OfType<AnimationClip>())
                    sb.AppendLine($"  clip {clip.name} len={clip.length:F2} legacy={clip.legacy}");
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go != null)
                {
                    sb.AppendLine($"  components: {string.Join(",", go.GetComponents<Component>().Select(c => c.GetType().Name))}");
                    var r = go.GetComponentsInChildren<Renderer>(true);
                    var b = r.Length > 0 ? r.Select(x => x.bounds).Aggregate((a, c) => { a.Encapsulate(c); return a; }) : new Bounds();
                    sb.AppendLine($"  bounds size={b.size} center={b.center}");
                    foreach (var rr in r)
                        sb.AppendLine($"  renderer {rr.name} {rr.GetType().Name} mats={string.Join(",", rr.sharedMaterials.Select(m => m ? m.name + ":" + m.shader.name : "null"))}");
                    Dump(go.transform, 1, 4, sb);
                }
                foreach (var clip in all.OfType<AnimationClip>().Take(1))
                {
                    foreach (var b in AnimationUtility.GetCurveBindings(clip).Take(12))
                        sb.AppendLine($"    binding {b.path} :: {b.type.Name}.{b.propertyName}");
                }
                var walk = all.OfType<AnimationClip>().FirstOrDefault(c => c.name == "Jog_Fwd_Loop" || c.name.EndsWith("_Walk"));
                if (walk != null)
                {
                    foreach (var b in AnimationUtility.GetCurveBindings(walk).Where(b => b.propertyName.Contains("Position")).Take(6))
                    {
                        var curve = AnimationUtility.GetEditorCurve(walk, b);
                        sb.AppendLine($"    pos {walk.name} {b.path}.{b.propertyName} first={curve.keys.First().value:F3} last={curve.keys.Last().value:F3}");
                    }
                }
            }
            System.IO.Directory.CreateDirectory("Logs");
            System.IO.File.WriteAllText("Logs/asset-probe.txt", sb.ToString());
            Debug.Log("[Unity.Eve] probe written to Logs/asset-probe.txt");
        }

        private static void Dump(Transform t, int depth, int max, StringBuilder sb)
        {
            sb.AppendLine(new string(' ', depth * 2) + t.name);
            if (depth >= max) return;
            foreach (Transform c in t) Dump(c, depth + 1, max, sb);
        }
    }
}
