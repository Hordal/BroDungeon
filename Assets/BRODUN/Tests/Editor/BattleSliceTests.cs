using System.Linq;
using Brodun.Flow;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Brodun.Presentation;
namespace Brodun.Tests
{
    public sealed class BattleSliceTests : RulesTest
    {
        private FlowSession Map(uint seed=17)
        {var f=new FlowSession(Data,s=>LayeredMapGenerator.Generate(s));f.FinishTransition(FlowScreen.Title);f.StartNewRun(seed);f.FinishTransition(FlowScreen.CharacterSelect);f.ChooseCharacter(CharacterId.Warrior);f.FinishTransition(FlowScreen.DungeonMap);return f;}
        private BattleContext Start(EnemyType rank=EnemyType.Normal)
        {
            var f=Map();var node=f.Run.Map.First(n=>(n.Type==RoomType.Battle||n.Type==RoomType.Boss)&&n.Encounter==rank);
            if(rank!=EnemyType.Normal)f.Run.CurrentNode=node.Previous[0];
            Assert.IsTrue(f.SelectRoom(node.Id));Assert.IsTrue(f.BeginBattle());f.FinishTransition(FlowScreen.Battle);return f.Battle;
        }
        [TestCase(EnemyType.Normal)][TestCase(EnemyType.Elite)][TestCase(EnemyType.Boss)]
        public void EncounterIdentityHpAndPatternAreReproducible(EnemyType rank)
        {var a=Start(rank);var b=Start(rank);Assert.AreEqual(rank,a.Runtime.Enemy.Definition.Type);Assert.AreEqual(a.Runtime.Enemy.Definition.Id,b.Runtime.Enemy.Definition.Id);Assert.AreEqual(a.Runtime.Enemy.MaxHP,b.Runtime.Enemy.MaxHP);Assert.AreEqual(a.Runtime.CurrentIntent.Amount,b.Runtime.CurrentIntent.Amount);}
        [Test] public void ContextRetainsRunDeckRoomSeedAndInitialHand()
        {var c=Start();Assert.AreSame(c.Run,c.Runtime.Run);Assert.AreEqual(c.Room.Id,c.Run.CurrentNode);Assert.AreEqual(17u,c.Seed);Assert.AreEqual(4,c.Runtime.Deck.Hand.Count);Assert.AreEqual(3,c.Runtime.Energy);Assert.IsNull(c.Runtime.Reward);}
        [Test] public void RepeatedCardAndTurnInputsAreBlockedUntilPresentationCompletes()
        {
            var c=Start();var card=c.Runtime.Deck.Hand.Single(x=>x.CardId=="earth_strike");int before=c.Runtime.Enemy.CurrentHP;
            Assert.IsTrue(c.Play(card.InstanceId));Assert.IsFalse(c.Play(card.InstanceId));Assert.IsFalse(c.EndTurn());Assert.AreEqual(2,c.Runtime.Energy);
            Assert.AreEqual(before-5,c.Runtime.Enemy.CurrentHP);c.FinishActionPresentation();Assert.IsTrue(c.EndTurn());Assert.IsFalse(c.EndTurn());c.FinishActionPresentation();Assert.AreEqual(BattlePhase.PlayerTurn,c.Phase);
        }
        [Test] public void BlockAndExecutedIntentComeFromExistingRuntime()
        {
            var c=Start();c.Play(c.Runtime.Deck.Hand.Single(x=>x.CardId=="stone_guard").InstanceId);c.FinishActionPresentation();Assert.AreEqual(5,c.Runtime.Block);
            var intent=c.Runtime.CurrentIntent;int hp=c.Run.CurrentHP;c.EndTurn();
            Assert.AreEqual(intent.Amount,c.Runtime.LastEnemyAction.Intent.Amount);Assert.AreEqual(intent.ExpectedHPDamage(5),hp-c.Run.CurrentHP);Assert.AreEqual(1,c.Runtime.Enemy.PatternIndex);
            Assert.AreEqual(3,c.Runtime.Energy);Assert.AreEqual(2,c.Runtime.Turn);Assert.AreEqual(4,c.Runtime.Deck.Hand.Count);
        }
        [TestCase(RewardType.Gold)][TestCase(RewardType.Card)][TestCase(RewardType.Skip)]
        public void VictoryWaitsForExactlyOneRewardThenCompletesNode(RewardType kind)
        {
            var c=Start();c.Runtime.Enemy.CurrentHP=1;c.Play(c.Runtime.Deck.Hand.Single(x=>x.CardId=="earth_strike").InstanceId);c.FinishActionPresentation();
            Assert.AreEqual(BattlePhase.Victory,c.Phase);Assert.AreEqual(0,c.Run.CompletedNodes.Count);Assert.IsFalse(c.EndTurn());Assert.IsFalse(c.ResolveReward(kind));
            Assert.IsTrue(c.OpenReward());Assert.IsFalse(c.OpenReward());var reward=c.Runtime.Reward;Assert.AreEqual(3,reward.Candidates.Distinct().Count());
            Assert.IsTrue(c.ResolveReward(kind,kind==RewardType.Card?reward.Candidates[0]:null));Assert.IsFalse(c.ResolveReward(kind,reward.Candidates[0]));
            Assert.AreEqual(kind==RewardType.Gold?50:0,c.Run.Gold);Assert.AreEqual(kind==RewardType.Card?5:4,c.Run.Deck.Count);
            Assert.Contains(c.Room.Id,c.Run.CompletedNodes.ToList());CollectionAssert.AreEquivalent(c.Room.Next,DungeonRuntime.Available(c.Run));Assert.AreEqual(BattlePhase.Finished,c.Phase);
        }
        [Test] public void DefeatDoesNotCompleteNodeOrGenerateReward()
        {var c=Start();c.Run.CurrentHP=1;c.EndTurn();c.FinishActionPresentation();Assert.AreEqual(BattlePhase.Defeat,c.Phase);Assert.AreEqual(RunStatus.Failed,c.Run.Status);Assert.IsNull(c.Runtime.Reward);Assert.AreEqual(0,c.Run.CompletedNodes.Count);Assert.IsFalse(c.OpenReward());Assert.IsFalse(c.Play("card-0"));}
        [Test] public void ReturnToMapRequiresRewardAndPreservesObjectIdentity()
        {
            var f=Map();var r=f.Run;f.SelectRoom("r1");f.BeginBattle();f.FinishTransition(FlowScreen.Battle);Assert.IsFalse(f.ReturnFromBattle());
            var c=f.Battle;c.Runtime.Enemy.CurrentHP=1;c.Play(c.Runtime.Deck.Hand.Single(x=>x.CardId=="earth_strike").InstanceId);c.FinishActionPresentation();c.OpenReward();c.ResolveReward(RewardType.Card,c.Runtime.Reward.Candidates[0]);
            Assert.IsTrue(f.ReturnFromBattle());Assert.IsFalse(f.ReturnFromBattle());f.FinishTransition(FlowScreen.DungeonMap);Assert.AreSame(r,f.Run);Assert.IsNull(f.Battle);Assert.IsNull(f.PendingRoom);Assert.AreEqual(5,r.Deck.Count);
        }
        [TestCase("wind_read",0,true)][TestCase("lightning_chain",1,false)][TestCase("poison_touch",1,false)]
        [TestCase("stone_retaliation",1,false)][TestCase("fire_strength",1,false)][TestCase("lightning_charge",0,true)]
        public void AddedCardsUseTheirExistingCostEffectsAndPileRules(string id,int cost,bool exhaust)
        {
            var f=Map();var added=f.Run.AddCard(id);f.SelectRoom("r1");f.BeginBattle();var c=f.Battle;
            var card=c.Runtime.Deck.Hand.Single(x=>x.InstanceId==added.InstanceId);Assert.AreEqual(cost,Data.Card(id).Cost);Assert.IsTrue(c.Play(card.InstanceId));
            Assert.IsTrue((exhaust?c.Runtime.Deck.ExhaustPile:c.Runtime.Deck.DiscardPile).Any(x=>x.InstanceId==card.InstanceId));
            if(id=="lightning_chain")Assert.AreEqual(6,c.Runtime.LastCardResult.Damage);
            if(id=="poison_touch")Assert.AreEqual(3,c.Runtime.Enemy.Status.Poison);
            if(id=="fire_strength")Assert.AreEqual(2,c.Runtime.Strength);
        }
        [Test] public void ConditionalFeedbackIsAnEffectTraceNotAnAlternativeCalculation()
        {var c=Start();c.Runtime.Block=5;var result=CardEffectResolver.Resolve(c.Runtime,Data.Card("stone_retaliation"));Assert.IsTrue(result.ConditionalTriggered);Assert.AreEqual(9,result.Damage);}
        [Test] public void PrefabsFramesAndScenesHaveNoMissingScripts()
        {
            var art=AssetDatabase.LoadAssetAtPath<BattlePresentationAssets>("Assets/BRODUN/Resources/BrodunBattle.asset");Assert.NotNull(art);Assert.NotNull(art.cardPrefab);Assert.NotNull(art.enemyPrefab);
            foreach(var enemy in art.enemies)foreach(var sequence in enemy.animations){Assert.AreEqual(4,sequence.frames.Length);Assert.IsTrue(sequence.frames.All(x=>x!=null));Assert.Greater(sequence.fps,0);}
            foreach(var prefab in new[]{art.cardPrefab.gameObject,art.enemyPrefab.gameObject})foreach(var t in prefab.GetComponentsInChildren<Transform>(true))Assert.AreEqual(0,GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject));
            var scene=EditorSceneManager.OpenScene("Assets/BRODUN/Scenes/BrodunBattle.unity",OpenSceneMode.Additive);
            try{foreach(var root in scene.GetRootGameObjects())foreach(var t in root.GetComponentsInChildren<Transform>(true))Assert.AreEqual(0,GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject));}
            finally{EditorSceneManager.CloseScene(scene,true);}
        }
        [Test] public void FindReproducibleEarlyEliteRouteForNativeQa()
        {
            for(uint seed=0;seed<1000;seed++)
            {
                var dungeon=LayeredMapGenerator.Generate(seed);var nodes=dungeon.Rooms.ToDictionary(n=>n.Id);
                foreach(var a in nodes["start"].Next)foreach(var b in nodes[a].Next)foreach(var e in nodes[b].Next)
                if(nodes[b].Type==RoomType.Battle&&nodes[e].Encounter==EnemyType.Elite)
                {Debug.Log("PHASE4 NATIVE QA seed="+seed+" route="+a+","+b+","+e);return;}
            }
            Assert.Fail("No early elite route found");
        }
    }
}
