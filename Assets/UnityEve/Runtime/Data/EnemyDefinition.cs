using UnityEngine;
using UnityEve.Core.Battle;

namespace UnityEve.Data
{
    [CreateAssetMenu(menuName = "Unity Eve/Enemy", fileName = "Enemy")]
    public sealed class EnemyDefinition : ScriptableObject
    {
        public EnemyStats Stats = new EnemyStats();
        [Tooltip("Prefab with an EnemyActor component.")]
        public GameObject Prefab;
        [Tooltip("Bosses only: what climbs out when its health crosses a phase threshold.")]
        public EnemyDefinition Brood;
    }
}
