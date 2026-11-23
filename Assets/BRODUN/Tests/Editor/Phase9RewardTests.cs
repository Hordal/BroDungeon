using System;
using System.Collections.Generic;
using System.Linq;
using Brodun.Data;
using NUnit.Framework;
using UnityEngine;
namespace Brodun.Tests
{
    public sealed class Phase9RewardTests:RulesTest
    {
        public static string[] Starting(CharacterId hero)
        {
            string p=hero==CharacterId.Warrior?"B-W":hero==CharacterId.Mage?"B-M":"B-P";
            string[] extra=hero==CharacterId.Warrior?new[]{"W01","W12","E04","E08"}:hero==CharacterId.Mage?new[]{"M05","M09","E04","E17"}:new[]{"P13","P22","E05","E08"};
            return Enumerable.Repeat(p+"01",2).Concat(Enumerable.Repeat(p+"02",4)).Concat(extra).ToArray();
        }
        private BattleRuntime BattleFor(CharacterId hero=CharacterId.Warrior,EnemyType type=EnemyType.Normal,uint seed=17,GameDefinitions data=null)
        {
            data=data??Data;
            var nodes=new[]{new RoomNode("start",RoomType.Start,0,50,new[]{"reward-room"},EventKind.None),
                new RoomNode("reward-room",type==EnemyType.Boss?RoomType.Boss:RoomType.Battle,1,50,Array.Empty<string>(),EventKind.None,encounter:type)};
            var run=new RunState(data,hero,seed,dungeon:new GeneratedDungeon(null,nodes));
            return new BattleRuntime(data,run,DungeonRuntime.Enter(run,"reward-room"));
        }
        private void Win(BattleRuntime b)
        {b.Enemy.CurrentHP=1;string id=b.Run.SelectedCharacter==CharacterId.Warrior?"B-W01":b.Run.SelectedCharacter==CharacterId.Mage?"B-M01":"B-P01";Assert.IsTrue(b.PlayCard(DrawCard(b,id).InstanceId));Assert.AreEqual(BattleResult.Won,b.Result);}
        [TestCase(CharacterId.Warrior)][TestCase(CharacterId.Mage)][TestCase(CharacterId.Priest)]
        public void FormalStartingDeckMixesEightOneCostAndTwoTwoCostCards(CharacterId hero)
        {
            var run=Run(hero);CollectionAssert.AreEqual(Starting(hero),run.Deck.Select(c=>c.CardId));
            Assert.IsEmpty(Data.Character(hero).ProvisionalStartingCards);Assert.IsTrue(run.Deck.All(c=>Data.Card(c.CardId).IsFormalCard));
            CollectionAssert.AreEquivalent(new[]{1,1,1,1,1,1,1,1,2,2},run.Deck.Select(c=>Data.Card(c.CardId).Cost));
            Assert.AreEqual(6,run.Deck.Count(c=>Data.Card(c.CardId).StarterOnly));
            Assert.AreEqual(2,run.Deck.Count(c=>!Data.Card(c.CardId).StarterOnly&&Data.Card(c.CardId).Owner==hero));
            Assert.AreEqual(2,run.Deck.Count(c=>Data.Card(c.CardId).ElementApplication==ElementApplication.SharedAttack));
            var a=BattleFor(hero);var b=BattleFor(hero);Assert.AreEqual(5,a.Deck.Hand.Count);Assert.AreEqual(5,a.Energy);Assert.AreEqual(8,Data.Config.EnergyCap);
            CollectionAssert.AreEqual(a.Deck.Hand.Select(c=>c.CardId),b.Deck.Hand.Select(c=>c.CardId));
            a.EndTurn();Assert.AreEqual(5,a.Deck.Hand.Count);Assert.AreEqual(5,a.Energy);
        }
        [Test]
        public void FirstAreaNormalAttacksMatchIntentAndThreatenThroughOneGuard()
        {
            var battle=BattleFor();
            var opponents=EncounterPool.ForArea(Data,1,EnemyType.Normal);
            foreach(var enemy in opponents)
                CollectionAssert.AreEqual(new[]{9,11,5},enemy.Pattern.Select(intent=>intent.Amount));

            foreach(int expectedAttack in new[]{9,11})
            {
                var guard=DrawCard(battle,"B-W02");
                Assert.IsTrue(battle.PlayCard(guard.InstanceId));
                Assert.AreEqual(5,battle.Block);
                Assert.AreEqual(expectedAttack,battle.CurrentIntent.TotalDamage);
                int hp=battle.Run.CurrentHP;
                Assert.IsTrue(battle.EndTurn());
                Assert.AreEqual(expectedAttack,battle.LastEnemyAction.Intent.TotalDamage);
                Assert.AreEqual(expectedAttack-5,hp-battle.Run.CurrentHP);
                Assert.AreEqual(5,battle.Energy);
            }
        }

        [Test] public void FormalCatalogExcludesLegacyStartersOtherClassesAndSyntheticQAIdentifiers()
        {
            Assert.AreEqual(124,Data.AllCards.Count);Assert.AreEqual(97,Data.GameplayCards.Count);Assert.AreEqual(91,Data.RewardCards.Count);Assert.AreEqual(27,Data.Cards.Count);
            foreach(CharacterId hero in Enum.GetValues(typeof(CharacterId)))
            {var cards=Data.RewardCardsFor(hero);Assert.AreEqual(43,cards.Count);Assert.AreEqual(24,cards.Count(c=>c.Owner==hero));Assert.AreEqual(19,cards.Count(c=>!c.Owner.HasValue));Assert.IsFalse(cards.Any(c=>c.StarterOnly||!c.IsFormalCard));}
            var fake=new CardDefinition("QA-probe","same name","",1,ElementType.None,new[]{new CardEffect(EffectType.Damage,9)},owner:CharacterId.Warrior,rarity:CardRarity.High,catalogOnly:true);
            Assert.IsFalse(fake.IsFormalCard);
            var same=new CardDefinition("W99","바위 방벽","",1,ElementType.None,new[]{new CardEffect(EffectType.Block,8)},owner:CharacterId.Warrior,rarity:CardRarity.Low);
            Assert.IsFalse(same.IsFormalCard);
        }
        [TestCase(EnemyType.Normal,10,15)][TestCase(EnemyType.Elite,20,25)]
        public void VictoryGrantsBaseOnceAndCardOrAdditionalGoldAreExclusive(EnemyType type,int basic,int extra)
        {
            foreach(var kind in new[]{RewardType.Card,RewardType.Gold})
            {
                var b=BattleFor(type:type);Win(b);var run=b.Run;var r=b.Reward;
                Assert.AreEqual(basic,run.Gold);Assert.IsTrue(r.BaseGoldGranted);Assert.IsFalse(RewardResolver.GrantVictory(b));
                Assert.AreSame(r,RewardResolver.Create(Data,run));Assert.AreEqual(basic,r.BaseGold);Assert.AreEqual(extra,r.Gold);
                Assert.AreEqual(3,r.Candidates.Distinct().Count());Assert.IsFalse(RewardResolver.Resolve(Data,run,r,RewardType.Skip));
                Assert.IsTrue(RewardResolver.Resolve(Data,run,r,kind,kind==RewardType.Card?r.Candidates[0]:null));
                Assert.AreEqual(basic+(kind==RewardType.Gold?extra:0),run.Gold);Assert.AreEqual(kind==RewardType.Card?11:10,run.Deck.Count);
                Assert.IsFalse(RewardResolver.Resolve(Data,run,r,kind==RewardType.Card?RewardType.Gold:RewardType.Card,r.Candidates[0]));
                Assert.AreEqual(1,run.ResolvedRewards.Count);Assert.Contains("reward-room",run.CompletedNodes.ToList());Assert.IsNull(run.ActiveRoom);
            }
        }
        [Test] public void BossPaysThirtyWithoutAChoiceAndKeepsTheExistingClearBoundary()
        {
            var b=BattleFor(type:EnemyType.Boss);Win(b);Assert.AreEqual(30,b.Run.Gold);Assert.IsEmpty(b.Reward.Candidates);Assert.IsFalse(b.Reward.AllowsChoice);
            Assert.IsTrue(b.Reward.Resolved);Assert.IsFalse(RewardResolver.GrantVictory(b));Assert.IsFalse(RewardResolver.Resolve(Data,b.Run,b.Reward,RewardType.Gold));
            Assert.AreEqual(RunStatus.InProgress,b.Run.Status);Assert.IsTrue(DungeonRuntime.Complete(b.Run));Assert.AreEqual(RunStatus.Complete,b.Run.Status);
        }
        [Test] public void ReenteringAndCrossRunClaimsCannotDuplicateCurrencyOrCards()
        {
            var a=BattleFor();var b=BattleFor();Win(a);Win(b);Assert.IsFalse(RewardResolver.Resolve(Data,b.Run,a.Reward,RewardType.Gold));
            Assert.IsFalse(RewardResolver.Resolve(Data,a.Run,a.Reward,RewardType.Card,"M01"));Assert.AreEqual(10,a.Run.Gold);
            Assert.IsTrue(RewardResolver.Resolve(Data,a.Run,a.Reward,RewardType.Card,a.Reward.Candidates[0]));
            for(int i=0;i<8;i++)Assert.IsFalse(RewardResolver.Resolve(Data,a.Run,a.Reward,RewardType.Gold));Assert.AreEqual(10,a.Run.Gold);Assert.AreEqual(11,a.Run.Deck.Count);
        }
        [Test] public void DefeatAndUnfinishedCombatCannotPayOrResolveVictoryRewards()
        {
            var b=BattleFor();var r=RewardResolver.Create(Data,b.Run);Assert.IsFalse(RewardResolver.GrantVictory(b));Assert.IsFalse(RewardResolver.Resolve(Data,b.Run,r,RewardType.Gold));
            b.Run.CurrentHP=1;Assert.IsTrue(b.EndTurn());Assert.AreEqual(BattleResult.Lost,b.Result);Assert.AreEqual(0,b.Run.Gold);Assert.IsNull(b.Reward);
        }
        [Test] public void RewardReproductionUsesNodeHeroCatalogAndSeedAndIgnoresOwnedCards()
        {
            foreach(CharacterId hero in Enum.GetValues(typeof(CharacterId)))
            {
                var a=BattleFor(hero);var b=BattleFor(hero);var x=RewardResolver.Create(Data,a.Run);var y=RewardResolver.Create(Data,b.Run);
                CollectionAssert.AreEqual(x.Candidates,y.Candidates);foreach(var id in x.Candidates)a.Run.AddCard(id);
                Assert.AreSame(x,RewardResolver.Create(Data,a.Run));Assert.IsTrue(x.Candidates.All(id=>Data.RewardCardsFor(hero).Any(c=>c.Id==id)));
            }
        }
        private sealed class FixedRandom:IRandomSource {private readonly double value;public FixedRandom(double v){value=v;}public double Next()=>value;}
        [Test] public void EachCategoryCompositionIsPossibleWithoutFixedTwoPlusOneSlots()
        {
            var allUnique=RewardResolver.SampleCandidates(Data,CharacterId.Warrior,EnemyType.Normal,new FixedRandom(0),3);
            var allElements=RewardResolver.SampleCandidates(Data,CharacterId.Warrior,EnemyType.Normal,new FixedRandom(.99),3);
            Assert.IsTrue(allUnique.All(id=>Data.Card(id).Owner==CharacterId.Warrior));Assert.IsTrue(allElements.All(id=>Data.Card(id).ElementApplication==ElementApplication.SharedAttack));
        }
        [Test] public void ExhaustedBucketsRenormalizeWithoutRepeatingOrInventingCards()
        {
            var subset=new[]{Data.Card("W07"),Data.Card("E01")};var small=new GameDefinitions(subset,Data.Characters,Data.Enemies,Data.Config,Data.Reward,Data.Elements);
            var reasons=new List<string>();var offered=RewardResolver.SampleCandidates(small,CharacterId.Warrior,EnemyType.Normal,new FixedRandom(0),3,reasons);
            CollectionAssert.AreEquivalent(new[]{"W07","E01"},offered);Assert.AreEqual(1,reasons.Count);
        }
        [Test] public void FormalAcquisitionCanOfferAnAlreadyOwnedUniqueStartingCard()
        {
            var run=Run();Assert.IsTrue(run.Deck.Any(c=>c.CardId=="W01"));
            var ids=RewardResolver.SampleCandidates(Data,CharacterId.Warrior,EnemyType.Normal,new FixedRandom(0),3);
            Assert.Contains("W01",ids.ToList());var before=run.Deck.Count;run.AddCard("W01");Assert.AreEqual(before+1,run.Deck.Count);
        }
        [Test] public void PoisonAndCounterVictoriesPayOnlyOneBaseGrant()
        {
            var poison=BattleFor();poison.Enemy.CurrentHP=1;poison.Enemy.Status.AddPoison(2);Assert.IsTrue(poison.EndTurn());
            Assert.AreEqual(BattleResult.Won,poison.Result);Assert.AreEqual(10,poison.Run.Gold);Assert.IsFalse(poison.EndTurn());Assert.IsFalse(RewardResolver.GrantVictory(poison));
            var counter=BattleFor();counter.Enemy.CurrentHP=1;counter.Enemy.CurrentIntent=new IntentData(IntentType.Attack,6);
            CardEffectResolver.Resolve(counter,Data.Card("W11"));Assert.IsTrue(counter.EndTurn());Assert.AreEqual(BattleResult.Won,counter.Result);
            Assert.IsTrue(counter.LastTurnResult.Events.Any(e=>e.Cause==CombatCause.Counter&&e.Hit.Killed));Assert.AreEqual(10,counter.Run.Gold);
        }
        [Test] public void SimultaneousAreaAndReactionKillsDoNotRepeatTheVictoryGrant()
        {
            var first=BattleFor();first.Run.AddCard("E02");var room=DungeonRuntime.Node(first.Run,first.Run.ActiveRoom);
            var b=new BattleRuntime(Data,first.Run,room,additionalEnemies:new[]{Data.Enemy("moss_slime")});
            foreach(var e in b.Enemies){e.CurrentHP=1;e.ElementMark=ElementType.Ice;}
            Assert.IsTrue(b.PlayCard(DrawCard(b,"E02").InstanceId));Assert.AreEqual(2,b.LastCardResult.HitEvents.Count(e=>e.Hit.Killed));
            Assert.AreEqual(10,b.Run.Gold);Assert.AreEqual(2,b.LastCardResult.Events.Count(e=>e.Reaction.HasValue));
            Assert.IsFalse(RewardResolver.GrantVictory(b));Assert.AreSame(b.Reward,RewardResolver.Create(Data,b.Run));
        }
        [Test] public void MassSamplingMatchesCategoryAndRarityWeightsAcrossAllCharacters()
        {
            foreach(var rank in new[]{EnemyType.Normal,EnemyType.Elite})
            {
                int own=0;int[] grades=new int[3];int total=0;var rng=new SeededRandom(rank==EnemyType.Normal?901u:902u);
                foreach(CharacterId hero in Enum.GetValues(typeof(CharacterId)))
                    for(int i=0;i<10000;i++)
                    {
                        var cards=RewardResolver.SampleCandidates(Data,hero,rank,rng,3);Assert.AreEqual(3,cards.Distinct().Count());
                        foreach(var id in cards){var c=Data.Card(id);Assert.IsTrue(!c.Owner.HasValue||c.Owner==hero);if(c.Owner.HasValue)own++;grades[(int)c.Rarity-(int)CardRarity.Low]++;total++;}
                    }
                double unique=own*100.0/total;double[] rates=grades.Select(n=>n*100.0/total).ToArray();var target=Data.Reward.Policy.For(rank).RarityWeights;
                Assert.That(unique,Is.InRange(64.2,65.8));for(int i=0;i<3;i++)Assert.That(rates[i],Is.InRange(target[i]-.8,target[i]+.8));
                TestContext.WriteLine("PHASE9_RATES "+rank+" samples="+total+" unique="+unique.ToString("F4",System.Globalization.CultureInfo.InvariantCulture)+" element="+(100-unique).ToString("F4",System.Globalization.CultureInfo.InvariantCulture)+" rarity="+string.Join(",",rates.Select(v=>v.ToString("F4",System.Globalization.CultureInfo.InvariantCulture))));
            }
        }
    }
}
