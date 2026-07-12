using UnityEngine;

namespace KoG.MiniMvp.World
{
    /// <summary>
    /// Fit imported building meshes to grid cells (Tripo FBX upright + CoC camera face).
    /// </summary>
    public static class BuildingFitUtil
    {
        public static void FitToCell(GameObject go, Vector3 cellWorld, float targetFootprint, bool forceUpright = false)
        {
            go.transform.localScale = Vector3.one;
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            var renderers = go.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0)
            {
                go.transform.position = cellWorld;
                go.transform.localScale = Vector3.one * 0.8f;
                return;
            }

            foreach (var r in renderers)
            {
                r.enabled = true;
                r.gameObject.SetActive(true);
            }

            var upright = EncapsulateBounds(renderers);
            var footprint0 = Mathf.Max(upright.size.x, upright.size.z, 0.01f);
            // Soft-GO / upright packs already stand — do NOT tip them with import-fix rotations.
            var alreadyUpright = upright.size.y >= footprint0 * 0.55f;

            if (!alreadyUpright)
            {
                // Tripo / FBX lying on XZ — pick rotation that maximizes height vs footprint.
                var candidates = new[]
                {
                    Quaternion.identity,
                    Quaternion.Euler(-90f, 0f, 0f),
                    Quaternion.Euler(-90f, 90f, 0f),
                    Quaternion.Euler(-90f, -90f, 0f),
                    Quaternion.Euler(-90f, 180f, 0f),
                    Quaternion.Euler(90f, 0f, 0f),
                };

                var bestRot = Quaternion.identity;
                var bestScore = float.NegativeInfinity;
                foreach (var rot in candidates)
                {
                    go.transform.SetPositionAndRotation(Vector3.zero, rot);
                    var b = EncapsulateBounds(renderers);
                    var fp = Mathf.Max(b.size.x, b.size.z, 0.01f);
                    var score = b.size.y - fp * 0.15f;
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestRot = rot;
                    }
                }

                go.transform.SetPositionAndRotation(Vector3.zero, bestRot);
                Debug.Log("[MiniMvp] Import upright rot=" + bestRot.eulerAngles + " hScore=" + bestScore.ToString("F2"));
            }
            else
            {
                go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            }

            var bounds = EncapsulateBounds(renderers);
            var sizeXZ = Mathf.Max(bounds.size.x, bounds.size.z, 0.01f);
            var scale = Mathf.Clamp(targetFootprint / sizeXZ, 0.5f, 25f);
            go.transform.localScale = Vector3.one * scale;

            bounds = EncapsulateBounds(renderers);
            sizeXZ = Mathf.Max(bounds.size.x, bounds.size.z, 0.01f);
            if (Mathf.Abs(sizeXZ - targetFootprint) > 0.05f)
            {
                go.transform.localScale *= targetFootprint / sizeXZ;
                bounds = EncapsulateBounds(renderers);
            }

            // Only for still-flat FBX imports — never for Soft-GO (alreadyUpright).
            if (forceUpright && !alreadyUpright
                && bounds.size.y < Mathf.Max(bounds.size.x, bounds.size.z) * 0.75f)
            {
                go.transform.rotation = Quaternion.Euler(-90f, go.transform.eulerAngles.y, 0f);
                bounds = EncapsulateBounds(renderers);
                Debug.LogWarning("[MiniMvp] Import still flat — forced -90 X");
            }

            var delta = cellWorld - new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            go.transform.position += delta;
            Debug.Log("[MiniMvp] Fit h=" + bounds.size.y.ToString("F2") + " fp=" + Mathf.Max(bounds.size.x, bounds.size.z).ToString("F2") + " scale=" + go.transform.localScale.x.ToString("F2") + " upright=" + alreadyUpright);
        }

        /// <summary>CoC-style: yaw only (pitch/roll stay 0 so Soft-GO never tips).</summary>
        public static void OrientTowardCamera(GameObject go, Vector3 cellWorld, float yawBiasDegrees = 25f)
        {
            var cam = UnityEngine.Camera.main;
            var toCam = (cam != null ? cam.transform.position : cellWorld + new Vector3(12f, 20f, -12f)) - cellWorld;
            toCam.y = 0f;
            if (toCam.sqrMagnitude < 0.0001f) return;

            var yaw = Quaternion.LookRotation(toCam.normalized).eulerAngles.y + yawBiasDegrees;
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

            var renderers = go.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0) return;
            var bounds = EncapsulateBounds(renderers);
            var delta = cellWorld - new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            go.transform.position += delta;
        }

        public static void ApplyCastleAlbedoIfMissing(GameObject go)
        {
            var albedo = Resources.Load<Texture2D>("Buildings/CastleMeshTextures/Color_5f773bf5-b6c0-45d4-bd53-2e31d222e9f7");
            if (albedo == null) return;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                var shared = r.sharedMaterials;
                if (shared == null) continue;
                var next = new Material[shared.Length];
                var changed = false;
                for (var i = 0; i < shared.Length; i++)
                {
                    var m = shared[i];
                    if (m == null) { next[i] = null; continue; }
                    if (m.mainTexture != null)
                    {
                        next[i] = m;
                        continue;
                    }
                    var copy = new Material(m);
                    copy.mainTexture = albedo;
                    if (copy.HasProperty("_BaseMap")) copy.SetTexture("_BaseMap", albedo);
                    if (copy.HasProperty("_MainTex")) copy.SetTexture("_MainTex", albedo);
                    next[i] = copy;
                    changed = true;
                }
                if (changed) r.sharedMaterials = next;
            }
        }

        public static void EnsureClickCollider(GameObject go)
        {
            if (go.GetComponentInChildren<Collider>() != null) return;
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            var box = go.AddComponent<BoxCollider>();
            if (renderers != null && renderers.Length > 0)
            {
                var b = EncapsulateBounds(renderers);
                var localCenter = go.transform.InverseTransformPoint(b.center);
                var lossy = go.transform.lossyScale;
                box.center = localCenter;
                box.size = new Vector3(
                    Mathf.Max(b.size.x / Mathf.Max(lossy.x, 0.001f), 0.5f),
                    Mathf.Max(b.size.y / Mathf.Max(lossy.y, 0.001f), 0.5f),
                    Mathf.Max(b.size.z / Mathf.Max(lossy.z, 0.001f), 0.5f));
            }
            else
            {
                box.size = new Vector3(2.5f, 2.5f, 2.5f);
                box.center = new Vector3(0f, 1.2f, 0f);
            }
        }

        public static Bounds EncapsulateBounds(Renderer[] renderers)
        {
            Bounds? bounds = null;
            for (var i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null) continue;
                // Shadow discs + TextMesh badges skew upright scoring (tip Soft-GO over).
                var n = r.gameObject.name;
                if (n == "Shadow" || n == "LvlText" || n == "LvlPlate") continue;
                if (bounds == null) bounds = r.bounds;
                else
                {
                    var b = bounds.Value;
                    b.Encapsulate(r.bounds);
                    bounds = b;
                }
            }
            if (bounds != null) return bounds.Value;
            return renderers.Length > 0 ? renderers[0].bounds : new Bounds(Vector3.zero, Vector3.one);
        }
    }
}
