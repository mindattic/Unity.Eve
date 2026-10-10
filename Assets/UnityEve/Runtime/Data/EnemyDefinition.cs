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
    }
}
