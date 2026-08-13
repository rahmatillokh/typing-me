using UnityEngine;

namespace TypingMe.Audio
{
    public enum WaveShape
    {
        Sine,
        Square,
        Saw,
        Triangle
    }

    /// <summary>
    /// Generates the gameplay and UI sounds at runtime, so the scaffold ships with audible feedback,
    /// no binary audio assets and no licensing to clear.
    /// <see cref="AudioManager"/> prefers an assigned clip over a generated one, so authored SFX
    /// drop in without touching this.
    /// </summary>
    public static class ProceduralSfx
    {
        private const int SampleRate = 44100;

        /// <summary>One pitched (or noisy) element inside a sound.</summary>
        public readonly struct Blip
        {
            public readonly float Start;
            public readonly float Duration;
            public readonly float StartHz;
            public readonly float EndHz;
            public readonly WaveShape Shape;
            public readonly float Gain;
            public readonly float Decay;
            public readonly float Noise;

            public Blip(float start, float duration, float startHz, float endHz,
                WaveShape shape = WaveShape.Square, float gain = 0.4f, float decay = 8f, float noise = 0f)
            {
                Start = start;
                Duration = duration;
                StartHz = startHz;
                EndHz = endHz;
                Shape = shape;
                Gain = gain;
                Decay = decay;
                Noise = noise;
            }
        }

        /// <summary>Single-element sound.</summary>
        public static AudioClip Create(string name, float startHz, float endHz, float duration,
            WaveShape shape = WaveShape.Square, float decay = 7f, float noise = 0f, float gain = 0.5f)
        {
            return CreateSequence(name, new Blip(0f, duration, startHz, endHz, shape, gain, decay, noise));
        }

        /// <summary>
        /// Layered/sequenced sound. Elements are mixed additively, so they can overlap into chords
        /// as well as follow one another as an arpeggio.
        /// </summary>
        public static AudioClip CreateSequence(string name, params Blip[] blips)
        {
            float total = 0.05f;
            for (int i = 0; i < blips.Length; i++)
                total = Mathf.Max(total, blips[i].Start + blips[i].Duration);

            int sampleCount = Mathf.Max(1, Mathf.RoundToInt(SampleRate * total));
            var data = new float[sampleCount];

            // Seeded from the name so a given sound is byte-identical every run.
            var rng = new System.Random(name.GetHashCode());

            for (int b = 0; b < blips.Length; b++)
                Render(data, blips[b], rng);

            SoftClip(data);

            AudioClip clip = AudioClip.Create(name, sampleCount, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static void Render(float[] data, Blip blip, System.Random rng)
        {
            int start = Mathf.RoundToInt(blip.Start * SampleRate);
            int length = Mathf.RoundToInt(Mathf.Max(0.005f, blip.Duration) * SampleRate);
            float phase = 0f;

            for (int i = 0; i < length; i++)
            {
                int index = start + i;
                if (index >= data.Length) break;

                float t = i / (float)length;
                float frequency = Mathf.Lerp(blip.StartHz, blip.EndHz, t);

                phase += frequency / SampleRate;
                if (phase > 1f) phase -= 1f;

                float value = blip.Shape switch
                {
                    WaveShape.Sine => Mathf.Sin(phase * Mathf.PI * 2f),
                    WaveShape.Square => phase < 0.5f ? 1f : -1f,
                    WaveShape.Saw => phase * 2f - 1f,
                    WaveShape.Triangle => 1f - 4f * Mathf.Abs(Mathf.Round(phase - 0.25f) - (phase - 0.25f)),
                    _ => 0f
                };

                if (blip.Noise > 0f)
                    value = Mathf.Lerp(value, (float)(rng.NextDouble() * 2.0 - 1.0), blip.Noise);

                // Exponential decay with a ~1ms attack so nothing starts on a click.
                float seconds = i / (float)SampleRate;
                float envelope = Mathf.Exp(-blip.Decay * seconds) * Mathf.Min(1f, seconds * 900f);

                data[index] += value * envelope * blip.Gain;
            }
        }

        /// <summary>Keeps layered elements from clipping without squashing the transient.</summary>
        private static void SoftClip(float[] data)
        {
            for (int i = 0; i < data.Length; i++)
                data[i] = (float)System.Math.Tanh(data[i] * 1.2);
        }
    }
}
