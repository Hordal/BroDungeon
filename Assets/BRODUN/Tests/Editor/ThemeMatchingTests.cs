using System;
using System.Linq;
using Brodun.Flow;
using NUnit.Framework;

namespace Brodun.Tests
{
    public sealed class ThemeMatchingTests : RulesTest
    {
        [Test]
        public void ExpansionIsIdempotentAndLeavesLegacyDefinitionsUnchanged()
        {
            var expanded=DungeonThemes.WithThemeEnemies(Data);
            Assert.AreEqual(12,Data.Enemies.Count);
            Assert.AreEqual(69,expanded.Enemies.Count);
            Assert.AreEqual(69,DungeonThemes.WithThemeEnemies(expanded).Enemies.Count);
            foreach(var enemy in Data.Enemies)Assert.AreSame(enemy,expanded.Enemy(enemy.Id));
            Assert.AreEqual(57,ThemeEnemyCatalog.Entries.Select(e=>e.Id).Distinct().Count());
        }

        [TestCase("ruins",2,1,1,"fallen_colossus")]
        [TestCase("fire",15,3,1,"fire_crowned_dragon")]
        [TestCase("frost",15,3,1,"frost_funeral_queen")]
        [TestCase("void",15,3,1,"void_eclipse_prellate")]
        public void PoolsContainOnlyTheirThemeAndExpectedBoss(string theme,int normal,int elite,int boss,string bossId)
        {
            var data=DungeonThemes.WithThemeEnemies(Data);
            Assert.IsTrue(DungeonThemes.IsAvailable(data,theme));
            Assert.AreEqual(normal,EncounterPool.ForTheme(data,theme,EnemyType.Normal).Count);
            Assert.AreEqual(elite,EncounterPool.ForTheme(data,theme,EnemyType.Elite).Count);
            Assert.AreEqual(boss,EncounterPool.ForTheme(data,theme,EnemyType.Boss).Count);
            Assert.AreEqual(bossId,EncounterPool.ForTheme(data,theme,EnemyType.Boss).Single().Id);
            for(uint seed=0;seed<128;seed++)
            {
                var run=new RunState(data,CharacterId.Warrior,seed,dungeon:LayeredMapGenerator.Generate(seed),firstTheme:theme);
                foreach(var node in run.Map.Where(n=>n.Type==RoomType.Battle||n.Type==RoomType.Boss))
                {
                    var enemy=EncounterPool.Select(data,run,node);
                    Assert.Contains(enemy.Id,DungeonThemes.Find(theme).EnemyIds.ToArray());
                    Assert.AreEqual(node.Type==RoomType.Boss?EnemyType.Boss:node.Encounter,enemy.Type);
                    Assert.AreEqual(enemy.Id,EncounterPool.Select(data,run,node).Id);
                }
            }
        }

        [Test]
        public void StartingOtherThemesDoesNotApplyLaterAreaHealthScaling()
        {
            var data=DungeonThemes.WithThemeEnemies(Data);
            foreach(var entry in ThemeEnemyCatalog.Entries)
            {
                var enemy=data.Enemy(entry.Id);
                Assert.AreEqual(1,enemy.Area);
                Assert.AreEqual(0,enemy.AreaHPStep);
                Assert.AreEqual(Data.Enemies.First(e=>e.Area==1&&e.Type==entry.Rank&&!e.IsLegacy).BaseHP,enemy.BaseHP);
            }
        }

        [Test]
        public void SelectionIsValidatedLockedDuringRunAndResetForNewRun()
        {
            var flow=new FlowSession(DungeonThemes.WithThemeEnemies(Data),chooseTheme:true);
            Assert.IsFalse(flow.ChooseTheme("fire"));
            flow.FinishTransition(FlowScreen.Title);flow.StartNewRun(17);
            Assert.IsFalse(flow.ChooseTheme("fire"));
            flow.FinishTransition(FlowScreen.CharacterSelect);
            Assert.IsFalse(flow.ChooseTheme("void"));
            Assert.IsTrue(flow.ChooseCharacter(CharacterId.Warrior));
            flow.FinishTransition(FlowScreen.ThemeSelect);
            Assert.IsFalse(flow.ChooseTheme("missing"));
            Assert.IsTrue(flow.ChooseTheme("void"));
            Assert.AreEqual("void",flow.Run.ThemeId);
            flow.FinishTransition(FlowScreen.DungeonMap);
            Assert.IsFalse(flow.ChooseTheme("fire"));
            Assert.IsTrue(flow.SelectRoom(DungeonRuntime.Available(flow.Run).First()));Assert.IsTrue(flow.BeginBattle());
            Assert.AreEqual("void",flow.Run.ThemeId);
            Assert.IsTrue(flow.Battle.Runtime.Enemy.Definition.Id.StartsWith("void_",StringComparison.Ordinal));
            flow.FinishTransition(FlowScreen.Battle);
            var run=flow.Run;flow.RestorePendingRoom();Assert.AreSame(run,flow.Run);
            Assert.AreEqual("void",flow.Run.ThemeId);
            Assert.IsTrue(flow.ReturnToTitle());flow.FinishTransition(FlowScreen.Title);
            Assert.IsTrue(flow.StartNewRun(29));Assert.IsNull(flow.Run);
        }

        [Test]
        public void IncompleteOrUnknownThemeCannotStart()
        {
            var flow=new FlowSession(Data,chooseTheme:true);
            flow.FinishTransition(FlowScreen.Title);flow.StartNewRun(17);
            flow.FinishTransition(FlowScreen.CharacterSelect);
            Assert.IsTrue(flow.ChooseCharacter(CharacterId.Warrior));
            flow.FinishTransition(FlowScreen.ThemeSelect);
            Assert.IsFalse(flow.ChooseTheme("fire"));
            Assert.IsFalse(flow.ChooseTheme("missing"));
            Assert.IsTrue(flow.ChooseTheme("ruins"));
        }
        [TestCase("ruins")][TestCase("fire")][TestCase("frost")][TestCase("void")]
        public void ThemeSurvivesNormalRewardAndBossVictory(string theme)
        {
            var data=DungeonThemes.WithThemeEnemies(Data);
            var nodes=new[]
            {
                new RoomNode("start",RoomType.Start,0,50,new[]{"fight"},EventKind.None),
                new RoomNode("fight",RoomType.Battle,1,50,new[]{"boss"},EventKind.None),
                new RoomNode("boss",RoomType.Boss,2,50,Array.Empty<string>(),EventKind.None)
            };
            var flow=new FlowSession(data,_=>new GeneratedDungeon(null,Array.AsReadOnly(nodes)),chooseTheme:true);
            flow.FinishTransition(FlowScreen.Title);flow.StartNewRun(17);flow.FinishTransition(FlowScreen.CharacterSelect);
            Assert.IsTrue(flow.ChooseCharacter(CharacterId.Warrior));flow.FinishTransition(FlowScreen.ThemeSelect);
            Assert.IsTrue(flow.ChooseTheme(theme));flow.FinishTransition(FlowScreen.DungeonMap);
            foreach(var node in new[]{"fight","boss"})
            {
                Assert.IsTrue(flow.SelectRoom(node));Assert.IsTrue(flow.BeginBattle());flow.FinishTransition(FlowScreen.Battle);
                var battle=flow.Battle;Assert.Contains(battle.Runtime.Enemy.Definition.Id,DungeonThemes.Find(theme).EnemyIds.ToArray());
                // Focus this test on lifecycle routing; combat damage formulas are covered by existing regression tests.
                battle.Runtime.Enemy.CurrentHP=1;
                var attack=battle.Runtime.Deck.Hand.First(c=>data.Card(c.CardId).BaseDamage>0);
                Assert.IsTrue(battle.Play(attack.InstanceId));battle.FinishActionPresentation();
                if(node=="fight")
                {
                    Assert.IsTrue(battle.OpenReward());
                    Assert.IsTrue(battle.ResolveReward(RewardType.Gold));
                    Assert.IsTrue(flow.ReturnFromBattle());flow.FinishTransition(FlowScreen.DungeonMap);
                    Assert.AreEqual(theme,flow.Run.ThemeId);
                }
                else
                {
                    Assert.IsTrue(flow.FinishRun());Assert.AreEqual(theme,flow.Run.ThemeId);
                    Assert.IsTrue(flow.Run.CanAdvanceArea);
                    flow.FinishTransition(FlowScreen.ThemeSelect);
                    Assert.IsFalse(flow.ChooseTheme(theme));
                }
            }
        }
    }
}
