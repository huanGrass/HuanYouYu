using UnityEngine;

namespace FarmPrototype
{
    internal static class FarmRuntimeAudioFactory
    {
        private const int SampleRate = 44100;
        private const float TwoPi = Mathf.PI * 2f;

        public static AudioClip CreateUiClickClip()
        {
            return CreateToneClip("FarmUiClick", 0.08f, 0.16f, 0.9f, 880f, 1320f);
        }

        public static AudioClip CreateTileClickClip()
        {
            return CreateToneClip("FarmTileClick", 0.12f, 0.22f, 0.85f, 420f, 620f);
        }

        public static AudioClip CreateBlockedClip()
        {
            return CreateToneClip("FarmBlocked", 0.1f, 0.2f, 0.8f, 260f, 180f);
        }

        public static AudioClip CreateHoeHitClip()
        {
            return CreateCompositeClip(
                "FarmHoeHit",
                0.14f,
                delegate(float t, float normalized)
                {
                    float body = Mathf.Sin(TwoPi * Mathf.Lerp(170f, 92f, normalized) * t) * 0.18f;
                    float grit = Mathf.Sin(TwoPi * Mathf.Lerp(310f, 160f, normalized) * t) * 0.1f;
                    float dust = (Mathf.PerlinNoise(normalized * 21f, 0.11f) - 0.5f) * 0.2f;
                    float envelope = Mathf.Pow(1f - normalized, 2.6f);
                    return (body + grit + dust) * envelope;
                });
        }

        public static AudioClip CreateWaterHitClip()
        {
            return CreateCompositeClip(
                "FarmWaterHit",
                0.2f,
                delegate(float t, float normalized)
                {
                    float splash = Mathf.Sin(TwoPi * Mathf.Lerp(760f, 420f, normalized) * t) * 0.08f;
                    float drip = Mathf.Sin(TwoPi * Mathf.Lerp(1180f, 620f, normalized) * t) * 0.05f;
                    float foam = (Mathf.PerlinNoise(0.31f, normalized * 28f) - 0.5f) * 0.18f;
                    float envelope = Mathf.Pow(1f - normalized, 1.8f);
                    return (splash + drip + foam) * envelope;
                });
        }

        public static AudioClip CreateSeedHitClip()
        {
            return CreateCompositeClip(
                "FarmSeedHit",
                0.13f,
                delegate(float t, float normalized)
                {
                    float chirp = Mathf.Sin(TwoPi * Mathf.Lerp(980f, 680f, normalized) * t) * 0.06f;
                    float grainA = Mathf.Sin(TwoPi * 1480f * t) * Pulse(normalized, 0.08f, 0.18f, 0.8f);
                    float grainB = Mathf.Sin(TwoPi * 1320f * t) * Pulse(normalized, 0.34f, 0.14f, 0.55f);
                    float grainC = Mathf.Sin(TwoPi * 1180f * t) * Pulse(normalized, 0.63f, 0.12f, 0.4f);
                    float envelope = Mathf.Pow(1f - normalized, 2.1f);
                    return (chirp + grainA + grainB + grainC) * envelope;
                });
        }

        public static AudioClip CreateHarvestHitClip()
        {
            return CreateCompositeClip(
                "FarmHarvestHit",
                0.16f,
                delegate(float t, float normalized)
                {
                    float swish = (Mathf.PerlinNoise(normalized * 34f, 0.57f) - 0.5f) * 0.22f;
                    float leaf = Mathf.Sin(TwoPi * Mathf.Lerp(520f, 780f, normalized) * t) * 0.08f;
                    float sparkle = Mathf.Sin(TwoPi * Mathf.Lerp(1320f, 980f, normalized) * t) * Pulse(normalized, 0.2f, 0.18f, 0.42f);
                    float envelope = Mathf.Pow(1f - normalized, 1.7f);
                    return (swish + leaf + sparkle) * envelope;
                });
        }

        private static AudioClip CreateToneClip(string name, float duration, float volume, float decayPower, float startFrequency, float endFrequency)
        {
            int sampleCount = Mathf.CeilToInt(duration * SampleRate);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)SampleRate;
                float normalized = i / Mathf.Max(1f, sampleCount - 1f);
                float envelope = Mathf.Pow(1f - normalized, decayPower);
                float frequency = Mathf.Lerp(startFrequency, endFrequency, normalized);
                samples[i] = Mathf.Sin(2f * Mathf.PI * frequency * t) * volume * envelope;
            }

            AudioClip clip = AudioClip.Create(name, sampleCount, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip CreateCompositeClip(string name, float duration, System.Func<float, float, float> sampleGenerator)
        {
            int sampleCount = Mathf.CeilToInt(duration * SampleRate);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)SampleRate;
                float normalized = i / Mathf.Max(1f, sampleCount - 1f);
                samples[i] = Mathf.Clamp(sampleGenerator(t, normalized), -1f, 1f);
            }

            AudioClip clip = AudioClip.Create(name, sampleCount, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static float Pulse(float normalized, float center, float width, float amplitude)
        {
            float distance = Mathf.Abs(normalized - center);
            if (distance >= width)
            {
                return 0f;
            }

            float falloff = 1f - (distance / width);
            return amplitude * falloff;
        }
    }
}
