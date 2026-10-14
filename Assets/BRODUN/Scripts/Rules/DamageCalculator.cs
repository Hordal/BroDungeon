using System;
namespace Brodun
{
    public readonly struct DamageResult
    {
        public int Blocked { get; }
        public int HPDamage { get; }
        public DamageResult(int blocked,int damage) { Blocked=blocked; HPDamage=damage; }
    }
    public static class DamageCalculator
    {
        public static int AttackAmount(int baseDamage,int strength,int weak,int weakReduction) =>
            Math.Max(0,baseDamage+strength-(weak>0?weakReduction:0));
        // Call separately for each hit; block is consumed before HP, overkill is not counted.
        public static DamageResult Hit(int amount,ref int hp,ref int block)
        {
            int raw=Math.Max(0,amount), absorb=Math.Min(block,raw), damage=Math.Min(hp,raw-absorb);
            block-=absorb; hp-=damage; return new DamageResult(absorb,damage);
        }
    }
    public sealed class StatusState
    {
        public int Strength { get; internal set; }
        public int Weak { get; internal set; }
        public int Poison { get; internal set; }
        public void AddStrength(int amount) { Strength+=amount; }
        public void ApplyWeak(int turns) { Weak=Math.Max(Weak,turns); }
        public void AddPoison(int amount) { Poison+=amount; }
        internal void AfterEnemyAction() { if (Weak>0) Weak--; }
        internal int TickPoison(int hp)
        { int damage=Math.Min(hp,Poison); if(Poison>0) Poison--; return damage; }
    }
    public static class ElementRules
    {
        public static bool Matches(ConditionType condition,int playerBlock,int poison,ElementType previous)
        {
            switch(condition)
            {
                case ConditionType.Block: return playerBlock>0;
                case ConditionType.Poison: return poison>0;
                case ConditionType.Water: return previous==ElementType.Water;
                case ConditionType.Fire: return previous==ElementType.Fire;
                default: return false;
            }
        }
    }
}
