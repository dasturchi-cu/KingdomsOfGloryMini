using System.Collections;
using KoG.MiniMvp.Camera;
using UnityEngine;
using UnityEngine.UI;

namespace KoG.MiniMvp.World
{
    /// <summary>
    /// Client-only raid juice: troop march cue, lite defender counter-charge (P1-06),
    /// camera punch, result flash. Does not change server payloads or battle math.
    /// </summary>
    public sealed class RaidPresentationFx : MonoBehaviour
    {
        static RaidPresentationFx _instance;
        static Material _troopMat;
        static Material _campMat;
        static Material _defenderMat;

        CanvasGroup _flashGroup;
        Image _flashImage;

        public static void PlayRaidStart(CoCCameraController cam, Vector3 from, Vector3 toward)
        {
            Ensure();
            if (cam != null) cam.Punch(0.55f, 0.22f);
            _instance.StartCoroutine(_instance.MarchTroops(from, toward));
            WorldFeedback.FloatLabel(from + Vector3.up, "⚔ Reyd!", new Color(1f, 0.75f, 0.35f));
        }

        /// <summary>Mid-raid stakes pulse (presentation only; timer/HP already on HUD).</summary>
        public static void PlayRaidMidPulse(Vector3 worldPos, int hpPercent)
        {
            Ensure();
            var hp = Mathf.Clamp(hpPercent, 0, 100);
            WorldFeedback.FloatLabel(
                worldPos + Vector3.up * 1.1f,
                "Yarim yo‘l · HP " + hp + "%",
                new Color(1f, 0.7f, 0.4f));
            if (_instance != null)
                _instance.StartCoroutine(_instance.FlashScreen(new Color(1f, 0.55f, 0.25f, 0.22f), 0.22f));
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
            WorldFeedback.FloatLabel(worldPos, "★ ×" + Mathf.Max(stars, 0), new Color(1f, 0.92f, 0.35f));
            if (lootGold > 0)
                WorldFeedback.FloatLabel(worldPos + Vector3.right * 0.6f, "+" + lootGold + " ●", new Color(1f, 0.85f, 0.25f));

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
            const int defenders = 3;
            EnsureMarchPool(count);
            EnsureDefenderPool(defenders);
            var right = Vector3.Cross(Vector3.up, dir).normalized;

            for (var i = 0; i < count; i++)
            {
                var t = _marchPool[i];
                t.gameObject.SetActive(true);
                var lateral = right * ((i - 2) * 0.28f);
                t.position = from + Vector3.up * 0.35f + lateral;
                t.localScale = new Vector3(0.28f, 0.32f, 0.28f);
            }

            // P1-06 lite AI: defenders idle at camp, then engage nearest attackers.
            for (var i = 0; i < defenders; i++)
            {
                var d = _defenderPool[i];
                d.gameObject.SetActive(true);
                var lateral = right * ((i - 1) * 0.32f);
                d.position = camp + Vector3.up * 0.35f + lateral + (-dir) * 0.15f;
                d.localScale = new Vector3(0.26f, 0.34f, 0.26f);
                d.rotation = Quaternion.LookRotation(-dir);
            }

            WorldFeedback.FloatLabel(camp + Vector3.up * 1.2f, "Himoya!", new Color(1f, 0.45f, 0.35f));

            var life = 1.35f;
            var t0 = 0f;
            var clashDone = false;
            while (t0 < life)
            {
                t0 += Time.deltaTime;
                var k = Mathf.SmoothStep(0f, 1f, t0 / life);
                // Defenders commit after a short reaction delay (lite AI feel).
                var defendK = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t0 - 0.22f) / (life - 0.22f)));

                if (!clashDone && k >= 0.62f)
                {
                    clashDone = true;
                    var clashPos = Vector3.Lerp(from, camp, 0.7f) + Vector3.up * 0.6f;
                    WorldFeedback.PlaceBurst(clashPos);
                    WorldFeedback.FloatLabel(clashPos + Vector3.up * 0.4f, "Zarb!", new Color(1f, 0.85f, 0.4f));
                }

                for (var i = 0; i < count; i++)
                {
                    var troop = _marchPool[i];
                    if (troop == null) continue;
                    var lateral = right * ((i - 2) * 0.28f);
                    var start = from + Vector3.up * 0.35f + lateral;
                    var end = camp + Vector3.up * 0.35f + lateral * 0.4f;
                    troop.position = Vector3.Lerp(start, end, k);
                    troop.rotation = Quaternion.LookRotation(dir);
                }

                for (var i = 0; i < defenders; i++)
                {
                    var def = _defenderPool[i];
                    if (def == null) continue;
                    // Engage nearest attacker lane (same index when possible).
                    var targetIdx = Mathf.Clamp(i + 1, 0, count - 1);
                    var attacker = _marchPool[targetIdx];
                    var homeLateral = right * ((i - 1) * 0.32f);
                    var home = camp + Vector3.up * 0.35f + homeLateral;
                    var meet = attacker != null
                        ? Vector3.Lerp(attacker.position, home, 0.35f)
                        : home + dir * 0.8f;
                    def.position = Vector3.Lerp(home, meet, defendK);
                    var face = meet - home;
                    face.y = 0f;
                    if (face.sqrMagnitude > 0.001f)
                        def.rotation = Quaternion.LookRotation(face.normalized);
                }

                yield return null;
            }

            for (var i = 0; i < count; i++)
            {
                if (_marchPool[i] != null)
                    _marchPool[i].gameObject.SetActive(false);
            }

            for (var i = 0; i < defenders; i++)
            {
                if (_defenderPool[i] != null)
                    _defenderPool[i].gameObject.SetActive(false);
            }
        }

        Transform[] _marchPool;
        Transform[] _defenderPool;

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

        void EnsureDefenderPool(int count)
        {
            if (_defenderPool != null && _defenderPool.Length >= count) return;
            _defenderPool = new Transform[count];
            for (var i = 0; i < count; i++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                go.name = "RaidDefenderCue";
                Object.Destroy(go.GetComponent<Collider>());
                go.transform.SetParent(transform, false);
                var rend = go.GetComponent<Renderer>();
                if (rend != null) rend.sharedMaterial = DefenderMat();
                go.SetActive(false);
                _defenderPool[i] = go.transform;
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

        static Material DefenderMat()
        {
            if (_defenderMat != null) return _defenderMat;
            _defenderMat = MakeMat(new Color(0.85f, 0.28f, 0.22f));
            return _defenderMat;
        }

        static Material MakeMat(Color c)
        {
            return UrpMaterialUtil.CreateColorMaterial(c, "KoG_RaidFx");
        }
    }
}
