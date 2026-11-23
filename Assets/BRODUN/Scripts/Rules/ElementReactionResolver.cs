using System.Collections.Generic;
using System.Linq;

namespace Brodun
{
    public enum ElementReaction { RapidFreeze, FrostGust, PoisonSpread, VenomBurn, Overload, Shock }

    public sealed class ElementReactionConfig
    {
        public int PerEnemyTurn { get; }
        public int FrostDamage { get; }
        public int FreezeReduction { get; }
        public int FrostWeak { get; }
        public int SpreadPoison { get; }
        public int SpreadFallbackDamage { get; }
        public int VenomBurnTicks { get; }
        public int VenomBurnDamage { get; }
        public int OverloadDamage { get; }
        public int ShockDamage { get; }
        public int ShockTargets { get; }
        public ElementReactionConfig(int perEnemyTurn=2,int frostDamage=3,int freezeReduction=4,int frostWeak=1,
            int spreadPoison=2,int venomBurnTicks=2,int venomBurnDamage=2,int overloadDamage=6,int shockDamage=3,int shockTargets=2,
            int spreadFallbackDamage=3)
        {
            PerEnemyTurn=perEnemyTurn; FrostDamage=frostDamage; FreezeReduction=freezeReduction; FrostWeak=frostWeak;
            SpreadPoison=spreadPoison; VenomBurnTicks=venomBurnTicks; VenomBurnDamage=venomBurnDamage;
            OverloadDamage=overloadDamage; ShockDamage=shockDamage; ShockTargets=shockTargets;
            SpreadFallbackDamage=spreadFallbackDamage;
        }
    }

    internal static class ElementReactionResolver
    {
        private static readonly Dictionary<(ElementType,ElementType),ElementReaction> pairs =
            new Dictionary<(ElementType,ElementType),ElementReaction>
        {
            {(ElementType.Water,ElementType.Ice),ElementReaction.RapidFreeze},
            {(ElementType.Ice,ElementType.Wind),ElementReaction.FrostGust},
            {(ElementType.Wind,ElementType.Poison),ElementReaction.PoisonSpread},
            {(ElementType.Poison,ElementType.Fire),ElementReaction.VenomBurn},
            {(ElementType.Fire,ElementType.Lightning),ElementReaction.Overload},
            {(ElementType.Lightning,ElementType.Water),ElementReaction.Shock}
        };
        internal static bool Supports(ElementType element) =>
            element==ElementType.Wind||element==ElementType.Water||element==ElementType.Ice||
            element==ElementType.Poison||element==ElementType.Fire||element==ElementType.Lightning;

        internal static void Apply(BattleRuntime battle,CombatTrace trace,ElementType element,
            IReadOnlyList<EnemyInstance> targets,IReadOnlyDictionary<string,ElementType> previous)
        {
            var config=battle.Data.Config.Reactions;
            bool shocked=false, spread=false;
            foreach(var enemy in targets)
            {
                var old=previous[enemy.TargetId];
                enemy.ElementMark=element;
                var mark=trace.Add(CombatEventKind.ElementMark,CombatCause.DirectAttack,CombatTrace.PlayerId,enemy.TargetId,
                    oldElement:old,newElement:element);
                if(!pairs.TryGetValue((old,element),out var reaction)&&!pairs.TryGetValue((element,old),out reaction))continue;
                if(enemy.ReactionTurn!=battle.Turn){enemy.ReactionTurn=battle.Turn;enemy.ReactionsThisTurn=0;}
                if(enemy.ReactionsThisTurn>=config.PerEnemyTurn)continue;
                enemy.ReactionsThisTurn++;
                int trigger=trace.Add(CombatEventKind.Reaction,CombatCause.Reaction,CombatTrace.PlayerId,enemy.TargetId,
                    reaction:reaction,oldElement:old,newElement:element,parent:mark.Index).Index;
                switch(reaction)
                {
                    case ElementReaction.RapidFreeze:
                        Hit(enemy,config.FrostDamage,CombatCause.Reaction);
                        Status(enemy,StatusEffectType.RapidFreeze,config.FreezeReduction);
                        break;
                    case ElementReaction.FrostGust:
                        Hit(enemy,config.FrostDamage,CombatCause.Reaction);
                        Status(enemy,StatusEffectType.Weak,config.FrostWeak);
                        break;
                    case ElementReaction.PoisonSpread:
                        var other=battle.Enemies.FirstOrDefault(e=>e!=enemy&&e.IsAlive);
                        if(other==null)Hit(enemy,config.SpreadFallbackDamage,CombatCause.Reaction);
                        else if(!spread)
                        {
                            spread=true;
                            battle.ApplyEnemyStatus(trace,other,StatusEffectType.Poison,config.SpreadPoison,CombatCause.Spread,trigger);
                        }
                        break;
                    case ElementReaction.VenomBurn:
                        if(enemy.Status.Poison>0)Status(enemy,StatusEffectType.VenomBurn,config.VenomBurnTicks);
                        break;
                    case ElementReaction.Overload:
                        Hit(enemy,config.OverloadDamage,CombatCause.Reaction);
                        break;
                    case ElementReaction.Shock:
                        Hit(enemy,config.ShockDamage,CombatCause.Reaction);
                        if(!shocked)
                        {
                            shocked=true;
                            foreach(var nearby in battle.Enemies.Where(e=>e!=enemy&&e.IsAlive).Take(config.ShockTargets).ToArray())
                                Hit(nearby,config.ShockDamage,CombatCause.Spread);
                        }
                        break;
                }
                battle.OnElementReaction(trace,enemy,trigger);
                void Hit(EnemyInstance target,int amount,CombatCause cause) =>
                    battle.HitEnemy(trace,target,amount,cause,parent:trigger,reactionBonus:amount);
                void Status(EnemyInstance target,StatusEffectType status,int value) =>
                    battle.ApplyEnemyStatus(trace,target,status,value,CombatCause.Reaction,trigger);
            }
        }
    }
}
