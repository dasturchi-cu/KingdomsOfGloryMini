using UnityEngine;
using UnityEngine.Rendering;

namespace KoG.MiniMvp.Lighting
{
    /// <summary>
    /// CoC / Might &amp; Glory day look + mobile quality knobs.
    /// MSAA off by default — MobileVillageOptimize re-asserts after Apply.
    /// </summary>
    public static class BaseLightingSetup
    {
        const string SunName = "KoG_Sun";

        public static void Apply(Vector3 fieldCenter)
        {
            ApplyMobileQuality();

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.70f, 0.84f, 0.96f);
            RenderSettings.ambientEquatorColor = new Color(0.94f, 0.95f, 0.72f);
            RenderSettings.ambientGroundColor = new Color(0.38f, 0.52f, 0.28f);
            RenderSettings.ambientIntensity = 1.18f;
            RenderSettings.subtractiveShadowColor = new Color(0.28f, 0.40f, 0.26f);
            RenderSettings.reflectionIntensity = 0.12f;

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.66f, 0.80f, 0.90f);
            RenderSettings.fogDensity = 0.0045f;

            var sun = EnsureSun();
            sun.transform.position = fieldCenter + new Vector3(-9f, 20f, -7f);
            sun.transform.rotation = Quaternion.Euler(46f, -32f, 0f);
            sun.color = new Color(1f, 0.99f, 0.88f);
            sun.intensity = 1.28f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.38f;
            sun.shadowBias = 0.035f;
            sun.shadowNormalBias = 0.4f;
            sun.shadowNearPlane = 0.2f;
            sun.renderMode = LightRenderMode.ForcePixel;

            var cam = UnityEngine.Camera.main;
            if (cam != null)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.60f, 0.80f, 0.93f);
                cam.allowHDR = false;
                cam.allowMSAA = false;
                cam.farClipPlane = 180f;
                cam.nearClipPlane = 0.3f;
            }
        }

        /// <summary>Mobile-friendly quality: no MSAA, soft shadows, short distance.</summary>
        public static void ApplyMobileQuality()
        {
            QualitySettings.antiAliasing = 0;
            QualitySettings.shadows = ShadowQuality.All;
            QualitySettings.shadowResolution = ShadowResolution.Medium;
            QualitySettings.shadowDistance = 36f;
            QualitySettings.shadowCascades = 1;
            QualitySettings.shadowProjection = ShadowProjection.StableFit;
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.Enable;
            QualitySettings.softParticles = false;
            QualitySettings.realtimeReflectionProbes = false;
            QualitySettings.billboardsFaceCameraPosition = true;
            QualitySettings.pixelLightCount = 1;
        }

        static Light EnsureSun()
        {
            var existing = GameObject.Find(SunName);
            if (existing != null)
            {
                var lit = existing.GetComponent<Light>();
                if (lit != null) return lit;
            }

            var lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
            for (var i = 0; i < lights.Length; i++)
            {
                var l = lights[i];
                if (l == null || l.type != LightType.Directional) continue;
                l.gameObject.name = SunName;
                return l;
            }

            var go = new GameObject(SunName);
            var sun = go.AddComponent<Light>();
            sun.type = LightType.Directional;
            return sun;
        }
    }
}
