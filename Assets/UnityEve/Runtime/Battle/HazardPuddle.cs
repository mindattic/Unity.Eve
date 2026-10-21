using UnityEngine;
using Unity.Eve.Core.Battle;
using Unity.Eve.Player;
using Unity.Eve.UI;

namespace Unity.Eve.Battle
{
    /// <summary>
    /// Acid/slime left where a ranged hit landed (<see cref="EnemyStats.LeavesPuddle"/>): ticks damage to
    /// the player while she stands in it, then burns out on its own. The rule (how much, how wide, how
    /// long) lives in <see cref="BattleConfig"/>; this just times the tick and checks distance. The glowing
    /// disc itself is <see cref="BattleFx.Puddle"/> — presentation, not a rule.
    /// </summary>
    public sealed class HazardPuddle : MonoBehaviour
    {
        private PlayerCharacter _player;
        private float _diesAt;
        private float _nextTickAt;

        public static void Spawn(Vector3 at, PlayerCharacter player)
        {
            var fx = BattleFx.Instance;
            if (fx == null) return;
            var go = fx.Puddle(at, new Color(0.55f, 0.85f, 0.25f, 0.65f), BattleConfig.PuddleRadius);
            var hazard = go.AddComponent<HazardPuddle>();
            hazard._player = player;
            hazard._diesAt = Time.time + BattleConfig.PuddleDurationSeconds;
            // Without this, _nextTickAt defaults to 0, which is already in the past the instant the game
            // has been running at all — the puddle would land its first tick on the very same frame as the
            // spit hit that created it, double-hitting her in the same instant instead of starting its DOT
            // one full tick interval later.
            hazard._nextTickAt = Time.time + BattleConfig.PuddleTickSeconds;
        }

        private void Update()
        {
            var battle = BattleDirector.Instance;
            if (Time.time >= _diesAt || battle == null || !battle.InBattle)
            {
                Destroy(gameObject);
                return;
            }
            if (_player == null || _player.IsDead || Time.time < _nextTickAt) return;
            var d = transform.position - _player.transform.position;
            d.y = 0f;
            if (d.magnitude > BattleConfig.PuddleRadius) return;
            _nextTickAt = Time.time + BattleConfig.PuddleTickSeconds;
            var dealt = _player.TakeDamage(BattleConfig.PuddleDamagePerTick);
            if (dealt <= 0) return;
            GameHud.Instance?.Hurt(0.15f);
            GameHud.Instance?.Popup(_player.transform.position + Vector3.up * 1.6f, dealt.ToString(), new Color(0.6f, 1f, 0.3f));
            AudioService.Play(SoundId.AcidSizzle, 0.5f);
        }
    }
}
