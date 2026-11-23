using UnityEngine;
namespace Brodun.Data
{
    [CreateAssetMenu(menuName = "BRODUN/Battle Config")]
    public sealed class BattleConfigData : ScriptableObject
    {
        [SerializeField] private ConfigRecord definition;
        [SerializeField, Min(0)] private int handSizeOverride;
        [SerializeField, Min(0)] private int baseEnergyOverride;
        public BattleConfig ToDefinition() => definition.ToDefinition(
            baseEnergyOverride>0 ? baseEnergyOverride : BattleConfig.DefaultBaseEnergy,BattleConfig.DefaultEnergyCap,
            handSizeOverride>0?handSizeOverride:null);
        public void Initialize(ConfigRecord value) { definition = value; }
    }
}
