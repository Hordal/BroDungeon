using UnityEngine;
namespace Brodun.Data
{
    [System.Serializable] public sealed class RewardTierRecord
    {
        public int baseGold,extraGold,low,middle,high;
        public RewardTierRecord(int basic,int extra,int l,int m,int h){baseGold=basic;extraGold=extra;low=l;middle=m;high=h;}
        public RewardTier ToDefinition()=>new RewardTier(baseGold,extraGold,low,middle,high);
    }
    [CreateAssetMenu(menuName = "BRODUN/Reward")]
    public sealed class RewardData : ScriptableObject
    {
        [SerializeField] private int gold;
        [SerializeField] private int candidateCount;
        [SerializeField] private bool formalBattleRewards;
        [SerializeField] private int uniqueWeight=65,elementWeight=35;
        [SerializeField] private RewardTierRecord normal=new RewardTierRecord(10,15,70,25,5);
        [SerializeField] private RewardTierRecord elite=new RewardTierRecord(20,25,45,40,15);
        [SerializeField] private RewardTierRecord boss=new RewardTierRecord(30,0,0,0,0);
        public RewardDefinition ToDefinition() => new RewardDefinition(gold,candidateCount,
            formalBattleRewards?new RewardPolicy(uniqueWeight,elementWeight,normal.ToDefinition(),elite.ToDefinition(),boss.ToDefinition()):null);
        public void Initialize(int amount, int count) { gold = amount; candidateCount = count; }
    }
}
