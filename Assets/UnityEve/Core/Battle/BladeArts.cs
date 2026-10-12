using System;

namespace UnityEve.Core.Battle
{
    /// <summary>
    /// Kat's katana, carried alongside the gun. Two blade arts, both taken as a turn from the command menu:
    /// <b>Slash</b>, a heavy cut that also catches anything beside the target in a forward arc, and
    /// <b>Rush</b>, a PE-fuelled dash through a distant target that cuts every creature along the line.
    /// Pure geometry on the ground plane (x, z); presentation supplies positions.
    /// </summary>
    public static class BladeArts
    {
        /// <summary>True when a creature at offset (dx, dz) from the player is inside the slash arc around the
        /// direction (fx, fz) the cut is aimed.</summary>
        public static bool InSlashArc(float fx, float fz, float dx, float dz, float reach)
        {
            var dist = (float)Math.Sqrt(dx * dx + dz * dz);
            if (dist > reach) return false;
            if (dist < 0.05f) return true;
            var flen = (float)Math.Sqrt(fx * fx + fz * fz);
            if (flen < 1e-5f) return true;
            var cos = (fx * dx + fz * dz) / (flen * dist);
            return cos >= (float)Math.Cos(BattleConfig.KatanaSlashArcDegrees * 0.5f * Math.PI / 180.0);
        }

        /// <summary>Where a rush from (sx, sz) through a target at (tx, tz) ends: past the target by the overshoot.</summary>
        public static (float x, float z) RushEnd(float sx, float sz, float tx, float tz)
        {
            var dx = tx - sx;
            var dz = tz - sz;
            var len = (float)Math.Sqrt(dx * dx + dz * dz);
            if (len < 1e-5f) return (tx, tz);
            var k = (len + BattleConfig.KatanaRushOvershoot) / len;
            return (sx + dx * k, sz + dz * k);
        }

        /// <summary>Where a rush aimed in direction (dx, dz) from (sx, sz) ends, at the given range — Line
        /// Rush's hand-aimed counterpart to <see cref="RushEnd"/>'s lock-on-and-extend-through-the-target.</summary>
        public static (float x, float z) AimedRushEnd(float sx, float sz, float dx, float dz, float range)
        {
            var len = (float)Math.Sqrt(dx * dx + dz * dz);
            if (len < 1e-5f) return (sx, sz);
            return (sx + dx / len * range, sz + dz / len * range);
        }

        /// <summary>True when a creature at (px, pz) with body radius r is cut by a rush from s to e.</summary>
        public static bool OnRushPath(float sx, float sz, float ex, float ez, float px, float pz, float bodyRadius)
        {
            var dx = ex - sx;
            var dz = ez - sz;
            var len2 = dx * dx + dz * dz;
            var t = len2 > 1e-6f ? ((px - sx) * dx + (pz - sz) * dz) / len2 : 0f;
            t = t < 0f ? 0f : t > 1f ? 1f : t;
            var cx = sx + dx * t - px;
            var cz = sz + dz * t - pz;
            var reach = BattleConfig.KatanaRushHalfWidth + bodyRadius;
            return cx * cx + cz * cz <= reach * reach;
        }

        /// <summary>Damage of one blade hit before variance and defence.</summary>
        public static int Power(int baseDamage, int level, bool rush) =>
            (int)Math.Round((baseDamage + level * 2) * (rush ? BattleConfig.KatanaRushDamageMultiplier : 1f));
    }
}
