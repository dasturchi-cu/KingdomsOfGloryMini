using UnityEngine;

namespace KoG.MiniMvp.Audio
{
    /// <summary>
    /// Soft-GO SFX + quiet BGM pad. Mute kills both sources.
    /// </summary>
    public sealed class MiniAudio : MonoBehaviour
    {
        const string MutePrefsKey = "kog_mini_mute";

        static MiniAudio _instance;
        AudioSource _sfx;
        AudioSource _bgm;
        AudioClip _tap;
        AudioClip _place;
        AudioClip _trainDone;
        AudioClip _collect;
        AudioClip _error;
        AudioClip _raidWin;
        AudioClip _raidLose;
        AudioClip _bgmLoop;
        bool _muted;

        public static bool Muted
        {
            get
            {
                Ensure();
                return _instance._muted;
            }
            set
            {
                Ensure();
                _instance._muted = value;
                PlayerPrefs.SetInt(MutePrefsKey, value ? 1 : 0);
                PlayerPrefs.Save();
                _instance.ApplyMute();
            }
        }

        public static void ToggleMute() => Muted = !Muted;

        public static void PlayTap() => Play(Cue.Tap);
        public static void PlayPlace() => Play(Cue.Place);
        public static void PlayTrainDone() => Play(Cue.TrainDone);
        public static void PlayCollect() => Play(Cue.Collect);
        public static void PlayError() => Play(Cue.Error);
        public static void PlayRaidWin() => Play(Cue.RaidWin);
        public static void PlayRaidLose() => Play(Cue.RaidLose);

        enum Cue
        {
            Tap,
            Place,
            TrainDone,
            Collect,
            Error,
            RaidWin,
            RaidLose
        }

        static void Ensure()
        {
            if (_instance != null) return;
            var go = new GameObject("MiniAudio");
            Object.DontDestroyOnLoad(go);
            _instance = go.AddComponent<MiniAudio>();
            _instance.Bootstrap();
        }

        void Bootstrap()
        {
            _muted = PlayerPrefs.GetInt(MutePrefsKey, 0) == 1;
            _sfx = gameObject.AddComponent<AudioSource>();
            _sfx.playOnAwake = false;
            _sfx.spatialBlend = 0f;
            _sfx.loop = false;
            _sfx.volume = 0.55f;

            _bgm = gameObject.AddComponent<AudioSource>();
            _bgm.playOnAwake = false;
            _bgm.spatialBlend = 0f;
            _bgm.loop = true;
            _bgm.volume = 0.08f;

            _tap = MakeBeep("sfx_tap", 880f, 0.045f, 0.22f);
            _place = MakeBeep("sfx_place", 520f, 0.09f, 0.35f, slideToHz: 720f);
            _trainDone = MakeChord("sfx_train", 0.16f, 0.4f, 440f, 554f, 659f);
            _collect = MakeBeep("sfx_collect", 740f, 0.08f, 0.32f, slideToHz: 980f);
            _error = MakeBeep("sfx_error", 180f, 0.14f, 0.38f, slideToHz: 110f);
            _raidWin = MakeChord("sfx_raid_win", 0.28f, 0.45f, 523f, 659f, 784f);
            _raidLose = MakeBeep("sfx_raid_lose", 220f, 0.22f, 0.4f, slideToHz: 140f);
            _bgmLoop = MakePad("bgm_soft", 4.5f, 0.18f, 196f, 247f, 294f);

            ApplyMute();
            if (!_muted)
            {
                _bgm.clip = _bgmLoop;
                _bgm.Play();
            }
        }

        void ApplyMute()
        {
            if (_sfx != null) _sfx.mute = _muted;
            if (_bgm == null) return;
            _bgm.mute = _muted;
            if (_muted)
            {
                if (_bgm.isPlaying) _bgm.Stop();
            }
            else if (_bgmLoop != null && !_bgm.isPlaying)
            {
                _bgm.clip = _bgmLoop;
                _bgm.Play();
            }
        }

        static void Play(Cue cue)
        {
            Ensure();
            if (_instance._muted || _instance._sfx == null) return;
            var clip = cue switch
            {
                Cue.Tap => _instance._tap,
                Cue.Place => _instance._place,
                Cue.TrainDone => _instance._trainDone,
                Cue.Collect => _instance._collect,
                Cue.Error => _instance._error,
                Cue.RaidWin => _instance._raidWin,
                Cue.RaidLose => _instance._raidLose,
                _ => null
            };
            if (clip == null) return;
            _instance._sfx.PlayOneShot(clip, 1f);
        }

        static AudioClip MakeBeep(string name, float hz, float seconds, float amp, float slideToHz = -1f)
        {
            var sampleRate = 22050;
            var samples = Mathf.Max(64, Mathf.CeilToInt(sampleRate * seconds));
            var data = new float[samples];
            for (var i = 0; i < samples; i++)
            {
                var t = i / (float)sampleRate;
                var k = i / (float)(samples - 1);
                var freq = slideToHz > 0f ? Mathf.Lerp(hz, slideToHz, k) : hz;
                var env = 1f - k;
                env *= env;
                data[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * amp * env;
            }

            var clip = AudioClip.Create(name, samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static AudioClip MakeChord(string name, float seconds, float amp, params float[] hz)
        {
            var sampleRate = 22050;
            var samples = Mathf.Max(64, Mathf.CeilToInt(sampleRate * seconds));
            var data = new float[samples];
            var inv = 1f / Mathf.Max(1, hz.Length);
            for (var i = 0; i < samples; i++)
            {
                var t = i / (float)sampleRate;
                var k = i / (float)(samples - 1);
                var env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(k * 1.15f));
                float s = 0f;
                for (var h = 0; h < hz.Length; h++)
                    s += Mathf.Sin(2f * Mathf.PI * hz[h] * t);
                data[i] = s * inv * amp * env;
            }

            var clip = AudioClip.Create(name, samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static AudioClip MakePad(string name, float seconds, float amp, params float[] hz)
        {
            var sampleRate = 22050;
            var samples = Mathf.Max(64, Mathf.CeilToInt(sampleRate * seconds));
            var data = new float[samples];
            var inv = 1f / Mathf.Max(1, hz.Length);
            for (var i = 0; i < samples; i++)
            {
                var t = i / (float)sampleRate;
                var k = i / (float)(samples - 1);
                // Seamless-ish loop envelope (soft edges).
                var edge = Mathf.Min(k, 1f - k) * 8f;
                var env = Mathf.Clamp01(edge);
                float s = 0f;
                for (var h = 0; h < hz.Length; h++)
                    s += Mathf.Sin(2f * Mathf.PI * hz[h] * t);
                data[i] = s * inv * amp * env;
            }

            var clip = AudioClip.Create(name, samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
