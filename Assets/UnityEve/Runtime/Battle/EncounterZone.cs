using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEve.Data;

namespace UnityEve.Battle
{
    [Serializable]
    public sealed class EncounterSpawn
    {
        public EnemyDefinition Enemy;
        public Vector3 LocalPosition;
    }

    /// <summary>
    /// A Parasite Eve battle space: walking in makes the creatures materialize and seals the area with
    /// invisible walls until they are dead or the player escapes. No screen transition; the fight
    /// happens where you stand. A cleared zone stays quiet.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public sealed class EncounterZone : MonoBehaviour
    {
        [SerializeField] private string _displayName = "Street";
        [SerializeField] private List<EncounterSpawn> _spawns = new List<EncounterSpawn>();
        [Tooltip("Re-arm delay after a successful escape, in seconds.")]
        [SerializeField] private float _rearmSeconds = 10f;

        private readonly List<Collider> _walls = new List<Collider>();
        private float _armedAt;

        public string DisplayName => _displayName;
        public IReadOnlyList<EncounterSpawn> Spawns => _spawns;
        public bool Cleared { get; private set; }
        public bool IsArmed => !Cleared && Time.time >= _armedAt;
        public Bounds Bounds => GetComponent<BoxCollider>().bounds;

        public void Configure(string displayName, List<EncounterSpawn> spawns)
        {
            _displayName = displayName;
            _spawns = spawns;
        }

        private void Awake()
        {
            var box = GetComponent<BoxCollider>();
            box.isTrigger = true;
            BuildWalls(box);
            SealWalls(false);
        }

        private void BuildWalls(BoxCollider box)
        {
            var size = box.size;
            var c = box.center;
            const float t = 0.3f;
            void Wall(Vector3 centre, Vector3 wallSize)
            {
                var go = new GameObject("ArenaWall");
                go.layer = gameObject.layer;
                go.transform.SetParent(transform, false);
                go.transform.localPosition = centre;
                var col = go.AddComponent<BoxCollider>();
                col.size = wallSize;
                _walls.Add(col);
            }
            Wall(c + new Vector3(size.x * 0.5f, 0f, 0f), new Vector3(t, size.y, size.z));
            Wall(c - new Vector3(size.x * 0.5f, 0f, 0f), new Vector3(t, size.y, size.z));
            Wall(c + new Vector3(0f, 0f, size.z * 0.5f), new Vector3(size.x, size.y, t));
            Wall(c - new Vector3(0f, 0f, size.z * 0.5f), new Vector3(size.x, size.y, t));
        }

        public void SealWalls(bool sealedOn)
        {
            foreach (var w in _walls) w.enabled = sealedOn;
        }

        public void MarkCleared()
        {
            Cleared = true;
            SealWalls(false);
        }

        public void MarkEscaped()
        {
            _armedAt = Time.time + _rearmSeconds;
            SealWalls(false);
        }

        public Vector3 SpawnWorldPosition(EncounterSpawn spawn) => transform.TransformPoint(spawn.LocalPosition);

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player") || !IsArmed) return;
            var director = BattleDirector.Instance;
            if (director != null && !director.InBattle && !director.SuppressEncounters) director.Begin(this);
        }

        private void OnDrawGizmos()
        {
            var box = GetComponent<BoxCollider>();
            Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.15f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(box.center, box.size);
            Gizmos.color = Color.red;
            foreach (var s in _spawns) Gizmos.DrawWireSphere(s.LocalPosition, 0.4f);
        }
    }
}
