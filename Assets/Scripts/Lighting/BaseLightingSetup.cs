using UnityEngine;
using UnityEngine.Rendering;

namespace KoG.MiniMvp.Lighting
{
    /// <summary>
    /// CoC-style day lighting + soft fog so the phone view feels filled and smooth.
    /// </summary>
    public static class BaseLightingSetup
    {
        const string SunName = "KoG_Sun";

        public static void Apply(Vector3 fieldCenter)
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.78f, 0.95f);
            RenderSettings.ambientEquatorColor = new Color(0.88f, 0.90f, 0.72f);
            RenderSettings.ambientGroundColor = new Color(0.36f, 0.46f, 0.28f);
            RenderSettings.ambientIntensity = 1.1f;
            RenderSettings.subtractiveShadowColor = new Color(0.38f, 0.46f, 0.36f);
            RenderSettings.reflectionIntensity = 0.2f;

            // Soft distance haze — hides hard world edge, CoC-like depth.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.62f, 0.78f, 0.90f);
            RenderSettings.fogDensity = 0.008f;

            var sun = EnsureSun();
            sun.transform.position = fieldCenter + new Vector3(-8f, 18f, -6f);
            // Reference: light from top-left, soft warm midday.
            sun.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
            sun.color = new Color(1f, 0.97f, 0.90f);
            sun.intensity = 1.2f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.42f;
            sun.shadowBias = 0.04f;
            sun.shadowNormalBias = 0.35f;
            sun.shadowNearPlane = 0.2f;
            sun.renderMode = LightRenderMode.ForcePixel;

            var cam = UnityEngine.Camera.main;
            if (cam != null)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                // Soft warm sky — reference midday fantasy, not harsh cyan.
                cam.backgroundColor = new Color(0.58f, 0.78f, 0.92f);
                cam.allowHDR = false;
                cam.allowMSAA = true;
                cam.farClipPlane = 220f;
            }
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
