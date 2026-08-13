using UnityEngine;

namespace TypingMe.Audio
{
    /// <summary>How a track's drums sit behind it.</summary>
    public enum DrumStyle
    {
        /// <summary>Kick on every beat — the synthwave default.</summary>
        FourOnFloor,

        /// <summary>Kick on 1, snare on 3. Slower, roomier.</summary>
        Halftime,

        /// <summary>Four on the floor plus 16th hats. Pushy.</summary>
        Driving
    }

    /// <summary>
    /// Synthesises the soundtrack: several seamless loops, each built from a pad, an octave bass,
    /// an arpeggio, drums and an optional bell lead.
    /// </summary>
    /// <remarks>
    /// Composed here rather than imported. The music is therefore original to this project and
    /// carries no third-party licence to clear — strictly safer than "open source", and unlike a
    /// downloaded file its key, tempo and mood are chosen to fit the game rather than hoped to.
    /// Every track is also seamless by construction: note tails wrap around the loop point.
    /// <see cref="AudioManager"/> still prefers assigned clips, so recorded music drops in on top.
    /// </remarks>
    public static class ProceduralMusic
    {
        private const int SampleRate = 44100;

        private readonly struct TrackRecipe
        {
            public readonly string Name;
            public readonly float Bpm;
            public readonly int Bars;
            public readonly float[][] Progression;
            public readonly DrumStyle Drums;
            public readonly bool Lead;
            public readonly bool OctaveUpSecondHalf;
            public readonly float PadGain;
            public readonly float BassGain;
            public readonly float ArpGain;

            public TrackRecipe(string name, float bpm, int bars, float[][] progression, DrumStyle drums,
                bool lead, bool octaveUpSecondHalf, float padGain, float bassGain, float arpGain)
            {
                Name = name;
                Bpm = bpm;
                Bars = bars;
                Progression = progression;
                Drums = drums;
                Lead = lead;
                OctaveUpSecondHalf = octaveUpSecondHalf;
                PadGain = padGain;
                BassGain = bassGain;
                ArpGain = arpGain;
            }
        }

        // Triads written out in Hz so each track sits in its own key.
        private static readonly float[][] AMinor =
        {
            new[] { 220.00f, 261.63f, 329.63f }, // Am
            new[] { 174.61f, 220.00f, 261.63f }, // F
            new[] { 261.63f, 329.63f, 392.00f }, // C
            new[] { 196.00f, 246.94f, 293.66f }  // G
        };

        private static readonly float[][] DMinor =
        {
            new[] { 293.66f, 349.23f, 440.00f }, // Dm
            new[] { 233.08f, 293.66f, 349.23f }, // Bb
            new[] { 174.61f, 220.00f, 261.63f }, // F
            new[] { 261.63f, 329.63f, 392.00f }  // C
        };

        private static readonly float[][] EMinor =
        {
            new[] { 329.63f, 392.00f, 493.88f }, // Em
            new[] { 261.63f, 329.63f, 392.00f }, // C
            new[] { 196.00f, 246.94f, 293.66f }, // G
            new[] { 293.66f, 369.99f, 440.00f }  // D
        };

        private static readonly float[][] CMinor =
        {
            new[] { 261.63f, 311.13f, 392.00f }, // Cm
            new[] { 207.65f, 261.63f, 311.13f }, // Ab
            new[] { 311.13f, 392.00f, 466.16f }, // Eb
            new[] { 233.08f, 293.66f, 349.23f }  // Bb
        };

        private static readonly TrackRecipe[] Tracks =
        {
            //                name            bpm  bars  key      drums                 lead  8va   pad    bass   arp
            new TrackRecipe("Neon Drift",     96f,  8, AMinor, DrumStyle.FourOnFloor, true,  true,  0.10f, 0.26f, 0.075f),
            new TrackRecipe("Cold Circuit",   84f,  8, DMinor, DrumStyle.Halftime,    false, false, 0.14f, 0.22f, 0.055f),
            new TrackRecipe("Overclock",     132f,  8, EMinor, DrumStyle.Driving,     true,  false, 0.07f, 0.28f, 0.090f),
            new TrackRecipe("Nightfall",      72f,  4, CMinor, DrumStyle.Halftime,    true,  false, 0.16f, 0.18f, 0.045f)
        };

        public static int TrackCount => Tracks.Length;

        public static string TrackName(int index) => Tracks[Mathf.Abs(index) % Tracks.Length].Name;

        /// <summary>Builds one track. Deterministic — the same index always yields the same audio.</summary>
        public static AudioClip CreateTrack(int index)
        {
            TrackRecipe recipe = Tracks[Mathf.Abs(index) % Tracks.Length];

            double beat = 60.0 / recipe.Bpm;
            double bar = beat * 4.0;
            int totalSamples = Mathf.Max(1, (int)(bar * recipe.Bars * SampleRate));

            var buffer = new float[totalSamples];
            var rng = new System.Random(20260813 + index * 977);

            for (int barIndex = 0; barIndex < recipe.Bars; barIndex++)
            {
                float[] chord = recipe.Progression[barIndex % recipe.Progression.Length];
                double barStart = barIndex * bar;
                bool secondHalf = barIndex >= recipe.Bars / 2;

                RenderPad(buffer, barStart, bar, chord, recipe.PadGain);
                RenderBass(buffer, barStart, beat, chord, recipe.BassGain);
                RenderArp(buffer, barStart, beat, chord, recipe.ArpGain,
                    recipe.OctaveUpSecondHalf && secondHalf);
                RenderDrums(buffer, barStart, beat, rng, recipe.Drums, secondHalf);

                if (recipe.Lead && secondHalf) RenderLead(buffer, barStart, beat, chord);
            }

            Normalise(buffer, 0.82f);

            AudioClip clip = AudioClip.Create($"TypingMe_{recipe.Name}", totalSamples, 1, SampleRate, false);
            clip.SetData(buffer, 0);
            return clip;
        }

        #region Layers

        /// <summary>Sustained detuned triad — the bed everything else sits on.</summary>
        private static void RenderPad(float[] buffer, double start, double duration, float[] chord, float gain)
        {
            foreach (float note in chord)
            {
                AddTone(buffer, start, duration * 1.05, note, WaveShape.Triangle, gain, 0.35, 0.5);
                AddTone(buffer, start, duration * 1.05, note * 1.005f, WaveShape.Triangle, gain * 0.7f, 0.4, 0.5);
            }
        }

        /// <summary>Eighth-note bass gate; the rests are what give it groove.</summary>
        private static readonly bool[] BassPattern = { true, false, true, true, false, true, false, true };

        private static void RenderBass(float[] buffer, double start, double beat, float[] chord, float gain)
        {
            double eighth = beat * 0.5;

            for (int step = 0; step < BassPattern.Length; step++)
            {
                if (!BassPattern[step]) continue;

                // Step 6 lifts to the fifth so the line moves instead of hammering the root.
                float note = (step == 6 ? chord[2] : chord[0]) * 0.5f;
                AddTone(buffer, start + step * eighth, eighth * 0.9, note, WaveShape.Saw, gain, 0.005, 5.0);
            }
        }

        /// <summary>Which chord tone the 16th-note arpeggio plays on each step of a bar.</summary>
        private static readonly int[] ArpPattern = { 0, 2, 1, 3, 0, 2, 4, 3, 0, 2, 1, 3, 4, 2, 1, 0 };

        private static void RenderArp(float[] buffer, double start, double beat, float[] chord, float gain, bool octaveUp)
        {
            double sixteenth = beat * 0.25;

            // Five tones spanning two octaves gives the arp somewhere to climb.
            float[] tones = { chord[0], chord[1], chord[2], chord[0] * 2f, chord[1] * 2f };

            for (int step = 0; step < ArpPattern.Length; step++)
            {
                float note = tones[ArpPattern[step] % tones.Length] * (octaveUp ? 2f : 1f);
                AddTone(buffer, start + step * sixteenth, sixteenth * 1.4, note,
                    WaveShape.Square, octaveUp ? gain * 0.75f : gain, 0.002, 9.0);
            }
        }

        private static void RenderDrums(float[] buffer, double start, double beat, System.Random rng,
            DrumStyle style, bool busy)
        {
            double eighth = beat * 0.5;

            switch (style)
            {
                case DrumStyle.Halftime:
                    AddKick(buffer, start, 0.44f);
                    AddNoise(buffer, start + beat * 2, 0.18, 0.15f, 22f, rng, 0.45f);

                    for (int step = 0; step < 4; step++)
                        AddNoise(buffer, start + step * beat, 0.05, 0.030f, 80f, rng, 0.9f);
                    break;

                case DrumStyle.Driving:
                    for (int b = 0; b < 4; b++) AddKick(buffer, start + b * beat, 0.40f);

                    AddNoise(buffer, start + beat, 0.14, 0.16f, 28f, rng, 0.45f);
                    AddNoise(buffer, start + beat * 3, 0.14, 0.16f, 28f, rng, 0.45f);

                    for (int step = 0; step < 16; step++)
                        AddNoise(buffer, start + step * beat * 0.25, 0.04,
                            step % 4 == 0 ? 0.022f : 0.034f, 110f, rng, 0.92f);
                    break;

                default:
                    for (int b = 0; b < 4; b++) AddKick(buffer, start + b * beat, 0.42f);

                    AddNoise(buffer, start + beat, 0.16, 0.16f, 26f, rng, 0.45f);
                    AddNoise(buffer, start + beat * 3, 0.16, 0.16f, 26f, rng, 0.45f);

                    for (int step = 0; step < 8; step++)
                        AddNoise(buffer, start + step * eighth, 0.05,
                            step % 2 == 0 ? 0.030f : 0.045f, 90f, rng, 0.9f);
                    break;
            }

            if (!busy || style == DrumStyle.Halftime) return;

            // A 16th-note snare flam closing the bar.
            AddNoise(buffer, start + beat * 3.5, 0.08, 0.10f, 40f, rng, 0.5f);
            AddNoise(buffer, start + beat * 3.75, 0.08, 0.13f, 40f, rng, 0.5f);
        }

        /// <summary>Sparse high bell so the second half lifts instead of repeating.</summary>
        private static void RenderLead(float[] buffer, double start, double beat, float[] chord)
        {
            AddTone(buffer, start, beat * 1.5, chord[1] * 2f, WaveShape.Sine, 0.11f, 0.01, 3.2);
            AddTone(buffer, start + beat * 2, beat * 1.5, chord[2] * 2f, WaveShape.Sine, 0.09f, 0.01, 3.2);
        }

        #endregion

        #region Synthesis primitives

        /// <summary>
        /// Renders one note additively. Anything past the end of the buffer wraps to the start,
        /// which is what makes the loop seamless — a pad tail continues over the loop point instead
        /// of being cut off.
        /// </summary>
        private static void AddTone(float[] buffer, double start, double duration, double frequency,
            WaveShape shape, float gain, double attack, double decayRate)
        {
            int startSample = (int)(start * SampleRate);
            int length = (int)(duration * SampleRate);
            if (length <= 0) return;

            double phase = 0.0;

            for (int i = 0; i < length; i++)
            {
                double t = i / (double)SampleRate;

                phase += frequency / SampleRate;
                if (phase > 1.0) phase -= 1.0;

                double value = shape switch
                {
                    WaveShape.Sine => Mathf.Sin((float)(phase * Mathf.PI * 2.0)),
                    WaveShape.Square => phase < 0.5 ? 1.0 : -1.0,
                    WaveShape.Saw => phase * 2.0 - 1.0,
                    WaveShape.Triangle => 1.0 - 4.0 * System.Math.Abs(System.Math.Round(phase - 0.25) - (phase - 0.25)),
                    _ => 0.0
                };

                double envelope = System.Math.Exp(-decayRate * t);
                if (attack > 0.0 && t < attack) envelope *= t / attack;

                int index = (startSample + i) % buffer.Length;
                buffer[index] += (float)(value * envelope) * gain;
            }
        }

        private static void AddKick(float[] buffer, double start, float gain)
        {
            int startSample = (int)(start * SampleRate);
            int length = (int)(0.14 * SampleRate);
            double phase = 0.0;

            for (int i = 0; i < length; i++)
            {
                double t = i / (double)length;

                // Pitch drop from a click down to sub — the whole character of a kick.
                double frequency = Mathf.Lerp(150f, 46f, (float)System.Math.Pow(t, 0.35));
                phase += frequency / SampleRate;

                double envelope = System.Math.Exp(-7.0 * t);
                int index = (startSample + i) % buffer.Length;
                buffer[index] += (float)(Mathf.Sin((float)(phase * Mathf.PI * 2.0)) * envelope) * gain;
            }
        }

        /// <summary>Filtered noise burst — hats at high cutoff, snare at low.</summary>
        private static void AddNoise(float[] buffer, double start, double duration, float gain,
            float decayRate, System.Random rng, float cutoff)
        {
            int startSample = (int)(start * SampleRate);
            int length = (int)(duration * SampleRate);
            float previous = 0f;

            for (int i = 0; i < length; i++)
            {
                float t = i / (float)SampleRate;
                var white = (float)(rng.NextDouble() * 2.0 - 1.0);

                // One-pole low-pass; cutoff near 1 passes the hiss, low values give a body thump.
                previous += (white - previous) * cutoff;

                float envelope = Mathf.Exp(-decayRate * t);
                int index = (startSample + i) % buffer.Length;
                buffer[index] += previous * envelope * gain;
            }
        }

        /// <summary>Scales to a target peak so layering can't clip.</summary>
        private static void Normalise(float[] buffer, float targetPeak)
        {
            float peak = 0f;
            for (int i = 0; i < buffer.Length; i++) peak = Mathf.Max(peak, Mathf.Abs(buffer[i]));

            if (peak <= 0.0001f) return;

            float scale = targetPeak / peak;
            for (int i = 0; i < buffer.Length; i++) buffer[i] *= scale;
        }

        #endregion
    }
}
