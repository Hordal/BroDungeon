using System;
using System.Linq;
using NUnit.Framework;

namespace Brodun.Tests
{
    public sealed class Phase4StarterTests : RulesTest
    {
        [TestCase(CharacterId.Warrior,"B-W01","기본 베기","B-W02","기본 방어")]
        [TestCase(CharacterId.Mage,"B-M01","기본 마력탄","B-M02","마력 보호막")]
        [TestCase(CharacterId.Priest,"B-P01","기본 신성타","B-P02","신성 보호")]
        public void StarterCardsAreOwnedUnmarkedReusableAndOutsideRewardPools(CharacterId hero,
            string attackId,string attackName,string guardId,string guardName)
        {
            var run=Run(hero);
            var attack=Data.Card(attackId);
            var guard=Data.Card(guardId);
            Assert.AreEqual(attackName,attack.Name);
            Assert.AreEqual(guardName,guard.Name);
            foreach(var card in new[]{attack,guard})
            {
                Assert.AreEqual(hero,card.Owner);
                Assert.AreEqual(CardRarity.Starter,card.Rarity);
                Assert.IsTrue(card.StarterOnly);
                Assert.AreEqual(1,card.Cost);
                Assert.AreEqual(ElementType.None,card.Element);
                Assert.AreEqual(ElementApplication.None,card.ElementApplication);
                Assert.IsFalse(card.Exhausts);
            }

            var legacy=Snapshot().characters.Single(c=>c.id==hero.ToString().ToLowerInvariant()).starter;
            CollectionAssert.AreEqual(Phase9RewardTests.Starting(hero),run.Deck.Select(c=>c.CardId));
            Assert.IsEmpty(Data.Character(hero).ProvisionalStartingCards);
            Assert.IsFalse(run.Deck.Any(c=>legacy.Contains(c.CardId)));
            Assert.AreEqual(10,run.Deck.Select(c=>c.InstanceId).Distinct().Count());

            var other=Data.AllCards.First(c=>c.StarterOnly&&c.Owner!=hero);
            Assert.Throws<InvalidOperationException>(()=>run.AddCard(other.Id));
            Assert.AreEqual(10,run.Deck.Count);
            Assert.IsEmpty(run.AcquiredCards);

            var room=DungeonRuntime.Enter(run,"r1");
            var battle=new BattleRuntime(Data,run,room,additionalEnemies:new[]{Data.Enemy("moss_slime")});
            Assert.AreEqual(5,battle.Deck.Hand.Count);
            Assert.AreEqual(5,battle.Energy);
            Assert.AreEqual(8,Data.Config.EnergyCap);
            var hit=DrawCard(battle,attackId);
            Assert.IsFalse(battle.PlayCard(hit.InstanceId));
            Assert.AreEqual(5,battle.Energy);
            var target=battle.Enemies[1];
            int hp=target.CurrentHP;
            target.ElementMark=ElementType.Fire;
            target.Element=ElementType.Ice;
            Assert.IsTrue(battle.PlayCard(hit.InstanceId,target.TargetId));
            Assert.AreEqual(4,battle.Energy);
            Assert.AreEqual(hp-6,target.CurrentHP);
            Assert.AreEqual(target.TargetId,battle.LastCardResult.HitEvents.Single().TargetId);
            Assert.IsFalse(battle.LastCardResult.Events.Any(e=>e.Kind==CombatEventKind.ElementMark||e.Kind==CombatEventKind.Reaction));
            Assert.AreEqual(ElementType.Fire,target.ElementMark);
            Assert.AreEqual(ElementType.Ice,target.Element);

            var block=DrawCard(battle,guardId);
            Assert.IsTrue(battle.PlayCard(block.InstanceId));
            Assert.AreEqual(3,battle.Energy);
            Assert.AreEqual(5,battle.Block);
            Assert.AreEqual(0,battle.LastCardResult.HitEvents.Count);
            CollectionAssert.IsSubsetOf(new[]{hit,block},battle.Deck.DiscardPile);
            Assert.IsTrue(battle.EndTurn());
            Assert.AreEqual(5,battle.Deck.Hand.Count);
            Assert.AreEqual(5,battle.Energy);

            Assert.AreEqual(6,Data.AllCards.Count(c=>c.StarterOnly));
            Assert.AreEqual(27,Data.Cards.Count);
            Assert.IsFalse(Data.Cards.Any(c=>c.StarterOnly));
            Assert.IsFalse(RewardResolver.Create(Data,run).Candidates.Any(id=>Data.Card(id).StarterOnly));
        }
    }
}