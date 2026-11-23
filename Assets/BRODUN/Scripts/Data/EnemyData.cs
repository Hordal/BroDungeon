using UnityEngine;
namespace Brodun.Data
{
    [CreateAssetMenu(menuName = "BRODUN/Enemy")]
    public sealed class EnemyData : ScriptableObject
    {
        [SerializeField] private EnemyRecord definition;
        [SerializeField, Min(0)] private int attackBonus;
        [SerializeField, Min(0)] private int healthBonus;
        // Stable asset_id is retained; no PNG/Animator import in PHASE 2.
        public EnemyDefinition ToDefinition() => definition.ToDefinition(attackBonus, healthBonus);
        public void Initialize(EnemyRecord value) { definition = value; }
    }
}
