using System;
using System.Linq;
using System.Collections.Generic;
namespace Brodun
{
    public sealed class EnemyInstance
    {
        public EnemyDefinition Definition { get; }
        public int CurrentHP { get; internal set; }
        public int MaxHP { get; }
        public int Block { get; internal set; }
        public StatusState Status { get; } = new StatusState();
        public ElementType Element { get; internal set; } = ElementType.None;
        public int PatternIndex { get; internal set; }
        public IntentData CurrentIntent { get; internal set; }
        public EnemyInstance(EnemyDefinition definition,RoomNode room,BattleConfig config)
        {
            Definition=definition;
            CurrentHP=MaxHP=definition.BaseHP+(definition.Area-1)*definition.AreaHPStep+
                (definition.Type==EnemyType.Boss?0:(room.Floor-1)*config.FloorHPStep);
        }
    }
    public sealed class EnemyActionResult
    {
        public IntentData Intent { get; }
        public int Blocked { get; }
        public int HPDamage { get; }
        public IReadOnlyList<DamageResult> Hits { get; }
        public EnemyActionResult(IntentData intent,int blocked,int damage,IEnumerable<DamageResult> hits=null)
        { Intent=intent; Blocked=blocked; HPDamage=damage; Hits=Array.AsReadOnly((hits??Enumerable.Empty<DamageResult>()).ToArray()); }
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
            return new IntentData(intent.Type,amount,intent.Hits,intent.PatternIndex);
        }
        // Execute the SAME immutable snapshot supplied to presentation; never choose another action here.
        public static EnemyActionResult Execute(RunState run,BattleRuntime battle,IntentData snapshot)
        {
            var enemy=battle.Enemy; enemy.Block=0;
            int blocked=0,damage=0;
            var hits=new List<DamageResult>();
            if(snapshot.Type==IntentType.Attack)
            {
                int hp=run.CurrentHP,block=battle.Block;
                for(int i=0;i<snapshot.Hits;i++)
                { var result=DamageCalculator.Hit(snapshot.Amount,ref hp,ref block); blocked+=result.Blocked; damage+=result.HPDamage; hits.Add(result); }
                run.CurrentHP=hp; battle.Block=block;
            }
            else if(snapshot.Type==IntentType.Block) enemy.Block=snapshot.Amount;
            else if(snapshot.Type==IntentType.Buff) enemy.Status.AddStrength(snapshot.Amount);
            else throw new ArgumentOutOfRangeException(nameof(snapshot), "Unknown intent type.");
            enemy.Status.AfterEnemyAction(); enemy.PatternIndex++;
            return new EnemyActionResult(snapshot,blocked,damage,hits);
        }
    }
}
