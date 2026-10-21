using System;

namespace Unity.Eve.Editor
{
    /// <summary>
    /// Synthesized music loops (original work). Every oscillator completes whole cycles over the loop
    /// and every note decays inside it, so the loops join seamlessly. Exploration: a dark drone with
    /// sparse minor-key bells and wind. Battle: a driving kick, bass ostinato and hats. Boss: heavier,
    /// with a tritone bass and dissonant stabs.
    /// </summary>
    public static partial class WavSynth
    {
        private static void WriteMusic(string folder)
        {
            Write(folder, "music_explore", Explore());
            Write(folder, "music_battle", Battle(bpm: 120f, seconds: 8f, root: 55f, tritone: false));
            Write(folder, "music_boss", Battle(bpm: 150f, seconds: 8f, root: 49f, tritone: true));
        }

        private static float Sine(double f, double t) => (float)Math.Sin(2 * Math.PI * f * t);

        private static float[] Explore()
        {
            const float seconds = 24f;
            var b = Buffer(seconds);
            for (var i = 0; i < b.Length; i++)
            {
                var t = i / (double)Rate;
                // Drone on A (55 Hz) and E (82.5 Hz): whole cycles in 24 s, with a slow swell.
                var swell = 0.75f + 0.25f * Sine(1.0 / 12.0, t);
                var drone = (Sine(55, t) * 0.5f + Sine(82.5, t) * 0.25f + Sine(110, t) * 0.12f) * swell;
                // Wind: incommensurate low-frequency sines, each completing whole cycles in 24 s like
                // every other oscillator here, summed for a shifting texture. A filtered-noise random
                // walk was used before, but it has no relationship between its value at sample 0 and at
                // the last sample, so it clicked at the loop seam.
                var wind = Sine(7.0 / 24.0, t + 3.1) * 0.4f + Sine(11.0 / 24.0, t + 7.4) * 0.3f
                         + Sine(17.0 / 24.0, t + 1.7) * 0.2f + Sine(29.0 / 24.0, t + 5.2) * 0.15f;
                b[i] = drone * 0.32f + wind * 1.6f * (0.6f + 0.4f * Sine(1.0 / 8.0, t));
            }
            // Sparse bells from A minor pentatonic, each fully decayed before the loop ends.
            var notes = new[] { 440f, 523.25f, 587.33f, 659.25f, 783.99f, 880f };
            var pattern = new[] { (1.0f, 3), (3.5f, 1), (6.0f, 4), (9.5f, 2), (12.0f, 0), (14.5f, 5), (17.0f, 3), (20.0f, 1) };
            foreach (var (at, n) in pattern)
            {
                var start = (int)(at * Rate);
                for (var i = start; i < b.Length; i++)
                {
                    var t = (i - start) / (double)Rate;
                    var env = (float)Math.Exp(-t * 1.6);
                    if (env < 0.001f) break;
                    b[i] += (Sine(notes[n], t) + 0.3f * Sine(notes[n] * 2.01, t)) * env * 0.16f;
                }
            }
            return b;
        }

        private static float[] Battle(float bpm, float seconds, float root, bool tritone)
        {
            var b = Buffer(seconds);
            var beat = 60f / bpm;
            var beats = (int)Math.Round(seconds / beat);
            uint seed = 99;
            // Bass ostinato (eighth notes): root, root, minor third, root / fifth or tritone.
            var riff = tritone
                ? new[] { 1f, 1f, 1.189f, 1f, 1.414f, 1f, 1.189f, 0.944f }
                : new[] { 1f, 1f, 1.189f, 1f, 1.498f, 1f, 1.335f, 1.189f };
            for (var e = 0; e < beats * 2; e++)
            {
                var start = (int)(e * beat * 0.5f * Rate);
                var f = root * 2f * riff[e % riff.Length];
                var len = (int)(beat * 0.45f * Rate);
                for (var i = 0; i < len && start + i < b.Length; i++)
                {
                    var t = i / (double)Rate;
                    var saw = 2f * (float)(t * f - Math.Floor(t * f + 0.5));
                    var env = (float)Math.Exp(-t * 6) * (1f - i / (float)len);
                    b[start + i] += saw * env * 0.32f;
                }
            }
            for (var k = 0; k < beats; k++)
            {
                // Kick on every beat, snare-ish noise on 2 and 4, hats on the off-beats.
                var start = (int)(k * beat * Rate);
                for (var i = 0; i < (int)(0.22f * Rate) && start + i < b.Length; i++)
                {
                    var t = i / (double)Rate;
                    var f = 50 + 90 * Math.Exp(-t * 30);
                    b[start + i] += (float)Math.Sin(2 * Math.PI * f * t) * (float)Math.Exp(-t * 14) * 0.75f;
                }
                if (k % 2 == 1)
                    for (var i = 0; i < (int)(0.16f * Rate) && start + i < b.Length; i++)
                        b[start + i] += Noise(ref seed) * (float)Math.Exp(-i / (double)Rate * 22) * 0.35f;
                var hat = start + (int)(beat * 0.5f * Rate);
                for (var i = 0; i < (int)(0.04f * Rate) && hat + i < b.Length; i++)
                    b[hat + i] += Noise(ref seed) * (1f - i / (0.04f * Rate)) * 0.14f;
            }
            if (tritone)
            {
                // Dissonant stabs every bar.
                for (var bar = 0; bar < beats / 4; bar++)
                {
                    var start = (int)(bar * 4 * beat * Rate);
                    for (var i = 0; i < (int)(0.5f * Rate) && start + i < b.Length; i++)
                    {
                        var t = i / (double)Rate;
                        var env = (float)Math.Exp(-t * 5);
                        b[start + i] += (Sine(root * 4, t) + Sine(root * 4 * 1.414, t) + Sine(root * 4 * 1.059, t)) * env * 0.12f;
                    }
                }
            }
            return b;
        }
    }
}
