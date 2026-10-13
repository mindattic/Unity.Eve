using System.Collections.Generic;
using UnityEngine;

namespace UnityEve
{
    /// <summary>Every sound cue in the game. "Every event has a sound."</summary>
    public enum SoundId
    {
        None,
        UiMove,
        UiConfirm,
        UiCancel,
        UiDeny,
        AtbReady,
        Gunshot,
        ShotgunBlast,
        Reload,
        DryFire,
        HitFlesh,
        HitCritical,
        PlayerHurt,
        EnemyTelegraph,
        EnemyDeath,
        Dodge,
        PerfectDodge,
        PeCast,
        Heal,
        Explosion,
        Throw,
        DoorOpen,
        DoorLocked,
        Rummage,
        ItemFound,
        NothingFound,
        EncounterStart,
        Victory,
        LevelUp,
        Footstep,
        EnemyBuffCharge,
        EnemyBuff,
        KatanaSlash,
        KatanaRush,
        EnemyCharge,
        Save,
        CoverBlock,
        ActiveReload,
        Shove,
        EnemySpit,
        Stinger,
        AcidSizzle,
        Shootdodge,
        HardLand,
    }

    /// <summary>Pooled 2D one-shot player. Sounds ignore time scale so menus stay responsive while paused.</summary>
    public sealed class AudioService : MonoBehaviour
    {
        [SerializeField] private SoundBank _bank;
        [SerializeField] private int _voices = 12;

        private readonly List<AudioSource> _pool = new List<AudioSource>();
        private int _next;

        public static AudioService Instance { get; private set; }

        public SoundBank Bank
        {
            get => _bank;
            set => _bank = value;
        }

        private void Awake()
        {
            Instance = this;
            for (var i = 0; i < _voices; i++)
            {
                var src = gameObject.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.spatialBlend = 0f;
                _pool.Add(src);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public static void Play(SoundId id, float volumeScale = 1f)
        {
            if (Instance != null) Instance.PlayInternal(id, volumeScale);
        }

        private void PlayInternal(SoundId id, float volumeScale)
        {
            var entry = _bank != null ? _bank.Find(id) : null;
            if (entry == null || entry.Clips.Length == 0) return;
            var clip = entry.Clips[Random.Range(0, entry.Clips.Length)];
            if (clip == null) return;
            var src = _pool[NextVoice()];
            src.pitch = Random.Range(entry.PitchRange.x, entry.PitchRange.y);
            src.PlayOneShot(clip, entry.Volume * volumeScale);
        }

        /// <summary>
        /// Round-robins voices, but prefers one that's finished playing: setting <c>pitch</c> on a source
        /// that's still mid one-shot would retroactively warp that sound, so reusing a free source first
        /// avoids stepping on it.
        /// </summary>
        private int NextVoice()
        {
            for (var n = 0; n < _pool.Count; n++)
            {
                var idx = (_next + n) % _pool.Count;
                if (_pool[idx].isPlaying) continue;
                _next = (idx + 1) % _pool.Count;
                return idx;
            }
            var fallback = _next;
            _next = (_next + 1) % _pool.Count;
            return fallback;
        }
    }
}
