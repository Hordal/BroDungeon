using UnityEngine;
namespace Brodun.Data
{
    [CreateAssetMenu(menuName = "BRODUN/Battle Config")]
    public sealed class BattleConfigData : ScriptableObject
    {
        [SerializeField] private ConfigRecord definition;
        public BattleConfig ToDefinition() => definition.ToDefinition();
        public void Initialize(ConfigRecord value) { definition = value; }
    }
}
