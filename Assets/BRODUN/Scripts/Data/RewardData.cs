using UnityEngine;
namespace Brodun.Data
{
    [CreateAssetMenu(menuName = "BRODUN/Reward")]
    public sealed class RewardData : ScriptableObject
    {
        [SerializeField] private int gold;
        [SerializeField] private int candidateCount;
        public RewardDefinition ToDefinition() => new RewardDefinition(gold, candidateCount);
        public void Initialize(int amount, int count) { gold = amount; candidateCount = count; }
    }
}
