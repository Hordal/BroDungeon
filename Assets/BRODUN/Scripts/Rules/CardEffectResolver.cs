using System;
using System.Collections.Generic;
using System.Linq;

namespace Brodun
{
    public sealed class CardResult
    {
        public string CardId => Trace.CardId;
        public string CardInstanceId { get; }
        public string SelectedTargetId { get; }
        public int BaseCost { get; }
        public int PaidCost { get; }
        public int Damage { get; }
        public int Blocked { get; }
        public bool ConditionalTriggered { get; }
        public CombatTrace Trace { get; }
        public IReadOnlyList<CombatEvent> Events => Trace.Events;
        public IReadOnlyList<EnemyEffectResult> EnemyResults => Trace.EnemyResults;
        public IReadOnlyList<CombatEvent> HitEvents { get; }
        public IReadOnlyList<DamageResult> Hits { get; }
        internal CardResult(CardDefinition card,CombatTrace trace,string instanceId,int paid,string targetId,bool conditional)
        {
            Trace=trace; CardInstanceId=instanceId; SelectedTargetId=targetId;
            BaseCost=card.Cost; PaidCost=paid; ConditionalTriggered=conditional;
            HitEvents=Array.AsReadOnly(trace.Events.Where(e=>e.Hit!=null).ToArray());
            Hits=Array.AsReadOnly(HitEvents.Select(e=>e.Hit.Result).ToArray());
            Damage=Hits.Sum(h=>h.HPDamage); Blocked=Hits.Sum(h=>h.Blocked);
        }
    }

    public static class CardEffectResolver
    {
        // Low-level entry for rules tests and probes; no hand mutation or cost payment.
        public static CardResult Resolve(BattleRuntime battle,CardDefinition card,EnemyInstance target=null,StatusEffectType? cleanseStatus=null,string recoveryInstanceId=null)
        {
            var targets=battle.SelectCardTargets(card,target?.TargetId);
            if(targets==null||!battle.TryCleanseChoice(card,cleanseStatus,out cleanseStatus)||!battle.TryRecoveryChoice(card,recoveryInstanceId,out recoveryInstanceId))throw new InvalidOperationException("Choose valid targets before resolving this card.");
            return Resolve(battle,card,targets,battle.NewTrace(card.Id),null,0,cleanseStatus,recoveryInstanceId);
        }

        internal static CardResult Resolve(BattleRuntime battle,CardDefinition card,IReadOnlyList<EnemyInstance> targets,
            CombatTrace trace,string instanceId,int paid,StatusEffectType? cleanseStatus=null,string recoveryInstanceId=null)
        {
            bool shared=card.ElementApplication==ElementApplication.SharedAttack;
            bool conditional=false;
            var values=new EffectContext(battle,card,targets,cleanseStatus,recoveryInstanceId);
            var previous=targets.ToDictionary(e=>e.TargetId,e=>shared?e.ElementMark:e.Element);
            var effects=shared?card.Effects.OrderBy(e=>e.Type==EffectType.Damage?0:1):card.Effects.AsEnumerable();
            // Reaction-dependent effects run only after the common reaction engine has recorded its results.
            var deferred=effects.Where(e=>e.Condition==ConditionType.ReactionOccurred).ToArray();
            foreach(var effect in effects.Where(e=>e.Condition!=ConditionType.ReactionOccurred))ApplyEffect(effect);
            void ApplyEffect(CardEffect effect)
            {
                if(effect.TargetsEnemy)
                {
                    foreach(var enemy in EffectTargets(targets,effect.Targets))
                    {
                        if(!enemy.IsAlive||!values.Matches(effect,enemy))continue;
                        int amount=values.Amount(effect,enemy);
                        if(effect.Type==EffectType.ConditionalDamage)
                        {
                            if(effect.Condition<=ConditionType.Fire&&
                                !ElementRules.Matches(effect.Condition,battle.Block,enemy.Status.Poison,previous[enemy.TargetId]))continue;
                            conditional=true;
                            battle.HitEnemy(trace,enemy,amount,CombatCause.Conditional,effectId:card.Id);
                        }
                        else ApplyEnemyEffect(battle,trace,enemy,effect,amount,values);
                    }
                }
                else if(values.Matches(effect))ApplyPlayerEffect(battle,trace,effect,values.Amount(effect),values);
            }
            if(shared)
            {
                var directlyHit=new HashSet<string>(trace.Events.Where(e=>e.Kind==CombatEventKind.Damage&&e.Cause==CombatCause.DirectAttack)
                    .Select(e=>e.TargetId));
                ElementReactionResolver.Apply(battle,trace,card.Element,targets.Where(e=>directlyHit.Contains(e.TargetId)).ToArray(),previous);
                values.Reacted=trace.Events.Any(e=>e.Kind==CombatEventKind.Reaction);
                foreach(var effect in deferred)ApplyEffect(effect);
            }
            else if(card.ElementApplication==ElementApplication.Legacy&&card.Element!=ElementType.None&&
                card.Effects.Any(e=>e.Type==EffectType.Damage||e.Type==EffectType.Poison||e.Type==EffectType.Weak))
            {
                foreach(var enemy in targets)
                {
                    trace.Add(CombatEventKind.LegacyElement,CombatCause.Card,CombatTrace.PlayerId,enemy.TargetId,
                        oldElement:enemy.Element,newElement:card.Element);
                    enemy.Element=card.Element;
                }
            }
            battle.RecordSharedUse(trace,card,instanceId);
            trace.Complete(battle.Enemies);
            return new CardResult(card,trace,instanceId,paid,card.RequiresTargetSelection?targets[0].TargetId:null,conditional);
        }

        private static IEnumerable<EnemyInstance> EffectTargets(IReadOnlyList<EnemyInstance> targets,EffectTargetScope scope)
        {
            switch(scope)
            {
                case EffectTargetScope.Primary:return targets.Take(1);
                case EffectTargetScope.Secondary:return targets.Skip(1);
                default:return targets;
            }
        }
        private static void ApplyEnemyEffect(BattleRuntime battle,CombatTrace trace,EnemyInstance enemy,CardEffect effect,int amount,EffectContext values)
        {
            switch(effect.Type)
            {
                case EffectType.Damage:
                    for(int i=0;i<effect.Hits&&enemy.IsAlive;i++)
                    {
                        int fire=values.TakeFireBonus(enemy);
                        var hit=battle.HitEnemy(trace,enemy,amount+battle.Strength+fire,CombatCause.DirectAttack,
                            effectId:fire>0?values.FirePowerId:null,baseDamage:amount,strengthBonus:battle.Strength,cardBonus:fire,
                            ignoredBlock:effect.Rule.Penetration==ArmorPenetration.Full?enemy.Block:effect.Rule.Penetration==ArmorPenetration.Half?enemy.Block/2:0);
                        values.RecordDirectHit(enemy,hit);
                        values.ApplyNextPower(trace,enemy,hit.Index);
                        values.TriggerAttackBrand(trace,enemy,hit.Index);
                    }
                    break;
                case EffectType.Brand:
                    battle.ApplyEnemyStatus(trace,enemy,StatusEffectType.Brand,amount,CombatCause.Card);break;
                case EffectType.Poison:
                    battle.ApplyEnemyStatus(trace,enemy,StatusEffectType.Poison,amount,CombatCause.Card);break;
                case EffectType.Weak:
                    battle.ApplyEnemyStatus(trace,enemy,StatusEffectType.Weak,amount,CombatCause.Card);break;
                case EffectType.ClearWeak:
                    int weak=enemy.Status.Weak;
                    enemy.Status.Weak=0;
                    if(weak>0)trace.Add(CombatEventKind.Status,CombatCause.Card,CombatTrace.PlayerId,enemy.TargetId,weak,0,status:StatusEffectType.Weak);
                    break;
                case EffectType.ClearPoison:
                    int poison=enemy.Status.Poison,burn=enemy.Status.VenomBurn;
                    enemy.Status.Poison=0; enemy.Status.VenomBurn=0;
                    if(poison>0)trace.Add(CombatEventKind.Status,CombatCause.Card,CombatTrace.PlayerId,enemy.TargetId,poison,0,status:StatusEffectType.Poison);
                    if(burn>0)trace.Add(CombatEventKind.Status,CombatCause.Card,CombatTrace.PlayerId,enemy.TargetId,burn,0,status:StatusEffectType.VenomBurn);
                    break;
            }
        }

        private static void ApplyPlayerEffect(BattleRuntime battle,CombatTrace trace,CardEffect effect,int amount,EffectContext values)
        {
            const string player=CombatTrace.PlayerId;
            switch(effect.Type)
            {
                case EffectType.Block:
                    battle.ChangeBlock(trace,battle.Block+amount,CombatCause.BlockGain);break;
                case EffectType.SpendBlock:
                    values.LastSpent=Math.Min(battle.Block,amount);
                    battle.ChangeBlock(trace,battle.Block-values.LastSpent,CombatCause.BlockSpent,effectId:values.CardId);break;
                case EffectType.RestoreBlock:
                    battle.ChangeBlock(trace,battle.Block+amount,CombatCause.BlockRestored);break;
                case EffectType.RetainBlock:
                    battle.RetainBlock(amount);break;
                case EffectType.Counter:
                    if(battle.RegisterCounter(values.CardId,amount,effect.Rule.TurnLimit,effect.Rule.AttackerLimit,
                        !effect.Rule.Persistent,effect.Rule.DrawOnCounter))
                        trace.Add(CombatEventKind.Status,CombatCause.Card,player,player,0,1,effectId:values.CardId);
                    break;
                case EffectType.DrawOnFirstSpend:
                    battle.RegisterSpendDraw(trace,values.CardId,amount);break;
                case EffectType.RestoreOnFirstSpend:
                    battle.RegisterBlockRecycle(trace,values.CardId,effect.Rule);break;
                case EffectType.RecoverSpentBlock:
                    battle.RestoreSpentBlock(trace,amount,values.CardId);break;
                case EffectType.NextElementAllPower:
                    battle.RegisterAllElementPower(trace,values.CardId,amount);break;
                case EffectType.DiscountShared:
                    battle.DiscountNextSharedAttack(amount);
                    trace.Add(CombatEventKind.Status,CombatCause.Card,player,player,0,amount,effectId:values.CardId);break;
                case EffectType.DrawOnFirstShared:
                    battle.RegisterSharedDraw(trace,values.CardId,amount);break;
                case EffectType.RecoverShared:
                    battle.RecoverShared(trace,values.RecoveryInstanceId);break;
                case EffectType.DamageOnFirstReaction:
                    battle.RegisterFirstReaction(trace,values.CardId,amount);break;
                case EffectType.FirstFirePower:
                    battle.RegisterFirePower(trace,values.CardId,amount);break;
                case EffectType.NextElementPower:
                    battle.RegisterNextElementPower(trace,values.CardId,amount);break;
                case EffectType.CleansePlayer:
                    battle.CleansePlayer(trace,values.Cleanse);break;
                case EffectType.ReactionSplash:
                    var original=values.Primary;
                    var other=battle.Enemies.FirstOrDefault(e=>e.IsAlive&&e!=original);
                    var splashTarget=other??(original!=null&&original.IsAlive?original:null);
                    var reaction=trace.Events.FirstOrDefault(e=>e.Kind==CombatEventKind.Reaction);
                    if(splashTarget!=null)battle.HitEnemy(trace,splashTarget,other!=null?amount:effect.Rule.FallbackAmount,
                        CombatCause.Conditional,reaction?.Index,effectId:values.CardId);
                    break;
                case EffectType.BlockOnFirstHeal:
                    battle.RegisterHealBlock(trace,values.CardId,amount);break;
                case EffectType.BlockAtTurnStart:
                    battle.RegisterTurnBlock(trace,values.CardId,amount);break;
                case EffectType.Heal:
                    battle.HealPlayer(trace,amount);
                    break;
                case EffectType.Draw:
                    values.RecordDraw(battle.DrawCards(trace,amount));
                    break;
                case EffectType.Strength:
                    int strength=battle.Strength;
                    battle.Strength+=amount;
                    trace.Add(CombatEventKind.Status,CombatCause.Card,player,player,strength,battle.Strength,status:StatusEffectType.Strength);
                    break;
                case EffectType.Energy:
                    battle.ChangeEnergy(trace,Math.Min(battle.Data.Config.EnergyCap,battle.Energy+amount),
                        effect.Condition==ConditionType.DirectKillThisCard?CombatCause.KillRefund:CombatCause.Card,
                        parent:effect.Condition==ConditionType.DirectKillThisCard?values.DirectKill?.Index:null,effectId:values.CardId);break;
                case EffectType.Exhaust:break; // DeckRuntime moved the card before resolving effects.
                default:throw new ArgumentOutOfRangeException(nameof(effect));
            }
        }
        private sealed class EffectContext
        {
            private readonly BattleRuntime battle;
            private readonly int blockBefore,spentBefore;
            private readonly int strengthBefore;
            private readonly bool priestAttack;
            private readonly Dictionary<string,EnemyCombatSnapshot> before;
            private readonly EnemyInstance primary;
            private readonly CardDefinition card;
            private readonly int enemyCountBefore,fireBonus,nextBonus,allBonus;
            private readonly string nextBonusId,allBonusId;
            private readonly HashSet<string> allBonusTargets=new HashSet<string>();
            private readonly bool reactedBefore;
            private bool drewShared;
            private bool nextApplied;
            private readonly HashSet<string> fireTargets=new HashSet<string>(),directTargets=new HashSet<string>();
            public EnemyInstance Primary=>primary;
            public string FirePowerId=>battle.FirstFirePowerId;
            public StatusEffectType? Cleanse { get; }
            public string RecoveryInstanceId { get; }
            public bool Reacted { get; set; }
            public CombatEvent DirectKill { get; set; }
            private readonly HashSet<string> attackBrands=new HashSet<string>();
            public string CardId { get; }
            public int LastSpent { get; set; }
            public EffectContext(BattleRuntime battle,CardDefinition card,IReadOnlyList<EnemyInstance> targets,StatusEffectType? cleanse,string recoveryInstanceId)
            {
                this.battle=battle;this.card=card;CardId=card.Id;Cleanse=cleanse;RecoveryInstanceId=recoveryInstanceId;reactedBefore=battle.ReactedThisTurn;
                enemyCountBefore=battle.Enemies.Count(e=>e.IsAlive);
                fireBonus=battle.TakeFirstFirePower(card);
                (allBonus,allBonusId)=battle.TakeAllElementPower(card);
                (nextBonus,nextBonusId)=battle.TakeNextElementPower(card);blockBefore=battle.Block;spentBefore=battle.BlockSpentThisTurn;
                strengthBefore=battle.Strength;primary=targets.FirstOrDefault();
                before=targets.ToDictionary(e=>e.TargetId,e=>new EnemyCombatSnapshot(e));
                priestAttack=card.Owner==CharacterId.Priest&&card.Element==ElementType.None&&
                    card.ElementApplication==ElementApplication.None&&card.Type==CardType.Attack;
            }
            private EnemyCombatSnapshot Before(EnemyInstance target) =>
                (target??primary) is EnemyInstance enemy?before[enemy.TargetId]:null;
            public bool Matches(CardEffect effect,EnemyInstance target=null) => effect.Condition switch
            {
                ConditionType.BlockBefore=>blockBefore>0,
                ConditionType.NoBlockBefore=>blockBefore==0,
                ConditionType.CurrentBlockAtLeast=>battle.Block>=effect.Rule.Threshold,
                ConditionType.SpentBlockThisTurn=>battle.BlockSpentThisTurn>0,
                ConditionType.CounteredLastEnemyTurn=>battle.CounteredLastEnemyTurn,
                ConditionType.StrengthBefore=>strengthBefore>0,
                ConditionType.WeakBefore=>Before(target)?.Weak>0,
                ConditionType.BrandBefore=>Before(target)?.Brand>0,
                ConditionType.MarkBefore=>Before(target)!=null&&Before(target).Mark!=ElementType.None,
                ConditionType.OtherMarkBefore=>Before(target)!=null&&Before(target).Mark!=ElementType.None&&Before(target).Mark!=card.Element,
                ConditionType.ReactionOccurred=>Reacted,
                ConditionType.OnlyEnemyBefore=>enemyCountBefore==1,
                ConditionType.NoSecondaryBefore=>before.Count==1,
                ConditionType.HPHalfBefore=>Before(target)!=null&&(long)Before(target).HP*2<=Before(target).MaxHP,
                ConditionType.DirectKillThisCard=>DirectKill!=null,
                ConditionType.AnyMarkedEnemy=>battle.Enemies.Any(e=>e.IsAlive&&e.ElementMark!=ElementType.None),
                ConditionType.DrewSharedCard=>drewShared,
                ConditionType.ReactedThisTurnBefore=>reactedBefore,
                ConditionType.MultipleEnemiesBefore=>enemyCountBefore>=2,
                _=>true
            };
            public int Amount(CardEffect effect,EnemyInstance target=null)
            {
                int basis=effect.Rule.Source switch
                {
                    EffectValueSource.CurrentBlock=>battle.Block,
                    EffectValueSource.LastBlockSpent=>LastSpent,
                    EffectValueSource.BlockSpentBeforeCard=>spentBefore,
                    EffectValueSource.WeakBefore=>Before(target)?.Weak??0,
                    EffectValueSource.AttackBrandsTriggered=>attackBrands.Count,
                    EffectValueSource.DirectTargetsHit=>directTargets.Count,
                    EffectValueSource.OtherEnemiesBefore=>Math.Max(0,enemyCountBefore-1),
                    _=>0
                };
                return effect.Amount+effect.Rule.Scale(basis);
            }
            public void RecordDirectHit(EnemyInstance enemy,CombatEvent hit)
            {directTargets.Add(enemy.TargetId);if(hit.Hit.Killed)DirectKill=hit;}
            public void RecordDraw(IReadOnlyList<CardInstance> drawn)
            {drewShared=drawn.Any(c=>battle.Data.Card(c.CardId).ElementApplication==ElementApplication.SharedAttack);}
            public int TakeFireBonus(EnemyInstance enemy)=>fireTargets.Add(enemy.TargetId)?fireBonus:0;
            public void ApplyNextPower(CombatTrace trace,EnemyInstance enemy,int parent)
            {
                if(allBonus>0&&allBonusTargets.Add(enemy.TargetId)&&enemy.IsAlive)
                    battle.HitEnemy(trace,enemy,allBonus,CombatCause.Enhancement,parent,effectId:allBonusId);
                if(nextApplied||nextBonus==0)return;
                // The first directly hit target owns the reservation, even if the direct hit kills it.
                nextApplied=true;
                if(enemy.IsAlive)battle.HitEnemy(trace,enemy,nextBonus,CombatCause.Enhancement,parent,effectId:nextBonusId);
            }
            public void TriggerAttackBrand(CombatTrace trace,EnemyInstance enemy,int parent)
            {
                if(!priestAttack||attackBrands.Contains(enemy.TargetId))return;
                if(battle.TriggerBrand(trace,enemy,parent))attackBrands.Add(enemy.TargetId);
            }
        }
    }
}
