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
            QualitySettings.lodBias = 1.25f;
            QualitySettings.maximumLODLevel = 0;
            QualitySettings.particleRaycastBudget = 64;
            QualitySettings.asyncUploadTimeSlice = 2;
            QualitySettings.asyncUploadBufferSize = 16;
            QualitySettings.streamingMipmapsActive = false;
            // Soft-GO clarity: keep MSAA 2 (BaseLightingSetup).
            QualitySettings.antiAliasing = Mathf.Max(QualitySettings.antiAliasing, 4);
        }

        static void ConfigureBuiltInCamera(UnityEngine.Camera cam)
        {
            if (cam == null) return;
            cam.allowHDR = false;
            cam.allowMSAA = true;
            cam.depthTextureMode = DepthTextureMode.None;
            cam.layerCullSpherical = true;
            cam.backgroundColor = new Color(0.42f, 0.68f, 0.38f);
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
            MarkEnvironmentStaticSafe(villageRoot);
            var staticRoots = new System.Collections.Generic.List<GameObject>(4);
            var nature = villageRoot.Find("Environment/Nature");
            var terrain = villageRoot.Find("Terrain");
            var decorations = villageRoot.Find("Environment/Decorations");
            if (nature != null) staticRoots.Add(nature.gameObject);
            if (terrain != null) staticRoots.Add(terrain.gameObject);
            if (decorations != null) staticRoots.Add(decorations.gameObject);
            for (var i = 0; i < staticRoots.Count; i++)
            {
                try { StaticBatchingUtility.Combine(staticRoots[i]); }
                catch (System.Exception e)
                {
                    Debug.LogWarning("[MiniMvp] StaticBatch skip: " + e.Message);
                }
            }
        }

        /// <summary>
        /// Terrain / nature / decorations only. Never Gameplay, buildings, troops, UI, camera.
        /// </summary>
        static void MarkEnvironmentStaticSafe(Transform villageRoot)
        {
            MarkBranchStatic(villageRoot.Find("Terrain"));
            MarkBranchStatic(villageRoot.Find("Environment"));
            // Explicitly keep Gameplay dynamic (grid + placeable buildings + troops).
            var gameplay = villageRoot.Find("Gameplay");
            if (gameplay != null)
            {
                foreach (var t in gameplay.GetComponentsInChildren<Transform>(true))
                    t.gameObject.isStatic = false;
            }
        }

        static void MarkBranchStatic(Transform branch)
        {
            if (branch == null) return;
            foreach (var t in branch.GetComponentsInChildren<Transform>(true))
            {
                var n = t.name;
                // Placement grid must stay dynamic if ever under Terrain (legacy layouts).
                if (n == "BuildingGrid" || n == "SnapSurface")
                {
                    t.gameObject.isStatic = false;
                    continue;
                }
                t.gameObject.isStatic = true;
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
            if (!ok)
            {
                // Soft-GO hard policy: strip remaining mesh shadows under Environment/Nature.
                var nature = villageRoot.Find("Environment/Nature");
                if (nature != null)
                {
                    foreach (var r in nature.GetComponentsInChildren<Renderer>(true))
                    {
                        if (r == null) continue;
                        r.shadowCastingMode = ShadowCastingMode.Off;
                    }
                }
                Debug.LogWarning("[MiniMvp] Draw budget HIGH — nature mesh shadows forced Off. renderers=" +
                                 enabled + " target<=" + SoftRendererBudget);
            }
            else
            {
                Debug.Log("[MiniMvp] Draw budget — renderers=" + enabled +
                          " materialSlots=" + mats +
                          " target<=" + SoftRendererBudget + " OK");
            }
        }
    }
}
