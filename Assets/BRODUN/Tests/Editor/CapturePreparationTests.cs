using System.Linq;
using Brodun.Editor;
using Brodun.Flow;
using NUnit.Framework;
namespace Brodun.Tests
{
    public sealed class CapturePreparationTests:RulesTest
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
            CollectionAssert.AreEqual(Data.Cards,preview.Cards);CollectionAssert.AreEqual(Data.Characters,preview.Characters);
            foreach(var enemy in Data.Enemies.Where(e=>e!=original))Assert.AreSame(enemy,preview.Enemy(enemy.Id));
        }
        [Test] public void CaptureScriptWinsOnTurnTwoAfterOneRealEnemyAttackAndReward()
        {
            var f=new FlowSession(CapturePreparation.PreviewDefinitions(Data),s=>LayeredMapGenerator.Generate(s));
            f.FinishTransition(FlowScreen.Title);f.StartNewRun(CapturePreparation.Seed);
            f.FinishTransition(FlowScreen.CharacterSelect);f.ChooseCharacter(CharacterId.Warrior);
            f.FinishTransition(FlowScreen.DungeonMap);Assert.IsTrue(f.SelectRoom("r2"));Assert.IsTrue(f.BeginBattle());
            f.FinishTransition(FlowScreen.Battle);var c=f.Battle;Assert.AreEqual(14,c.Runtime.Enemy.MaxHP);
            foreach(string id in new[]{"earth_strike","wind_cut"})Play(c,id);
            Assert.AreEqual(5,c.Runtime.Enemy.CurrentHP);Assert.AreEqual(1,c.Runtime.Energy);
            c.EndTurn();c.FinishActionPresentation();Assert.AreEqual(54,c.Run.CurrentHP);
            Assert.AreEqual(6,c.Runtime.LastEnemyAction.Intent.Amount);Assert.AreEqual(8,c.Runtime.CurrentIntent.Amount);
            Play(c,"earth_strike");Assert.AreEqual(BattlePhase.Victory,c.Phase);
            Assert.IsFalse(c.Run.CompletedNodes.Contains("r2"));c.OpenReward();Assert.IsTrue(c.ResolveReward(RewardType.Gold));
            Assert.IsFalse(c.ResolveReward(RewardType.Gold));Assert.AreEqual(50,c.Run.Gold);
            Assert.IsTrue(f.ReturnFromBattle());f.FinishTransition(FlowScreen.DungeonMap);
            Assert.IsTrue(f.Run.CompletedNodes.Contains("r2"));Assert.IsNotEmpty(DungeonRuntime.Available(f.Run));
        }
        private static void Play(BattleContext c,string id)
        {Assert.IsTrue(c.Play(c.Runtime.Deck.Hand.Single(x=>x.CardId==id).InstanceId));c.FinishActionPresentation();}
    }
}
