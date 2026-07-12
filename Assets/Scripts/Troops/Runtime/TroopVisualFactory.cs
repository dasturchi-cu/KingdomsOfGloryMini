using KoG.MiniMvp.World;
using UnityEngine;
using UnityEngine.Rendering;

namespace KoG.MiniMvp.Troops
{
    /// <summary>Builds a mobile-cheap troop visual (prefab or capsule greybox).</summary>
    public static class TroopVisualFactory
    {
        static Material _sharedMat;
        static readonly System.Collections.Generic.Dictionary<int, Material> TintCache =
            new System.Collections.Generic.Dictionary<int, Material>(8);

        public static GameObject Create(TroopDefinition def, Transform parent)
        {
            GameObject go;
            if (def.Prefab != null)
            {
                go = Object.Instantiate(def.Prefab, parent);
                go.name = "Troop_" + def.Id;
                UrpMaterialUtil.RemapToUrp(go);
            }
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                go.name = "Troop_" + def.Id + "_Grey";
                go.transform.SetParent(parent, false);
                go.transform.localScale = new Vector3(0.45f, 0.55f, 0.45f);
                Object.Destroy(go.GetComponent<Collider>());
                var col = go.AddComponent<CapsuleCollider>();
                col.height = 2f;
                col.radius = 0.5f;
                col.isTrigger = false;

                var rend = go.GetComponent<Renderer>();
                if (rend != null)
                {
                    rend.sharedMaterial = SharedTintMat(def.Tint);
                    rend.shadowCastingMode = def.CastShadows
                        ? ShadowCastingMode.On
                        : ShadowCastingMode.Off;
                    rend.receiveShadows = false;
                }
            }

            if (go.GetComponent<TroopAnimEventSink>() == null)
                go.AddComponent<TroopAnimEventSink>();
            if (go.GetComponent<TroopActor>() == null)
                go.AddComponent<TroopActor>();

            return go;
        }

        static Material SharedTintMat(Color tint)
        {
            var key = tint.GetHashCode();
            if (TintCache.TryGetValue(key, out var cached) && cached != null)
                return cached;

            if (_sharedMat == null)
            {
                _sharedMat = UrpMaterialUtil.CreateColorMaterial(Color.white, "KoG_TroopShared");
                if (_sharedMat == null) return null;
                _sharedMat.enableInstancing = true;
            }

            var m = new Material(_sharedMat);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", tint);
            if (m.HasProperty("_Color")) m.SetColor("_Color", tint);
            m.enableInstancing = true;
            TintCache[key] = m;
            return m;
        }
    }
}
