using UnityEngine;
using UnityEngine.Rendering;

namespace KoG.MiniMvp.Lighting
{
    /// <summary>
    /// Mobile polish for Built-in pipeline: quality caps, static batching, occlusion flags.
    /// (URP Volume / UniversalAdditionalCameraData removed — project is Built-in Standard.)
    /// </summary>
    public static class MobileVillageOptimize
    {
        // Authored NatureBorder (~9 combined) + SoftField + AO — keep mobile-safe.
        const int SoftRendererBudget = 70;

        public static void Apply(Transform villageRoot, UnityEngine.Camera cam)
        {
            ApplyExtraQuality();
            ConfigureBuiltInCamera(cam);
            ForceFieldReceiveShadows(villageRoot);
            TryStaticBatch(villageRoot);
            LogDrawBudget(villageRoot);
        }

        static void ApplyExtraQuality()
        {
            QualitySettings.pixelLightCount = 1;
            QualitySettings.globalTextureMipmapLimit = 0;
            QualitySettings.lodBias = 1f;
            QualitySettings.maximumLODLevel = 0;
            QualitySettings.particleRaycastBudget = 64;
            QualitySettings.asyncUploadTimeSlice = 2;
            QualitySettings.asyncUploadBufferSize = 16;
            QualitySettings.streamingMipmapsActive = false;
            QualitySettings.antiAliasing = 2;
        }

        static void ConfigureBuiltInCamera(UnityEngine.Camera cam)
        {
            if (cam == null) return;
            cam.allowHDR = false;
            cam.allowMSAA = true;
            cam.depthTextureMode = DepthTextureMode.None;
            cam.layerCullSpherical = true;
        }

        static void ForceFieldReceiveShadows(Transform villageRoot)
        {
            if (villageRoot == null) return;
            foreach (var r in villageRoot.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null) continue;
                var n = r.gameObject.name;
                if (n == "CheckerGrass" || n == "BaseField" || n.Contains("Grass"))
                {
                    r.receiveShadows = true;
                    r.shadowCastingMode = ShadowCastingMode.Off;
                }
                r.allowOcclusionWhenDynamic = true;
            }
        }

        static void TryStaticBatch(Transform villageRoot)
        {
            if (villageRoot == null) return;
            var staticRoots = new System.Collections.Generic.List<GameObject>(4);
            var nature = villageRoot.Find("Environment/Nature");
            var terrain = villageRoot.Find("Terrain");
            if (nature != null) staticRoots.Add(nature.gameObject);
            if (terrain != null) staticRoots.Add(terrain.gameObject);
            for (var i = 0; i < staticRoots.Count; i++)
            {
                try { StaticBatchingUtility.Combine(staticRoots[i]); }
                catch (System.Exception e)
                {
                    Debug.LogWarning("[MiniMvp] StaticBatch skip: " + e.Message);
                }
            }
        }

        static void LogDrawBudget(Transform villageRoot)
        {
            if (villageRoot == null) return;
            var renderers = villageRoot.GetComponentsInChildren<Renderer>(true);
            var mats = 0;
            var enabled = 0;
            for (var i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null) continue;
                if (r.enabled && r.gameObject.activeInHierarchy) enabled++;
                var sm = r.sharedMaterials;
                if (sm != null) mats += sm.Length;
            }

            var ok = enabled <= SoftRendererBudget;
            Debug.Log("[MiniMvp] Draw budget — renderers=" + enabled +
                      " materialSlots=" + mats +
                      " target<=" + SoftRendererBudget +
                      " " + (ok ? "OK" : "HIGH"));
        }
    }
}
