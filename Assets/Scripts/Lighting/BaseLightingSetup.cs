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
            RenderSettings.ambientSkyColor = new Color(0.55f, 0.72f, 0.95f);
            RenderSettings.ambientEquatorColor = new Color(0.82f, 0.86f, 0.72f);
            RenderSettings.ambientGroundColor = new Color(0.32f, 0.40f, 0.26f);
            RenderSettings.ambientIntensity = 1.05f;
            RenderSettings.subtractiveShadowColor = new Color(0.35f, 0.42f, 0.38f);
            RenderSettings.reflectionIntensity = 0.25f;

            // Soft distance haze — hides hard world edge, CoC-like depth.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.55f, 0.72f, 0.88f);
            RenderSettings.fogDensity = 0.012f;

            var sun = EnsureSun();
            sun.transform.position = fieldCenter + new Vector3(-8f, 18f, -6f);
            sun.transform.rotation = Quaternion.Euler(50f, -38f, 0f);
            sun.color = new Color(1f, 0.96f, 0.88f);
            sun.intensity = 1.15f;
            sun.shadows = LightShadows.Hard;
            sun.shadowStrength = 0.55f;
            sun.shadowBias = 0.04f;
            sun.shadowNormalBias = 0.35f;
            sun.shadowNearPlane = 0.2f;
            sun.renderMode = LightRenderMode.ForcePixel;

            var cam = UnityEngine.Camera.main;
            if (cam != null)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                // Soft sky — matches fog, no harsh cyan void.
                cam.backgroundColor = new Color(0.52f, 0.74f, 0.92f);
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
