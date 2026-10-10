using System.IO;
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

            // PS1-era look: no MSAA, no HDR bloom, hard shadows from a few lights.
            pipeline.msaaSampleCount = 1;
            pipeline.supportsHDR = false;
            pipeline.shadowDistance = 30f;
            pipeline.maxAdditionalLightsCount = 8;
            EditorUtility.SetDirty(pipeline);

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
    }
}
