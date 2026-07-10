using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace KoG.MiniMvp.Lighting
{
    /// <summary>
    /// Final mobile polish: URP post (saturation), static batching, occlusion flags,
    /// draw-call budget log, camera depth-off.
    /// </summary>
    public static class MobileVillageOptimize
    {
        const string VolumeName = "KoG_PostVolume";
        // Authored NatureBorder (~9 combined) + SoftField + AO — keep mobile-safe.
        const int SoftRendererBudget = 70;

        public static void Apply(Transform villageRoot, UnityEngine.Camera cam)
        {
            ApplyExtraQuality();
            EnsurePostVolume(villageRoot);
            ConfigureUrpCamera(cam);
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
            // Umbra occlusion data is bake-time; runtime flag still helps dynamic occlusion queries.
            QualitySettings.streamingMipmapsActive = false;
        }

        static void EnsurePostVolume(Transform villageRoot)
        {
            var existing = GameObject.Find(VolumeName);
            if (existing != null) return;

            var go = new GameObject(VolumeName);
            if (villageRoot != null)
            {
                var lighting = villageRoot.Find("Lighting");
                go.transform.SetParent(lighting != null ? lighting : villageRoot, false);
            }

            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 1f;
            volume.weight = 1f;

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "KoG_MobilePost";
            volume.profile = profile;

            var color = profile.Add<ColorAdjustments>(true);
            color.active = true;
            color.saturation.Override(11f);  // fantasy pop — still clean, not neon
            color.contrast.Override(6f);     // canopy / path separation on phone LCDs
            color.postExposure.Override(0.06f);
            color.hueShift.Override(2f);     // tiny warm bias toward golden hour greens

            // Keep look clean — no bloom / vignette for mobile CoC feel.
            var bloom = profile.Add<Bloom>(true);
            bloom.active = false;

            var vignette = profile.Add<Vignette>(true);
            vignette.active = false;
        }

        static void ConfigureUrpCamera(UnityEngine.Camera cam)
        {
            if (cam == null) return;
            cam.allowHDR = false;
            cam.allowMSAA = true;
            cam.depthTextureMode = DepthTextureMode.None;
            cam.layerCullSpherical = true;

            var data = cam.GetUniversalAdditionalCameraData();
            if (data == null) return;
            data.renderPostProcessing = true;
            data.renderShadows = true;
            data.requiresDepthOption = CameraOverrideOption.Off;
            data.requiresColorOption = CameraOverrideOption.Off;
            data.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            data.antialiasingQuality = AntialiasingQuality.Medium;
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
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
                r.allowOcclusionWhenDynamic = true;
            }
        }

        static void TryStaticBatch(Transform villageRoot)
        {
            if (villageRoot == null) return;
            // Combine static nature/field meshes — fewer draw calls on device.
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
