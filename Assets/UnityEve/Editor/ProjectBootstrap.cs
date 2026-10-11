using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace UnityEve.Editor
{
    /// <summary>
    /// One-shot project configuration that would otherwise be clicked together in the editor:
    /// creates the URP pipeline asset and assigns it to Graphics and every Quality level.
    /// Idempotent, so it is safe to re-run from the menu or from batch mode
    /// (<c>-executeMethod UnityEve.Editor.ProjectBootstrap.Run</c>).
    /// </summary>
    public static class ProjectBootstrap
    {
        private const string SettingsFolder = "Assets/UnityEve/Settings";
        private const string PipelinePath = SettingsFolder + "/UnityEve_URP.asset";
        private const string RendererPath = SettingsFolder + "/UnityEve_URP_Renderer.asset";

        [MenuItem("Unity Eve/Setup/Configure Project")]
        public static void Run()
        {
            Directory.CreateDirectory(SettingsFolder);

            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (pipeline == null)
            {
                var rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(rendererData, RendererPath);

                pipeline = UniversalRenderPipelineAsset.Create(rendererData);
                AssetDatabase.CreateAsset(pipeline, PipelinePath);
            }

            // Cel-shaded, inked look (Borderlands / Parasite Mutant): full resolution, smooth edges, no bloom.
            pipeline.msaaSampleCount = 4;
            pipeline.supportsHDR = false;
            pipeline.shadowDistance = 30f;
            pipeline.maxAdditionalLightsCount = 8;
            EditorUtility.SetDirty(pipeline);
            ConfigureInk(AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath));

            GraphicsSettings.defaultRenderPipeline = pipeline;
            var currentQuality = QualitySettings.GetQualityLevel();
            for (var i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = pipeline;
            }
            QualitySettings.SetQualityLevel(currentQuality, false);

            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.companyName = "MindAttic";
            PlayerSettings.productName = "Unity Eve";

            AssetDatabase.SaveAssets();
            Debug.Log("[UnityEve] Project configured: URP assigned to Graphics and all Quality levels.");
        }

        private const string InkMaterialPath = SettingsFolder + "/InkOutline.mat";

        /// <summary>
        /// The ink lines: a Full Screen Pass Renderer Feature running UnityEve/InkOutline over the depth and
        /// normals textures, before transparents so the range dome, effects and popups stay un-inked.
        /// </summary>
        private static void ConfigureInk(UniversalRendererData renderer)
        {
            if (renderer == null) return;
            var material = AssetDatabase.LoadAssetAtPath<Material>(InkMaterialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("UnityEve/InkOutline"));
                AssetDatabase.CreateAsset(material, InkMaterialPath);
            }
            material.shader = Shader.Find("UnityEve/InkOutline");
            material.SetColor("_InkColor", new Color(0.03f, 0.025f, 0.04f, 1f));
            material.SetFloat("_Thickness", 1.1f);
            material.SetFloat("_DepthThreshold", 0.04f);
            material.SetFloat("_NormalThreshold", 0.35f);
            material.SetFloat("_FadeDistance", 40f);
            EditorUtility.SetDirty(material);

            var feature = renderer.rendererFeatures.OfType<FullScreenPassRendererFeature>().FirstOrDefault(f => f.name == "InkOutline");
            if (feature == null)
            {
                feature = ScriptableObject.CreateInstance<FullScreenPassRendererFeature>();
                feature.name = "InkOutline";
                AssetDatabase.AddObjectToAsset(feature, renderer);
                renderer.rendererFeatures.Add(feature);
                // The renderer keeps a parallel list of the features' local file ids; keep it in step.
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long localId);
                var mapField = typeof(ScriptableRendererData).GetField("m_RendererFeatureMap", BindingFlags.NonPublic | BindingFlags.Instance);
                if (mapField?.GetValue(renderer) is List<long> map) map.Add(localId);
            }
            feature.passMaterial = material;
            feature.passIndex = 0;
            feature.injectionPoint = FullScreenPassRendererFeature.InjectionPoint.BeforeRenderingTransparents;
            feature.requirements = ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal;
            feature.fetchColorBuffer = true;
            feature.SetActive(true);
            EditorUtility.SetDirty(feature);
            EditorUtility.SetDirty(renderer);
        }
    }
}
