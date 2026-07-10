using UnityEngine;
using UnityEngine.Rendering;

namespace KoG.MiniMvp.Lighting
{
    /// <summary>
    /// Mobile-friendly CoC-style day lighting for the Mini-MVP base view.
    /// One directional sun + trilight ambient; no extra realtime lights.
    /// </summary>
    public static class BaseLightingSetup
    {
        const string SunName = "KoG_Sun";

        /// <summary>Apply ambient + sun + camera clear color. Safe to call every BuildGround.</summary>
        public static void Apply(Vector3 fieldCenter)
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.52f, 0.70f, 0.92f);
            RenderSettings.ambientEquatorColor = new Color(0.78f, 0.82f, 0.70f);
            RenderSettings.ambientGroundColor = new Color(0.38f, 0.34f, 0.28f);
            RenderSettings.ambientIntensity = 1f;
            RenderSettings.fog = false;
            RenderSettings.subtractiveShadowColor = new Color(0.40f, 0.46f, 0.52f);
            RenderSettings.reflectionIntensity = 0.35f;

            var sun = EnsureSun();
            sun.transform.position = fieldCenter + new Vector3(-8f, 18f, -6f);
            // High afternoon sun — readable building silhouettes on the diamond field.
            sun.transform.rotation = Quaternion.Euler(52f, -40f, 0f);
            sun.color = new Color(1f, 0.95f, 0.86f);
            sun.intensity = 1.2f;
            sun.shadows = LightShadows.Hard; // Mobile_RPAsset soft shadows off
            sun.shadowStrength = 0.62f;
            sun.shadowBias = 0.04f;
            sun.shadowNormalBias = 0.35f;
            sun.shadowNearPlane = 0.2f;
            sun.renderMode = LightRenderMode.ForcePixel;

            var cam = UnityEngine.Camera.main;
            if (cam != null)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.42f, 0.70f, 0.90f);
                cam.allowHDR = false;
                cam.allowMSAA = false;
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

            // Reuse scene Directional Light if present.
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
