using UnityEngine;
using Unity.Eve.Battle;

namespace Unity.Eve
{
    /// <summary>
    /// Music: the exploration loop while walking the town, the battle loop in a fight, the boss loop
    /// against the Mother. Crossfades between two sources on real time (music never pauses).
    /// </summary>
    public sealed class MusicDirector : MonoBehaviour
    {
        [SerializeField] private AudioClip _explore;
        [SerializeField] private AudioClip _battle;
        [SerializeField] private AudioClip _boss;
        [SerializeField] private float _volume = 0.32f;

        private AudioSource _a, _b;
        private AudioSource _current;
        private float _fade = 1f;

        public static MusicDirector Instance { get; private set; }
        /// <summary>The loop now playing: "explore", "battle" or "boss" (autoplay checks this).</summary>
        public string Playing => _current == null || _current.clip == null ? "none"
            : _current.clip == _boss ? "boss" : _current.clip == _battle ? "battle" : "explore";

        public void Configure(AudioClip explore, AudioClip battle, AudioClip boss)
        {
            _explore = explore;
            _battle = battle;
            _boss = boss;
        }

        private AudioSource Source()
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.loop = true;
            s.playOnAwake = false;
            s.spatialBlend = 0f;
            s.volume = 0f;
            s.ignoreListenerPause = true;
            return s;
        }

        private void Awake()
        {
            Instance = this;
            _a = Source();
            _b = Source();
        }

        private void Start()
        {
            Play(_explore);
            var battle = BattleDirector.Instance;
            if (battle == null) return;
            battle.BattleStarted += _ => Play(battle.BossFight ? _boss : _battle);
            battle.BattleEnded += (_, __) => Play(_explore);
        }

        public void Play(AudioClip clip)
        {
            if (clip == null || (_current != null && _current.clip == clip)) return;
            var next = _current == _a ? _b : _a;
            next.clip = clip;
            next.volume = 0f;
            next.Play();
            _current = next;
            _fade = 0f;
        }

        private void Update()
        {
            if (_current == null) return;
            _fade = Mathf.Min(1f, _fade + Time.unscaledDeltaTime / 1.2f);
            var other = _current == _a ? _b : _a;
            _current.volume = _volume * _fade;
            other.volume = _volume * (1f - _fade);
            if (_fade >= 1f && other.isPlaying) other.Stop();
        }
    }
}
