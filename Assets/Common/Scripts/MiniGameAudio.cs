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
        Settle,
        Collision
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
                case MiniGameSfxType.Collision:
                    return BuildCollision();
                default:
                    return BuildUiTap();
            }
        }

        private static AudioClip BuildUiTap()
        {
            return BuildClip("sfx_ui_tap", 0.10f, delegate(float[] samples)
            {
                AddMallet(samples, 0f, 0.10f, 660f, 0.20f);
            });
        }

        private static AudioClip BuildUiBack()
        {
            return BuildClip("sfx_ui_back", 0.16f, delegate(float[] samples)
            {
                AddMallet(samples, 0f, 0.10f, 587.33f, 0.15f);
                AddMallet(samples, 0.055f, 0.105f, 440f, 0.13f);
            });
        }

        private static AudioClip BuildTileSelect()
        {
            return BuildClip("sfx_tile_select", 0.085f, delegate(float[] samples)
            {
                AddMallet(samples, 0f, 0.085f, 523.25f, 0.22f);
                AddNoise(samples, 0f, 0.025f, 0.025f, 0.003f, 0.88f);
            });
        }

        private static AudioClip BuildMatchSuccess()
        {
            return BuildClip("sfx_match_success", 0.30f, delegate(float[] samples)
            {
                AddMallet(samples, 0f, 0.18f, 659.25f, 0.20f);
                AddMallet(samples, 0.075f, 0.225f, 783.99f, 0.19f);
            });
        }

        private static AudioClip BuildMatchFail()
        {
            return BuildClip("sfx_match_fail", 0.18f, delegate(float[] samples)
            {
                AddMallet(samples, 0f, 0.12f, 349.23f, 0.17f);
                AddMallet(samples, 0.06f, 0.12f, 293.66f, 0.14f);
            });
        }

        private static AudioClip BuildShuffle()
        {
            return BuildClip("sfx_shuffle", 0.26f, delegate(float[] samples)
            {
                AddNoise(samples, 0f, 0.22f, 0.09f, 0.035f, 0.80f);
                AddMallet(samples, 0.015f, 0.07f, 392f, 0.10f);
                AddMallet(samples, 0.075f, 0.07f, 523.25f, 0.11f);
                AddMallet(samples, 0.135f, 0.125f, 659.25f, 0.12f);
            });
        }

        private static AudioClip BuildCombo()
        {
            return BuildClip("sfx_combo", 0.38f, delegate(float[] samples)
            {
                AddMallet(samples, 0f, 0.18f, 523.25f, 0.18f);
                AddMallet(samples, 0.07f, 0.20f, 659.25f, 0.18f);
                AddMallet(samples, 0.14f, 0.24f, 783.99f, 0.18f);
            });
        }

        private static AudioClip BuildSettle()
        {
            return BuildClip("sfx_settle", 0.62f, delegate(float[] samples)
            {
                AddMallet(samples, 0f, 0.26f, 523.25f, 0.17f);
                AddMallet(samples, 0.10f, 0.28f, 659.25f, 0.16f);
                AddMallet(samples, 0.20f, 0.30f, 783.99f, 0.16f);
                AddMallet(samples, 0.30f, 0.32f, 1046.5f, 0.14f);
                AddMallet(samples, 0.30f, 0.32f, 523.25f, 0.07f);
            });
        }

        private static AudioClip BuildCollision()
        {
            return BuildClip("sfx_collision", 0.10f, delegate(float[] samples)
            {
                AddSineSweep(samples, 0f, 0.10f, 220f, 150f, 0.16f, 0.003f, 0.97f);
                AddMallet(samples, 0f, 0.055f, 440f, 0.08f);
                AddNoise(samples, 0f, 0.025f, 0.045f, 0.002f, 0.92f);
            });
        }

        // 基音保留温暖主体，较快衰减的泛音提供轻敲质感。
        private static void AddMallet(float[] data, float startSeconds, float durationSeconds, float frequency, float amplitude)
        {
            var startIndex = Mathf.FloorToInt(startSeconds * SampleRate);
            var length = Mathf.Max(1, Mathf.CeilToInt(durationSeconds * SampleRate));
            var endIndex = Mathf.Min(data.Length, startIndex + length);
            for (var i = startIndex; i < endIndex; i++)
            {
                var time = (i - startIndex) / (float)SampleRate;
                var progress = (i - startIndex) / (float)Mathf.Max(1, length - 1);
                var attack = Mathf.Clamp01(time / 0.004f);
                attack *= attack * (3f - 2f * attack);
                var tail = 1f - progress;
                var envelope = attack * tail * tail * Mathf.Exp(-3f * progress);
                var phase = 2f * Mathf.PI * frequency * time;
                var tone = Mathf.Sin(phase)
                    + 0.22f * Mathf.Sin(phase * 2f) * Mathf.Exp(-8f * progress)
                    + 0.08f * Mathf.Sin(phase * 3f) * Mathf.Exp(-12f * progress);
                data[i] += tone * amplitude * envelope;
            }
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
