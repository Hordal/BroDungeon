using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Brodun.Data;
using Brodun.Editor;
using NUnit.Framework;
using UnityEditor;

namespace Brodun.Tests
{
    public sealed class WarriorCardTests : RulesTest
    {
        private BattleRuntime Setup(int enemies=1)
        {
            var run=Run();
            var battle=new BattleRuntime(Data,run,DungeonRuntime.Enter(run,"r1"),
                additionalEnemies:Enumerable.Repeat(Data.Enemy("moss_slime"),enemies-1));
            foreach(var enemy in battle.Enemies){enemy.CurrentHP=1000;enemy.CurrentIntent=new IntentData(IntentType.Attack,3,3);}
            return battle;
        }
        private CardResult Use(BattleRuntime battle,string id) => CardEffectResolver.Resolve(battle,Data.Card(id),Data.Card(id).RequiresTargetSelection?battle.Enemy:null);
        private static IEnumerable CardSpecifications()
        {
            foreach(var line in File.ReadLines("BRODUN_CARD_SPEC_V1_1.md"))
            {
                if(!Regex.IsMatch(line,@"^\| .W\d\d. \|"))continue;
                var cells=line.Trim('|').Split('|').Select(c=>c.Trim()).ToArray();
                var grade=cells[2].Split('/').Select(c=>c.Trim()).ToArray();
                yield return new TestCaseData(cells[0].Trim((char)96),cells[1],int.Parse(grade[1]),grade[0],cells[4]);
            }
        }
        [TestCaseSource(nameof(CardSpecifications))]
        public void AssetMatchesPublishedSpecification(string id,string name,int cost,string grade,string description)
        {
            var card=Data.Card(id);
            Assert.AreEqual(name,card.Name);Assert.AreEqual(cost,card.Cost);Assert.AreEqual(description,card.Description);
            Assert.AreEqual(grade=="하급"?CardRarity.Low:grade=="중급"?CardRarity.Middle:CardRarity.High,card.Rarity);
            Assert.AreEqual(CharacterId.Warrior,card.Owner);
            Assert.IsTrue(card.RewardEligible);Assert.IsTrue(card.CatalogOnly);Assert.IsFalse(card.StarterOnly);
            Assert.AreEqual(ElementType.None,card.Element);Assert.AreEqual(ElementApplication.None,card.ElementApplication);
            Assert.IsFalse(card.Exhausts);Assert.IsFalse(Data.Cards.Contains(card));
        }
        [Test] public void CatalogOwnershipLegacyArtAndStartingDecksRemainIndependent()
        {
            Assert.AreEqual(124,Data.AllCards.Select(c=>c.Id).Distinct().Count());
            var warriors=Data.AllCards.Where(c=>c.Owner==CharacterId.Warrior&&!c.StarterOnly).ToArray();
            Assert.AreEqual(24,warriors.Length);
            Assert.AreEqual(9,warriors.Count(c=>c.Rarity==CardRarity.Low));
            Assert.AreEqual(9,warriors.Count(c=>c.Rarity==CardRarity.Middle));
            Assert.AreEqual(6,warriors.Count(c=>c.Rarity==CardRarity.High));
            var catalog=AssetDatabase.LoadAssetAtPath<GameCatalogData>(DataAssetBuilder.CatalogPath);
            foreach(var pair in new[]{("W01","stone_guard"),("W04","stone_fortress"),("W09","explosion_barrier"),("W10","stone_retaliation"),("W17","earth_strike"),("W21","earth_crush")})
                Assert.AreSame(catalog.CardArtwork(pair.Item2),catalog.CardArtwork(pair.Item1));
            foreach(CharacterId hero in Enum.GetValues(typeof(CharacterId)))
            {
                var run=Run(hero);Assert.AreEqual(10,run.Deck.Count);
                Assert.IsTrue(run.Deck.All(c=>Data.Card(c.CardId).IsFormalCard));
                Assert.AreEqual(5,Data.BaseEnergyFor(hero));
                if(hero!=CharacterId.Warrior)
                    foreach(var card in warriors)Assert.Throws<InvalidOperationException>(()=>run.AddCard(card.Id));
            }
            Assert.AreEqual(5,Data.Config.HandSize);Assert.AreEqual(8,Data.Config.EnergyCap);
            Assert.AreEqual(6,Data.AllCards.Count(c=>c.StarterOnly));Assert.AreEqual(27,Data.Cards.Count);
            Assert.That(Data.Card("lightning_charge").Description,Does.Contain("최대 8"));
        }
        [Test] public void RewardsAcrossSeedsExcludeTheUnreleasedCards()
        {
            for(uint seed=0;seed<50;seed++)
                foreach(CharacterId hero in Enum.GetValues(typeof(CharacterId)))
                {
                    var run=Run(hero,seed);DungeonRuntime.Enter(run,"r1");
                    var reward=RewardResolver.Create(Data,run);
                    Assert.AreEqual(3,reward.Candidates.Distinct().Count());
                    Assert.IsTrue(reward.Candidates.All(id=>Data.Card(id).IsFormalCard&&!Data.Card(id).StarterOnly&&(!Data.Card(id).Owner.HasValue||Data.Card(id).Owner==hero)));
                }
        }
        [TestCase("W01",8,0)][TestCase("W02",4,1)][TestCase("W04",20,0)]
        public void FortressBlockAndDrawUseOrderedEffects(string id,int block,int draw)
        {
            var b=Setup();int hand=b.Deck.Hand.Count;var r=Use(b,id);
            Assert.AreEqual(block,b.Block);Assert.AreEqual(hand+draw,b.Deck.Hand.Count);
            Assert.IsFalse(r.Events.Any(e=>e.Hit!=null));
        }
        [TestCase(6,11,0)][TestCase(7,12,1)]
        public void FormationChecksBlockAfterGainingIt(int before,int after,int draw)
        {
            var b=Setup();b.Block=before;int hand=b.Deck.Hand.Count;Use(b,"W03");
            Assert.AreEqual(after,b.Block);Assert.AreEqual(hand+draw,b.Deck.Hand.Count);
        }
        [TestCase(0,14,0)][TestCase(1,10,1)][TestCase(8,17,1)]
        public void RepairUsesOneExclusivePreCardBranch(int before,int after,int draw)
        {
            var b=Setup();b.Block=before;int hand=b.Deck.Hand.Count;Use(b,"W05");
            Assert.AreEqual(after,b.Block);Assert.AreEqual(hand+draw,b.Deck.Hand.Count);
        }
        [TestCase(11,0)][TestCase(12,1)]
        public void PressureUsesTheCurrentBlockThreshold(int block,int weak)
        {
            var b=Setup();b.Block=block;Use(b,"W06");
            Assert.AreEqual(990,b.Enemy.CurrentHP);Assert.AreEqual(weak,b.Enemy.Status.Weak);
        }
        [TestCase(0,5)][TestCase(6,10)]
        public void WillRetainsOnlyRemainingBlockAndExpires(int before,int kept)
        {
            var b=Setup();b.Block=before;Use(b,"W07");b.EndTurn();Assert.AreEqual(kept,b.Block);
            b.EndTurn();Assert.AreEqual(0,b.Block);
            Assert.IsTrue(b.BlockHistory.Any(e=>e.Cause==CombatCause.BlockRetained&&e.After==kept));
        }
        [TestCase(0,10)][TestCase(7,15)][TestCase(20,25)][TestCase(100,25)]
        public void GuardianRoundsDownCapsBonusAndDoesNotSpend(int block,int damage)
        {
            var b=Setup();b.Block=block;b.Strength=2;var r=Use(b,"W08");
            Assert.AreEqual(damage+2,r.HitEvents.Single().Hit.Amount);Assert.AreEqual(block,b.Block);
            Assert.AreEqual(0,b.BlockSpentThisTurn);
        }
        [Test] public void BarrierGrantsBlockBeforeDealingDamage()
        {
            var b=Setup();var r=Use(b,"W09");
            Assert.AreEqual(4,b.Block);Assert.AreEqual(996,b.Enemy.CurrentHP);
            Assert.Less(r.Events.Single(e=>e.Cause==CombatCause.BlockGain).Index,r.HitEvents.Single().Index);
        }
        [TestCase(0,6)][TestCase(1,10)]
        public void RetaliationAttackUsesPreCardBlock(int block,int damage)
        {
            var b=Setup();b.Block=block;b.Strength=2;var r=Use(b,"W10");
            Assert.AreEqual(damage+2,r.Damage);Assert.AreEqual(block,b.Block);
        }
        [TestCase("W11",1,3)][TestCase("W12",3,4)][TestCase("W14",1,2)]
        [TestCase("W15",3,9)][TestCase("W16",2,3)]
        public void CountersApplyPerActionWithEachCardsLimit(string id,int count,int damage)
        {
            var b=Setup(3);Use(b,id);b.Block=100;b.EndTurn();
            var trace=b.LastTurnResult.Events;
            var counters=trace.Where(e=>e.Cause==CombatCause.Counter&&e.Hit!=null).ToArray();
            Assert.AreEqual(count,counters.Length);
            Assert.IsTrue(counters.All(e=>e.Hit.Amount==damage&&e.EffectId==id));
            foreach(var entry in counters)
            {
                var action=trace.Single(e=>e.Index==entry.ParentEventIndex);
                Assert.AreEqual(action.ActorId,entry.TargetId);
                Assert.IsTrue(trace.Where(e=>e.ParentEventIndex==action.Index&&e.TargetId==CombatTrace.PlayerId).All(e=>e.Index<entry.Index));
            }
            Assert.IsTrue(b.CounteredLastEnemyTurn);Assert.AreEqual(id=="W14"?6:5,b.Deck.Hand.Count);
        }
        [TestCase("W11")][TestCase("W12")][TestCase("W14")][TestCase("W15")][TestCase("W16")]
        public void CounterNeedsRealAbsorptionAndSameCardDoesNotStack(string id)
        {
            var b=Setup();Use(b,id);int block=b.Block;Use(b,id);Assert.AreEqual(block*2,b.Block);
            b.Block=0;b.EndTurn();
            Assert.IsFalse(b.LastTurnResult.Events.Any(e=>e.Cause==CombatCause.Counter));
            Assert.IsFalse(b.CounteredLastEnemyTurn);Assert.AreEqual(5,b.Deck.Hand.Count);
            if(id=="W16"){b.Block=50;b.EndTurn();Assert.AreEqual(1,b.LastTurnResult.Events.Count(e=>e.Hit!=null&&e.Cause==CombatCause.Counter));}
        }
        [Test] public void CounterBonusesUseTheImmediatelyPreviousEnemyTurn()
        {
            var b=Setup();Use(b,"W11");b.EndTurn();Assert.AreEqual(18,Use(b,"W13").Damage);
            b.EndTurn();Assert.IsFalse(b.CounteredLastEnemyTurn);Assert.AreEqual(10,Use(b,"W13").Damage);
        }
        [Test] public void DifferentCountersCombineAndCounterVictoryCreatesReward()
        {
            var b=Setup();Use(b,"W11");Use(b,"W14");
            b.Enemy.CurrentHP=4;b.Enemy.CurrentIntent=new IntentData(IntentType.Attack,2,3);b.EndTurn();
            CollectionAssert.AreEqual(new[]{"W11","W14"},b.LastTurnResult.Events.Where(e=>e.Cause==CombatCause.Counter&&e.Hit!=null).Select(e=>e.EffectId));
            Assert.AreEqual(BattleResult.Won,b.Result);Assert.IsNotNull(b.Reward);
            Assert.IsTrue(b.LastTurnResult.EnemyResults.Single().KilledByEvent.HasValue);
            var next=new BattleRuntime(Data,b.Run,b.Run.Map.Single(n=>n.Id=="r1"));
            Assert.IsFalse(next.CounteredLastEnemyTurn);
            next.Block=50;next.EndTurn();Assert.IsFalse(next.CounteredLastEnemyTurn);
        }
        [Test] public void StrikeAndSpendConditionsUseOnlyRealCardSpending()
        {
            var b=Setup();Assert.AreEqual(7,Use(b,"W17").Damage);
            b.Block=5;b.EndTurn();Assert.AreEqual(0,b.BlockSpentThisTurn);
            Assert.AreEqual(7,Use(b,"W19").Damage);Assert.AreEqual(0,b.Enemy.Status.Weak);
            Assert.AreEqual(21,Use(b,"W21").Damage);
            b.Block=3;Use(b,"W20");Assert.AreEqual(3,b.BlockSpentThisTurn);
            Assert.AreEqual(7,Use(b,"W19").Damage);Assert.AreEqual(1,b.Enemy.Status.Weak);
            Assert.AreEqual(28,Use(b,"W21").Damage);
        }
        [TestCase(0,6,0)][TestCase(3,10,0)][TestCase(8,18,0)][TestCase(12,18,4)]
        public void ShieldBashUsesActualSpendAndRoundsDown(int before,int damage,int after)
        {
            var b=Setup();b.Block=before;var r=Use(b,"W20");
            Assert.AreEqual(damage,r.HitEvents.Single().Hit.Amount);
            Assert.AreEqual(after,b.Block);Assert.AreEqual(before-after,b.BlockSpentThisTurn);
        }
        [Test] public void PreparationDrawsOnceOnFirstActualSpendAndDoesNotStack()
        {
            var b=Setup();Use(b,"W18");Use(b,"W18");int hand=b.Deck.Hand.Count;
            var first=Use(b,"W20");Assert.AreEqual(hand+1,b.Deck.Hand.Count);
            var spend=first.Events.Single(e=>e.Cause==CombatCause.BlockSpent&&e.Kind==CombatEventKind.Block);
            Assert.AreEqual(spend.Index,first.Events.Single(e=>e.Kind==CombatEventKind.Draw).ParentEventIndex);
            Use(b,"W20");Assert.AreEqual(hand+1,b.Deck.Hand.Count);
            b.EndTurn();b.Block=8;Use(b,"W20");Assert.AreEqual(5,b.Deck.Hand.Count);
            var zero=Setup();Use(zero,"W18");zero.Block=0;Use(zero,"W20");
            Assert.AreEqual(0,zero.BlockSpentThisTurn);zero.Block=3;Use(zero,"W20");Assert.AreEqual(6,zero.Deck.Hand.Count);
        }
        [Test] public void PreparationAfterEarlierSpendingDoesNotCountAnotherSpendAsFirst()
        {
            var b=Setup();b.Block=8;Use(b,"W20");Use(b,"W18");int hand=b.Deck.Hand.Count;
            Use(b,"W20");Assert.AreEqual(hand,b.Deck.Hand.Count);
        }
        [TestCase(0,10,0)][TestCase(5,15,0)][TestCase(20,22,8)]
        public void CollapseSpendsOnceAndReportsEachTarget(int block,int damage,int remaining)
        {
            var b=Setup(3);b.Block=block;var r=Use(b,"W23");
            Assert.AreEqual(3,r.HitEvents.Count);
            CollectionAssert.AreEqual(b.Enemies.Select(e=>e.TargetId),r.HitEvents.Select(e=>e.TargetId));
            Assert.IsTrue(r.HitEvents.All(e=>e.Hit.Amount==damage));
            Assert.AreEqual(remaining,b.Block);
            Assert.AreEqual(1,r.Events.Count(e=>e.Kind==CombatEventKind.Block&&e.Cause==CombatCause.BlockSpent));
        }
        [TestCase(false,4)][TestCase(true,5)]
        public void ReconstructionAndCycleShareTheFiveBlockBudget(bool cycleFirst,int restored)
        {
            var b=Setup();b.Block=20;if(cycleFirst)Use(b,"W24");
            Use(b,"W20");Use(b,"W22");if(!cycleFirst)Use(b,"W24");
            Use(b,"W20");Assert.AreEqual(restored,b.RestoredBlockThisTurn);
            Assert.IsFalse(Use(b,"W22").Events.Any(e=>e.Cause==CombatCause.BlockRestored));
            b.EndTurn();b.Block=8;
            var next=Use(b,"W20");Assert.AreEqual(3,next.Events.Where(e=>e.Cause==CombatCause.BlockRestored).Sum(e=>e.After-e.Before));
            Assert.AreEqual(3,b.Block);Assert.AreEqual(8,b.BlockSpentThisTurn);
        }
        [Test] public void CycleCannotInflateSpendOrTriggerOnEnemyAbsorption()
        {
            var b=Setup();Use(b,"W24");Use(b,"W24");b.Block=10;var bash=Use(b,"W20");
            Assert.AreEqual(18,bash.HitEvents.Single().Hit.Amount);
            Assert.AreEqual(8,b.BlockSpentThisTurn);Assert.AreEqual(5,b.Block);Assert.AreEqual(3,b.RestoredBlockThisTurn);
            var restored=bash.Events.Single(e=>e.Cause==CombatCause.BlockRestored);
            Assert.AreEqual(bash.Events.Single(e=>e.Cause==CombatCause.BlockSpent).Index,restored.ParentEventIndex);
            b.EndTurn();Assert.AreEqual(0,b.BlockSpentThisTurn);Assert.AreEqual(0,b.RestoredBlockThisTurn);
            b.Block=4;Use(b,"W20");Assert.AreEqual(2,b.Block);
        }
        [Test] public void PreviewDecksUseRealCatalogValuesAndResetOnNormalRuns()
        {
            foreach(var build in new[]{CardBuild.Fortress,CardBuild.Counter,CardBuild.BlockSpend})
            {
                var preview=Brodun.Editor.WarriorPreview.Definitions(Data,build);
                Assert.AreSame(Data.Config,preview.Config);
                CollectionAssert.AreEqual(Data.AllCards,preview.AllCards);
                Assert.AreEqual(10,preview.Character(CharacterId.Warrior).StartingDeck.Count);
                Assert.AreEqual(8,preview.Character(CharacterId.Warrior).StartingDeck.Count(id=>preview.Card(id).Build==build));
                foreach(var hero in new[]{CharacterId.Mage,CharacterId.Priest})
                    CollectionAssert.AreEqual(Data.Character(hero).StartingDeck,preview.Character(hero).StartingDeck);
            }
            Assert.IsTrue(Run().Deck.All(c=>Data.Card(c.CardId).IsFormalCard));
        }
        [TestCase("W11")][TestCase("W12")][TestCase("W14")][TestCase("W15")]
        public void TemporaryCountersExpireAfterTheirScheduledEnemyTurn(string id)
        {
            var b=Setup();Use(b,id);b.Block=100;b.EndTurn();
            Assert.IsTrue(b.CounteredLastEnemyTurn);
            b.Block=100;b.EndTurn();
            Assert.IsFalse(b.CounteredLastEnemyTurn);
            Assert.AreEqual(5,b.Deck.Hand.Count);
        }
        [Test] public void ReconstructionRoundsDownOnceAndResetsOnNextTurn()
        {
            var b=Setup();b.Block=7;Use(b,"W20");
            var first=Use(b,"W22");
            Assert.AreEqual(3,b.Block);Assert.AreEqual(3,b.RestoredBlockThisTurn);
            Assert.AreEqual(3,first.Events.Single(e=>e.Cause==CombatCause.BlockRestored).After);
            Use(b,"W20");Use(b,"W22");
            Assert.AreEqual(0,b.Block);
            b.EndTurn();b.Block=8;Use(b,"W20");Use(b,"W22");
            Assert.AreEqual(4,b.Block);Assert.AreEqual(4,b.RestoredBlockThisTurn);
        }
        [Test] public void CardRestoreCannotCreateAnotherFirstSpendTrigger()
        {
            var b=Setup();Use(b,"W24");Use(b,"W18");int hand=b.Deck.Hand.Count;
            Use(b,"W20");Assert.AreEqual(hand+1,b.Deck.Hand.Count);
            Assert.AreEqual(7,b.BlockSpentThisTurn);
            Use(b,"W20");Assert.AreEqual(hand+1,b.Deck.Hand.Count);
            Assert.AreEqual(10,b.BlockSpentThisTurn);
            Assert.AreEqual(3,b.RestoredBlockThisTurn);
        }
        [Test] public void PersistentThornsAndCycleAreAbsentFromTheNextEncounter()
        {
            var b=Setup();Use(b,"W16");Use(b,"W24");
            b.Enemy.CurrentHP=1;b.Deck.Draw(20);
            var attack=b.Deck.Hand.First(c=>Data.Card(c.CardId).BaseDamage>0);
            Assert.IsTrue(b.PlayCard(attack.InstanceId));
            Assert.AreEqual(BattleResult.Won,b.Result);
            var next=new BattleRuntime(Data,b.Run,b.Run.Map.Single(n=>n.Id=="r1"));
            next.Block=8;Use(next,"W20");
            Assert.AreEqual(0,next.Block);Assert.AreEqual(0,next.RestoredBlockThisTurn);
            next.Block=50;next.EndTurn();Assert.IsFalse(next.CounteredLastEnemyTurn);
        }

        [Test] public void WarriorCardsUseNormalCostsAndNeverApplyElements()
        {
            foreach(var card in Data.AllCards.Where(c=>c.Owner==CharacterId.Warrior&&!c.StarterOnly))
            {
                var run=Run();var instance=run.AddCard(card.Id);
                var b=new BattleRuntime(Data,run,DungeonRuntime.Enter(run,"r1"));
                b.Enemy.CurrentHP=1000;b.Deck.Draw(30);
                Assert.IsTrue(b.PlayCard(instance.InstanceId),card.Id);
                Assert.AreEqual(5-card.Cost,b.Energy);Assert.AreEqual(card.Id,b.LastCardResult.CardId);
                Assert.IsFalse(b.LastCardResult.Events.Any(e=>e.Kind==CombatEventKind.ElementMark||e.Kind==CombatEventKind.Reaction));
            }
        }
    }
}
