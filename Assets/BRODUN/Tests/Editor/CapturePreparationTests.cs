using System.Linq;
using Brodun.Editor;
using Brodun.Flow;
using NUnit.Framework;
namespace Brodun.Tests
{
    public sealed class CapturePreparationTests:LegacyRulesTest
    {
        [Test] public void PreviewIsSeparateAndLeavesAllOriginalRulesUntouched()
        {
            var original=Data.Enemies.Single(e=>e.Area==1&&e.Type==EnemyType.Normal&&!e.IsLegacy);
            int hp=original.BaseHP;
            var preview=CapturePreparation.PreviewDefinitions(Data);
            Assert.AreNotSame(Data,preview);Assert.AreEqual(hp,original.BaseHP);
            Assert.AreEqual(14,preview.Enemy(original.Id).BaseHP);
            Assert.AreSame(original.Pattern[0],preview.Enemy(original.Id).Pattern[0]);
            Assert.AreSame(Data.Config,preview.Config);Assert.AreSame(Data.Reward,preview.Reward);
            CollectionAssert.AreEqual(Data.AllCards,preview.AllCards);CollectionAssert.AreEqual(Data.Characters,preview.Characters);
            foreach(var enemy in Data.Enemies.Where(e=>e!=original))Assert.AreSame(enemy,preview.Enemy(enemy.Id));
        }
        [Test] public void CaptureScriptWinsOnTurnTwoAfterOneRealEnemyAttackAndReward()
        {
            var f=new FlowSession(CapturePreparation.PreviewDefinitions(Data),s=>LayeredMapGenerator.Generate(s));
            f.FinishTransition(FlowScreen.Title);f.StartNewRun(CapturePreparation.Seed);
            f.FinishTransition(FlowScreen.CharacterSelect);f.ChooseCharacter(CharacterId.Warrior);
            f.FinishTransition(FlowScreen.DungeonMap);Assert.IsTrue(f.SelectRoom("r2"));Assert.IsTrue(f.BeginBattle());
            f.FinishTransition(FlowScreen.Battle);var c=f.Battle;Assert.AreEqual(14,c.Runtime.Enemy.MaxHP);
            var attacks=c.Runtime.Deck.Hand.Where(x=>Data.Card(x.CardId).Type==CardType.Attack).Take(2).ToArray();
            Assert.AreEqual(2,attacks.Length);
            foreach(var attack in attacks)Play(c,attack.InstanceId);
            Assert.AreEqual(14-attacks.Sum(x=>Data.Card(x.CardId).BaseDamage),c.Runtime.Enemy.CurrentHP);
            Assert.AreEqual(3,c.Runtime.Energy);
            c.EndTurn();c.FinishActionPresentation();Assert.AreEqual(51,c.Run.CurrentHP);
            Assert.AreEqual(9,c.Runtime.LastEnemyAction.Intent.Amount);Assert.AreEqual(11,c.Runtime.CurrentIntent.Amount);
            Play(c,c.Runtime.Deck.Hand.First(x=>Data.Card(x.CardId).BaseDamage>=c.Runtime.Enemy.CurrentHP).InstanceId);Assert.AreEqual(BattlePhase.Victory,c.Phase);
            Assert.IsFalse(c.Run.CompletedNodes.Contains("r2"));c.OpenReward();Assert.IsTrue(c.ResolveReward(RewardType.Gold));
            Assert.IsFalse(c.ResolveReward(RewardType.Gold));Assert.AreEqual(50,c.Run.Gold);
            Assert.IsTrue(f.ReturnFromBattle());f.FinishTransition(FlowScreen.DungeonMap);
            Assert.IsTrue(f.Run.CompletedNodes.Contains("r2"));Assert.IsNotEmpty(DungeonRuntime.Available(f.Run));
        }
        private static void Play(BattleContext c,string instanceId)
        {Assert.IsTrue(c.Play(instanceId));c.FinishActionPresentation();}
    }
}
