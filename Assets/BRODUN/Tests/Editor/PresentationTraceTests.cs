using System.Linq;
using Brodun.Flow;
using NUnit.Framework;
namespace Brodun.Tests
{
    public sealed class PresentationTraceTests:RulesTest
    {
        private BattleRuntime Battle()
        {
            var f=new FlowSession(Data,s=>LayeredMapGenerator.Generate(s));f.FinishTransition(FlowScreen.Title);
            f.StartNewRun(7);f.FinishTransition(FlowScreen.CharacterSelect);f.ChooseCharacter(CharacterId.Warrior);
            f.FinishTransition(FlowScreen.DungeonMap);f.SelectRoom("r2");f.BeginBattle();return f.Battle.Runtime;
        }
        [Test] public void MultiHitTraceContainsTheActualCalculatedResults()
        {
            var b=Battle();b.Enemy.Block=4;int startingHP=b.Enemy.CurrentHP;
            var result=CardEffectResolver.Resolve(b,Data.Card("lightning_chain"));
            Assert.AreEqual(2,result.Hits.Count);Assert.AreEqual(4,result.Hits.Sum(x=>x.Blocked));
            Assert.AreEqual(result.Damage,result.Hits.Sum(x=>x.HPDamage));
            Assert.AreEqual(2,result.Damage);Assert.AreEqual(startingHP-2,b.Enemy.CurrentHP);
        }
        [Test] public void EnemyMultiHitTraceConsumesBlockOnceAndMatchesTotals()
        {
            var b=Battle();b.Block=3;var intent=new IntentData(IntentType.Attack,5,2);
            var action=IntentResolver.Execute(b.Run,b,intent);
            Assert.AreEqual(2,action.Hits.Count);Assert.AreEqual(2,action.Hits[0].HPDamage);
            Assert.AreEqual(5,action.Hits[1].HPDamage);Assert.AreEqual(7,action.HPDamage);
            Assert.AreEqual(53,b.Run.CurrentHP);Assert.AreEqual(0,b.Block);
        }
        [Test] public void NonAttackIntentHasNoInventedDamageHits()
        {
            var b=Battle();var action=IntentResolver.Execute(b.Run,b,new IntentData(IntentType.Block,5));
            Assert.IsEmpty(action.Hits);Assert.AreEqual(60,b.Run.CurrentHP);Assert.AreEqual(5,b.Enemy.Block);
        }
    }
}
