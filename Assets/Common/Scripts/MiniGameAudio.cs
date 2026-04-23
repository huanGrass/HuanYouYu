using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace HuanYouYu.MiniGameHall
{
    /// <summary>
    /// 小游戏通用音效类型定义。
    /// </summary>
    public enum MiniGameSfxType
    {
        UiTap,
        UiBack,
        TileSelect,
        MatchSuccess,
        MatchFail,
        Shuffle,
        Combo,
        Settle
    }

    [DisallowMultipleComponent]
    /// <summary>
    /// 运行时音效播放器，按类型合成并缓存短促提示音。
    /// </summary>
    public sealed class MiniGameSfxPlayer : MonoBehaviour
    {
        private const int SampleRate = 44100;

        private static MiniGameSfxPlayer instance;
        private static uint noiseSeed = 2463534242u;

        private readonly Dictionary<MiniGameSfxType, AudioClip> clipCache = new Dictionary<MiniGameSfxType, AudioClip>();

        private AudioSource sfxSource;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(this);
                return;
            }

            instance = this;
            sfxSource = gameObject.AddComponent<AudioSource>();

            sfxSource.playOnAwake = false;
            sfxSource.loop = false;
            sfxSource.spatialBlend = 0f;
            sfxSource.volume = 1f;
            sfxSource.ignoreListenerPause = true;
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }

        /// <summary>
        /// 给按钮绑定点击音效。
        /// </summary>
        public static void Attach(Button button, MiniGameSfxType type = MiniGameSfxType.UiTap, float volumeScale = 1f)
        {
            if (button == null)
            {
                return;
            }

            button.onClick.AddListener(delegate { Play(type, volumeScale); });
        }

        /// <summary>
        /// 播放指定类型音效，可传入音量和音高缩放。
        /// </summary>
        public static void Play(MiniGameSfxType type, float volumeScale = 1f, float pitch = 1f)
        {
            if (!MiniGameRuntimeSettings.SfxEnabled)
            {
                return;
            }

            var current = GetInstance();
            if (current == null)
            {
                return;
            }

            PlayInternal(current.GetOrCreateClip(type), volumeScale, pitch);
        }

        /// <summary>
        /// 播放指定音频剪辑，供需要自定义音效的小游戏复用。
        /// </summary>
        public static void Play(AudioClip clip, float volumeScale = 1f, float pitch = 1f)
        {
            PlayInternal(clip, volumeScale, pitch);
        }

        private static void PlayInternal(AudioClip clip, float volumeScale, float pitch)
        {
            if (!MiniGameRuntimeSettings.SfxEnabled || clip == null)
            {
                return;
            }

            var current = GetInstance();
            if (current == null || current.sfxSource == null)
            {
                return;
            }

            var clampedVolume = Mathf.Clamp01(volumeScale);
            var clampedPitch = Mathf.Clamp(pitch, 0.6f, 1.6f);
            current.sfxSource.pitch = clampedPitch;
            current.sfxSource.PlayOneShot(clip, clampedVolume);
            current.sfxSource.pitch = 1f;
        }

        private static MiniGameSfxPlayer GetInstance()
        {
            if (instance != null)
            {
                return instance;
            }

            instance = FindObjectOfType<MiniGameSfxPlayer>();
            if (instance != null)
            {
                return instance;
            }

            var go = new GameObject("MiniGameSfxPlayer");
            instance = go.AddComponent<MiniGameSfxPlayer>();
            return instance;
        }

        private AudioClip GetOrCreateClip(MiniGameSfxType type)
        {
            AudioClip clip;
            if (clipCache.TryGetValue(type, out clip))
            {
                return clip;
            }

            clip = BuildClip(type);
            clipCache[type] = clip;
            return clip;
        }

        private static AudioClip BuildClip(MiniGameSfxType type)
        {
            switch (type)
            {
                case MiniGameSfxType.UiBack:
                    return BuildUiBack();
                case MiniGameSfxType.TileSelect:
                    return BuildTileSelect();
                case MiniGameSfxType.MatchSuccess:
                    return BuildMatchSuccess();
                case MiniGameSfxType.MatchFail:
                    return BuildMatchFail();
                case MiniGameSfxType.Shuffle:
                    return BuildShuffle();
                case MiniGameSfxType.Combo:
                    return BuildCombo();
                case MiniGameSfxType.Settle:
                    return BuildSettle();
                default:
                    return BuildUiTap();
            }
        }

        private static AudioClip BuildUiTap()
        {
            return BuildClip("sfx_ui_tap", 0.07f, delegate(float[] samples)
            {
                AddSineSweep(samples, 0f, 0.07f, 1500f, 980f, 0.24f, 0.006f, 0.85f);
            });
        }

        private static AudioClip BuildUiBack()
        {
            return BuildClip("sfx_ui_back", 0.09f, delegate(float[] samples)
            {
                AddSineSweep(samples, 0f, 0.09f, 900f, 420f, 0.23f, 0.004f, 0.92f);
            });
        }

        private static AudioClip BuildTileSelect()
        {
            return BuildClip("sfx_tile_select", 0.08f, delegate(float[] samples)
            {
                AddSineSweep(samples, 0f, 0.08f, 1040f, 760f, 0.26f, 0.004f, 0.88f);
            });
        }

        private static AudioClip BuildMatchSuccess()
        {
            return BuildClip("sfx_match_success", 0.20f, delegate(float[] samples)
            {
                AddSineSweep(samples, 0.00f, 0.09f, 760f, 1020f, 0.23f, 0.005f, 0.80f);
                AddSineSweep(samples, 0.08f, 0.11f, 940f, 1360f, 0.20f, 0.005f, 0.80f);
            });
        }

        private static AudioClip BuildMatchFail()
        {
            return BuildClip("sfx_match_fail", 0.16f, delegate(float[] samples)
            {
                AddSquareSweep(samples, 0f, 0.16f, 340f, 230f, 0.15f, 0.004f, 0.86f);
                AddNoise(samples, 0f, 0.10f, 0.04f, 0.003f, 0.90f);
            });
        }

        private static AudioClip BuildShuffle()
        {
            return BuildClip("sfx_shuffle", 0.22f, delegate(float[] samples)
            {
                AddNoise(samples, 0.00f, 0.22f, 0.16f, 0.004f, 0.65f);
                AddSineSweep(samples, 0.05f, 0.16f, 320f, 560f, 0.09f, 0.005f, 0.85f);
            });
        }

        private static AudioClip BuildCombo()
        {
            return BuildClip("sfx_combo", 0.26f, delegate(float[] samples)
            {
                AddSineSweep(samples, 0.00f, 0.09f, 820f, 1080f, 0.21f, 0.005f, 0.80f);
                AddSineSweep(samples, 0.07f, 0.09f, 1040f, 1380f, 0.20f, 0.005f, 0.80f);
                AddSineSweep(samples, 0.14f, 0.12f, 1240f, 1650f, 0.18f, 0.005f, 0.80f);
            });
        }

        private static AudioClip BuildSettle()
        {
            return BuildClip("sfx_settle", 0.34f, delegate(float[] samples)
            {
                AddSineSweep(samples, 0.00f, 0.10f, 520f, 700f, 0.19f, 0.005f, 0.78f);
                AddSineSweep(samples, 0.10f, 0.10f, 680f, 920f, 0.18f, 0.005f, 0.78f);
                AddSineSweep(samples, 0.20f, 0.14f, 900f, 1260f, 0.17f, 0.005f, 0.78f);
            });
        }

        private static AudioClip BuildClip(string name, float durationSeconds, Action<float[]> writer)
        {
            var sampleCount = Mathf.Max(1, Mathf.CeilToInt(durationSeconds * SampleRate));
            var data = new float[sampleCount];
            writer(data);
            for (var i = 0; i < sampleCount; i++)
            {
                data[i] = Mathf.Clamp(data[i] * 0.95f, -1f, 1f);
            }

            var clip = AudioClip.Create(name, sampleCount, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static void AddSineSweep(float[] data, float startSeconds, float durationSeconds, float startFreq, float endFreq, float amplitude, float attackSeconds, float releaseFactor)
        {
            var startIndex = Mathf.FloorToInt(startSeconds * SampleRate);
            var length = Mathf.Max(1, Mathf.CeilToInt(durationSeconds * SampleRate));
            var endIndex = Mathf.Min(data.Length, startIndex + length);
            var phase = 0f;
            var releaseSeconds = Mathf.Max(0.001f, durationSeconds * Mathf.Clamp01(releaseFactor));
            var sustainSeconds = Mathf.Max(0f, durationSeconds - attackSeconds - releaseSeconds);

            for (var i = startIndex; i < endIndex; i++)
            {
                var t = (i - startIndex) / (float)Mathf.Max(1, length - 1);
                var freq = Mathf.Lerp(startFreq, endFreq, t);
                phase += (2f * Mathf.PI * freq) / SampleRate;
                var env = Envelope((i - startIndex) / (float)SampleRate, attackSeconds, sustainSeconds, releaseSeconds);
                data[i] += Mathf.Sin(phase) * amplitude * env;
            }
        }

        private static void AddSquareSweep(float[] data, float startSeconds, float durationSeconds, float startFreq, float endFreq, float amplitude, float attackSeconds, float releaseFactor)
        {
            var startIndex = Mathf.FloorToInt(startSeconds * SampleRate);
            var length = Mathf.Max(1, Mathf.CeilToInt(durationSeconds * SampleRate));
            var endIndex = Mathf.Min(data.Length, startIndex + length);
            var phase = 0f;
            var releaseSeconds = Mathf.Max(0.001f, durationSeconds * Mathf.Clamp01(releaseFactor));
            var sustainSeconds = Mathf.Max(0f, durationSeconds - attackSeconds - releaseSeconds);

            for (var i = startIndex; i < endIndex; i++)
            {
                var t = (i - startIndex) / (float)Mathf.Max(1, length - 1);
                var freq = Mathf.Lerp(startFreq, endFreq, t);
                phase += (2f * Mathf.PI * freq) / SampleRate;
                var env = Envelope((i - startIndex) / (float)SampleRate, attackSeconds, sustainSeconds, releaseSeconds);
                var square = Mathf.Sign(Mathf.Sin(phase));
                data[i] += square * amplitude * env;
            }
        }

        private static void AddNoise(float[] data, float startSeconds, float durationSeconds, float amplitude, float attackSeconds, float releaseFactor)
        {
            var startIndex = Mathf.FloorToInt(startSeconds * SampleRate);
            var length = Mathf.Max(1, Mathf.CeilToInt(durationSeconds * SampleRate));
            var endIndex = Mathf.Min(data.Length, startIndex + length);
            var releaseSeconds = Mathf.Max(0.001f, durationSeconds * Mathf.Clamp01(releaseFactor));
            var sustainSeconds = Mathf.Max(0f, durationSeconds - attackSeconds - releaseSeconds);
            var prev = 0f;

            for (var i = startIndex; i < endIndex; i++)
            {
                var env = Envelope((i - startIndex) / (float)SampleRate, attackSeconds, sustainSeconds, releaseSeconds);
                var white = NextNoise();
                prev = Mathf.Lerp(prev, white, 0.34f);
                data[i] += prev * amplitude * env;
            }
        }

        private static float NextNoise()
        {
            noiseSeed = noiseSeed * 1664525u + 1013904223u;
            var value = (noiseSeed >> 8) & 0x00FFFFFFu;
            return (value / 8388607.5f) - 1f;
        }

        private static float Envelope(float timeSeconds, float attackSeconds, float sustainSeconds, float releaseSeconds)
        {
            if (timeSeconds <= attackSeconds)
            {
                return attackSeconds <= 0.0001f ? 1f : Mathf.Clamp01(timeSeconds / attackSeconds);
            }

            var sustainEnd = attackSeconds + sustainSeconds;
            if (timeSeconds <= sustainEnd)
            {
                return 1f;
            }

            var releaseTime = timeSeconds - sustainEnd;
            if (releaseSeconds <= 0.0001f)
            {
                return 0f;
            }

            return Mathf.Clamp01(1f - (releaseTime / releaseSeconds));
        }
    }
}
