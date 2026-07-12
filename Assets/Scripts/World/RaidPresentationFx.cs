using System.Collections;
using KoG.MiniMvp.Camera;
using UnityEngine;
using UnityEngine.UI;

namespace KoG.MiniMvp.World
{
    /// <summary>
    /// Client-only raid juice: troop march cue, camera punch, result flash.
    /// Does not change server payloads or battle math.
    /// </summary>
    public sealed class RaidPresentationFx : MonoBehaviour
    {
        static RaidPresentationFx _instance;
        static Material _troopMat;
        static Material _campMat;

        CanvasGroup _flashGroup;
        Image _flashImage;

        public static void PlayRaidStart(CoCCameraController cam, Vector3 from, Vector3 toward)
        {
            Ensure();
            if (cam != null) cam.Punch(0.55f, 0.22f);
            _instance.StartCoroutine(_instance.MarchTroops(from, toward));
            WorldFeedback.FloatLabel(from + Vector3.up, "⚔ Raid!", new Color(1f, 0.75f, 0.35f));
        }

        public static IEnumerator PlayRaidWin(
            CoCCameraController cam,
            Vector3 worldPos,
            int stars,
            long lootGold)
        {
            Ensure();
            if (cam != null)
            {
                cam.FocusSmooth(worldPos);
                cam.Punch(1.05f, 0.32f);
            }

            WorldFeedback.PlaceBurst(worldPos);
            WorldFeedback.FloatLabel(worldPos, "★ x" + Mathf.Max(stars, 0), new Color(1f, 0.92f, 0.35f));
            if (lootGold > 0)
                WorldFeedback.FloatLabel(worldPos + Vector3.right * 0.6f, "+" + lootGold + " gold", new Color(1f, 0.85f, 0.25f));

            yield return _instance.FlashScreen(new Color(1f, 0.92f, 0.45f, 0.55f), 0.35f);
        }

        public static IEnumerator PlayRaidLose(CoCCameraController cam, Vector3 worldPos)
        {
            Ensure();
            if (cam != null) cam.Punch(0.7f, 0.28f);
            WorldFeedback.FloatLabel(worldPos + Vector3.up, "Mag‘lubiyat", new Color(1f, 0.35f, 0.3f));
            WorldFeedback.PlaceBurst(worldPos);
            yield return _instance.FlashScreen(new Color(0.55f, 0.08f, 0.08f, 0.5f), 0.4f);
        }

        static void Ensure()
        {
            if (_instance != null) return;
            var go = new GameObject("RaidPresentationFx");
            _instance = go.AddComponent<RaidPresentationFx>();
            Object.DontDestroyOnLoad(go);
            _instance.BuildFlashOverlay();
        }

        void BuildFlashOverlay()
        {
            var canvasGo = new GameObject("RaidFlashCanvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 250;
            canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasGo.AddComponent<GraphicRaycaster>().enabled = false;

            var imgGo = new GameObject("Flash", typeof(RectTransform));
            imgGo.transform.SetParent(canvasGo.transform, false);
            var rt = imgGo.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            _flashImage = imgGo.AddComponent<Image>();
            _flashImage.color = new Color(1f, 1f, 1f, 0f);
            _flashImage.raycastTarget = false;
            _flashGroup = imgGo.AddComponent<CanvasGroup>();
            _flashGroup.alpha = 0f;
            _flashGroup.blocksRaycasts = false;
            _flashGroup.interactable = false;
        }

        IEnumerator FlashScreen(Color color, float duration)
        {
            if (_flashImage == null || _flashGroup == null) yield break;
            _flashImage.color = new Color(color.r, color.g, color.b, 1f);
            var half = Mathf.Max(0.05f, duration * 0.45f);
            var t = 0f;
            while (t < half)
            {
                t += Time.deltaTime;
                _flashGroup.alpha = color.a * Mathf.Clamp01(t / half);
                yield return null;
            }
            t = 0f;
            var fade = Mathf.Max(0.05f, duration - half);
            while (t < fade)
            {
                t += Time.deltaTime;
                _flashGroup.alpha = color.a * (1f - Mathf.Clamp01(t / fade));
                yield return null;
            }
            _flashGroup.alpha = 0f;
        }

        IEnumerator MarchTroops(Vector3 from, Vector3 toward)
        {
            var dir = toward - from;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) dir = new Vector3(1f, 0f, 1f);
            dir.Normalize();
            var camp = toward;
            SpawnCampCue(camp);

            const int count = 5;
            EnsureMarchPool(count);
            for (var i = 0; i < count; i++)
            {
                var t = _marchPool[i];
                t.gameObject.SetActive(true);
                var lateral = Vector3.Cross(Vector3.up, dir).normalized * ((i - 2) * 0.28f);
                t.position = from + Vector3.up * 0.35f + lateral;
                t.localScale = new Vector3(0.28f, 0.32f, 0.28f);
            }

            var life = 1.15f;
            var t0 = 0f;
            while (t0 < life)
            {
                t0 += Time.deltaTime;
                var k = Mathf.SmoothStep(0f, 1f, t0 / life);
                for (var i = 0; i < count; i++)
                {
                    var troop = _marchPool[i];
                    if (troop == null) continue;
                    var lateral = Vector3.Cross(Vector3.up, dir).normalized * ((i - 2) * 0.28f);
                    var start = from + Vector3.up * 0.35f + lateral;
                    var end = camp + Vector3.up * 0.35f + lateral * 0.4f;
                    troop.position = Vector3.Lerp(start, end, k);
                    troop.rotation = Quaternion.LookRotation(dir);
                }
                yield return null;
            }

            for (var i = 0; i < count; i++)
            {
                if (_marchPool[i] != null)
                    _marchPool[i].gameObject.SetActive(false);
            }
        }

        Transform[] _marchPool;

        void EnsureMarchPool(int count)
        {
            if (_marchPool != null && _marchPool.Length >= count) return;
            _marchPool = new Transform[count];
            for (var i = 0; i < count; i++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                go.name = "RaidTroopCue";
                Object.Destroy(go.GetComponent<Collider>());
                go.transform.SetParent(transform, false);
                var rend = go.GetComponent<Renderer>();
                if (rend != null) rend.sharedMaterial = TroopMat();
                go.SetActive(false);
                _marchPool[i] = go.transform;
            }
        }

        void SpawnCampCue(Vector3 pos)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "RaidCampCue";
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(transform, false);
            go.transform.position = pos + Vector3.up * 0.15f;
            go.transform.localScale = new Vector3(1.1f, 0.12f, 1.1f);
            var rend = go.GetComponent<Renderer>();
            if (rend != null) rend.sharedMaterial = CampMat();

            var flag = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.Destroy(flag.GetComponent<Collider>());
            flag.transform.SetParent(go.transform, false);
            flag.transform.localPosition = new Vector3(0f, 4f, 0f);
            flag.transform.localScale = new Vector3(0.35f, 3.2f, 0.12f);
            var fr = flag.GetComponent<Renderer>();
            if (fr != null) fr.sharedMaterial = TroopMat();

            StartCoroutine(FadeDestroy(go, 1.4f));
        }

        IEnumerator FadeDestroy(GameObject go, float life)
        {
            yield return new WaitForSeconds(life);
            if (go != null) Object.Destroy(go);
        }

        static Material TroopMat()
        {
            if (_troopMat != null) return _troopMat;
            _troopMat = MakeMat(new Color(0.35f, 0.55f, 0.95f));
            return _troopMat;
        }

        static Material CampMat()
        {
            if (_campMat != null) return _campMat;
            _campMat = MakeMat(new Color(0.75f, 0.22f, 0.18f));
            return _campMat;
        }

        static Material MakeMat(Color c)
        {
            var shader = UrpMaterialUtil.FindLitShader() ?? Shader.Find("Standard");
            var m = new Material(shader);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            UrpMaterialUtil.ApplyMobileSurface(m);
            return m;
        }
    }
}
