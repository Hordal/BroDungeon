using System.Collections;
using System.Linq;
using Brodun.Data;
using Brodun.Editor;
using NUnit.Framework;
using UnityEditor;
namespace Brodun.Tests
{
    public sealed class WebParityTests : RulesTest
    {
        private static IEnumerable CardCases()
        {
            foreach(var c in Golden().cases)yield return new TestCaseData(c).SetName("WebCard_"+c.id+"_"+c.previous);
        }
        private static IEnumerable TraceCases()
        {
            foreach(var t in Golden().traces)yield return new TestCaseData(t).SetName("WebBattle_"+t.character+"_"+t.seed);
        }
        private static IEnumerable Seeds()
        {
            foreach(var f in Snapshot().fixtures)yield return new TestCaseData(f).SetName("WebSeed_"+f.seed);
        }
        [Test] public void ActualAssetsAndIndependentDefinitions()
        {
            Assert.AreEqual(142,AssetDatabase.FindAssets("t:ScriptableObject",new[]{"Assets/BRODUN/Data/Generated"}).Length);
            Assert.AreEqual(27,Data.Cards.Count); Assert.AreEqual(124,Data.AllCards.Count); Assert.AreEqual(3,Data.Characters.Count); Assert.AreEqual(12,Data.Enemies.Count);
            Assert.AreEqual(9,AssetDatabase.LoadAssetAtPath<GameCatalogData>(DataAssetBuilder.CatalogPath).ElementCount);
            foreach(var c in Snapshot().cards)
            {
                var d=Data.Card(c.id); Assert.AreEqual(c.name,d.Name); Assert.AreEqual(c.id=="lightning_charge"?c.description.Replace("최대 6","최대 "+Data.Config.EnergyCap):c.description,d.Description);
                Assert.AreEqual(c.cost,d.Cost); Assert.AreEqual(Ids.Parse<ElementType>(c.element),d.Element);
                Assert.AreEqual(c.effects.Length,d.Effects.Count);
                for(int i=0;i<c.effects.Length;i++)
                {
                    var e=c.effects[i].ToDefinition();
                    Assert.AreEqual(e.Type,d.Effects[i].Type);Assert.AreEqual(e.Amount,d.Effects[i].Amount);
                    Assert.AreEqual(e.Hits,d.Effects[i].Hits);Assert.AreEqual(e.Condition,d.Effects[i].Condition);
                }
            }
        }
        [TestCaseSource(nameof(CardCases))] public void CardEffectsMatchActualJavaScript(GoldenCard c)
        {
            var b=Battle(); b.Enemy.CurrentHP=100; b.Enemy.Block=3; b.Enemy.Status.AddPoison(2);
            b.Enemy.Element=Ids.Parse<ElementType>(c.previous); b.Block=3;b.Strength=2;b.Energy=3;b.Run.CurrentHP=40;
            var r=CardEffectResolver.Resolve(b,Data.Card(c.id));
            Assert.AreEqual(c.hp,b.Enemy.CurrentHP);Assert.AreEqual(c.enemyBlock,b.Enemy.Block);
            Assert.AreEqual(c.poison,b.Enemy.Status.Poison);Assert.AreEqual(c.weak,b.Enemy.Status.Weak);
            Assert.AreEqual(Ids.Parse<ElementType>(c.element),b.Enemy.Element);Assert.AreEqual(c.playerHP,b.Run.CurrentHP);
            Assert.AreEqual(c.block,b.Block);Assert.AreEqual(c.strength,b.Strength);Assert.AreEqual(c.energy,b.Energy);
            Assert.AreEqual(c.damage,r.Damage);Assert.AreEqual(c.blocked,r.Blocked);
        }
        [TestCaseSource(nameof(Seeds))] public void SeedHandRewardAndMapMatchActualJavaScript(FixtureRecord f)
        {
            var web=WebDefinitions();var run=new RunState(web,CharacterId.Warrior,(uint)f.seed,"test-run");
            var b=new BattleRuntime(web,run,DungeonRuntime.Enter(run,"r1"));
            CollectionAssert.AreEqual(f.hand,b.Deck.Hand.Select(x=>x.CardId));
            CollectionAssert.AreEqual(f.reward,RewardResolver.Create(web,run).Candidates);
            for(int i=0;i<f.map.Length;i++)
            {
                var expected=f.map[i];var actual=run.Map[i];
                Assert.AreEqual(expected.id,actual.Id); Assert.AreEqual(Ids.Parse<RoomType>(expected.room_type),actual.Type);
                CollectionAssert.AreEqual(expected.next_room_ids,actual.Next);
                Assert.AreEqual(expected.column,actual.Column);Assert.AreEqual(expected.floor_index,actual.Floor);
                Assert.AreEqual(expected.x,actual.X,0.000001);Assert.AreEqual(expected.y,actual.Y);
                if(expected.room_type=="event")Assert.AreEqual(Ids.Parse<EventKind>(expected.event_kind),actual.Event);
            }
        }
        private GameDefinitions WebDefinitions()
        {
            var snapshot=Snapshot();
            return new GameDefinitions(Data.Cards,snapshot.characters.Select(c=>c.ToDefinition()),snapshot.enemies.Select(e=>e.ToDefinition()),
                snapshot.config.ToDefinition(),new RewardDefinition(Data.Reward.Gold,Data.Reward.CandidateCount),Data.Elements);
        }
        [TestCaseSource(nameof(TraceCases))] public void FullBattleTraceMatchesActualJavaScript(GoldenTrace trace)
        {
            // The recorded browser run uses its own historical AP configuration.
            var web=WebDefinitions();
            var run=new RunState(web,Ids.Parse<CharacterId>(trace.character),(uint)trace.seed,"test-run");
            var b=new BattleRuntime(web,run,DungeonRuntime.Enter(run,"r1"));
            foreach(var step in trace.steps)
            {
                if(step.kind=="card")Assert.IsTrue(b.PlayCard(step.instance));
                if(step.kind=="end")Assert.IsTrue(b.EndTurn());
                Assert.AreEqual(step.hp,b.Run.CurrentHP,"Player HP at turn "+step.turn);
                Assert.AreEqual(step.enemyHP,b.Enemy.CurrentHP);Assert.AreEqual(step.enemyBlock,b.Enemy.Block);
                Assert.AreEqual(step.block,b.Block);Assert.AreEqual(step.strength,b.Strength);
                Assert.AreEqual(step.poison,b.Enemy.Status.Poison);Assert.AreEqual(step.weak,b.Enemy.Status.Weak);
                Assert.AreEqual(step.energy,b.Energy);Assert.AreEqual(step.turn,b.Turn);
                Assert.AreEqual(Ids.Parse<IntentType>(step.intentType),b.CurrentIntent.Type);
                Assert.AreEqual(step.intentAmount,b.CurrentIntent.Amount);Assert.AreEqual(step.intentHits,b.CurrentIntent.Hits);
                CollectionAssert.AreEqual(step.hand,b.Deck.Hand.Select(x=>x.InstanceId));
                CollectionAssert.AreEqual(step.draw,b.Deck.DrawPile.Select(x=>x.InstanceId));
                CollectionAssert.AreEqual(step.discard,b.Deck.DiscardPile.Select(x=>x.InstanceId));
                CollectionAssert.AreEqual(step.exhaust,b.Deck.ExhaustPile.Select(x=>x.InstanceId));
            }
            Assert.AreNotEqual(BattleResult.Active,b.Result,"Trace must reach victory or defeat.");
        }
    }
}
