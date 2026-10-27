using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace Unity.Eve.Core.Battle
{
    /// <summary>
    /// Battle tuning as data. Every public static property of <see cref="BattleConfig"/> is a knob, named in
    /// the tuning file exactly as in code. <see cref="Apply"/> reads a flat JSON object of knob numbers,
    /// validates the whole file, and changes nothing unless every value passes, so a bad edit can never
    /// leave the battle half-tuned.
    /// </summary>
    public static partial class BattleConfig
    {
        private static Dictionary<string, PropertyInfo> _knobs;
        private static Dictionary<string, double> _defaults;

        /// <summary>Knobs that may be negative (a cosine).</summary>
        private static readonly HashSet<string> Signed = new HashSet<string> { nameof(EnemyFlankOpeningFacing) };

        /// <summary>Every knob name, in declaration order.</summary>
        public static IReadOnlyList<string> Knobs => KnobMap().Keys.ToList();

        private static Dictionary<string, PropertyInfo> KnobMap()
        {
            if (_knobs != null) return _knobs;
            var map = new Dictionary<string, PropertyInfo>();
            foreach (var p in typeof(BattleConfig).GetProperties(BindingFlags.Public | BindingFlags.Static))
                if (p.PropertyType == typeof(float) || p.PropertyType == typeof(int))
                    map[p.Name] = p;
            // Taken before any knob can change, so these are the built-in values.
            _defaults = map.ToDictionary(kv => kv.Key, kv => Convert.ToDouble(kv.Value.GetValue(null), CultureInfo.InvariantCulture));
            return _knobs = map;
        }

        /// <summary>The value a knob has now.</summary>
        public static double Get(string knob) =>
            Convert.ToDouble(KnobMap()[knob].GetValue(null), CultureInfo.InvariantCulture);

        /// <summary>The built-in value of a knob.</summary>
        public static double Default(string knob)
        {
            KnobMap();
            return _defaults[knob];
        }

        /// <summary>Puts every knob back to its built-in value.</summary>
        public static void ResetToDefaults()
        {
            foreach (var kv in KnobMap()) Set(kv.Value, _defaults[kv.Key]);
        }

        /// <summary>The current tuning as the file format, every knob in declaration order.</summary>
        public static string ToJson()
        {
            var sb = new StringBuilder("{\n");
            var names = Knobs;
            for (var i = 0; i < names.Count; i++)
                sb.Append("  \"").Append(names[i]).Append("\": ")
                  .Append(((float)Get(names[i])).ToString("R", CultureInfo.InvariantCulture))
                  .Append(i < names.Count - 1 ? ",\n" : "\n");
            return sb.Append("}\n").ToString();
        }

        private static readonly Regex Pair = new Regex(@"""(?<k>[^""]*)""\s*:\s*(?<v>[^,}\s]+)\s*(,|$)");

        /// <summary>Reads a tuning file. Knobs it leaves out keep their current value.</summary>
        /// <returns>Every problem found; empty when the file was applied. On any problem nothing is applied.</returns>
        public static IReadOnlyList<string> Apply(string json)
        {
            var errors = new List<string>();
            var values = Parse(json, errors);
            if (errors.Count == 0) Validate(values, errors);
            if (errors.Count > 0) return errors;
            var map = KnobMap();
            foreach (var kv in values) Set(map[kv.Key], kv.Value);
            return errors;
        }

        private static Dictionary<string, double> Parse(string json, List<string> errors)
        {
            var values = new Dictionary<string, double>();
            var text = (json ?? "").Trim();
            if (!text.StartsWith("{") || !text.EndsWith("}"))
            {
                errors.Add("not a JSON object");
                return values;
            }
            var body = text.Substring(1, text.Length - 2);
            foreach (Match m in Pair.Matches(body))
            {
                var key = m.Groups["k"].Value;
                if (!KnobMap().ContainsKey(key)) errors.Add($"unknown knob '{key}'");
                else if (values.ContainsKey(key)) errors.Add($"'{key}' given twice");
                else if (!double.TryParse(m.Groups["v"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                    errors.Add($"'{key}' is not a number: {m.Groups["v"].Value}");
                else values[key] = v;
            }
            if (Regex.Replace(body, @"\s", "").Length != Regex.Replace(string.Concat(Pair.Matches(body).Cast<Match>().Select(x => x.Value)), @"\s", "").Length)
                errors.Add("only \"Knob\": number pairs are allowed");
            return values;
        }

        private static void Validate(Dictionary<string, double> values, List<string> errors)
        {
            double V(string k) => values.TryGetValue(k, out var v) ? v : Get(k);
            var map = KnobMap();
            foreach (var kv in values)
            {
                var v = kv.Value;
                if (double.IsNaN(v) || double.IsInfinity(v) || Math.Abs(v) > 1e6) { errors.Add($"'{kv.Key}' is out of range: {v}"); continue; }
                if (v < 0 && !Signed.Contains(kv.Key)) errors.Add($"'{kv.Key}' may not be negative: {v}");
                if (map[kv.Key].PropertyType == typeof(int) && Math.Abs(v - Math.Round(v)) > 1e-9) errors.Add($"'{kv.Key}' must be a whole number: {v}");
                if (kv.Key.EndsWith("Chance") && v > 1) errors.Add($"'{kv.Key}' is a chance and may not exceed 1: {v}");
            }
            void Ordered(string min, string max)
            {
                if (V(min) > V(max)) errors.Add($"'{min}' ({V(min)}) is above '{max}' ({V(max)})");
            }
            Ordered(nameof(DamageVarianceLow), nameof(DamageVarianceHigh));
            Ordered(nameof(EnemyThinkMinSeconds), nameof(EnemyThinkMaxSeconds));
            Ordered(nameof(EnemyRingMinRadius), nameof(EnemyRingMaxRadius));
            Ordered(nameof(TokenBreatherMinSeconds), nameof(TokenBreatherMaxSeconds));
            Ordered(nameof(EscapeMinChance), nameof(EscapeMaxChance));
            if (V(nameof(MaxCommittedAttackers)) < 1) errors.Add($"'{nameof(MaxCommittedAttackers)}' must be at least 1");
            if (V(nameof(BaseAtbSeconds)) <= 0) errors.Add($"'{nameof(BaseAtbSeconds)}' must be above 0");
            if (V(nameof(ExpCurveGrowth)) < 1) errors.Add($"'{nameof(ExpCurveGrowth)}' must be at least 1");
        }

        private static void Set(PropertyInfo knob, double value)
        {
            if (knob.PropertyType == typeof(int)) knob.SetValue(null, (int)Math.Round(value));
            else knob.SetValue(null, (float)value);
        }
    }
}
