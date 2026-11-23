using System;
using System.Linq;
using NUnit.Framework;
namespace Brodun.Tests
{
    public sealed class CoreRulesTests : LegacyRulesTest
    {
        [TestCase(CharacterId.Warrior,"earth_strike,stone_guard,wind_cut,wind_read")]
        [TestCase(CharacterId.Mage,"fire_bolt,ice_guard,lightning_strike,lightning_charge")]
        [TestCase(CharacterId.Priest,"water_wave,earth_blessing,wind_judgment,water_rest")]
        public void CharacterStartingState(CharacterId id,string deck)
        {
            var r=Run(id);Assert.AreEqual(60,r.MaxHP);Assert.AreEqual(60,r.CurrentHP);Assert.AreEqual(0,r.Gold);
            Assert.AreEqual(id,r.SelectedCharacter);Assert.AreEqual(RunStatus.InProgress,r.Status);
            CollectionAssert.AreEqual(deck.Split(','),r.Deck.Skip(6).Select(c=>c.CardId));
            CollectionAssert.AreEqual(Enumerable.Range(0,10).Select(i=>"card-"+i),r.Deck.Select(c=>c.InstanceId));
            Assert.AreEqual(5,Data.BaseEnergyFor(id));Assert.AreEqual("start",r.CurrentNode);
        }
        [Test] public void RunInstancesAndAssetDefinitionsAreIndependent()
        {
            var first=Run();var second=Run();first.AddCard("earth_strike");first.AddGold(50);
            Assert.AreEqual(10,second.Deck.Count);Assert.AreEqual(0,second.Gold);Assert.AreEqual(10,Data.Character(CharacterId.Warrior).StartingDeck.Count);
            Assert.AreEqual("card-10",first.AcquiredCards.Single());Assert.AreNotSame(first.Deck[0],second.Deck[0]);
        }
        [Test] public void GoldAndHealValidation()
        {
            var r=Run();r.AddGold(50);Assert.IsFalse(r.SpendGold(51));Assert.IsFalse(r.SpendGold(-1));
            Assert.IsTrue(r.SpendGold(20));Assert.AreEqual(30,r.Gold);
            Assert.Throws<ArgumentOutOfRangeException>(()=>r.AddGold(-1));
            r.CurrentHP=59;r.Heal(8);Assert.AreEqual(60,r.CurrentHP);
        }
        [Test] public void CostsAreChargedOnceAndInvalidPlaysDoNothing()
        {
            var b=Battle();var attack=DrawCard(b,"earth_strike");
            Assert.IsFalse(b.PlayCard("missing"));Assert.AreEqual(5,b.Energy);
            Assert.IsTrue(b.PlayCard(attack.InstanceId));Assert.AreEqual(4,b.Energy);
            Assert.IsFalse(b.PlayCard(attack.InstanceId));Assert.AreEqual(4,b.Energy);
            b.Energy=0;var guard=DrawCard(b,"stone_guard");
            Assert.IsFalse(b.PlayCard(guard.InstanceId));Assert.IsTrue(b.Deck.Hand.Contains(guard));
        }
        [Test] public void DrawDiscardReshuffleMaintainEveryInstanceExactlyOnce()
        {
            var b=Battle();b.Deck.EndTurn();Assert.AreEqual(0,b.Deck.Hand.Count);Assert.AreEqual(5,b.Deck.DiscardPile.Count);
            b.Deck.Draw(20);Assert.AreEqual(10,b.Deck.Hand.Count);Assert.AreEqual(0,b.Deck.DiscardPile.Count);
            Assert.AreEqual(10,b.Deck.Hand.Select(c=>c.InstanceId).Distinct().Count());
            b.Deck.EndTurn();b.Deck.NextTurn();Assert.AreEqual(5,b.Deck.Hand.Count);
        }
        [Test] public void ExhaustIsBattleLocalAndUsePrecedesDrawEffect()
        {
            var run=Run();var b=Battle(run);
            var wind=DrawCard(b,"wind_read");
            Assert.IsTrue(b.PlayCard(wind.InstanceId));Assert.AreEqual(1,b.Deck.ExhaustPile.Count);
            b.Deck.EndTurn();b.Deck.Draw(20);Assert.AreEqual(9,b.Deck.Hand.Count);
            Assert.AreEqual(10,run.Deck.Count);
            Assert.AreEqual(5,DeckRuntime.Begin(Data,run,DungeonRuntime.Node(run,"r1")).Hand.Count);
            var recycle=new DeckRuntime(Data,new[]{new CardInstance("draw-self","water_stream")},new SeededRandom(1),1);
            var def=recycle.Use("draw-self");Assert.AreEqual("water_stream",def.Id);
            recycle.Draw(1);Assert.AreEqual("draw-self",recycle.Hand.Single().InstanceId);
        }
        [TestCase(5,2,0,7)]
        [TestCase(5,2,1,4)]
        [TestCase(2,0,2,0)]
        public void DamageStrengthAndWeakAreFlatPerHit(int damage,int strength,int weak,int expected)
        { Assert.AreEqual(expected,DamageCalculator.AttackAmount(damage,strength,weak,3)); }
        [Test] public void BlockAbsorbsBeforeHPAndOverkillIsClamped()
        {
            int hp=4,block=3;var r=DamageCalculator.Hit(10,ref hp,ref block);
            Assert.AreEqual(3,r.Blocked);Assert.AreEqual(4,r.HPDamage);Assert.AreEqual(0,hp);Assert.AreEqual(0,block);
        }
        [Test] public void ConditionalDamageDoesNotReceiveStrength()
        {
            var b=Battle();b.Strength=2;b.Block=1;
            var r=CardEffectResolver.Resolve(b,Data.Card("stone_retaliation"));
            Assert.AreEqual(11,r.Damage); // (4 + 2 strength) + 5, NOT 13.
        }
        [Test] public void MultiHitReceivesStrengthOnEachHit()
        {
            var b=Battle();b.Strength=2;b.Enemy.Block=3;
            var r=CardEffectResolver.Resolve(b,Data.Card("lightning_chain"));
            Assert.AreEqual(7,r.Damage);Assert.AreEqual(3,r.Blocked);
        }
        [Test] public void WeakAddsDurationUpToThreeAndTicksOnNonAttack()
        {
            var b=Battle();b.Enemy.Status.ApplyWeak(2);b.Enemy.Status.ApplyWeak(1);
            Assert.AreEqual(3,b.Enemy.Status.Weak);b.Enemy.PatternIndex=2;IntentResolver.Decide(b.Enemy);
            b.EndTurn();Assert.AreEqual(2,b.Enemy.Status.Weak);Assert.AreEqual(5,b.Enemy.Block);
        }
        [Test] public void PoisonIgnoresBlockTicksBeforeEnemyAndDecreases()
        {
            var b=Battle();b.Enemy.Block=20;b.Enemy.Status.AddPoison(3);int hp=b.Enemy.CurrentHP;
            b.EndTurn();Assert.AreEqual(hp-3,b.Enemy.CurrentHP);Assert.AreEqual(2,b.Enemy.Status.Poison);
        }
        [Test] public void PoisonKillSkipsEnemyAndOffersOnlyOnePendingReward()
        {
            var b=Battle();b.Enemy.CurrentHP=2;b.Enemy.Status.AddPoison(3);
            Assert.IsTrue(b.EndTurn());Assert.AreEqual(BattleResult.Won,b.Result);Assert.AreEqual(60,b.Run.CurrentHP);
            Assert.IsNull(b.LastEnemyAction);var reward=b.Reward;Assert.IsFalse(b.EndTurn());Assert.AreSame(reward,b.Reward);
            Assert.AreEqual("r1",b.Run.ActiveRoom);Assert.AreEqual(0,b.Run.CompletedNodes.Count);
        }
        [Test] public void SupportCardsDoNotOverwritePreviousOffensiveElement()
        {
            var b=Battle();CardEffectResolver.Resolve(b,Data.Card("water_wave"));
            CardEffectResolver.Resolve(b,Data.Card("stone_guard"));Assert.AreEqual(ElementType.Water,b.Enemy.Element);
            Assert.AreEqual(11,CardEffectResolver.Resolve(b,Data.Card("ice_link")).Damage);
        }
        [Test] public void FireExplosionAndPoisonConditionsUseWebRules()
        {
            var b=Battle();CardEffectResolver.Resolve(b,Data.Card("fire_bolt"));
            Assert.AreEqual(13,CardEffectResolver.Resolve(b,Data.Card("explosion_link")).Damage);
            CardEffectResolver.Resolve(b,Data.Card("poison_touch"));
            Assert.AreEqual(10,CardEffectResolver.Resolve(b,Data.Card("poison_sting")).Damage);
            Assert.AreEqual(3,b.Enemy.Status.Poison);
        }
        [Test] public void EnergyCapAndTurnRecovery()
        {
            var b=Battle();b.Energy=8;CardEffectResolver.Resolve(b,Data.Card("lightning_charge"));Assert.AreEqual(8,b.Energy);
            b.Energy=0;b.Block=8;b.EndTurn();Assert.AreEqual(5,b.Energy);Assert.AreEqual(0,b.Block);Assert.AreEqual(2,b.Turn);
        }
        [Test] public void IntentPreviewEqualsExecutedDamageAndAdvancesOnce()
        {
            var b=Battle();b.Enemy.Status.AddStrength(2);b.Enemy.Status.ApplyWeak(2);b.Block=2;
            var preview=b.CurrentIntent;Assert.AreEqual(8,preview.Amount);Assert.AreEqual(6,preview.ExpectedHPDamage(b.Block));
            b.EndTurn();Assert.AreEqual(8,b.LastEnemyAction.Intent.Amount);Assert.AreEqual(6,b.LastEnemyAction.HPDamage);
            Assert.AreEqual(54,b.Run.CurrentHP);Assert.AreEqual(1,b.Enemy.PatternIndex);Assert.AreEqual(10,b.CurrentIntent.Amount);
        }
        [Test] public void FrozenMultiAttackAppliesWeakPerHitAndBlockAcrossHits()
        {
            var b=Battle();b.Enemy.Status.AddStrength(2);b.Enemy.Status.ApplyWeak(1);b.Block=3;
            var preview=new IntentData(IntentType.Attack,DamageCalculator.AttackAmount(5,2,1,3),2);
            b.Enemy.Status.AddStrength(100); // Execution must NOT recalculate the frozen snapshot.
            var result=IntentResolver.Execute(b.Run,b,preview);
            Assert.AreEqual(5,result.HPDamage);Assert.AreEqual(3,result.Blocked);Assert.AreEqual(55,b.Run.CurrentHP);
        }
        [Test] public void EnemyBlockResetsOnOwnActionAndBuffPersists()
        {
            var b=Battle();b.Enemy.Block=9;
            IntentResolver.Execute(b.Run,b,new IntentData(IntentType.Buff,2));
            Assert.AreEqual(0,b.Enemy.Block);Assert.AreEqual(2,b.Enemy.Status.Strength);
        }
        [TestCase("ruin_gargoyle",1,44,9)]
        [TestCase("moss_slime",3,48,9)]
        [TestCase("gale_warden",3,68,10)]
        [TestCase("fallen_colossus",3,82,13)]
        [TestCase("venom_eel",1,42,6)]
        [TestCase("coil_golem",3,68,8)]
        [TestCase("foundry_tyrant",3,80,10)]
        public void EnemyStatsPreserveRankAreaAndFloorScaling(string id,int floor,int hp,int attack)
        {
            var room=DungeonRuntime.Generate(17).First(n=>n.Floor==floor);
            var e=new EnemyInstance(Data.Enemy(id),room,Data.Config);
            Assert.AreEqual(hp,e.MaxHP);IntentResolver.Decide(e);Assert.AreEqual(attack,IntentResolver.Preview(e,Data.Config).Amount);
        }
        [Test] public void FailedBattleLocksFurtherActionsAndMap()
        {
            var b=Battle();b.Run.CurrentHP=1;b.EndTurn();
            Assert.AreEqual(BattleResult.Lost,b.Result);Assert.AreEqual(RunStatus.Failed,b.Run.Status);
            Assert.IsFalse(b.EndTurn());Assert.IsFalse(b.PlayCard("card-0"));Assert.AreEqual(0,DungeonRuntime.Available(b.Run).Count);
        }
        [Test] public void EnemySelectorSharesOneBattleSystemAndPreservesLegacyRoom()
        {
            var r=Run();
            Assert.AreEqual("ruin_gargoyle",IntentResolver.Select(Data,r,DungeonRuntime.Node(r,"r1")).Id);
            Assert.AreEqual("moss_slime",IntentResolver.Select(Data,r,DungeonRuntime.Node(r,"r8")).Id);
            Assert.AreEqual("gale_warden",IntentResolver.Select(Data,r,DungeonRuntime.Node(r,"r9")).Id);
            Assert.AreEqual("fallen_colossus",IntentResolver.Select(Data,r,DungeonRuntime.Node(r,"boss")).Id);
        }
    }
}
