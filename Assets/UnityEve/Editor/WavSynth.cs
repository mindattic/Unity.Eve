using System;
using System.IO;

namespace UnityEve.Editor
{
    /// <summary>
    /// Reproducible procedural sound effects (gunshots, explosions, PE stings) written as 16-bit mono
    /// WAV. Kenney's CC0 packs have no firearms, so these are synthesized: original, license-free,
    /// and regenerated identically on every content build.
    /// </summary>
    public static partial class WavSynth
    {
        private const int Rate = 22050;

        public static void WriteAll(string folder)
        {
            Directory.CreateDirectory(folder);
            Write(folder, "gunshot", Gunshot(0.32f, 1f));
            Write(folder, "shotgun", Gunshot(0.6f, 0.55f));
            Write(folder, "explosion", Explosion());
            Write(folder, "pe_cast", Sweep(220f, 880f, 0.55f, 0.5f));
            Write(folder, "heal", Arpeggio(new[] { 523f, 659f, 784f, 1047f }, 0.09f, 0.4f));
            Write(folder, "atb_ready", Arpeggio(new[] { 880f, 1320f }, 0.06f, 0.35f));
            Write(folder, "encounter", Chord(new[] { 110f, 116.5f, 164.8f, 233f }, 1.4f));
            Write(folder, "victory", Arpeggio(new[] { 392f, 523f, 659f, 784f, 1047f }, 0.11f, 0.45f));
            Write(folder, "level_up", Arpeggio(new[] { 523f, 784f, 1047f, 1568f }, 0.08f, 0.45f));
            Write(folder, "telegraph", Growl(0.45f));
            Write(folder, "perfect_dodge", PerfectDodge());
            Write(folder, "dodge", Whoosh(0.28f));
            Write(folder, "throw", Whoosh(0.22f));
            Write(folder, "enemy_death", Sweep(300f, 60f, 0.7f, 0.6f, noise: 0.5f));
            Write(folder, "enemy_buff_charge", Sweep(70f, 260f, 1.1f, 0.45f, noise: 0.25f));
            Write(folder, "enemy_buff", Chord(new[] { 98f, 103.8f, 146.8f }, 0.6f));
            Write(folder, "katana_slash", Mix(Whoosh(0.24f), Sweep(2400f, 1800f, 0.24f, 0.18f)));
            Write(folder, "enemy_charge", Growl(0.35f));
            Write(folder, "save", Arpeggio(new[] { 659f, 880f, 1319f }, 0.12f, 0.35f));
            Write(folder, "cover_block", Mix(Chord(new[] { 880f, 1320f }, 0.1f), Sweep(600f, 150f, 0.14f, 0.35f)));
            Write(folder, "active_reload", Arpeggio(new[] { 1047f, 1568f, 2093f }, 0.045f, 0.4f));
            WriteMusic(folder);
            Write(folder, "katana_rush", Mix(Whoosh(0.4f), Sweep(900f, 3200f, 0.4f, 0.22f, noise: 0.2f)));
        }

        private static float[] Buffer(float seconds) => new float[(int)(seconds * Rate)];

        /// <summary>Sums two sounds (the longer sets the length).</summary>
        private static float[] Mix(float[] a, float[] b)
        {
            var o = new float[System.Math.Max(a.Length, b.Length)];
            for (var i = 0; i < o.Length; i++) o[i] = (i < a.Length ? a[i] : 0f) + (i < b.Length ? b[i] : 0f);
            return o;
        }

        private static float Noise(ref uint s)
        {
            s ^= s << 13;
            s ^= s >> 17;
            s ^= s << 5;
            return (s / (float)uint.MaxValue) * 2f - 1f;
        }

        private static float[] Gunshot(float seconds, float brightness)
        {
            var b = Buffer(seconds);
            uint seed = 12345;
            float lp = 0f;
            for (var i = 0; i < b.Length; i++)
            {
                var t = i / (float)Rate;
                var env = (float)Math.Exp(-t * 18f);
                lp += (Noise(ref seed) - lp) * (0.25f + 0.6f * brightness);
                var thump = (float)Math.Sin(2 * Math.PI * 70f * t) * (float)Math.Exp(-t * 30f);
                b[i] = lp * env * 1.2f + thump * 0.8f;
            }
            return b;
        }

        private static float[] Explosion()
        {
            var b = Buffer(1.4f);
            uint seed = 777;
            float lp = 0f, lp2 = 0f;
            for (var i = 0; i < b.Length; i++)
            {
                var t = i / (float)Rate;
                var env = t < 0.02f ? t / 0.02f : (float)Math.Exp(-(t - 0.02f) * 3.2f);
                lp += (Noise(ref seed) - lp) * 0.08f;
                lp2 += (lp - lp2) * 0.2f;
                var rumble = (float)Math.Sin(2 * Math.PI * (45f - t * 15f) * t);
                b[i] = (lp2 * 3.5f + rumble * 0.5f) * env;
            }
            return b;
        }

        private static float[] Sweep(float from, float to, float seconds, float level, float noise = 0f)
        {
            var b = Buffer(seconds);
            uint seed = 99;
            double phase = 0;
            for (var i = 0; i < b.Length; i++)
            {
                var k = i / (float)b.Length;
                var f = from + (to - from) * k;
                phase += 2 * Math.PI * f / Rate;
                var env = (float)Math.Sin(Math.PI * k);
                b[i] = ((float)Math.Sin(phase) * (1f - noise) + Noise(ref seed) * noise) * env * level;
            }
            return b;
        }

        private static float[] Arpeggio(float[] notes, float step, float level)
        {
            var b = Buffer(step * notes.Length + 0.35f);
            for (var n = 0; n < notes.Length; n++)
            {
                var start = (int)(n * step * Rate);
                for (var i = start; i < b.Length; i++)
                {
                    var t = (i - start) / (float)Rate;
                    b[i] += (float)Math.Sin(2 * Math.PI * notes[n] * t) * (float)Math.Exp(-t * 7f) * level;
                }
            }
            return b;
        }

        private static float[] Chord(float[] notes, float seconds)
        {
            var b = Buffer(seconds);
            for (var i = 0; i < b.Length; i++)
            {
                var t = i / (float)Rate;
                var env = Math.Min(1f, t * 20f) * (float)Math.Exp(-t * 1.6f);
                float s = 0;
                foreach (var f in notes) s += (float)Math.Sign(Math.Sin(2 * Math.PI * f * t)) * 0.12f;
                b[i] = s * env;
            }
            return b;
        }

        private static float[] Growl(float seconds)
        {
            var b = Buffer(seconds);
            for (var i = 0; i < b.Length; i++)
            {
                var t = i / (float)Rate;
                var f = 85f + 25f * (float)Math.Sin(2 * Math.PI * 9f * t);
                var saw = 2f * (float)(t * f - Math.Floor(t * f + 0.5f));
                var env = (float)Math.Sin(Math.PI * t / seconds);
                b[i] = saw * env * 0.45f;
            }
            return b;
        }

        private static float[] Whoosh(float seconds)
        {
            var b = Buffer(seconds);
            uint seed = 4242;
            float lp = 0f;
            for (var i = 0; i < b.Length; i++)
            {
                var k = i / (float)b.Length;
                lp += (Noise(ref seed) - lp) * (0.05f + 0.3f * k);
                b[i] = lp * (float)Math.Sin(Math.PI * k) * 1.4f;
            }
            return b;
        }

        private static float[] PerfectDodge()
        {
            var whoosh = Whoosh(0.3f);
            var chime = Arpeggio(new[] { 1568f, 2093f }, 0.05f, 0.35f);
            var b = Buffer(0.7f);
            for (var i = 0; i < b.Length; i++)
            {
                if (i < whoosh.Length) b[i] += whoosh[i];
                var j = i - (int)(0.12f * Rate);
                if (j >= 0 && j < chime.Length) b[i] += chime[j];
            }
            return b;
        }

        private static void Write(string folder, string name, float[] samples)
        {
            var peak = 0f;
            foreach (var s in samples) peak = Math.Max(peak, Math.Abs(s));
            var gain = peak > 0.95f ? 0.95f / peak : 1f;
            using var w = new BinaryWriter(File.Create(Path.Combine(folder, name + ".wav")));
            var dataBytes = samples.Length * 2;
            w.Write(new[] { 'R', 'I', 'F', 'F' });
            w.Write(36 + dataBytes);
            w.Write(new[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' });
            w.Write(16);
            w.Write((short)1);
            w.Write((short)1);
            w.Write(Rate);
            w.Write(Rate * 2);
            w.Write((short)2);
            w.Write((short)16);
            w.Write(new[] { 'd', 'a', 't', 'a' });
            w.Write(dataBytes);
            foreach (var s in samples) w.Write((short)(Math.Max(-1f, Math.Min(1f, s * gain)) * short.MaxValue));
        }
    }
}
