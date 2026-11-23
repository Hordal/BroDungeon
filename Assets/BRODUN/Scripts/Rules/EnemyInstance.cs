using System;
using System.Linq;
using System.Collections.Generic;
namespace Brodun
{
    public sealed class EnemyInstance
    {
        public EnemyDefinition Definition { get; }
        public string TargetId { get; }
        public bool IsAlive => CurrentHP>0;
        public int CurrentHP { get; internal set; }
        public int MaxHP { get; }
        public int Block { get; internal set; }
        public StatusState Status { get; } = new StatusState();
        public ElementType Element { get; internal set; } = ElementType.None;
        public ElementType ElementMark { get; internal set; } = ElementType.None;
        internal int ReactionTurn,ReactionsThisTurn;
        public int PatternIndex { get; internal set; }
        public IntentData CurrentIntent { get; internal set; }
        public EnemyInstance(EnemyDefinition definition,RoomNode room,BattleConfig config,string targetId="enemy-0")
        {
            Definition=definition;
            TargetId=targetId;
            CurrentHP=MaxHP=definition.BaseHP+(definition.Area-1)*definition.AreaHPStep+
                (definition.Type==EnemyType.Boss?0:(room.Floor-1)*config.FloorHPStep);
        }
    }
    public sealed class EnemyActionResult
    {
        public EnemyInstance Actor { get; }
        public IntentData Intent { get; }
        public int Blocked { get; }
        public int HPDamage { get; }
        public IReadOnlyList<DamageResult> Hits { get; }
        public EnemyActionResult(IntentData intent,int blocked,int damage,IEnumerable<DamageResult> hits=null,EnemyInstance actor=null)
        { Actor=actor; Intent=intent; Blocked=blocked; HPDamage=damage; Hits=Array.AsReadOnly((hits??Enumerable.Empty<DamageResult>()).ToArray()); }
    }
    public static class IntentResolver
    {
        public static EnemyDefinition Select(GameDefinitions data,RunState run,RoomNode room)
        {
            return EncounterPool.Select(data,run,room);
        }
        public static IntentData Decide(EnemyInstance enemy)
        {
            var pattern=enemy.Definition.Pattern[enemy.PatternIndex%enemy.Definition.Pattern.Count];
            return enemy.CurrentIntent=new IntentData(pattern.Type,pattern.Amount,pattern.Hits,enemy.PatternIndex);
        }
        public static IntentData Preview(EnemyInstance enemy,BattleConfig config)
        {
            var intent=enemy.CurrentIntent;
            if(intent==null)return null;
            int amount=intent.Type==IntentType.Attack
                ?DamageCalculator.AttackAmount(intent.Amount,enemy.Status.Strength,enemy.Status.Weak,config.WeakReduction):intent.Amount;
            return new IntentData(intent.Type,amount,intent.Hits,intent.PatternIndex,
                intent.Type==IntentType.Attack?enemy.Status.RapidFreeze:0);
        }
        // Execute the SAME immutable snapshot supplied to presentation; never choose another action here.
        public static EnemyActionResult Execute(RunState run,BattleRuntime battle,IntentData snapshot,EnemyInstance actor=null)
            => Execute(run,battle,snapshot,actor,battle.NewTrace());

        internal static EnemyActionResult Execute(RunState run,BattleRuntime battle,IntentData snapshot,EnemyInstance actor,CombatTrace trace)
        {
            var enemy=actor??battle.Enemy;
            int action=trace.Add(CombatEventKind.EnemyAction,CombatCause.EnemyAction,enemy.TargetId,enemy.TargetId,
                effectId:snapshot.Type.ToString()).Index;
            if(enemy.Block>0)trace.Add(CombatEventKind.Block,CombatCause.BlockReset,enemy.TargetId,enemy.TargetId,enemy.Block,0,parent:action);
            enemy.Block=0;
            int blocked=0,damage=0;
            var hits=new List<DamageResult>();
            if(snapshot.Type==IntentType.Attack)
            {
                for(int i=0;i<snapshot.Hits;i++)
                {
                    int hp=run.CurrentHP,block=battle.Block,amount=snapshot.HitAmount(i);
                    var result=DamageCalculator.Hit(amount,ref hp,ref block);
                    var hit=new CombatHit(amount,run.CurrentHP,hp,battle.Block,block);
                    var entry=trace.Add(CombatEventKind.Damage,CombatCause.EnemyAction,enemy.TargetId,CombatTrace.PlayerId,hit:hit,parent:action);
                    run.CurrentHP=hp;
                    if(result.Blocked>0)battle.ChangeBlock(trace,block,CombatCause.BlockAbsorbed,enemy.TargetId,entry.Index);
                    blocked+=result.Blocked; damage+=result.HPDamage; hits.Add(result);
                }
                if(enemy.Status.RapidFreeze>0)
                {
                    trace.Add(CombatEventKind.Status,CombatCause.EnemyAction,enemy.TargetId,enemy.TargetId,
                        enemy.Status.RapidFreeze,0,status:StatusEffectType.RapidFreeze,parent:action);
                    enemy.Status.RapidFreeze=0;
                }
                if(blocked>0)battle.CounterAttack(trace,enemy,action);
            }
            else if(snapshot.Type==IntentType.Block)
            {
                enemy.Block=snapshot.Amount;
                trace.Add(CombatEventKind.Block,CombatCause.BlockGain,enemy.TargetId,enemy.TargetId,0,enemy.Block,parent:action);
            }
            else if(snapshot.Type==IntentType.Buff)
            {
                int previous=enemy.Status.Strength;
                enemy.Status.AddStrength(snapshot.Amount);
                trace.Add(CombatEventKind.Status,CombatCause.EnemyAction,enemy.TargetId,enemy.TargetId,
                    previous,enemy.Status.Strength,status:StatusEffectType.Strength,parent:action);
            }
            else throw new ArgumentOutOfRangeException(nameof(snapshot), "Unknown intent type.");
            int weak=enemy.Status.Weak;
            enemy.Status.AfterEnemyAction(); enemy.PatternIndex++;
            if(weak>0)trace.Add(CombatEventKind.Status,CombatCause.EnemyAction,enemy.TargetId,enemy.TargetId,weak,enemy.Status.Weak,
                status:StatusEffectType.Weak,parent:action);
            return new EnemyActionResult(snapshot,blocked,damage,hits,enemy);
        }
    }
}
