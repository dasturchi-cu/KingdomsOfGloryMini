using UnityEngine;
using UnityEngine.Rendering;

namespace KoG.MiniMvp.Lighting
{
    /// <summary>
    /// CoC / Might &amp; Glory day look + mobile quality knobs (soft shadow, MSAA, short shadow distance).
    /// </summary>
    public static class BaseLightingSetup
    {
        const string SunName = "KoG_Sun";

        public static void Apply(Vector3 fieldCenter)
        {
            ApplyMobileQuality();

            RenderSettings.ambientMode = AmbientMode.Trilight;
            // Warm midday fantasy — bright, clean, never muddy (CoC / M&G mobile).
            RenderSettings.ambientSkyColor = new Color(0.72f, 0.86f, 0.98f);
            RenderSettings.ambientEquatorColor = new Color(0.96f, 0.94f, 0.70f);
            RenderSettings.ambientGroundColor = new Color(0.40f, 0.54f, 0.30f);
            RenderSettings.ambientIntensity = 1.22f;
            RenderSettings.subtractiveShadowColor = new Color(0.26f, 0.38f, 0.24f);
            RenderSettings.reflectionIntensity = 0.10f;

            // Soft atmospheric depth — still readable on phone LCDs.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.68f, 0.82f, 0.92f);
            RenderSettings.fogDensity = 0.0042f;

            var sun = EnsureSun();
            sun.transform.position = fieldCenter + new Vector3(-9f, 20f, -7f);
            // Top-left key light (matches reference shadow direction).
            sun.transform.rotation = Quaternion.Euler(48f, -34f, 0f);
            sun.color = new Color(1f, 0.98f, 0.86f);
            sun.intensity = 1.34f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.46f;
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
                cam.allowMSAA = true;
                cam.farClipPlane = 180f;
                cam.nearClipPlane = 0.3f;
            }
        }

        /// <summary>Mobile-friendly quality: MSAA, soft shadows, short distance, 2 cascades.</summary>
        public static void ApplyMobileQuality()
        {
            QualitySettings.antiAliasing = 4;
            QualitySettings.shadows = ShadowQuality.All;
            QualitySettings.shadowResolution = ShadowResolution.Medium;
            QualitySettings.shadowDistance = 48f;
            QualitySettings.shadowCascades = 2;
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
